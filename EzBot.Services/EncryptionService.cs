using System.Security.Cryptography;

namespace EzBot.Services;
public class EncryptionService(string encryptionSecret) : IEncryptionService
{
    private readonly string _encryptionSecret = encryptionSecret;

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        byte[] salt = new byte[16];
        RandomNumberGenerator.Fill(salt);

        using var keyDerivation = new Rfc2898DeriveBytes(_encryptionSecret, salt, 100_000, HashAlgorithmName.SHA256);
        using var aes = Aes.Create();
        aes.Key = keyDerivation.GetBytes(aes.KeySize / 8);  // 256 bits
        aes.IV = keyDerivation.GetBytes(aes.BlockSize / 8); // 128 bits
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var msEncrypt = new MemoryStream();
        using var cryptoStream = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
        using (var swEncrypt = new StreamWriter(cryptoStream))
        {
            swEncrypt.Write(plainText);
        }

        byte[] cipherBytes = msEncrypt.ToArray();

        // Prepend the salt to the ciphertext
        // final format: [salt (16 bytes) | encrypted data (... bytes) ]
        byte[] result = new byte[salt.Length + cipherBytes.Length];
        Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, salt.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        byte[] fullCipher = Convert.FromBase64String(cipherText);

        // Extract the salt (first 16 bytes) and the actual ciphertext
        byte[] salt = new byte[16];
        byte[] cipherBytes = new byte[fullCipher.Length - 16];

        Buffer.BlockCopy(fullCipher, 0, salt, 0, salt.Length);
        Buffer.BlockCopy(fullCipher, salt.Length, cipherBytes, 0, cipherBytes.Length);

        using var keyDerivation = new Rfc2898DeriveBytes(_encryptionSecret, salt, 100_000, HashAlgorithmName.SHA256);
        using var aes = Aes.Create();
        aes.Key = keyDerivation.GetBytes(aes.KeySize / 8);  // 256 bits
        aes.IV = keyDerivation.GetBytes(aes.BlockSize / 8); // 128 bits
        aes.Padding = PaddingMode.PKCS7;

        using ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var msDecrypt = new MemoryStream(cipherBytes);
        using var cryptoStream = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(cryptoStream);

        return srDecrypt.ReadToEnd();
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