using Microsoft.AspNetCore.Components.Forms;
using System.Text;

namespace CodeCrafters.Bittorrent.src;

public class BencodeDecoder
{
    private bool _decodeStringsAsUtf8 = false;

    public (object Value, int Consumed) DecodeInput(byte[] input, int offset, bool decodeStringsAsUtf8 = false, string? currentKey = null)
    {
        _decodeStringsAsUtf8 = decodeStringsAsUtf8;
        if (offset >= input.Length)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset is out of bounds.");

        return input[offset] switch
        {
            (byte)'i' => DecodeInteger(input, offset),
            (byte)'l' => DecodeList(input, offset, decodeStringsAsUtf8),
            (byte)'d' => DecodeDictionary(input, offset, decodeStringsAsUtf8),
            >= (byte)'0' and <= (byte)'9' =>
                DecodeStringOrBytes(input, offset, decodeStringsAsUtf8: currentKey != "pieces" && decodeStringsAsUtf8),
            _ => throw new InvalidOperationException($"Unknown bencode type '{(char)input[offset]}' at offset {offset}")
        };
    }

    private (object, int) DecodeStringOrBytes(byte[] data, int offset, bool decodeStringsAsUtf8 = true)
    {
        // Find the colon that separates length from data
        // We need to be careful - only look for colon after consecutive digits
        int colonPos = offset;
        
        // First, find where the digits end
        while (colonPos < data.Length && data[colonPos] >= (byte)'0' && data[colonPos] <= (byte)'9')
        {
            colonPos++;
        }
        
        // The next character should be a colon
        if (colonPos >= data.Length || data[colonPos] != (byte)':')
            throw new FormatException($"Invalid bencoded string: expected ':' after length at offset {colonPos}");

        int length = int.Parse(Encoding.ASCII.GetString(data, offset, colonPos - offset));
        int valueStart = colonPos + 1;

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

    private (Dictionary<string, object>, int) DecodeDictionary(byte[] data, int offset, bool parentDecodeStringsAsUtf8 = false)
    {
        var dict = new Dictionary<string, object>();
        int start = offset;
        offset += 1;

        while (offset < data.Length && data[offset] != (byte)'e')
        {
            var (key, keyUsed) = DecodeStringOrBytes(data, offset, decodeStringsAsUtf8: true);
            string keyString = (string)key;
            offset += keyUsed;

            bool isPiecesKey = keyString == "pieces";

            var (value, valUsed) = DecodeInput(data, offset, decodeStringsAsUtf8: !isPiecesKey && parentDecodeStringsAsUtf8, currentKey: keyString);
            dict.Add(keyString, value);
            offset += valUsed;
        }

        if (offset >= data.Length || data[offset] != (byte)'e')
            throw new FormatException("Unterminated dictionary");

        int consumed = (offset - start + 1);
        return (dict, consumed);
    }

    private (List<object>, int) DecodeList(byte[] data, int offset, bool decodeStringsAsUtf8 = false)
    {
        var list = new List<object>();
        int start = offset;
        offset += 1;

        while (offset < data.Length && data[offset] != (byte)'e')
        {
            var (elem, used) = DecodeInput(data, offset, decodeStringsAsUtf8);
            list.Add(elem);
            offset += used;
        }

        if (offset >= data.Length || data[offset] != (byte)'e')
            throw new FormatException("Unterminated list");

        return (list, offset - start + 1);
    }

    public async Task<byte[]> ReadFileByte(IBrowserFile file)
    {
        using var stream = file.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

}