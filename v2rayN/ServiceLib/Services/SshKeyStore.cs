using System.Security.AccessControl;
using System.Security.Principal;
using Renci.SshNet;
using Renci.SshNet.Common;
using SshNet.Keygen;

namespace ServiceLib.Services;

public sealed class SshSetupException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public record SshLocalKey(string Path, string? PublicKey, string? Fingerprint, string? Algorithm,
    bool Encrypted, string? Error = null, bool Duplicate = false)
{
    public string Display => $"{Path}\n{Algorithm ?? "?"} · {Fingerprint ?? "?"}"
        + $" · {(Encrypted ? SshStrings.KeyLocked : SshStrings.KeyUnencrypted)}"
        + (Duplicate ? $" · {SshStrings.SamePublicKey}" : "")
        + (Error != null ? $" · {SshStrings.SetupError(Error)}" : "");
}

/// <summary>Only paths and public identity are persisted here. No passphrase is stored.</summary>
public sealed class SshKeyStore
{
    private const int MaxKeyBytes = 1024 * 1024;
    private static readonly SemaphoreSlim GenerationGate = new(1, 1);
    public string DirectoryPath { get; }
    private string DefaultPath => Path.Combine(DirectoryPath, "default.json");

    public SshKeyStore(string? directory = null) => DirectoryPath = directory ?? Path.Combine(Utils.StartupPath(), "sshKeys");

    public SshLocalKey? GetDefault()
    {
        if (!File.Exists(DefaultPath)) return null;
        try { return JsonSerializer.Deserialize<SshLocalKey>(File.ReadAllText(DefaultPath)); }
        catch { return null; }
    }

