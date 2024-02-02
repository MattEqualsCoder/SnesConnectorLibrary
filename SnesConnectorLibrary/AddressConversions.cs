namespace SnesConnectorLibrary;

internal static class AddressConversions
{
    public static int Convert(int address, SnesMemoryDomain domain, AddressFormat from, AddressFormat to)
    {
        if (from == to)
        {
            return address;
        }
        
        switch (from)
        {
            case AddressFormat.Snes9x:
                switch (to)
                {
                    case AddressFormat.BizHawk:
                        return ConvertFromSnes9xToBizHawk(address, domain);
                    case AddressFormat.FxPakPro:
                        return ConvertFromSnes9xToFxPakPro(address, domain);
                }

                break;
            case AddressFormat.BizHawk:
                switch (to)
                {
                    case AddressFormat.Snes9x:
                        return ConvertFromBizHawkToSnes9x(address, domain);
                    case AddressFormat.FxPakPro:
                        return ConvertFromBizHawkToFxPakPro(address, domain);
                }

                break;
            case AddressFormat.FxPakPro:
                switch (to)
                {
                    case AddressFormat.Snes9x:
                        return ConvertFromFxPakProToSnes9x(address, domain);
                    case AddressFormat.BizHawk:
                        return ConvertFromFxPakProToBizHawk(address, domain);
                }

                break;
        }

        throw new InvalidOperationException($"Invalid conversion from {from} to {to}");
    }

    public static int ConvertFromSnes9xToBizHawk(int address, SnesMemoryDomain domain)
    {
        switch (domain)
        {
            case SnesMemoryDomain.ConsoleRAM:
                return address - 0x7E0000;
            case SnesMemoryDomain.CartridgeSave:
                address -= 0xa06000;
                return (address / 0x010000) * 0x002000 + (address % 0x010000);
            default:
                return address;
        }
    }
    
    public static int ConvertFromSnes9xToFxPakPro(int address, SnesMemoryDomain domain)
    {
        return domain switch
        {
            SnesMemoryDomain.ConsoleRAM => ConvertFromSnes9xToBizHawk(address, domain) + 0xF50000,
            SnesMemoryDomain.CartridgeSave => ConvertFromSnes9xToBizHawk(address, domain) + 0xE00000,
            _ => address
        };
    }
    
    public static int ConvertFromBizHawkToSnes9x(int address, SnesMemoryDomain domain)
    {
        return domain switch
        {
            SnesMemoryDomain.ConsoleRAM => address + 0x7E0000,
            SnesMemoryDomain.CartridgeSave => (address / 0x002000) * 0x010000 + (address % 0x002000) + 0x006000 + 0xA00000,
            _ => address
        };
    }
    
    public static int ConvertFromBizHawkToFxPakPro(int address, SnesMemoryDomain domain)
    {
        return domain switch
        {
            SnesMemoryDomain.ConsoleRAM => address + 0xF50000,
            SnesMemoryDomain.CartridgeSave => address + 0xE00000,
            _ => address
        };
    }
    
    public static int ConvertFromFxPakProToSnes9x(int address, SnesMemoryDomain domain)
    {
        switch (domain)
        {
            case SnesMemoryDomain.ConsoleRAM:
                return address - 0x770000;
            case SnesMemoryDomain.CartridgeSave:
                address -= 0xE00000;
                return (address / 0x002000) * 0x010000 + (address % 0x002000) + 0x006000 + 0xA00000;
            default:
                return address;
        }
    }
    
    public static int ConvertFromFxPakProToBizHawk(int address, SnesMemoryDomain domain)
    {
        return domain switch
        {
            SnesMemoryDomain.ConsoleRAM => address - 0xF50000,
            SnesMemoryDomain.CartridgeSave => address - 0xE00000,
            _ => address
        };
    }
}