using System.Security.Cryptography;

namespace Magazyn.Security;

public static class PasswordGenerator
{
    private const string Lower = "abcdefghijklmnopqrstuvwxyz";
    private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits = "0123456789";
    private const string Special = "!@#$%^&*()-_=+[]{};:,.?";

    private const string All = Lower + Upper + Digits + Special;

    // Hasło 10 znaków: min. 1 mała, 1 duża, 1 cyfra, 1 specjalny
    public static string Generate(int length = 10)
    {
        if (length != 10)
            throw new ArgumentOutOfRangeException(nameof(length), "Hasło musi mieć dokładnie 10 znaków.");

        var chars = new char[length];
        int idx = 0;

        chars[idx++] = Pick(Lower);
        chars[idx++] = Pick(Upper);
        chars[idx++] = Pick(Digits);
        chars[idx++] = Pick(Special);

        for (; idx < length; idx++)
            chars[idx] = Pick(All);

        Shuffle(chars);
        return new string(chars);
    }

    // Token do loginu typu del_xxxxxxxx (bez znaków specjalnych)
    public static string RandomToken(int length)
    {
        const string tokenAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        var charBuffer = new char[length];

        for (int i = 0; i < length; i++)
            charBuffer[i] = tokenAlphabet[RandomNumberGenerator.GetInt32(tokenAlphabet.Length)];

        return new string(charBuffer);
    }

    // Alias, żeby stary kod działał (bez "RODO_")
    public static string GenerateRodoPassword(int tokenLength = 10) => Generate(10);

    private static char Pick(string alphabet)
        => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

    private static void Shuffle(char[] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }
}