using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaloniaControls.Controls;
using Microsoft.Extensions.DependencyInjection;
using SnesConnectorApp.Views;

namespace SnesConnectorApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && Program.MainHost != null)
        {
            var mainWindow = Program.MainHost.Services.GetRequiredService<MainWindow>();
            MessageWindow.GlobalParentWindow = mainWindow;
            desktop.MainWindow = mainWindow;
            ExceptionWindow.ParentWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}