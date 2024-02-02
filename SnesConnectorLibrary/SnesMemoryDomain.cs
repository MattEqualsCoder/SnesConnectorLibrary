namespace SnesConnectorLibrary;

/// <summary>
/// Enum for where to retrieve/update memory
/// </summary>
public enum SnesMemoryDomain
{
    /// <summary>
    /// The SNES Console RAM (WRAM)
    /// </summary>
    ConsoleRAM,
    
    /// <summary>
    /// The cartridge save RAM (SRAM/CartRAM)
    /// </summary>
    CartridgeSave,
    
    /// <summary>
    /// The actual ROM of the game (CartROM)
    /// </summary>
    Rom
}