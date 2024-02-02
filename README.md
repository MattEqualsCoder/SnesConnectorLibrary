# SNES Connector Library

The SNES Connector Library is a C# based nuget package intended to facilitate communicating with the various types of methods of retrieving and updating data in SNES emulators and hardware.

The following was tested as part of the creation of this library:

- SNI with FxPak Pro, RetroArch, snes9x-rr, snes9x-nwa, BizHawk
- QUSB2SNES with FxPak Pro, RetroArch, snes9x-rr, snes9x-nwa, and BizHawk
- Lua scripts for snes9x-rr and BizHawk, including the EmoTracker and Crowd Control Lua scripts

## Installation

Add the MattEqualsCoder.SNESConnectorLibrary nuget package to your project: https://www.nuget.org/packages/MattEqualsCoder.SnesConnectorLibrary/0.1.0

## Instantiating ISnesConnectorService

You can instantiate the ISnesConnectorLibrary in two ways:

1. Using dependency injection (recommended)

When building your services, you can use the following code to add all of the SNES Connector Library services. This is recommended if you use the Microsoft Extensions Logging so that it'll print info and exception statements when connecting and disconnecting.

```
var serviceCollection = new ServiceCollection()
    .AddSnesConnectorServices();
var services = serviceCollection.BuildServiceProvider();
var snesConnectorService = services.GetRequiredService<ISnesConnectorService>();
```

2. Using the static function

You can also call the following function, but it will not include any logging:

```
var snesConnectorService = ISnesConnectorService.CreateService();
```

## Connecting

Connecting is as simple as calling the SNES Connector Service's Connect method. You can either pass it just the SnesConnectorType desired, or pass in an SnesConnectorSettings object. By using the SnesConnectorSettings object, you can allow people to change the IP addresses used by the connectors. This can be useful for users who may have it changed for whatever reason (like me!)

The types of connectors are as follows. Only one connector can be used as a single time, but any or all of them can be used as options for your users. Note that there are multiple types of Lua connectors because each one works slightly differently and uses different ports. If a user is using multiple Lua scripts simultaneously (say for a tracker and crowd control), then it's imperitive that they use different ports. Because of this, there are multiple Lua script connectors available.

