using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Shortboxerr.Core.Services;

namespace Shortboxerr.Infrastructure.Services;

/// <summary>
/// Implementation of credential encryption using AES-256-GCM.
/// 
/// Key derivation uses machine-specific identifiers to ensure credentials
/// can only be decrypted on the same machine where they were encrypted.
/// 
/// Format: ENC:1:{base64(nonce + ciphertext + tag)}
/// - ENC:1: prefix indicating encrypted value and version
/// - nonce: 12 bytes (96 bits) for AES-GCM
/// - tag: 16 bytes (128 bits) authentication tag
/// </summary>
public class CredentialEncryptionService : ICredentialEncryptionService
{
    private const string EncryptionPrefix = "ENC:1:";
    private const int NonceSize = 12; // 96 bits for AES-GCM
    private const int TagSize = 16;   // 128 bits auth tag
    private const int KeySize = 32;   // 256 bits for AES-256
    
    private readonly byte[] _encryptionKey;
    private readonly ILogger<CredentialEncryptionService>? _logger;

    public CredentialEncryptionService(ILogger<CredentialEncryptionService>? logger = null)
    {
        _logger = logger;
        _encryptionKey = DeriveEncryptionKey();
    }

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return plaintext;

        // Don't double-encrypt
        if (IsEncrypted(plaintext))
            return plaintext;

        try
        {
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);

            var ciphertext = new byte[plaintextBytes.Length];
            var tag = new byte[TagSize];

            using var aes = new AesGcm(_encryptionKey, TagSize);
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            // Combine: nonce + ciphertext + tag
            var combined = new byte[NonceSize + ciphertext.Length + TagSize];
            Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
            Buffer.BlockCopy(ciphertext, 0, combined, NonceSize, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, combined, NonceSize + ciphertext.Length, TagSize);

            return EncryptionPrefix + Convert.ToBase64String(combined);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to encrypt credential");
            throw new CryptographicException("Failed to encrypt credential", ex);
        }
    }

    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return ciphertext;

        // If not encrypted, return as-is (for backward compatibility with existing plaintext)
        if (!IsEncrypted(ciphertext))
            return ciphertext;

        try
        {
            var base64 = ciphertext[EncryptionPrefix.Length..];
            var combined = Convert.FromBase64String(base64);

            if (combined.Length < NonceSize + TagSize)
                throw new CryptographicException("Invalid encrypted data format");

            var nonce = new byte[NonceSize];
            var ciphertextLength = combined.Length - NonceSize - TagSize;
            var encryptedBytes = new byte[ciphertextLength];
            var tag = new byte[TagSize];

            Buffer.BlockCopy(combined, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(combined, NonceSize, encryptedBytes, 0, ciphertextLength);
            Buffer.BlockCopy(combined, NonceSize + ciphertextLength, tag, 0, TagSize);

            var plaintextBytes = new byte[ciphertextLength];

            using var aes = new AesGcm(_encryptionKey, TagSize);
            aes.Decrypt(nonce, encryptedBytes, tag, plaintextBytes);

            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (AuthenticationTagMismatchException)
        {
            _logger?.LogError("Credential decryption failed: authentication tag mismatch (wrong key or corrupted data)");
            throw new CryptographicException("Failed to decrypt credential: data may be corrupted or encrypted on a different machine");
        }
        catch (Exception ex) when (ex is not CryptographicException)
        {
            _logger?.LogError(ex, "Failed to decrypt credential");
            throw new CryptographicException("Failed to decrypt credential", ex);
        }
    }

    public bool IsEncrypted(string? value)
    {
        return !string.IsNullOrEmpty(value) && value.StartsWith(EncryptionPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Derives a 256-bit encryption key from machine-specific identifiers.
    /// Uses PBKDF2 with SHA-256, 100,000 iterations.
    /// 
    /// The key is derived from:
    /// - Linux: /etc/machine-id (if available) + hostname
    /// - Windows: MachineGuid from registry + hostname  
    /// - Fallback: hostname + environment variables
    /// </summary>
    private byte[] DeriveEncryptionKey()
    {
        var machineIdentifier = GetMachineIdentifier();
        var salt = Encoding.UTF8.GetBytes("Shortboxerr.Credentials.v1");
        
        // PBKDF2 with 100,000 iterations
        using var pbkdf2 = new Rfc2898DeriveBytes(
            machineIdentifier,
            salt,
            iterations: 100_000,
            HashAlgorithmName.SHA256);

        return pbkdf2.GetBytes(KeySize);
    }

    /// <summary>
    /// Gets a machine-specific identifier for key derivation.
    /// </summary>
    private string GetMachineIdentifier()
    {
        var components = new List<string>();

        // Try to get machine ID
        var machineId = GetMachineId();
        if (!string.IsNullOrEmpty(machineId))
            components.Add(machineId);

        // Add hostname
        try
        {
            components.Add(Environment.MachineName);
        }
        catch
        {
            // Ignore if we can't get hostname
        }

        // Add username as additional entropy
        try
        {
            components.Add(Environment.UserName);
        }
        catch
        {
            // Ignore if we can't get username
        }

        // Ensure we have at least something
        if (components.Count == 0)
        {
            _logger?.LogWarning("Could not determine machine identifier, using fallback");
            components.Add("shortboxerr-default-key-source");
        }

        return string.Join(":", components);
    }

    /// <summary>
    /// Gets the machine ID from the operating system.
    /// </summary>
    private string? GetMachineId()
    {
        // Linux: /etc/machine-id
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                if (File.Exists("/etc/machine-id"))
                    return File.ReadAllText("/etc/machine-id").Trim();
            }
            catch
            {
                // Ignore read errors
            }
        }

        // macOS: hardware UUID
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                // Use IOPlatformUUID via ioreg
                using var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "/usr/sbin/ioreg";
                process.StartInfo.Arguments = "-rd1 -c IOPlatformExpertDevice";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var match = System.Text.RegularExpressions.Regex.Match(
                    output, @"""IOPlatformUUID""\s*=\s*""([^""]+)""");
                if (match.Success)
                    return match.Groups[1].Value;
            }
            catch
            {
                // Ignore errors
            }
        }

        // Windows: MachineGuid from registry
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Cryptography");
                var value = key?.GetValue("MachineGuid");
                if (value is string machineGuid)
                    return machineGuid;
            }
            catch
            {
                // Ignore registry errors
            }
        }

        return null;
    }
}
