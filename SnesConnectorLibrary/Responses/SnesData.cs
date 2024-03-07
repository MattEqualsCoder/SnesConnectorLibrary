namespace SnesConnectorLibrary.Responses;

/// <summary>
/// Class that holds data returned by the SNES
/// </summary>
public class SnesData
{
    private readonly byte[] _bytes;

    public SnesData(byte[] data)
    {
        _bytes = data;
    }
    
    /// <summary>
    /// The raw byte array returned from he SNES
    /// </summary>
    public byte[] Raw => _bytes;

    /// <summary>
    /// Returns the int8 value at a memory location
    /// </summary>
    /// <param name="offset">The memory address to return in relation to the first address requested</param>
    /// <returns>The value of the location in memory</returns>
    public byte? ReadUInt8(int offset)
    {
        if (offset < 0 || offset >= _bytes.Length)
        {
            return null;
        }

        return _bytes[offset];
    }

    /// <summary>
    /// Checks the value of an int8 (1 byte) flag
    /// </summary>
    /// <param name="offset">The memory address to return in relation to the first address requested</param>
    /// <param name="flag">The flag to check if true or not</param>
    /// <returns>True if the flag is set</returns>
    public bool CheckUInt8Flag(int offset, int flag)
    {
        return (ReadUInt8(offset) & flag) == flag;
    }

    /// <summary>
    /// Returns the int16 value at a memory location
    /// </summary>
    /// <param name="offset">The memory address to return in relation to the first address requested</param>
    /// <returns>The value of the location in memory</returns>
    public int? ReadUInt16(int offset)
    {
        if (offset < 0 || offset >= _bytes.Length - 1)
        {
            return null;
        }

        return _bytes[offset + 1] * 256 + _bytes[offset];
    }

    /// <summary>
    /// Checks the value of an int16 (2 byte) flag
    /// </summary>
    /// <param name="offset">The memory address to return in relation to the first address requested</param>
    /// <param name="flag">The flag to check if true or not</param>
    /// <returns>True if the flag is set</returns>
    public bool CheckInt16Flag(int offset, int flag)
    {
        var data = ReadUInt16(offset);
        if (data == null)
        {
            return false;
        }
        var adjustedFlag = 1 << flag;
        var temp = data & adjustedFlag;
        return temp == adjustedFlag;
    }
    
    /// <summary>
    /// Returns if this SnesData equals another
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public override bool Equals(object? other)
    {
        if (other is not SnesData otherData) return false;
        return otherData._bytes.SequenceEqual(_bytes);
    }
    
    /// <summary>
    /// Returns the hash code of the bytes array
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
        return _bytes.GetHashCode();
    }
    
}