- **SnesConnectorType.Usb2Snes**: Connector for the [QUSB2SNES application](https://skarsnik.github.io/QUsb2snes/). A cross platform application that can be used to communicate to various hardware and emulators via a socket connection. This or SNI are the suggested methods of connecting.
- **SnesConnectorType.Sni**: Connector for the [SNI application](https://github.com/alttpo/sni), a cross platform gRPC application that can be used to communicate to various hardware and emulators via a gRPC connection. This or QUSB2SNES are the suggested methods of connecting.
- **SnesConnectorType.Lua**: Connector that can be used for [snes9x-rr](https://github.com/gocha/snes9x-rr) and [BizHawk](https://tasvideos.org/Bizhawk) by loading up the provided Lua script in them. This Lua script uses a unique port from the Emo Tracker and Crowd Control Lua scripts, so if users are using your application alongside them, then this is recommended over the other Lua connectors.
- **SnesConnectorType.LuaEmoTracker**: Connector that uses the Lua script provided with [EmoTracker](https://emotracker.net/). Including this as an option can be useful for trackers where users may use EmoTracker for other games/events so that they don't have to switch Lua scripts.
- **SNesConnectorType.LuaCrowdControl**: Similar to the LuaEmoTracker, only it uses the Lua script provided with [Crowd Control](https://crowdcontrol.live/).

### Creating Lua Scripts

The SNES Connector Library includes Lua scripts to be used for the connector type SnesConnectorType.Lua. You will need to make those Lua scripts available and direct the user to them to use for snes9x-rr and BizHawk. You can create the Lua scripts using the Snes Connector Service CreateLuaScriptsFolder method, like the following:

```
var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnesLua")
_snesConnectorService.CreateLuaScriptsFolder(path);
```

## Making Requests

There are two functions for making requests, depending on the need. All requests are asynchronous and added to a queue for processing at certain intervals. This is done due to the limitations of some connectors of not working well with multiple requests being sent simultaneously, requiring only request to be pending at a time.

### Single Requests

You can make a single memory retrieval or update request using the SNES Connector Service MakeRequest method.

```
// Makes a single request to retrieve 2 bytes from the WRAM location 0x7E09C2
_snesConnectorService.MakeRequest(new SnesMemoryRequest()
{
    RequestType = SnesMemoryRequestType.Update, 
    SnesMemoryDomain = SnesMemoryDomain.ConsoleRAM,
    SniMemoryMapping = MemoryMapping.ExHiRom,
    AddressFormat = AddressFormat.Snes9x,
    Address = 0x7E09C2,
    Length = 2,
    OnResponse = data =>
    {
        Console.WriteLine(data.ReadUInt16(0x7E09C2));
    }
});

// Makes a single request to update the WRAM location 0x7E09C2
_snesConnectorService.MakeRequest(new SnesMemoryRequest()
{
    RequestType = SnesMemoryRequestType.Update, 
    SnesMemoryDomain = SnesMemoryDomain.ConsoleRAM,
    SniMemoryMapping = MemoryMapping.ExHiRom,
    AddressFormat = AddressFormat.Snes9x,
    Address = 0x7E09C2,
    Data =  = new byte[] { 0xFF, 0xA0 }
});
```

The fields are as follows:

- **RequestType**: Whether this is retrieving or updating memory
- **SnesMemoryDomain**: What type of memory is being requested. Either ConsoleRAM (WRAM), CartridgeSave (SRAM), or Rom.
- **AddressFormat**: The address format used in the request so that the library can convert it to the proper format for the connector type. Can be either Snes9x, BizHawk, or FxPakPro. The differences are explained below.
- **SniMemoryMapping**: The MemoryMapping for the cart. Required for SNI. Either HiRom, LoRom, ExHiRom, or SA1.
- **Address**: The starting address location to retrieve or update.
- **Length**: For retrievals only. The number of bytes to retrieve from the SNES connector.
- **OnResponse**: For retrievals only. A callback action after retrieving the data from the SNES connector.
- **Data**: For updates only. The bytes to send to the SNES connector to update.

### Recurring Requests

You can make recurring requests that will repeat at certain intervals using the SNES Connector Service AddRecurringRequest method.

```
// Get the rom title from the ROM data
var request = _snesConnectorService.AddRecurringRequest(new SnesRecurringMemoryRequest()
{
    RequestType = SnesMemoryRequestType.Retrieve, 
    SnesMemoryDomain = SnesMemoryDomain.Rom,
    SniMemoryMapping = MemoryMapping.ExHiRom,
    AddressFormat = AddressFormat.Snes9x,
    Address = 0x00FFC0,
    Length = 20,
    FrequencySeconds = 0.5,
    RespondOnChangeOnly = true,
    OnResponse = data =>
    {
        _model.Title = Encoding.ASCII.GetString(data.Raw);
    },
    Filter = () => _model.CurrentGame == "Super Metroid"
});
```

The fields are the same as SnesMemoryRequest, only with the following fields added:

- **FrequencySeconds**: How frequently the memory should be polled from the connector.
- **RespondOnChangeOnly**: For retrievals only. Only calls the OnResponse call back if the values retrieved from the SNES connector have changed since the previous call.
- **Filter**: If provided, this will be called to see if the request should be made or not.

If requests ever need to be removed and not called anymore, you can call the SNES Connector Service RemoveRecurringRequest method, passing in the request object passed into (or returned by) the AddRecurringRequest method.

### Address Formats

There are three different address formats that can be used which are outlined below. When creating requests, you can specify any of these as long as the address locations match the expected ranges. The SNES Connector Service will automatically adjust it to match what is required for the particular connector.

#### Snes9x

| Memory Type | Addresses |
| ---- | ---- |
| ConsoleRAM (WRAM) | 0x7E0000 - 0x7FFFFF |
| CartridgeSave (SRAM) | 0xA06000, 0xA07000, 0xA16000, 0xA17000 ... 0x11F7FFF |
| Rom | 0x000000 - 0xDFFFFF

#### BizHawk

| Memory Type | Addresses |
| ---- | ---- |
| ConsoleRAM (WRAM) | 0x000000 - 0x01FFFF |
| CartridgeSave (SRAM) | 0x000000 - 0x0FFFFF |
| Rom | 0x000000 - 0xDFFFFF

#### Fx Pak Pro

| Memory Type | Addresses |
| ---- | ---- |
| ConsoleRAM (WRAM) | 0xF50000 - 0xF6FFFF |
| CartridgeSave (SRAM) | 0xE00000 - 0xEFFFFF |
| Rom | 0x000000 - 0xDFFFFF

### SnesData Object

The SnesData object is a wrapper around the bytes returned from a retrieval request. It's got multiple functions which can be called to retrieve and check the data that was received.

- **ReadUInt8** - Returns a byte from the SNES data. By default, you can enter the exact memory address similar to what you looked up, but if you set isRaw = true, then you'll want to enter the offset from the address requested. And example is below.

    ```
    _snesConnectorService.MakeRequest(new SnesMemoryRequest()
    {
        RequestType = SnesMemoryRequestType.Update, 
        SnesMemoryDomain = SnesMemoryDomain.ConsoleRAM,
        SniMemoryMapping = MemoryMapping.ExHiRom,
        AddressFormat = AddressFormat.Snes9x,
        Address = 0x7E09C2,
        Length = 2,
        OnResponse = data =>
        {
            // The below will return the same value
            Console.WriteLine(data.ReadUInt16(0x7E09C2));
            Console.WriteLine(data.ReadUInt16(0, true));
        }
    });
    ```
- **CheckUInt8Flag** - Checks if the binary flag matches the byte at the location
- **ReadUInt16** - Returns a 16 bit unsigned integer from two bytes.
- **CheckInt16Flag** - Checks if a binary flag matches the two bytes at the location
- **Raw** - This is the raw bytes returned from the connector

## Snes Connector App

The SNES Connector App is a very simple cross platform UI app that can be used as an example of how to request and update different types of memory. This was made with [the SMZ3 Cas' fork](https://github.com/Vivelin/SMZ3Randomizer) in mind, but I believe it should work with mainline SMZ3 as well. It'll display the rom game title, determine whether you're in Metroid or Zelda, and get either Link's or Samus's X, Y coordinates depending on the current game. There are a couple buttons that can be used to either refill your hearts/energy or give you 20 rupee items.

![image](https://github.com/MattEqualsCoder/SnesConnectorLibrary/assets/63823784/a47c20c5-5b73-4126-823d-488e1877a884)

## Known Connector Issues

As of this moment, there are a few known limitations for some connectors due to downstream services.

- SNI and RetroArch on Linux have issues when SNI loses a connection to RetroArch either from restarting the application or when RetroArch creates a new process after selecting a rom.
- QUSB2SNES is unable to write to the Console RAM / WRAM.

## Credits

- Skarsnik for QUsb2Snes
- jsd1982 for SNI
- Berserker, jsd1982, zig, and others for Lua scripts that I used to flesh out the Lua scripts in this repo
- EmoTracker creators as when I originally dug into memory reading, it was based on some of the provided scripts in it.
