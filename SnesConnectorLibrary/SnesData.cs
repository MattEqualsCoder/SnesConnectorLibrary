namespace SnesConnectorLibrary;

/// <summary>
/// Class that holds data returned by the SNES
/// </summary>
public class SnesData
{
    private readonly byte[] _bytes;
    private readonly int _offset;

    public SnesData(int location, byte[] data)
    {
        _bytes = data;
        _offset = location;
    }
    
    /// <summary>
    /// The raw byte array returned from he SNES
    /// </summary>
    public byte[] Raw => _bytes;

    /// <summary>
    /// Returns the int8 value at a memory location
    /// </summary>
    /// <param name="location">The memory address to return</param>
    /// <param name="isRaw">Set to true if the provided location is the number of bytes from the start of the actual
    /// requested memory location</param>
    /// <returns>The value of the location in memory</returns>
    public byte? ReadUInt8(int location, bool isRaw = false)
    {
        if (!isRaw)
        {
            return ReadUInt8(location - _offset, true);
        }
        
        if (location < 0 || location >= _bytes.Length)
        {
            return null;
        }

        return _bytes[location];
    }

    /// <summary>
    /// Checks the value of an int8 (1 byte) flag
    /// </summary>
    /// <param name="location">The memory address to return</param>
    /// <param name="flag">The flag to check if true or not</param>
    /// <param name="isRaw">Set to true if the provided location is the number of bytes from the start of the actual
    /// requested memory location</param>
    /// <returns>True if the flag is set</returns>
    public bool CheckUInt8Flag(int location, int flag, bool isRaw = false)
    {
        return (ReadUInt8(location, isRaw) & flag) == flag;
    }

    /// <summary>
    /// Returns the int16 value at a memory location
    /// </summary>
    /// <param name="location">The memory address to return</param>
    /// <param name="isRaw">Set to true if the provided location is the number of bytes from the start of the actual
    /// requested memory location</param>
    /// <returns>The value of the location in memory</returns>
    public int? ReadUInt16(int location, bool isRaw = false)
    {
        if (!isRaw)
        {
            return ReadUInt16(location - _offset, true);
        }
        
        if (location < 0 || location >= _bytes.Length - 1)
        {
            return null;
        }

        return _bytes[location + 1] * 256 + _bytes[location];
    }

    /// <summary>
    /// Checks the value of an int16 (2 byte) flag
    /// </summary>
    /// <param name="location">The memory address to return</param>
    /// <param name="flag">The flag to check if true or not</param>
    /// <param name="isRaw">Set to true if the provided location is the number of bytes from the start of the actual
    /// requested memory location</param>
    /// <returns>True if the flag is set</returns>
    public bool CheckInt16Flag(int location, int flag, bool isRaw = false)
    {
        var data = ReadUInt16(location, isRaw);
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
        return Enumerable.SequenceEqual(otherData._bytes, _bytes);
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