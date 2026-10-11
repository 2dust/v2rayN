using System.Buffers.Binary;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace ServiceLib.Services;

public record SshSetupTarget(string Address, int Port, string Username, SshHostKey HostKey);
public record SshSetupResult(SshLocalKey? Key, bool Verified, string? Error = null, bool NeedsPassword = false,
    bool RemoteMayHaveChanged = false);

/// <summary>One foreground setup operation at a time; no live profile or running proxy is mutated.</summary>
public sealed class SshKeyInstaller(SshKeyStore store)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<SshSetupResult> ConfigureAsync(SshSetupTarget target, SshLocalKey? selected,
        bool generate, string? passphrase, string? password, IProgress<string>? progress, CancellationToken cancellation)
    {
        if (!await Gate.WaitAsync(0, cancellation)) return new(generate ? null : selected, false, "busy");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var token = deadline.Token;
        // A selected list row is not the user's choice when Generate is active.
        // Authentication failure must keep that explicit choice instead of reverting to reuse.
        var key = generate ? null : selected;
        var operation = Guid.NewGuid().ToString();
        var journal = Path.Combine(store.DirectoryPath, "install-" + operation + ".json");
        var remote = false;
        var passwordAttempted = false;
        var checkingInstalledKey = false;
        void Record(string phase) => store.WriteMetadata(journal, new
        {
            Version = 1, OperationId = operation, target.Address, target.Port, target.Username,
            HostFingerprint = target.HostKey.Fingerprint, KeyPath = key?.Path, KeyFingerprint = key?.Fingerprint,
            Phase = phase, RemoteMayHaveChanged = remote, UpdatedAt = DateTimeOffset.UtcNow,
        });
        try
        {
            if (string.IsNullOrWhiteSpace(target.Address) || target.Port is < 1 or > 65535 || string.IsNullOrWhiteSpace(target.Username))
                throw new SshSetupException("endpoint");
            // Validate the pin before passing any credentials to SSH.NET.
            if (SshProfileService.ParseHostKey(target.HostKey.PublicKey, target.HostKey.Algorithm) != target.HostKey)
                throw new SshSetupException("hostKey");
            if (!generate)
            {
                if (key == null || key.Error != null) throw new SshSetupException(key?.Error ?? "invalidKey");
                using var privateKey = store.Open(key, passphrase);
                var identity = SshKeyStore.PublicIdentity(privateKey);
                key = key with { PublicKey = identity.PublicKey, Fingerprint = identity.Fingerprint,
                    Algorithm = identity.PublicKey.Split(' ')[0], Error = null };
                progress?.Report("verifying");
                Record("verifyingExisting");
                try
                {
                    using var verified = await ConnectAsync(target, new PrivateKeyAuthenticationMethod(target.Username, privateKey), token);
                    Record("verified");
                    return new(key, true);
                }
                catch (SshAuthenticationException)
                {
                    // Only an explicit authentication refusal permits the password-install path.
                    if (string.IsNullOrEmpty(password)) return new(key, false, "passwordRequired", true);
                }
            }
            if (string.IsNullOrEmpty(password)) return new(key, false, "passwordRequired", true);
            progress?.Report("connecting");
            Record("passwordAuthentication");
            passwordAttempted = true;
            using (var authenticated = await ConnectAsync(target, new PasswordAuthenticationMethod(target.Username, password), token))
            {
                if (generate)
                {
                    progress?.Report("generating");
                    key = await store.GenerateAsync(passphrase, token);
                    Record("keySaved");
                }
                if (key?.PublicKey == null) throw new SshSetupException("invalidKey");
                progress?.Report("installing");
                Record("reading");
                await SshAuthorizedKeys.InstallAsync(authenticated, operation, key.PublicKey, () =>
                {
                    remote = true;
                    Record("writeUnknown"); // Must be durable before sending an append request.
                }, token);
                Record("publicKeyPresent");
            }
            checkingInstalledKey = true;
            progress?.Report("verifying");
            using (var privateKey = store.Open(key, passphrase))
            using (var verified = await ConnectAsync(target, new PrivateKeyAuthenticationMethod(target.Username, privateKey), token)) { }
            Record("verified");
            return new(key, true, RemoteMayHaveChanged: remote);
        }
        catch (Exception e)
        {
            var code = token.IsCancellationRequested ? (cancellation.IsCancellationRequested ? "cancelled" : "timeout") : e switch
            {
                SshSetupException known => known.Code,
                OperationCanceledException => cancellation.IsCancellationRequested ? "cancelled" : "timeout",
                SshAuthenticationException => checkingInstalledKey ? "verifyFailed" : "authentication",
                SshOperationTimeoutException => "timeout",
                EndOfStreamException => "remoteUnsupported",
                SocketException => "connection",
                IOException or UnauthorizedAccessException => "storage",
                _ => "connection",
            };
            // Errors are deliberately mapped, never exception text from credentials or a remote shell.
            try { Record(remote ? "writeOrVerificationUncertain" : "failed"); }
            catch { /* Do not conceal the original failure, and never replay a write here. */ }
            return new(key, false, code, NeedsPassword: passwordAttempted && code == "authentication", RemoteMayHaveChanged: remote);
        }
        finally { Gate.Release(); }
    }

    private static async Task<SshClient> ConnectAsync(SshSetupTarget target, AuthenticationMethod authentication, CancellationToken token)
    {
        var info = new ConnectionInfo(target.Address, target.Port, target.Username, authentication)
        { Timeout = TimeSpan.FromSeconds(12) };
        // Limit negotiation to the algorithm which was actually confirmed by the user.
        foreach (var algorithm in info.HostKeyAlgorithms.Keys.Where(a => a != target.HostKey.Algorithm).ToArray())
            info.HostKeyAlgorithms.Remove(algorithm);
        var client = new SshClient(info);
        var mismatch = false;
        client.HostKeyReceived += (_, e) =>
        {
            var expected = Convert.FromBase64String(target.HostKey.PublicKey.Split(' ')[1]);
            e.CanTrust = CryptographicOperations.FixedTimeEquals(expected, e.HostKey);
            mismatch = !e.CanTrust;
        };
        try
        {
            await client.ConnectAsync(token);
            return client;
        }
        catch
        {
            client.Dispose();
            if (mismatch) throw new SshSetupException("hostKey");
            throw;
        }
    }
}

