using BenchmarkDotNet.Attributes;
using EzBot.Services.Encryption;

[MemoryDiagnoser]
public class EncryptionServiceBenchmark
{
    private const string Secret = "MyUltraSecureSecret123!";
    private IEncryptionService _encryptionService = null!;
    private string _plainText = null!;
    private string _cipherText = null!;

    // 1. Global setup: runs once per Benchmark run
    [GlobalSetup]
    public void Setup()
    {
        // Instantiate your encryption service with a sample secret
        _encryptionService = new EncryptionService(Secret);

        // Prepare some sample data to encrypt
        // The bigger the plainText, the more "stress" on the benchmark
        _plainText = new string('X', 1000);

        // Pre-encrypt once so we can measure Decrypt in isolation
        _cipherText = _encryptionService.Encrypt(_plainText);
    }

    // 2. Benchmark the Encrypt() method
    [Benchmark]
    public string EncryptTest()
    {
        // Return the result just for clarity
        return _encryptionService.Encrypt(_plainText);
    }

    // 3. Benchmark the Decrypt() method
    [Benchmark]
    public string DecryptTest()
    {
        return _encryptionService.Decrypt(_cipherText);
    }
}