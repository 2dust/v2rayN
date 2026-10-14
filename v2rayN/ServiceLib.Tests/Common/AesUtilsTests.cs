namespace ServiceLib.Tests.Common;

public class AesUtilsTests
{
    [Test]
    public async Task Encrypt_And_Decrypt_Should_RoundTrip()
    {
        var original = "mySecretSudoPassword123!@#$%^&*()_+-=";
        var encrypted = AesUtils.Encrypt(original);

        await encrypted.IsNotEmpty().Should().BeTrue();
        await (encrypted != original).Should().BeTrue();

        var decrypted = AesUtils.Decrypt(encrypted);
        await decrypted.Should().BeEqualTo(original);
    }

    [Test]
    public async Task Encrypt_EmptyOrNull_Should_Return_Empty()
    {
        await AesUtils.Encrypt(null).Should().BeEqualTo(string.Empty);
        await AesUtils.Encrypt(string.Empty).Should().BeEqualTo(string.Empty);
    }

    [Test]
    public async Task Decrypt_EmptyOrNull_Should_Return_Empty()
    {
        await AesUtils.Decrypt(null).Should().BeEqualTo(string.Empty);
        await AesUtils.Decrypt(string.Empty).Should().BeEqualTo(string.Empty);
    }

    [Test]
    public async Task Decrypt_InvalidString_Should_Return_Empty()
    {
        await AesUtils.Decrypt("NotValidBase64!!").Should().BeEqualTo(string.Empty);
        await AesUtils.Decrypt("AQIDBA==").Should().BeEqualTo(string.Empty);
    }

    [Test]
    public async Task TunModeItem_LinuxSudoPwd_Should_Serialize_And_Deserialize()
    {
        var item = new TunModeItem
        {
            EnableTun = true,
            LinuxSudoPwd = AesUtils.Encrypt("rootPassword!"),
        };

        var json = JsonUtils.Serialize(item);
        var restored = JsonUtils.Deserialize<TunModeItem>(json);

        await restored.Should().NotBeNull();
        await restored!.EnableTun.Should().BeTrue();
        await restored.LinuxSudoPwd.Should().BeEqualTo(item.LinuxSudoPwd);
        await AesUtils.Decrypt(restored.LinuxSudoPwd).Should().BeEqualTo("rootPassword!");
    }
}
