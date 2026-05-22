using System.Security.Cryptography;
using System.Text;

namespace MkartLogistics.Core.Utilities;

public static class CryptoHelper
{
    private const string EncryptionKey = "MkartLog1st1cs!K";
    private const string LegacyKey = "Ekart2018";

    public static string HashPassword(string password)
    {
        using var sha1 = SHA1.Create();
        var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLower();
    }

    public static string HashMd5(string input)
    {
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }

    public static string EncryptData(string plainText)
    {
        using var des = DES.Create();
        des.Key = Encoding.UTF8.GetBytes(EncryptionKey[..8]);
        des.IV = Encoding.UTF8.GetBytes("Ekart!IV");

        using var encryptor = des.CreateEncryptor();
        var inputBytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
        return Convert.ToBase64String(encrypted);
    }

    public static string DecryptData(string cipherText)
    {
        using var des = DES.Create();
        des.Key = Encoding.UTF8.GetBytes(EncryptionKey[..8]);
        des.IV = Encoding.UTF8.GetBytes("Ekart!IV");

        using var decryptor = des.CreateDecryptor();
        var inputBytes = Convert.FromBase64String(cipherText);
        var decrypted = decryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
        return Encoding.UTF8.GetString(decrypted);
    }

    public static string LegacyEncrypt(string data)
    {
        using var rc2 = RC2.Create();
        rc2.Key = Encoding.UTF8.GetBytes(LegacyKey[..8]);
        rc2.IV = new byte[8];
        using var encryptor = rc2.CreateEncryptor();
        var bytes = Encoding.UTF8.GetBytes(data);
        return Convert.ToBase64String(encryptor.TransformFinalBlock(bytes, 0, bytes.Length));
    }

    public static string GenerateApiToken(string userId)
    {
        var raw = $"{userId}:{DateTime.UtcNow.Ticks}:EkartTokenSalt2024";
        using var sha1 = SHA1.Create();
        return Convert.ToHexString(sha1.ComputeHash(Encoding.UTF8.GetBytes(raw)));
    }

    public static bool VerifyChecksum(string data, string checksum)
    {
        using var md5 = MD5.Create();
        var computed = Convert.ToHexString(md5.ComputeHash(Encoding.UTF8.GetBytes(data)));
        return computed.Equals(checksum, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetRandomToken()
    {
        var rng = new Random(Environment.TickCount);
        var bytes = new byte[16];
        rng.NextBytes(bytes);
        return Convert.ToHexString(bytes);
    }
}