/// <summary>Adapter for the bundled Linux helper. Untrusted file bytes never enter diagnostics.</summary>
public static class SshAuthorizedKeys
{
    public const int MaxFileBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private sealed record Identity(bool Exists, ulong Device, ulong Inode, uint Size, string Sha256);
    private sealed record Reply(int Version, string OperationId, string Action, string Status, Identity? Identity);

    public static bool ContainsKey(byte[] contents, string publicKey)
    {
        if (contents.Length > MaxFileBytes) throw new SshSetupException("authorizedKeys");
        var identity = SshProfileService.ParseHostKey(publicKey).PublicKey;
        string text;
        try { text = new UTF8Encoding(false, true).GetString(contents); }
        catch { throw new SshSetupException("authorizedKeys"); }
        var found = false;
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            if (trimmed.Length > 8192 || trimmed.Contains('\0')) throw new SshSetupException("authorizedKeys");
            // Tokenize only the options/key/blob prefix; comments may contain arbitrary quotes.
            var offset = 0;
            string Next()
            {
                while (offset < trimmed.Length && char.IsWhiteSpace(trimmed[offset])) offset++;
                var start = offset;
                var quoted = false;
                for (; offset < trimmed.Length; offset++)
                {
                    if (trimmed[offset] == '\\' && quoted) { offset++; continue; }
                    if (trimmed[offset] == '"') quoted = !quoted;
                    if (!quoted && char.IsWhiteSpace(trimmed[offset])) break;
                }
                if (quoted || offset > trimmed.Length) throw new SshSetupException("authorizedKeys");
                return trimmed[start..offset];
            }
            var type = Next();
            if (!IsKeyType(type)) type = Next();
            if (!IsKeyType(type)) throw new SshSetupException("authorizedKeys");
            var blob = Next();
            try { found |= SshProfileService.ParseHostKey(type + " " + blob).PublicKey == identity; }
            catch { throw new SshSetupException("authorizedKeys"); }
        }
        return found;
    }

    private static bool IsKeyType(string type) => type is "ssh-ed25519" or "ssh-rsa"
        or "ecdsa-sha2-nistp256" or "ecdsa-sha2-nistp384" or "ecdsa-sha2-nistp521";

    public static async Task InstallAsync(SshClient client, string operation, string publicKey, Action beforeWrite, CancellationToken token)
    {
        using var resource = typeof(SshAuthorizedKeys).Assembly.GetManifestResourceStream("ServiceLib.Sample.ssh_install_key.sh")!;
        using var reader = new StreamReader(resource);
        var script = await reader.ReadToEndAsync(token);
        // Only bundled source is shell-quoted. All variable input uses framed stdin.
        using var command = client.CreateCommand("timeout --signal=TERM --kill-after=2 60 sh -c '" + script.Replace("'", "'\\''") + "'");
        using var cancelled = token.Register(() => { try { client.Disconnect(); } catch { } });
        var execution = command.ExecuteAsync(token);
        using var input = command.CreateInputStream();
        var stderr = DrainErrorsAsync(command.ExtendedOutputStream, token);
        try
        {
            var (snapshot, before) = await Exchange("read", null, null);
            if (!ContainsKey(before, publicKey))
            {
                beforeWrite();
                var (_, after) = await Exchange("append", snapshot, publicKey + "\n");
                var newline = before.Length > 0 && before[^1] != (byte)'\n' ? "\n" : "";
                var append = Encoding.UTF8.GetBytes(newline + publicKey + "\n");
                if (after.Length != before.Length + append.Length || !after.AsSpan(0, before.Length).SequenceEqual(before)
                    || !after.AsSpan(before.Length).SequenceEqual(append)) throw new SshSetupException("remoteConflict");
            }
            input.Dispose();
            await execution.WaitAsync(token);
            await stderr;
            if (command.ExitStatus != 0) throw new SshSetupException("remoteUnsupported");
        }
        finally
        {
            input.Dispose();
            // Disconnect alone does not reliably complete SSH.NET's command task.
            // Dispose closes its pipe streams and completes pending command waiters.
            command.Dispose();
            try { await execution; } catch { }
            try { await stderr; } catch { }
        }

        async Task<(Identity, byte[])> Exchange(string action, Identity? expected, string? key)
        {
            var request = new JsonObject { ["version"] = 1, ["operationId"] = operation, ["action"] = action };
            if (expected != null) request["expected"] = JsonSerializer.SerializeToNode(expected, Json);
            if (key != null) request["publicKey"] = key;
            var bytes = Encoding.UTF8.GetBytes(request.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            var header = new byte[12];
            "SKP1"u8.CopyTo(header); header[4] = 1;
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(8), bytes.Length);
            await input.WriteAsync(header, token);
            await input.WriteAsync(bytes, token);
            await input.FlushAsync(token);
            var response = JsonSerializer.Deserialize<Reply>(await ReadFrame(command.OutputStream, 2, 16384, token), Json);
            if (response == null || response.Version != 1 || response.OperationId != operation || response.Action != action)
                throw new SshSetupException("remoteUnsupported");
            if (response.Status is not ("ok" or "added"))
                throw new SshSetupException(response.Status == "conflict" ? "remoteConflict" : "remoteUnsupported");
            var identity = response.Identity ?? throw new SshSetupException("remoteUnsupported");
            var data = await ReadFrame(command.OutputStream, 3, MaxFileBytes, token);
            if (identity.Size != data.Length || (identity.Exists
                ? identity.Inode == 0 || Convert.ToHexStringLower(SHA256.HashData(data)) != identity.Sha256
                : data.Length != 0 || identity.Device != 0 || identity.Inode != 0 || identity.Sha256 != ""))
                throw new SshSetupException("remoteConflict");
            return (identity, data);
        }
    }

    private static async Task<byte[]> ReadFrame(Stream stream, byte kind, int limit, CancellationToken token)
    {
        var header = new byte[12];
        await stream.ReadExactlyAsync(header, token);
        var size = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(8));
        if (!header.AsSpan(0, 4).SequenceEqual("SKP1"u8) || header[4] != kind || header[5] != 0 || header[6] != 0 || header[7] != 0
            || size < 0 || size > limit) throw new SshSetupException("remoteUnsupported");
        var data = new byte[size];
        await stream.ReadExactlyAsync(data, token);
        return data;
    }

    private static async Task DrainErrorsAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[1024];
        var total = 0;
        int count;
        while ((count = await stream.ReadAsync(buffer, token)) != 0)
            if ((total += count) > 16384) throw new SshSetupException("remoteUnsupported");
    }
}
