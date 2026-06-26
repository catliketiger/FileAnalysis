namespace FileStruct.Core.Models;

public class FormatRule
{
    public string RuleVersion { get; set; } = "1.0";
    public string Format { get; set; } = "";
    public string? Description { get; set; }
    public List<FormatSignature> Signatures { get; set; } = new();
    public List<FormatStructure> Structures { get; set; } = new();
    public string? SourcePath { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime LoadedAt { get; set; } = DateTime.UtcNow;
}

public class FormatSignature
{
    public string Name { get; set; } = "";
    public byte[] Pattern { get; set; } = [];
    public int Offset { get; set; }
    public byte[]? Mask { get; set; }
    public int? MinFileSize { get; set; }
}

public class FormatStructure
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "struct";
    public bool Sequential { get; set; }
    public bool Repeating { get; set; }
    public int StepSize { get; set; }
    public int? FixedCount { get; set; }
    public string? CountField { get; set; }
    public int BaseRepeatOffset { get; set; }
    public List<FormatField> Fields { get; set; } = new();
}

public class FormatField
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "uint32";
    public int Offset { get; set; }
    public int? Length { get; set; }
    public string? Endianness { get; set; }
    public string? OffsetFromField { get; set; }
    public string? LengthField { get; set; }
}
