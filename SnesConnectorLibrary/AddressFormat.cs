namespace SnesConnectorLibrary;

/// <summary>
/// Enum of different address formats that can be used for Snes Memory Requests
/// </summary>
public enum AddressFormat
{
    /// <summary>
    /// Snes9x Emulator Format
    ///     Memory/WRAM - starts at 0x7E0000
    ///     Save/CartRAM/SRAM - starts at 0xA06000
    ///     Rom - starts at 0x000000 
    /// </summary>
    Snes9x,
    
    /// <summary>
    /// BizHawk Emulator Format
    ///     Memory/WRAM - starts at 0x000000
    ///     Save/CartRAM/SRAM - starts at 0x000000
    ///     Rom - starts at 0x000000 
    /// </summary>
    BizHawk,
    
    /// <summary>
    /// FxPakPro (QUSB2SNES/SNI) Format
    ///     Memory/WRAM - starts at 0xF50000
    ///     Save/CartRAM/SRAM - starts at 0xE00000
    ///     Rom - starts at 0x000000 
    /// </summary>
    FxPakPro
}