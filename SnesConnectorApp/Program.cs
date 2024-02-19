using Avalonia;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SnesConnectorLibrary;

namespace SnesConnectorApp;

class Program
{
    internal static IHost? MainHost { get; private set; }
    
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.File("snes-connector-app_.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
            .WriteTo.Console()
            .WriteTo.Debug()
            .CreateLogger();
        
        MainHost = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureLogging(logging =>
            {
                logging.AddSerilog(dispose: true);
            })
            .ConfigureServices(services =>
            {
                services.AddSnesConnectorServices();
                services.AddSingleton<MainWindow>();
            })
            .Build();
        
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}