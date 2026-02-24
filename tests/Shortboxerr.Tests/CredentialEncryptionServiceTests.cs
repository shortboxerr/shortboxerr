using Shortboxerr.Core.Services;
using Shortboxerr.Infrastructure.Services;
using Xunit;

namespace Shortboxerr.Tests;

public class CredentialEncryptionServiceTests
{
    private readonly ICredentialEncryptionService _service;

    public CredentialEncryptionServiceTests()
    {
        _service = new CredentialEncryptionService();
    }

    [Fact]
    public void Encrypt_EmptyString_ReturnsEmptyString()
    {
        var result = _service.Encrypt("");
        Assert.Equal("", result);
    }

    [Fact]
    public void Encrypt_NullString_ReturnsNull()
    {
        var result = _service.Encrypt(null!);
        Assert.Null(result);
    }

    [Fact]
    public void Encrypt_ValidString_ReturnsEncryptedWithPrefix()
    {
        var plaintext = "my-secret-password";
        var encrypted = _service.Encrypt(plaintext);
        
        Assert.StartsWith("ENC:1:", encrypted);
        Assert.NotEqual(plaintext, encrypted);
    }

    [Fact]
    public void Decrypt_EmptyString_ReturnsEmptyString()
    {
        var result = _service.Decrypt("");
        Assert.Equal("", result);
    }

    [Fact]
    public void Decrypt_PlaintextString_ReturnsAsIs()
    {
        var plaintext = "not-encrypted-value";
        var result = _service.Decrypt(plaintext);
        
        Assert.Equal(plaintext, result);
    }

    [Fact]
    public void EncryptDecrypt_RoundTrip_ReturnsOriginal()
    {
        var plaintext = "my-secret-api-key-12345";
        var encrypted = _service.Encrypt(plaintext);
        var decrypted = _service.Decrypt(encrypted);
        
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void EncryptDecrypt_SpecialCharacters_ReturnsOriginal()
    {
        var plaintext = "p@$$w0rd!#$%^&*()_+-=[]{}|;':\",./<>?`~";
        var encrypted = _service.Encrypt(plaintext);
        var decrypted = _service.Decrypt(encrypted);
        
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void EncryptDecrypt_UnicodeCharacters_ReturnsOriginal()
    {
        var plaintext = "密码123🔑パスワード";
        var encrypted = _service.Encrypt(plaintext);
        var decrypted = _service.Decrypt(encrypted);
        
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void EncryptDecrypt_LongString_ReturnsOriginal()
    {
        var plaintext = new string('x', 10000);
        var encrypted = _service.Encrypt(plaintext);
        var decrypted = _service.Decrypt(encrypted);
        
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_SameValue_ProducesDifferentCiphertext()
    {
        var plaintext = "same-password";
        var encrypted1 = _service.Encrypt(plaintext);
        var encrypted2 = _service.Encrypt(plaintext);
        
        // Different nonces should produce different ciphertext
        Assert.NotEqual(encrypted1, encrypted2);
        
        // But both should decrypt to the same value
        Assert.Equal(plaintext, _service.Decrypt(encrypted1));
        Assert.Equal(plaintext, _service.Decrypt(encrypted2));
    }

    [Fact]
    public void Encrypt_AlreadyEncrypted_ReturnsUnchanged()
    {
        var plaintext = "my-password";
        var encrypted = _service.Encrypt(plaintext);
        var doubleEncrypted = _service.Encrypt(encrypted);
        
        // Should not double-encrypt
        Assert.Equal(encrypted, doubleEncrypted);
    }

    [Fact]
    public void IsEncrypted_PlaintextValue_ReturnsFalse()
    {
        Assert.False(_service.IsEncrypted("plain-value"));
        Assert.False(_service.IsEncrypted("ENC:"));
        Assert.False(_service.IsEncrypted("ENC:2:something"));
        Assert.False(_service.IsEncrypted(null));
        Assert.False(_service.IsEncrypted(""));
    }

    [Fact]
    public void IsEncrypted_EncryptedValue_ReturnsTrue()
    {
        var encrypted = _service.Encrypt("test");
        Assert.True(_service.IsEncrypted(encrypted));
    }

    [Fact]
    public void Decrypt_CorruptedData_ThrowsCryptographicException()
    {
        // Valid prefix but corrupted base64 data
        var corrupted = "ENC:1:invalidbase64!!!";
        
        Assert.ThrowsAny<Exception>(() => _service.Decrypt(corrupted));
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsCryptographicException()
    {
        var plaintext = "my-password";
        var encrypted = _service.Encrypt(plaintext);
        
        // Tamper with the ciphertext
        var tampered = encrypted[..^5] + "XXXXX";
        
        Assert.ThrowsAny<Exception>(() => _service.Decrypt(tampered));
    }
}
