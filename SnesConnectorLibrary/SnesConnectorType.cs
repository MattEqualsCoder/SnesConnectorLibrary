using System.ComponentModel;

namespace SnesConnectorLibrary;

/// <summary>
/// Enum for the different types of possible connectors
/// </summary>
public enum SnesConnectorType
{
    [Description("QUSB2SNES/USB2SNES")]
    Usb2Snes,
    
    [Description("SNI")]
    Sni,
    
    [Description("Lua Script")]
    Lua,
    
    [Description("EmoTracker Lua Script")]
    LuaEmoTracker,
    
    [Description("Crowd Control Lua Script (BizHawk only)")]
    LuaCrowdControl,
}