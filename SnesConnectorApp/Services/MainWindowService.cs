using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AvaloniaControls.ControlServices;
using Microsoft.Extensions.Logging;
using SnesConnectorApp.ViewModels;
using SnesConnectorLibrary;
using SnesConnectorLibrary.Requests;
using SNI;

namespace SnesConnectorApp.Services;

public class MainWindowService(ILogger<MainWindowService> logger, ISnesConnectorService snesConnectorService) : IControlService
{
    public MainWindowViewModel Model { get; set; } = new();

    public MainWindowViewModel InitializeModel()
    {
        snesConnectorService.CreateLuaScriptsFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SnesConnectorApp"));
        
        // Retrieve whether the player is currently in Super Metroid or A Link to the Past from SRAM
        snesConnectorService.AddRecurringMemoryRequest(new SnesRecurringMemoryRequest()
        {
            MemoryRequestType = SnesMemoryRequestType.RetrieveMemory,
            SnesMemoryDomain = SnesMemoryDomain.CartridgeSave,
            AddressFormat = AddressFormat.Snes9x,
            SniMemoryMapping = MemoryMapping.ExHiRom,
            Address = 0xA173FE,
            Length = 2,
            FrequencySeconds = 0.5,
            OnResponse = data =>
            {
                Model.CurrentGame = data.ReadUInt8(0) == 0xFF ? "Super Metroid" : "A Link to the Past";
            },
        });
        
        // If in ALttP, get the player's X,Y coordinates from WRAM
        snesConnectorService.AddRecurringMemoryRequest(new SnesRecurringMemoryRequest()
        {
            MemoryRequestType = SnesMemoryRequestType.RetrieveMemory,
            SnesMemoryDomain = SnesMemoryDomain.ConsoleRAM,
            AddressFormat = AddressFormat.Snes9x,
            SniMemoryMapping = MemoryMapping.ExHiRom,
            Address = 0x7E0020,
            Length = 4,
            FrequencySeconds = 0.5,
            RespondOnChangeOnly = true,
            OnResponse = data =>
            {
                Model.Position = $"({data.ReadUInt16(2)}, {data.ReadUInt16(0)})";
            },
            Filter = () => Model.CurrentGame == "A Link to the Past"
        });
        
        // If in SM, get the player's X,Y coordinates from WRAM
        snesConnectorService.AddRecurringMemoryRequest(new SnesRecurringMemoryRequest()
        {
            MemoryRequestType = SnesMemoryRequestType.RetrieveMemory,
            SnesMemoryDomain = SnesMemoryDomain.ConsoleRAM,
            AddressFormat = AddressFormat.Snes9x,
            SniMemoryMapping = MemoryMapping.ExHiRom,
            Address = 0x7E0AF6,
            Length = 8,
            FrequencySeconds = 0.5,
            RespondOnChangeOnly = true,
            OnResponse = data =>
            {
                Model.Position = $"({data.ReadUInt16(0)}, {data.ReadUInt16(4)})";
            },
            Filter = () => Model.CurrentGame == "Super Metroid"
        });
        
        // Get the rom title from the ROM data
        snesConnectorService.AddRecurringMemoryRequest(new SnesRecurringMemoryRequest()
        {
            MemoryRequestType = SnesMemoryRequestType.RetrieveMemory,
            SnesMemoryDomain = SnesMemoryDomain.Rom,
            AddressFormat = AddressFormat.Snes9x,
            SniMemoryMapping = MemoryMapping.ExHiRom,
            Address = 0x00FFC0,
            Length = 20,
            FrequencySeconds = 0.5,
            OnResponse = data =>
            {
                Model.Title = Encoding.ASCII.GetString(data.Raw);
            }
        });
        
        snesConnectorService.Connected += (sender, args) =>
        {
            Model.IsConnected = true;
            Model.Status = "Connected";
        };
        
        snesConnectorService.Disconnected += (sender, args) =>
        {
            Model.IsConnected = false;
            Model.Status = "Disconnected";
            Model.CurrentGame = "N/A";
            Model.Position = "N/A";
            Model.Title = "N/A";
            Model.Roms = [];
            Model.SelectedRom = null;
        };
        
        return Model;
    }

    public void Connect()
    {
        if (Model.ConnectorType == SnesConnectorType.None)
        {
            return;
        }

        logger.LogInformation("Connecting");
        Model.Status = "Connecting";
        snesConnectorService.Connect(Model.ConnectorType);
    }

