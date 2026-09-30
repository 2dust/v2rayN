using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace v2rayN.Web.Security;

public sealed class WebAuthService
{
    private const int CurrentVersion = 1;
    private const int Pbkdf2Iterations = 600_000;
    private const int SaltLength = 16;
    private const int VerifierLength = 32;
    public const int MinimumKeyLength = 12;
    public const int MaximumKeyLength = 4096;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly byte[] DummySalt = RandomNumberGenerator.GetBytes(SaltLength);

    private readonly string _configPath;
    private readonly string? _environmentKey;
    private readonly SemaphoreSlim _setupLock = new(1, 1);
    private StoredWebAuth? _storedAuth;

    public WebAuthService(string configPath, string? environmentKey)
    {
        _configPath = Path.GetFullPath(configPath);
        _environmentKey = string.IsNullOrWhiteSpace(environmentKey) ? null : environmentKey;
        if (_environmentKey is null && File.Exists(_configPath))
        {
            _storedAuth = LoadStoredAuth(_configPath);
        }
    }

    public bool EnvironmentKeyConfigured => _environmentKey is not null;

    public bool SetupRequired => _environmentKey is null && _storedAuth is null;

    public bool ValidateManagementKey(string? suppliedKey)
    {
        var key = suppliedKey ?? string.Empty;

        if (_environmentKey is not null)
        {
            DeriveAndDiscard(key, DummySalt, Pbkdf2Iterations);
            if (string.IsNullOrEmpty(suppliedKey))
            {
                return false;
            }
            return FixedTimeEquals(_environmentKey, suppliedKey);
        }

        var stored = _storedAuth;
        if (stored is null)
        {
            DeriveAndDiscard(key, DummySalt, Pbkdf2Iterations);
            return false;
        }

        var salt = Convert.FromBase64String(stored.Salt);
        var expected = Convert.FromBase64String(stored.KeyVerifier);
        var supplied = DeriveVerifier(key, salt, stored.Iterations);
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }

    public async Task<WebSetupResult> SetupAsync(
        string? key,
        string? confirmKey,
        bool setupAccessAllowed,
        CancellationToken cancellationToken = default)
    {
        if (!setupAccessAllowed)
        {
            return WebSetupResult.Forbidden;
        }

        if (key is null || key.Length < MinimumKeyLength)
        {
            return WebSetupResult.KeyTooShort;
        }
        if (key.Length > MaximumKeyLength)
        {
            return WebSetupResult.KeyTooLong;
        }

        if (!string.Equals(key, confirmKey, StringComparison.Ordinal))
        {
            return WebSetupResult.KeysDoNotMatch;
        }

        await _setupLock.WaitAsync(cancellationToken);
        try
        {
            if (!SetupRequired)
            {
                return WebSetupResult.AlreadyConfigured;
            }

            // Do not replace a config created by another process between startup and setup.
            if (File.Exists(_configPath))
            {
                _storedAuth = LoadStoredAuth(_configPath);
                return WebSetupResult.AlreadyConfigured;
            }

            var salt = RandomNumberGenerator.GetBytes(SaltLength);
            var verifier = DeriveVerifier(key, salt, Pbkdf2Iterations);
            var stored = new StoredWebAuth(
                CurrentVersion,
                "PBKDF2-HMAC-SHA256",
                Pbkdf2Iterations,
                Convert.ToBase64String(salt),
                Convert.ToBase64String(verifier));
            await WriteAtomicallyAsync(stored, cancellationToken);
            _storedAuth = stored;
            return WebSetupResult.Created;
        }
        catch (IOException) when (File.Exists(_configPath))
        {
            _storedAuth = LoadStoredAuth(_configPath);
            return WebSetupResult.AlreadyConfigured;
        }
        finally
        {
            _setupLock.Release();
        }
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static byte[] DeriveVerifier(string key, byte[] salt, int iterations)
    {
        var password = Encoding.UTF8.GetBytes(key);
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                VerifierLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
        }
    }

    private static void DeriveAndDiscard(string key, byte[] salt, int iterations)
    {
        var derived = DeriveVerifier(key, salt, iterations);
        CryptographicOperations.ZeroMemory(derived);
    }

    private static StoredWebAuth LoadStoredAuth(string path)
    {
        try
        {
            var stored = JsonSerializer.Deserialize<StoredWebAuth>(File.ReadAllBytes(path), JsonOptions)
                ?? throw new InvalidDataException("Web auth config is empty.");
            var salt = Convert.FromBase64String(stored.Salt);
            var verifier = Convert.FromBase64String(stored.KeyVerifier);
            if (stored.Version != CurrentVersion
                || stored.Kdf != "PBKDF2-HMAC-SHA256"
                || stored.Iterations is < 100_000 or > 2_000_000
                || salt.Length != SaltLength
                || verifier.Length != VerifierLength)
            {
                throw new InvalidDataException("Web auth config has unsupported or invalid parameters.");
            }

            return stored;
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            throw new InvalidDataException("Web auth config is malformed.", exception);
        }
    }

    private async Task WriteAtomicallyAsync(StoredWebAuth stored, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_configPath)
            ?? throw new InvalidOperationException("Web auth config path has no parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".web-auth-{Guid.NewGuid():N}.tmp");
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.WriteThrough,
            };
            if (OperatingSystem.IsLinux())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            await using (var stream = new FileStream(temporaryPath, options))
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(stored, JsonOptions);
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(temporaryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            File.Move(temporaryPath, _configPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private sealed record StoredWebAuth(int Version, string Kdf, int Iterations, string Salt, string KeyVerifier);
}

public enum WebSetupResult
{
    Created,
    Forbidden,
    KeyTooShort,
    KeyTooLong,
    KeysDoNotMatch,
    AlreadyConfigured,
}
