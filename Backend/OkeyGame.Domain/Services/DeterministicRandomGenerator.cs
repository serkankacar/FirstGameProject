using System.Security.Cryptography;
using System.Text;

namespace OkeyGame.Domain.Services;

/// <summary>
/// Deterministik rastgele sayı üreteci.
/// Verilen bir seed (tohum) değerine göre her zaman aynı sayı dizisini üretir.
/// HMAC-SHA256 algoritmasını kullanır.
/// </summary>
public class DeterministicRandomGenerator : IRandomGenerator
{
    private readonly byte[] _key;
    private long _counter;
    private byte[] _currentBuffer;
    private int _bufferIndex;

    public DeterministicRandomGenerator(string seed)
    {
        ArgumentException.ThrowIfNullOrEmpty(seed);
        _key = Encoding.UTF8.GetBytes(seed);
        _counter = 0;
        _currentBuffer = GenerateNextBlock();
        _bufferIndex = 0;
    }

    public int NextInt(int maxValue)
    {
        if (maxValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxValue), "Maksimum değer pozitif olmalıdır.");
        }

        // Rejection sampling logic (CryptoRandomGenerator ile aynı mantık)
        uint range = (uint)maxValue;
        uint threshold = (uint.MaxValue - range + 1) % range;

        uint result;
        do
        {
            result = GetNextUInt32();
        } while (result < threshold);

        return (int)(result % range);
    }

    private uint GetNextUInt32()
    {
        // 4 byte al
        if (_bufferIndex + 4 > _currentBuffer.Length)
        {
            _currentBuffer = GenerateNextBlock();
            _bufferIndex = 0;
        }

        uint value = BitConverter.ToUInt32(_currentBuffer, _bufferIndex);
        _bufferIndex += 4;
        return value;
    }

    private byte[] GenerateNextBlock()
    {
        using var hmac = new HMACSHA256(_key);
        // Counter'ı byte array'e çevir ve hash'le
        var message = BitConverter.GetBytes(_counter++);
        // Big-endian veya Little-endian fark etmez, yeter ki tutarlı olsun.
        // BitConverter sistem endianness kullanır.
        return hmac.ComputeHash(message);
    }
}
