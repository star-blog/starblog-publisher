using FluentAssertions;
using StarBlogPublisher.Services.Security;

namespace StarBlogPublisher.Tests.Services.Security;

public class EncryptionServiceTests {
    [Fact]
    public void EncryptAndDecrypt_RoundTripsNonEmptySecrets() {
        const string secret = "a secret with Unicode: 测试 🔐";

        var encrypted = EncryptionService.Encrypt(secret);

        encrypted.Should().NotBeNullOrWhiteSpace();
        encrypted.Should().NotBe(secret);
        EncryptionService.Decrypt(encrypted).Should().Be(secret);
    }

    [Fact]
    public void Decrypt_InvalidPayload_ReturnsEmptyString() {
        EncryptionService.Decrypt("not-base64").Should().BeEmpty();
    }
}
