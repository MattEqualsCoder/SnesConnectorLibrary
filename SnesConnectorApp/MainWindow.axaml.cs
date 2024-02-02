using System;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.VisualBasic.FileIO;
using SnesConnectorLibrary;
using SNI;

namespace SnesConnectorApp;

public partial class MainWindow : Window
{
    private ISnesConnectorService? _snesConnectorService;
    private MainWindowViewModel? _model;
    
    public MainWindow() : this(null)
    {
    }
    
    public MainWindow(ISnesConnectorService? snesConnectorService)
    {
        _snesConnectorService = snesConnectorService;
        DataContext = _model = new MainWindowViewModel();
        InitializeComponent();

        if (_snesConnectorService == null)
        {
            return;
        }

        _snesConnectorService.CreateLuaScriptsFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SnesConnectorApp"));
        
        // Retrieve whether the player is currently in Super Metroid or A Link to the Past from SRAM
        _snesConnectorService.AddRecurringRequest(new SnesRecurringMemoryRequest()
        {
            RequestType = SnesMemoryRequestType.Retrieve, 
            SnesMemoryDomain = SnesMemoryDomain.CartridgeSave,
            AddressFormat = AddressFormat.Snes9x,
            SniMemoryMapping = MemoryMapping.ExHiRom,
            Address = 0xA173FE,
            Length = 2,
            FrequencySeconds = 0.5,
            OnResponse = data =>
            {
                _model.CurrentGame = data.ReadUInt8(0xA173FE) == 0xFF ? "Super Metroid" : "A Link to the Past";
            },
        });
        
        // If in ALttP, get the player's X,Y coordinates from WRAM
        _snesConnectorService.AddRecurringRequest(new SnesRecurringMemoryRequest()
        {
            RequestType = SnesMemoryRequestType.Retrieve, 
            SnesMemoryDomain = SnesMemoryDomain.ConsoleRAM,
            AddressFormat = AddressFormat.Snes9x,
            SniMemoryMapping = MemoryMapping.ExHiRom,
            Address = 0x7E0020,
            Length = 4,
            FrequencySeconds = 0.5,
            RespondOnChangeOnly = true,
            OnResponse = data =>
            {
                _model.Position = $"({data.ReadUInt16(0x7E0022)}, {data.ReadUInt16(0x7E0020)})";
            },
            Filter = () => _model.CurrentGame == "A Link to the Past"
        });
        
        // If in SM, get the player's X,Y coordinates from WRAM
        _snesConnectorService.AddRecurringRequest(new SnesRecurringMemoryRequest()
        {
            RequestType = SnesMemoryRequestType.Retrieve, 
            SnesMemoryDomain = SnesMemoryDomain.ConsoleRAM,
            AddressFormat = AddressFormat.Snes9x,
            SniMemoryMapping = MemoryMapping.ExHiRom,
            Address = 0x7E0AF6,
            Length = 8,
            FrequencySeconds = 0.5,
            RespondOnChangeOnly = true,
            OnResponse = data =>
            {
                _model.Position = $"({data.ReadUInt16(0x7E0AF6)}, {data.ReadUInt16(0x7E0AFA)})";
            },
            Filter = () => _model.CurrentGame == "Super Metroid"
        });
        
        // Get the rom title from the ROM data
        _snesConnectorService.AddRecurringRequest(new SnesRecurringMemoryRequest()
        {
            RequestType = SnesMemoryRequestType.Retrieve, 
            SnesMemoryDomain = SnesMemoryDomain.Rom,
            AddressFormat = AddressFormat.Snes9x,
            SniMemoryMapping = MemoryMapping.ExHiRom,
            Address = 0x00FFC0,
            Length = 20,
            FrequencySeconds = 0.5,
            OnResponse = data =>
            {
                _model.Title = Encoding.ASCII.GetString(data.Raw);
            }
        });
        
        _snesConnectorService.OnConnected += (sender, args) =>
        {
            _model.IsConnected = true;
            _model.IsConnectorConnecting = false;
            _model.IsDisconnected = false;
        };
        
        _snesConnectorService.OnDisconnected += (sender, args) =>
        {
            _model.IsConnected = false;
            _model.IsConnectorConnecting = false;
            _model.IsDisconnected = true;
            _model.CurrentGame = "N/A";
            _model.Position = "N/A";
            _model.Title = "N/A";
        };
    }
    
