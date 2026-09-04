using System.Collections.Generic;
using System.Text;

namespace RealmGate.Server;

/// <summary>
/// Self-contained IBM PC code page 437 encoding used by classic BBS sessions.
/// Control bytes 0x00-0x7F remain standard ASCII/control characters and the
/// upper half maps to the DOS CP437 character set.
/// </summary>
internal sealed class LegacyCodePage437Encoding : Encoding
{
    private static readonly char[] DecodeTable = BuildDecodeTable();
    private static readonly Dictionary<char, byte> EncodeTable = BuildEncodeTable();

    public override string EncodingName => "IBM PC code page 437";
    public override string WebName => "ibm437";
    public override int CodePage => 437;
    public override bool IsSingleByte => true;

    public override int GetByteCount(
        char[] chars,
        int index,
        int count)
    {
        ArgumentNullException.ThrowIfNull(chars);
        ValidateRange(chars.Length, index, count);
        return count;
    }

    public override int GetBytes(
        char[] chars,
        int charIndex,
        int charCount,
        byte[] bytes,
        int byteIndex)
    {
        ArgumentNullException.ThrowIfNull(chars);
        ArgumentNullException.ThrowIfNull(bytes);
        ValidateRange(chars.Length, charIndex, charCount);

        if (byteIndex < 0 || byteIndex > bytes.Length - charCount)
            throw new ArgumentOutOfRangeException(nameof(byteIndex));

        for (var offset = 0; offset < charCount; offset++)
            bytes[byteIndex + offset] = Encode(chars[charIndex + offset]);

        return charCount;
    }

    public override int GetBytes(
        ReadOnlySpan<char> chars,
        Span<byte> bytes)
    {
        if (bytes.Length < chars.Length)
            throw new ArgumentException(
                "The destination byte buffer is too small.",
                nameof(bytes));

        for (var index = 0; index < chars.Length; index++)
            bytes[index] = Encode(chars[index]);

        return chars.Length;
    }

    public override int GetCharCount(
        byte[] bytes,
        int index,
        int count)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ValidateRange(bytes.Length, index, count);
        return count;
    }

    public override int GetChars(
        byte[] bytes,
        int byteIndex,
        int byteCount,
        char[] chars,
        int charIndex)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(chars);
        ValidateRange(bytes.Length, byteIndex, byteCount);

        if (charIndex < 0 || charIndex > chars.Length - byteCount)
            throw new ArgumentOutOfRangeException(nameof(charIndex));

        for (var offset = 0; offset < byteCount; offset++)
            chars[charIndex + offset] =
                DecodeTable[bytes[byteIndex + offset]];

        return byteCount;
    }

    public override int GetChars(
        ReadOnlySpan<byte> bytes,
        Span<char> chars)
    {
        if (chars.Length < bytes.Length)
            throw new ArgumentException(
                "The destination character buffer is too small.",
                nameof(chars));

        for (var index = 0; index < bytes.Length; index++)
            chars[index] = DecodeTable[bytes[index]];

        return bytes.Length;
    }

    public override int GetMaxByteCount(int charCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(charCount);
        return charCount;
    }

    public override int GetMaxCharCount(int byteCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteCount);
        return byteCount;
    }

    public override byte[] GetPreamble() => [];

    private static byte Encode(char value)
    {
        if (value <= 0x7F)
            return (byte)value;

        return EncodeTable.TryGetValue(value, out var encoded)
            ? encoded
            : (byte)'?';
    }

    private static char[] BuildDecodeTable()
    {
        var table = new char[256];

        for (var value = 0; value < 128; value++)
            table[value] = (char)value;

        const string extended =
            "\u00C7\u00FC\u00E9\u00E2\u00E4\u00E0\u00E5\u00E7\u00EA\u00EB\u00E8\u00EF\u00EE\u00EC\u00C4\u00C5\u00C9\u00E6\u00C6\u00F4\u00F6\u00F2\u00FB\u00F9\u00FF\u00D6\u00DC\u00A2\u00A3\u00A5\u20A7\u0192\u00E1\u00ED\u00F3\u00FA\u00F1\u00D1\u00AA\u00BA\u00BF\u2310\u00AC\u00BD\u00BC\u00A1\u00AB\u00BB\u2591\u2592\u2593\u2502\u2524\u2561\u2562\u2556\u2555\u2563\u2551\u2557\u255D\u255C\u255B\u2510\u2514\u2534\u252C\u251C\u2500\u253C\u255E\u255F\u255A\u2554\u2569\u2566\u2560\u2550\u256C\u2567\u2568\u2564\u2565\u2559\u2558\u2552\u2553\u256B\u256A\u2518\u250C\u2588\u2584\u258C\u2590\u2580\u03B1\u00DF\u0393\u03C0\u03A3\u03C3\u00B5\u03C4\u03A6\u0398\u03A9\u03B4\u221E\u03C6\u03B5\u2229\u2261\u00B1\u2265\u2264\u2320\u2321\u00F7\u2248\u00B0\u2219\u00B7\u221A\u207F\u00B2\u25A0\u00A0";

        for (var index = 0; index < extended.Length; index++)
            table[index + 128] = extended[index];

        return table;
    }

    private static Dictionary<char, byte> BuildEncodeTable()
    {
        var table = new Dictionary<char, byte>();

        for (var value = 0; value < DecodeTable.Length; value++)
        {
            var character = DecodeTable[value];
            if (!table.ContainsKey(character))
                table.Add(character, (byte)value);
        }

        return table;
    }

    private static void ValidateRange(
        int length,
        int index,
        int count)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (index > length - count)
            throw new ArgumentOutOfRangeException(nameof(count));
    }
}