    public IReadOnlyList<SshLocalKey> Discover(IEnumerable<string?> configuredPaths, string? home)
    {
        var paths = new List<string?>();
        var preferred = GetDefault();
        paths.Add(preferred?.Path);
        paths.AddRange(configuredPaths);
        foreach (var directory in new[] { DirectoryPath, home == null ? null : Path.Combine(home, ".ssh") })
        {
            if (directory == null || !Directory.Exists(directory)) continue;
            try { paths.AddRange(Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        var seenPaths = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var seenKeys = new HashSet<string>();
        var result = new List<SshLocalKey>();
        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            string canonical;
            try { canonical = Path.GetFullPath(path!); }
            catch { continue; }
            if (!seenPaths.Add(canonical)) continue;
            if (canonical.EndsWith(".pending", StringComparison.Ordinal)) continue;
            var key = Inspect(canonical);
            if (key == null && preferred?.Path == canonical)
                key = preferred with { Error = "invalidKey" };
            if (key == null) continue;
            if (preferred?.Path == canonical && preferred.Fingerprint != null && key.Fingerprint != null
                && key.Fingerprint != preferred.Fingerprint)
                key = key with { Error = "keyChanged" };
            if (preferred?.Path == canonical && key.Encrypted && key.Fingerprint == null)
                key = key with { Fingerprint = preferred.Fingerprint, PublicKey = preferred.PublicKey, Algorithm = preferred.Algorithm };
            if (key.Fingerprint != null) key = key with { Duplicate = !seenKeys.Add(key.Fingerprint) };
            result.Add(key);
        }
        return result;
    }

    public SshLocalKey? Inspect(string path, string? passphrase = null)
    {
        SshHostKey? visibleIdentity = null;
        var encrypted = false;
        try
        {
            path = Path.GetFullPath(path);
            var info = new FileInfo(path);
            if (!info.Exists || (info.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0
                || info.Length is <= 0 or > MaxKeyBytes) return null;
            using (var reader = new StreamReader(path))
            {
                var header = reader.ReadLine();
                if (header is not ("-----BEGIN OPENSSH PRIVATE KEY-----" or "-----BEGIN RSA PRIVATE KEY-----"
                    or "-----BEGIN EC PRIVATE KEY-----" or "-----BEGIN PRIVATE KEY-----" or "-----BEGIN ENCRYPTED PRIVATE KEY-----"))
                    return null;
            }
            ValidatePrivatePermissions(path);
            var pem = File.ReadAllText(path);
            encrypted = pem.Contains("ENCRYPTED", StringComparison.Ordinal);
            if (pem.StartsWith("-----BEGIN OPENSSH PRIVATE KEY-----", StringComparison.Ordinal))
                (visibleIdentity, encrypted) = ReadOpenSshEnvelope(pem);
            using var file = new PrivateKeyFile(path, passphrase);
            var publicKey = PublicIdentity(file);
            if (visibleIdentity != null && visibleIdentity.Fingerprint != publicKey.Fingerprint)
                throw new SshSetupException("invalidKey");
            return new(path, publicKey.PublicKey, publicKey.Fingerprint, publicKey.PublicKey.Split(' ')[0], encrypted);
        }
        catch (SshPassPhraseNullOrEmptyException)
        {
            // OpenSSH stores a public envelope outside its encrypted private section.
            // It is displayed for selection; Open still verifies the decrypted key before use.
            return new(path, visibleIdentity?.PublicKey, visibleIdentity?.Fingerprint,
                visibleIdentity?.PublicKey.Split(' ')[0], true);
        }
        catch (SshSetupException e) { return new(path, null, null, null, false, e.Code); }
        catch { return new(path, null, null, null, !string.IsNullOrEmpty(passphrase), "invalidKey"); }
    }

    private static (SshHostKey Identity, bool Encrypted) ReadOpenSshEnvelope(string pem)
    {
        var bytes = Convert.FromBase64String(string.Concat(pem.Split('\n').Where(l => !l.StartsWith("-----"))));
        if (!bytes.AsSpan().StartsWith("openssh-key-v1\0"u8)) throw new FormatException();
        var offset = 15;
        byte[] Field()
        {
            if (bytes.Length - offset < 4) throw new FormatException();
            var size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset));
            offset += 4;
            if (size > bytes.Length - offset) throw new FormatException();
            var field = bytes.AsSpan(offset, (int)size).ToArray();
            offset += (int)size;
            return field;
        }
        var cipher = Encoding.ASCII.GetString(Field());
        _ = Field(); // KDF name and options are checked by SSH.NET when opening the key.
        _ = Field();
        if (bytes.Length - offset < 4 || System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)) != 1)
            throw new FormatException();
        offset += 4;
        var blob = Field();
        if (blob.Length < 4) throw new FormatException();
        var typeSize = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(blob);
        if (typeSize < 1 || typeSize > blob.Length - 4) throw new FormatException();
        return (SshProfileService.ParseHostKey(Encoding.ASCII.GetString(blob, 4, typeSize) + " "
            + Convert.ToBase64String(blob)), cipher != "none");
    }

    public static SshHostKey PublicIdentity(PrivateKeyFile file)
    {
        if (file.Certificate != null) throw new SshSetupException("invalidKey");
        var blob = file.HostKeyAlgorithms.First().Data;
        var size = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(blob);
        if (size < 1 || size > blob.Length - 4) throw new SshSetupException("invalidKey");
        var type = Encoding.ASCII.GetString(blob, 4, size);
        return SshProfileService.ParseHostKey(type + " " + Convert.ToBase64String(blob));
    }

    public PrivateKeyFile Open(SshLocalKey key, string? passphrase)
    {
        try
        {
            ValidatePrivatePermissions(key.Path);
            if (new FileInfo(key.Path).Length is <= 0 or > MaxKeyBytes) throw new SshSetupException("invalidKey");
            var file = new PrivateKeyFile(key.Path, passphrase);
            try
            {
                var identity = PublicIdentity(file);
                if (key.Fingerprint != null && key.Fingerprint != identity.Fingerprint)
                    throw new SshSetupException("keyChanged");
                return file;
            }
            catch { file.Dispose(); throw; }
        }
        catch (SshSetupException) { throw; }
        catch { throw new SshSetupException("invalidKey"); }
    }

    public async Task<SshLocalKey> GenerateAsync(string? passphrase, CancellationToken token)
    {
        await GenerationGate.WaitAsync(token);
        try
        {
            EnsureDirectory();
            var id = Guid.NewGuid().ToString("N");
            var path = Path.Combine(DirectoryPath, $"id_ed25519_{id}");
            var temporary = path + ".pending";
            try
            {
                // Cancellation never removes a committed key: it may already be authorized remotely.
                await Task.Run(() =>
                {
                    using var stream = CreatePrivateFile(temporary);
                    var builder = SshKey.Builder(SshKeyType.ED25519).WithComment("v2rayN-" + id);
                    if (!string.IsNullOrEmpty(passphrase)) builder.WithPassphrase(passphrase);
                    builder.Generate(stream);
                    stream.Flush(true);
                }, token);
                token.ThrowIfCancellationRequested();
                File.Move(temporary, path, false);
                var key = Inspect(path, passphrase);
                if (key?.PublicKey == null || key.Error != null) throw new SshSetupException("invalidKey");
                return key;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        finally { GenerationGate.Release(); }
    }

    public void SaveDefault(SshLocalKey key)
    {
        // Called only after the node has actually been saved.
        WriteMetadata(DefaultPath, key with { Error = null, Duplicate = false });
    }

    public void WriteMetadata<T>(string path, T value)
    {
        if (Path.GetDirectoryName(Path.GetFullPath(path)) != Path.GetFullPath(DirectoryPath))
            throw new SshSetupException("storage");
        EnsureDirectory();
        var temporary = Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N") + ".pending");
        try
        {
            using (var stream = CreatePrivateFile(temporary))
            {
                JsonSerializer.Serialize(stream, value);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public void EnsureDirectory()
    {
        if (OperatingSystem.IsWindows()) EnsureWindowsDirectory(DirectoryPath);
        else Directory.CreateDirectory(DirectoryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        if ((File.GetAttributes(DirectoryPath) & FileAttributes.ReparsePoint) != 0) throw new SshSetupException("keyPermissions");
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(DirectoryPath) & (UnixFileMode)63) != 0)
            throw new SshSetupException("keyPermissions");
    }

    public static void ValidatePrivatePermissions(string path)
    {
        if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new SshSetupException("invalidKey");
        if (OperatingSystem.IsWindows()) ValidateWindowsPermissions(path);
        else if ((File.GetUnixFileMode(path) & (UnixFileMode)63) != 0) throw new SshSetupException("keyPermissions");
    }

    internal static FileStream CreatePrivateFile(string path)
    {
        if (OperatingSystem.IsWindows()) return CreateWindowsFile(path);
        return new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        });
    }

    [SupportedOSPlatform("windows")]
    private static FileStream CreateWindowsFile(string path)
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(true, false);
        security.SetOwner(WindowsIdentity.GetCurrent().User!);
        security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, AccessControlType.Allow));
        return new FileInfo(path).Create(FileMode.CreateNew, FileSystemRights.FullControl, FileShare.None, 4096, FileOptions.None, security);
    }

    [SupportedOSPlatform("windows")]
    private static void EnsureWindowsDirectory(string path)
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        security.SetOwner(WindowsIdentity.GetCurrent().User!);
        security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        if (!Directory.Exists(path)) new DirectoryInfo(path).Create(security);
        ValidateWindowsPermissions(path);
    }

    [SupportedOSPlatform("windows")]
    private static void ValidateWindowsPermissions(string path)
    {
        FileSystemSecurity security = Directory.Exists(path)
            ? new DirectoryInfo(path).GetAccessControl() : new FileInfo(path).GetAccessControl();
        var user = WindowsIdentity.GetCurrent().User!;
        if (!user.Equals(security.GetOwner(typeof(SecurityIdentifier)))) throw new SshSetupException("keyPermissions");
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && !user.Equals(rule.IdentityReference)
                && rule.IdentityReference.Value is not ("S-1-5-18" or "S-1-5-32-544")
                && (rule.FileSystemRights & (FileSystemRights.ReadData | FileSystemRights.WriteData | FileSystemRights.ChangePermissions)) != 0)
                throw new SshSetupException("keyPermissions");
    }
}
