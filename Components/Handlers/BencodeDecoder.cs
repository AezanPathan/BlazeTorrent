using System.Text;

namespace CodeCrafters.Bittorrent.src;

public class BencodeDecoder
{
    private bool _decodeStringsAsUtf8 = false;

    public (object Value, int Consumed) DecodeInput(byte[] input, int offset, bool decodeStringsAsUtf8 = false)
    {
        _decodeStringsAsUtf8 = decodeStringsAsUtf8;

        return input[offset] switch
        {
            //>= (byte)'0' and <= (byte)'9' => DecodeString(input, offset),
            >= (byte)'0' and <= (byte)'9' => DecodeStringOrBytes(input, offset),
            (byte)'i' => DecodeInteger(input, offset),
            (byte)'l' => DecodeList(input, offset),
            (byte)'d' => DecodeDictionary(input, offset),
            _ => throw new InvalidOperationException($"Unknown bencode type '{(char)input[offset]}' at offset {offset}")
        };
    }

    private (string, int) DecodeString(byte[] data, int offset)
    {
        int colonIndex = Array.IndexOf(data, (byte)':', offset);

        if (colonIndex != -1)
        {
            int length = int.Parse(Encoding.ASCII.GetString(data, offset, colonIndex - offset));
            string value = Encoding.ASCII.GetString(data, colonIndex + 1, length);
            return (value, colonIndex + 1 + length - offset);
        }
        else
            throw new FormatException("Invalid string: missing colon" + data);
    }

    public (object, int) DecodeStringOrBytes(byte[] data, int offset, bool decodeStringsAsUtf8 = true)
    {
        int colonIndex = Array.IndexOf(data, (byte)':', offset);
        if (colonIndex == -1) throw new FormatException("Invalid string: missing colon");

        int length = int.Parse(Encoding.ASCII.GetString(data, offset, colonIndex - offset));
        int valueStart = colonIndex + 1;

        if (decodeStringsAsUtf8)
        {
            string value = Encoding.UTF8.GetString(data, valueStart, length);
            return (value, valueStart + length - offset);
        }
        else
        {
            byte[] valueBytes = new byte[length];
            Array.Copy(data, valueStart, valueBytes, 0, length);
            return (valueBytes, valueStart + length - offset);
        }
    }
    private (long, int) DecodeInteger(byte[] data, int offset)
    {
        int end = Array.IndexOf(data, (byte)'e', offset);
        if (end < 0)
            throw new FormatException("Invalid integer, missing 'e' terminator");

        string numStr = Encoding.ASCII.GetString(data, offset + 1, end - offset - 1);

        return (long.Parse(numStr), end - offset + 1);
    }

    private (Dictionary<string, object>, int) DecodeDictionary(byte[] data, int offset)
    {
        var dict = new Dictionary<string, object>();

        int start = offset;
        offset += 1;

        while (offset < data.Length && data[offset] != (byte)'e')
        {
            var (key, keyUsed) = DecodeStringOrBytes(data, offset, decodeStringsAsUtf8: true);
            string keyString = (string)key;
            offset += keyUsed;

            var (value, valUsed) = DecodeInput(data, offset);
            dict.Add(keyString, value);
            offset += valUsed;
        }

        if (offset >= data.Length || data[offset] != (byte)'e')
            throw new FormatException("Unterminated dictionary");

        int consumed = (offset - start + 1);
        return (dict, consumed);
    }

    private (List<object>, int) DecodeList(byte[] data, int offset)
    {
        var list = new List<object>();
        offset += 1;

        while (offset < data.Length && data[offset] != (byte)'e')
        {
            var (elem, used) = DecodeInput(data, offset);
            list.Add(elem);
            offset += used;
        }

        if (offset >= data.Length || data[offset] != (byte)'e')
            throw new FormatException("Unterminated list");

        return (list, offset + 1);
    }

}