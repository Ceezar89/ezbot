using EzBot.Common.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace EzBot.Services.Encryption;

public class EncryptionService(string encryptionSecret) : IEncryptionService
{
    private readonly byte[] _key = SHA256.HashData(Encoding.UTF8.GetBytes(encryptionSecret));
    private const int TagLength = 16; // 128-bit tag
    private const int NonceLength = 12; // 96-bit nonce
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        // 2. Generate a random 12-byte nonce (IV) for each encryption.
        //    12 bytes is typical for GCM.
        byte[] nonce = new byte[NonceLength];
        RandomNumberGenerator.Fill(nonce);

        // 3. Encrypt and authenticate using AesGcm
        using var aesGcm = new AesGcm(_key, TagLength);

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherBytes = new byte[plainBytes.Length]; // same length as plaintext
        byte[] tag = new byte[TagLength]; // 16 bytes

        aesGcm.Encrypt(
            nonce: nonce,
            plaintext: plainBytes,
            ciphertext: cipherBytes,
            tag: tag
        );

        // 4. Combine [ nonce (12 bytes) | ciphertext (... bytes) | tag (16 bytes) ]
        byte[] result = new byte[nonce.Length + cipherBytes.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, nonce.Length, cipherBytes.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length + cipherBytes.Length, tag.Length);

        // 5. Return Base64-encoded string
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        byte[] fullCipher = Convert.FromBase64String(cipherText);

        // 1. Parse [ nonce (12 bytes) | ciphertext (... bytes) | tag (16 bytes) ]
        byte[] nonce = new byte[NonceLength];
        Array.Copy(fullCipher, 0, nonce, 0, nonce.Length);

        byte[] tag = new byte[TagLength];
        Array.Copy(fullCipher, fullCipher.Length - tag.Length, tag, 0, tag.Length);

        int cipherLen = fullCipher.Length - nonce.Length - tag.Length;
        byte[] cipherBytes = new byte[cipherLen];
        Array.Copy(fullCipher, nonce.Length, cipherBytes, 0, cipherLen);

        // 2. Decrypt with AesGcm
        using var aesGcm = new AesGcm(_key, TagLength);

        byte[] plainBytes = new byte[cipherLen];
        aesGcm.Decrypt(
            nonce: nonce,
            ciphertext: cipherBytes,
            tag: tag,
            plaintext: plainBytes
        );

        // 3. Convert back to string
        return Encoding.UTF8.GetString(plainBytes);
    }
}

// Using this class when encryption is disabled globally in the application
public class NoOpEncryptionService : IEncryptionService
{
    public string Encrypt(string plainText)
    {
        // Return the original text, no encryption performed
        return plainText;
    }

    public string Decrypt(string cipherText)
    {
        // Return the original text, no decryption performed
        return cipherText;
    }
}