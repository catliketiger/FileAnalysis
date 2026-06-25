namespace FileStruct.Core.Models;

/// <summary>
/// 结构字段数据类型
/// </summary>
public enum FieldDataType
{
    // 原始整数
    UInt8,
    Int8,
    UInt16LE,
    UInt16BE,
    Int16LE,
    Int16BE,
    UInt32LE,
    UInt32BE,
    Int32LE,
    Int32BE,
    UInt64LE,
    UInt64BE,
    Int64LE,
    Int64BE,

    // 浮点
    FloatLE,
    FloatBE,
    DoubleLE,
    DoubleBE,

    // 字符串
    ASCII,
    UTF8,
    UTF16LE,
    UTF16BE,

    // 二进制/特殊
    Bytes,
    BitField,

    // 复合
    Struct,
    Array,
    Padding,

    // 特殊类型
    TimestampUnix32,
    TimestampUnix64,
    GUID,
}
