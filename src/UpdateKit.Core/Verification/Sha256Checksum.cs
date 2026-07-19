using System.Security.Cryptography;

namespace UpdateKit;

internal static class Sha256Checksum
{
    internal const int HexLength = 64;

    public static bool TryParse(string? value, out byte[] bytes)
    {
        bytes = [];

        if (value is null || value.Length != HexLength)
        {
            return false;
        }

        try
        {
            bytes = Convert.FromHexString(value);
            return bytes.Length == SHA256.HashSizeInBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool Equals(byte[] left, byte[] right) =>
        CryptographicOperations.FixedTimeEquals(left, right);
}
