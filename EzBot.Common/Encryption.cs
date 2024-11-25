using System.Security.Cryptography;
using System.Text;

namespace EzBot.Common;

public class Encryption
{
    // Encrypts a string using SHA256
    public static string Encrypt(string input)
    {
        var data = Encoding.UTF8.GetBytes(input);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToBase64String(hash);
    }

    // Encrypts a string using HMACSHA256
    public static string Encrypt(string input, string key)
    {
        var data = Encoding.UTF8.GetBytes(input);
        var keyData = Encoding.UTF8.GetBytes(key);
        using var hmac = new HMACSHA256(keyData);
        var hash = hmac.ComputeHash(data);
        return Convert.ToBase64String(hash);
    }

    // Encrypts a string using AES
    public static string Encrypt(string input, string key, string iv)
    {
        var data = Encoding.UTF8.GetBytes(input);
        var keyData = Encoding.UTF8.GetBytes(key);
        var ivData = Encoding.UTF8.GetBytes(iv);
        using var aes = Aes.Create();
        aes.Key = keyData;
        aes.IV = ivData;
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        cs.Write(data, 0, data.Length);
        cs.Close();
        return Convert.ToBase64String(ms.ToArray());
    }

    // Decrypts a string using SHA256
    public static string Decrypt(string input)
    {
        var data = Convert.FromBase64String(input);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToBase64String(hash);
    }

    // Decrypts a string using HMACSHA256
    public static string Decrypt(string input, string key)
    {
        var data = Convert.FromBase64String(input);
        var keyData = Encoding.UTF8.GetBytes(key);
        using var hmac = new HMACSHA256(keyData);
        var hash = hmac.ComputeHash(data);
        return Convert.ToBase64String(hash);
    }

    // Decrypts a string using AES
    public static string Decrypt(string input, string key, string iv)
    {
        var data = Convert.FromBase64String(input);
        var keyData = Encoding.UTF8.GetBytes(key);
        var ivData = Encoding.UTF8.GetBytes(iv);
        using var aes = Aes.Create();
        aes.Key = keyData;
        aes.IV = ivData;
        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(data);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        return sr.ReadToEnd();
    }
}
