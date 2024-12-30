using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaControls;
using AvaloniaControls.Controls;
using AvaloniaControls.Models;
using AvaloniaControls.Services;
using SnesConnectorApp.Services;
using SnesConnectorApp.ViewModels;

namespace SnesConnectorApp.Views;

public partial class MainWindow : RestorableWindow
{
    private MainWindowService? _service;
    
    public MainWindow()
    {
        InitializeComponent();
        if (Design.IsDesignMode)
        {
            DataContext = new MainWindowViewModel()
            {
                Roms =
                {
                    "/Roms/test.sfc",
                    "/Roms/test2.sfc",
                    "/Roms/test3.sfc",
                    "/Roms/test4.sfc",
                    "/Roms/test5.sfc",
                }
            };
            return;
        }
        _service = IControlServiceFactory.GetControlService<MainWindowService>();
        DataContext = _service?.InitializeModel();
    }

    protected override string RestoreFilePath => "main-window.json";
    protected override int DefaultWidth => 800;
    protected override int DefaultHeight => 385;

    private void EnumComboBox_OnValueChanged(object sender, EnumValueChangedEventArgs args)
    {
        _service?.Connect();
    }

    private void RefillHealthButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _service?.RefillHealth();
    }
    

    private void GiveItemButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _service?.GiveItem();
    }

    private void ScanFilesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _service?.ScanFiles();
    }

    private void LoadRomButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _service?.BootRom();
    }

    private void FileControl_OnOnUpdated(object? sender, FileControlUpdatedEventArgs e)
    {
        _service?.UploadFile(e.Path);
    }

    private void DeleteRomButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _service?.DeleteFile();
    }

    private void RefillHealthAsyncButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = _service?.RefillHealthAsync();
    }

    private void GiveItemButtonAsync_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = _service?.GiveItemAsync();
    }

    private void ScanFoldersButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _service?.ScanFolders();
    }

    private async void CreateDirectoryButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var messageWindow = new MessageWindow(new MessageWindowRequest()
            {
                Buttons = MessageWindowButtons.OKCancel,
                DisplayTextBox = true,
                Message = "Enter the directory path you want to create",
            });
        
            await messageWindow.ShowDialog(this);

            if (messageWindow.DialogResult?.PressedAcceptButton != true)
            {
                return;
            }
        
            _service?.CreateDirectory(messageWindow.DialogResult.ResponseText);
        }
        catch
        {
            // Do nothing
        }
    }
}