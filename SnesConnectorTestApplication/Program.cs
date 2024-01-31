using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SnesConnectorLibrary;
using SNI;

namespace SnesConnectorTestApplication;

public static class Program
{
    private static ServiceProvider s_services = null!;

    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Debug()
            .CreateLogger();
        
        s_services = new ServiceCollection()
            .AddLogging(logging =>
            {
                logging.AddSerilog(dispose: true);
            })
            .AddSnesConnectorServices()
            .BuildServiceProvider();

        Console.ReadKey();
    }

}