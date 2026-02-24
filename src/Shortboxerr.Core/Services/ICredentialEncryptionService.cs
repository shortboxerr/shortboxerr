using System.Security.Cryptography;
using System.Text;

namespace Shortboxerr.Core.Services;

/// <summary>
/// Service for encrypting and decrypting sensitive credentials.
/// Uses AES-256-GCM for authenticated encryption.
/// </summary>
public interface ICredentialEncryptionService
{
    /// <summary>
    /// Encrypts a plaintext credential value.
    /// </summary>
    /// <param name="plaintext">The plaintext value to encrypt</param>
    /// <returns>Base64-encoded encrypted value with IV and tag</returns>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts an encrypted credential value.
    /// </summary>
    /// <param name="ciphertext">Base64-encoded encrypted value</param>
    /// <returns>The decrypted plaintext value</returns>
    string Decrypt(string ciphertext);

    /// <summary>
    /// Checks if a value appears to be encrypted (has the encryption prefix).
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <returns>True if the value appears to be encrypted</returns>
    bool IsEncrypted(string? value);
}

/// <summary>
/// Marks a property as containing sensitive credential data that should be encrypted.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SensitiveCredentialAttribute : Attribute
{
}
