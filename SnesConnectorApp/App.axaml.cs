using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SnesConnectorLibrary;

namespace SnesConnectorApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.File("snes-connector-app_.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
            .WriteTo.Console()
            .WriteTo.Debug()
            .CreateLogger();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var serviceCollection = new ServiceCollection()
                .AddSnesConnectorServices()
                .AddLogging(logging =>
                {
                    logging.AddSerilog(dispose: true);
                })
                .AddSingleton<MainWindow>();
            var services = serviceCollection.BuildServiceProvider();
            desktop.MainWindow = services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}