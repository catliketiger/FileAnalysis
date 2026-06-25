using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using FileStruct.Core.Models;

namespace FileStruct.App.ViewModels;

public partial class LivePreviewViewModel : ObservableObject
{
    [ObservableProperty]
    private string _uint8Value = "";

    [ObservableProperty]
    private string _int16Value = "";

    [ObservableProperty]
    private string _int32Value = "";

    [ObservableProperty]
    private string _uint32Value = "";

    [ObservableProperty]
    private string _floatValue = "";

    [ObservableProperty]
    private string _asciiValue = "";

    [ObservableProperty]
    private string _utf8Value = "";

    [ObservableProperty]
    private string _gbkValue = "";

    [ObservableProperty]
    private string _hexValue = "";

    [ObservableProperty]
    private string _binaryValue = "";

    [ObservableProperty]
    private string _timestamp32Value = "";

    [ObservableProperty]
    private bool _hasData;

    public void UpdateFromBuffer(BinaryBuffer buffer, long offset, int length,
        bool isLittleEndian = true)
    {
        if (buffer == null || !buffer.IsValidRange(offset, length))
        {
            Clear();
            return;
        }

        HasData = true;
        HexValue = BitConverter.ToString(buffer.ReadBytes(offset, Math.Min(length, 16)));

        var firstByte = buffer.ReadByte(offset);
        Uint8Value = $"0x{firstByte:X2} ({firstByte})";

        if (length >= 2)
            Int16Value = buffer.ReadInt16(offset, isLittleEndian).ToString();
        if (length >= 4)
        {
            Int32Value = buffer.ReadInt32(offset, isLittleEndian).ToString();
            Uint32Value = $"0x{buffer.ReadUInt32(offset, isLittleEndian):X8}";
            FloatValue = buffer.ReadSingle(offset, isLittleEndian).ToString("G6");

            var ts = buffer.ReadUInt32(offset, isLittleEndian);
            try { Timestamp32Value = DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"); }
            catch { Timestamp32Value = $"{ts} (超出范围)"; }
        }

        var asciiBytes = buffer.ReadBytes(offset, Math.Min(length, 8));
        AsciiValue = Encoding.ASCII.GetString(asciiBytes)
            .Replace("\0", "\\0").Replace("\n", "\\n").Replace("\r", "\\r");

        var utf8Bytes = buffer.ReadBytes(offset, Math.Min(length, 16));
        var utf8Str = Encoding.UTF8.GetString(utf8Bytes);
        Utf8Value = utf8Str.Contains('\0') ? utf8Str.Replace("\0", "\\0") : utf8Str;

        // GBK 解码
        try
        {
            var gbkBytes = buffer.ReadBytes(offset, Math.Min(length, 16));
            var gbkStr = Encoding.GetEncoding("gb2312").GetString(gbkBytes);
            GbkValue = gbkStr.Contains('\0') ? gbkStr.Replace("\0", "\\0") : gbkStr;
        }
        catch
        {
            GbkValue = "";
        }

        var sb = new StringBuilder();
        for (int i = 0; i < Math.Min(length, 4); i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(Convert.ToString(buffer.ReadByte(offset + i), 2).PadLeft(8, '0'));
        }
        BinaryValue = sb.ToString();
    }

    public void Clear()
    {
        HasData = false;
        Uint8Value = Int16Value = Int32Value = Uint32Value = "";
        FloatValue = AsciiValue = Utf8Value = GbkValue = HexValue = BinaryValue = Timestamp32Value = "";
    }
}