    private void SelectingItemsControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_model == null)
        {
            return;
        }
        var selectedItem = (sender as ComboBox)!.SelectedItem as string;
        if (string.IsNullOrEmpty(selectedItem) || !_model.ConnectorMap.TryGetValue(selectedItem, out var selectedConnectorType))
        {
            _snesConnectorService?.Disconnect();
            _model.IsConnected = false;
            _model.IsConnectorConnecting = false;
            _model.IsDisconnected = true;
            _model.CurrentGame = "N/A";
            _model.Position = "N/A";
            _model.Title = "N/A";
        }
        else
        {
            _snesConnectorService?.Connect(selectedConnectorType);
            _model.IsConnected = false;
            _model.IsConnectorConnecting = true;
            _model.IsDisconnected = false;
        }
    }

    private void RefillHealthButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_snesConnectorService?.IsConnected != true || _model?.CurrentGame is not ("Super Metroid" or "A Link to the Past"))
        {
            return;
        }

        if (_model.CurrentGame == "Super Metroid")
        {
            // Request the max energy, then set the player's energy to its value
            _snesConnectorService.MakeRequest(new SnesMemoryRequest()
            {
                RequestType = SnesMemoryRequestType.Retrieve, 
                SnesMemoryDomain = SnesMemoryDomain.ConsoleRAM,
                AddressFormat = AddressFormat.Snes9x,
                SniMemoryMapping = MemoryMapping.ExHiRom,
                Address = 0x7E09C4,
                Length = 2,
                OnResponse = data =>
                {
                    var maxEnergy = data.ReadUInt16(0x7E09C4);
                    if (maxEnergy == null) return;
                    _snesConnectorService.MakeRequest(new SnesMemoryRequest()
                    {
                        RequestType = SnesMemoryRequestType.Update, 
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
            _snesConnectorService.MakeRequest(new SnesMemoryRequest()
            {
                RequestType = SnesMemoryRequestType.Retrieve, 
                SnesMemoryDomain = SnesMemoryDomain.ConsoleRAM,
                AddressFormat = AddressFormat.Snes9x,
                SniMemoryMapping = MemoryMapping.ExHiRom,
                Address = 0x7EF36C,
                Length = 1,
                OnResponse = data =>
                {
                    var maxHealth = data.ReadUInt8(0x7EF36C);
                    if (maxHealth == null) return;
                    _snesConnectorService.MakeRequest(new SnesMemoryRequest()
                    {
                        RequestType = SnesMemoryRequestType.Update, 
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

    private void GiveItemButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_snesConnectorService?.IsConnected != true || _model?.CurrentGame is not ("Super Metroid" or "A Link to the Past"))
        {
            return;
        }
        
        // Get the number of items previously given the the player, then give them another 20 rupee item
        _snesConnectorService.MakeRequest(new SnesMemoryRequest()
        {
            RequestType = SnesMemoryRequestType.Retrieve, 
            SnesMemoryDomain = SnesMemoryDomain.CartridgeSave,
            AddressFormat = AddressFormat.Snes9x,
            SniMemoryMapping = MemoryMapping.ExHiRom,
            Address = 0xA26602,
            Length = 2,
            OnResponse = data =>
            {
                var giftedItemCount = data.ReadUInt16(0xA26602);
                if (giftedItemCount == null) return;
                
                // Give the player the item from "player 0"
                _snesConnectorService.MakeRequest(new SnesMemoryRequest()
                {
                    RequestType = SnesMemoryRequestType.Update, 
                    SnesMemoryDomain = SnesMemoryDomain.CartridgeSave,
                    AddressFormat = AddressFormat.Snes9x,
                    SniMemoryMapping = MemoryMapping.ExHiRom,
                    Address = 0xA26000 + giftedItemCount.Value * 4,
                    Data = Int16ToBytes(0).Concat(Int16ToBytes(0x36)).ToArray()
                });
                
                // Increase the number of gifted items by 1
                _snesConnectorService.MakeRequest(new SnesMemoryRequest()
                {
                    RequestType = SnesMemoryRequestType.Update, 
                    SnesMemoryDomain = SnesMemoryDomain.CartridgeSave,
                    AddressFormat = AddressFormat.Snes9x,
                    SniMemoryMapping = MemoryMapping.ExHiRom,
                    Address = 0xA26602,
                    Data = Int16ToBytes(giftedItemCount.Value + 1)
                });
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