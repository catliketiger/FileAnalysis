namespace FileStruct.Services.AuxTools;

public class BaseConverter
{
    public string DecimalToBinary(long value) => Convert.ToString(value, 2);
    public string DecimalToHex(long value) => $"0x{value:X}";
    public long BinaryToDecimal(string binary) => Convert.ToInt64(binary.Replace("0b", "").Replace(" ", ""), 2);
    public long HexToDecimal(string hex) => Convert.ToInt64(hex.Replace("0x", "").Replace(" ", ""), 16);
    public string HexToBinary(string hex) => DecimalToBinary(HexToDecimal(hex));
    public string BinaryToHex(string binary) => DecimalToHex(BinaryToDecimal(binary));

    public ConversionResult ConvertAll(string input)
    {
        input = input.Trim().Replace(" ", "");

        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            var dec = HexToDecimal(input);
            return new ConversionResult(dec, DecimalToBinary(dec), input.ToUpper());
        }
        if (input.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
        {
            var dec = BinaryToDecimal(input);
            return new ConversionResult(dec, input, DecimalToHex(dec));
        }
        if (long.TryParse(input, out var decValue))
        {
            return new ConversionResult(decValue, DecimalToBinary(decValue), DecimalToHex(decValue));
        }

        throw new FormatException($"无法解析输入: {input}");
    }
}

public record ConversionResult(long Decimal, string Binary, string Hex);
