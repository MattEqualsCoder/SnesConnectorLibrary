namespace SnesConnectorLibrary;

public class SnesData
{
    private readonly byte[] _bytes;
    private readonly int _offset;

    public SnesData(int location, byte[] data)
    {
        _bytes = data;
        _offset = location;
    }
    
    public byte[] Raw => _bytes;

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

    public bool CheckUInt8Flag(int location, int flag, bool isRaw = false)
    {
        return (ReadUInt8(location, isRaw) & flag) == flag;
    }

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

    public bool CheckWordFlag(int location, int flag, bool isRaw = false)
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