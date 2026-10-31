using System.Security.Cryptography;
using System.Text;

namespace TONE;

public class HashAuth(string hashValue, string salt = null, HashAlgorithm algorithm = null)
{
    public readonly string HashValue = hashValue;

    private readonly string salt = salt;
    private readonly HashAlgorithm algorithm = algorithm ?? SHA256.Create();

    public bool CheckString(string value)
    {
        var hash = CalculateHash(value);
        return HashValue == hash;
    }
    public string CalculateHash(string source)
        => CalculateHash(source, salt, algorithm);

    public static string CalculateHash(string source, string salt = null, HashAlgorithm algorithm = null)
    {

        algorithm ??= SHA256.Create();

        if (salt != null) source += salt;

        var sourceBytes = Encoding.UTF8.GetBytes(source);

        var hashBytes = algorithm.ComputeHash(sourceBytes);

        var sb = new StringBuilder();
        foreach (var b in hashBytes)
            sb.Append(b.ToString("x2"));

        return sb.ToString();
    }

    public static HashAuth CreateByUnhashedValue(string value, string salt = null)
    {

        var algorithm = SHA256.Create();
        string hashValue = CalculateHash(value, salt, algorithm);

        Logger.Info($"ハッシュ値算出結果: {value} => {hashValue} {(salt == null ? "" : $"(salt: {salt})")}", "HashAuth");
        Logger.Warn("以上の値をソースコード上にペーストしてください。", "HashAuth");

        return new HashAuth(hashValue, salt, algorithm);
    }
}