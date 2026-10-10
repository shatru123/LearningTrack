using System;
using System.Text;

namespace Day23.TinyUrl;

/// <summary>
/// Bi-directional Base62 encoder and decoder.
/// Uses alphanumeric characters [0-9a-zA-Z] ensuring URL safety without escaping.
/// Guarantees a 1-to-1 mathematical bijection with zero hash collisions.
/// </summary>
public static class Base62Codec
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly int Base = Alphabet.Length; // 62

    /// <summary>
    /// Encodes a positive 64-bit integer into a Base62 string.
    /// </summary>
    public static string Encode(long value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Value must be non-negative.");
        if (value == 0) return "0";

        var sb = new StringBuilder();
        var temp = value;

        while (temp > 0)
        {
            var remainder = (int)(temp % Base);
            sb.Append(Alphabet[remainder]);
            temp /= Base;
        }

        // Reverse to maintain big-endian positional notation
        var chars = sb.ToString().ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    /// <summary>
    /// Decodes a Base62 string back into its original 64-bit integer.
    /// </summary>
    public static long Decode(string base62String)
    {
        if (string.IsNullOrWhiteSpace(base62String))
            throw new ArgumentException("Base62 string cannot be null or empty.", nameof(base62String));

        long result = 0;
        foreach (char c in base62String)
        {
            var index = Alphabet.IndexOf(c);
            if (index == -1)
            {
                throw new FormatException($"Invalid character '{c}' in Base62 string '{base62String}'.");
            }

            result = checked((result * Base) + index);
        }

        return result;
    }
}