    public void RefillHealth()
    {
        if (Model.CurrentGame == "Super Metroid")
        {
            // Request the max energy, then set the player's energy to its value
            snesConnectorService.MakeMemoryRequest(new SnesSingleMemoryRequest()
            {
                MemoryRequestType = SnesMemoryRequestType.RetrieveMemory, 
                SnesMemoryDomain = SnesMemoryDomain.ConsoleRAM,
                AddressFormat = AddressFormat.Snes9x,
                SniMemoryMapping = MemoryMapping.ExHiRom,
                Address = 0x7E09C4,
                Length = 2,
                OnResponse = data =>
                {
                    var maxEnergy = data.ReadUInt16(0);
                    if (maxEnergy == null) return;
                    snesConnectorService.MakeMemoryRequest(new SnesSingleMemoryRequest()
                    {
                        MemoryRequestType = SnesMemoryRequestType.UpdateMemory, 
                        SnesMemoryDomain = SnesMemoryDomain.ConsoleRAM,
                        AddressFormat = AddressFormat.Snes9x,
                        SniMemoryMapping = MemoryMapping.ExHiRom,
                        Address = 0x7E09C2,
                        Data = data.Raw
                    });
                }
            });
        }
        else
        {
            // Request the max hearts, then set the player's health to its value
            snesConnectorService.MakeMemoryRequest(new SnesSingleMemoryRequest()
            {
                MemoryRequestType = SnesMemoryRequestType.RetrieveMemory, 
                SnesMemoryDomain = SnesMemoryDomain.ConsoleRAM,
                AddressFormat = AddressFormat.Snes9x,
                SniMemoryMapping = MemoryMapping.ExHiRom,
                Address = 0x7EF36C,
                Length = 1,
                OnResponse = data =>
                {
                    var maxHealth = data.ReadUInt8(0);
                    if (maxHealth == null) return;
                    snesConnectorService.MakeMemoryRequest(new SnesSingleMemoryRequest()
                    {
                        MemoryRequestType = SnesMemoryRequestType.UpdateMemory, 
                        SnesMemoryDomain = SnesMemoryDomain.ConsoleRAM,
                        AddressFormat = AddressFormat.Snes9x,
                        SniMemoryMapping = MemoryMapping.ExHiRom,
                        Address = 0x7EF372,
                        Data = data.Raw
                    });
                }
            });
        }
    }

    public void GiveItem()
    {
        snesConnectorService.MakeMemoryRequest(new SnesSingleMemoryRequest()
        {
            MemoryRequestType = SnesMemoryRequestType.RetrieveMemory, 
            SnesMemoryDomain = SnesMemoryDomain.CartridgeSave,
            AddressFormat = AddressFormat.Snes9x,
            SniMemoryMapping = MemoryMapping.ExHiRom,
            Address = 0xA26602,
            Length = 2,
            OnResponse = data =>
            {
                var giftedItemCount = data.ReadUInt16(0);
                if (giftedItemCount == null) return;
                
                // Give the player the item from "player 0"
                snesConnectorService.MakeMemoryRequest(new SnesSingleMemoryRequest()
                {
                    MemoryRequestType = SnesMemoryRequestType.UpdateMemory, 
                    SnesMemoryDomain = SnesMemoryDomain.CartridgeSave,
                    AddressFormat = AddressFormat.Snes9x,
                    SniMemoryMapping = MemoryMapping.ExHiRom,
                    Address = 0xA26000 + giftedItemCount.Value * 4,
                    Data = Int16ToBytes(0).Concat(Int16ToBytes(0x36)).ToArray()
                });
                
                // Increase the number of gifted items by 1
                snesConnectorService.MakeMemoryRequest(new SnesSingleMemoryRequest()
                {
                    MemoryRequestType = SnesMemoryRequestType.UpdateMemory, 
                    SnesMemoryDomain = SnesMemoryDomain.CartridgeSave,
                    AddressFormat = AddressFormat.Snes9x,
                    SniMemoryMapping = MemoryMapping.ExHiRom,
                    Address = 0xA26602,
                    Data = Int16ToBytes(giftedItemCount.Value + 1)
                });
            }
        });
    }

    public void ScanFiles()
    {
        Model.Status = "Scanning for roms";
        snesConnectorService.GetFileList(new SnesFileListRequest()
        {
            Path = "",
            Recursive = true,
            Filter = file => file.Name.EndsWith(".sfc", StringComparison.OrdinalIgnoreCase) || file.Name.EndsWith(".smc", StringComparison.OrdinalIgnoreCase),
            OnResponse = (files) =>
            {
                Model.Roms = files.Select(x => x.FullPath).ToList();
                Model.SelectedRom = Model.Roms.First();
                Model.Status = $"{Model.Roms.Count} roms found";
            }
        });
    }

    public void BootRom()
    {
        if (string.IsNullOrEmpty(Model.SelectedRom))
        {
            return;
        }
        
        Model.Status = $"Booting {Model.SelectedRom}";

        snesConnectorService.BootRom(new SnesBootRomRequest()
        {
            Path = Model.SelectedRom,
            OnComplete = () =>
            {
                Model.Status = $"{Model.SelectedRom} booted";
            }
        });
    }

    public void UploadFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }
        
        var file = new FileInfo(filePath);

        Model.Status = $"Uploading {filePath} to /{file.Name}";

        snesConnectorService.UploadFile(new SnesUploadFileRequest()
        {
            LocalFilePath = filePath,
            TargetFilePath = "/" + file.Name,
            OnComplete = () =>
            {
                Model.Status = $"Upload of {filePath} complete";
                ScanFiles();
            }
        });
    }
    
    public void DeleteFile()
    {
        if (string.IsNullOrEmpty(Model.SelectedRom))
        {
            return;
        }
        
        Model.Status = $"Deleting {Model.SelectedRom}";

        snesConnectorService.DeleteFile(new SnesDeleteFileRequest()
        {
            Path = Model.SelectedRom,
            OnComplete = () =>
            {
                Model.Status = $"{Model.SelectedRom} deleted";
                ScanFiles();
            }
        });
    }

    private static byte[] Int16ToBytes(int value)
    {
        var bytes = BitConverter.GetBytes((short)value).ToList();
        if (!BitConverter.IsLittleEndian)
        {
            bytes.Reverse();
        }
        return bytes.ToArray();
    }
}