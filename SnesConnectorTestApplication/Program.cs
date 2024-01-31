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

        var snesConnectorService = s_services.GetRequiredService<ISnesConnectorService>();
        snesConnectorService.Connect(SnesConnectorType.Sni);
        snesConnectorService.AddRecurringRequest(new SnesRecurringMemoryRequest()
        {
            RequestType = SnesMemoryRequestType.Retrieve, 
            SnesMemoryDomain = SnesMemoryDomain.Memory,
            Address = 0x7e09C2,
            Length = 0x400,
            FrequencySeconds = 1,
            OnResponse = data =>
            {
                Log.Information("Response: {Data}", data.ReadUInt16(0x7e09C2));

                /*if (data.ReadUInt16(0x7e09C2) != 321)
                {
                    Log.Information("Updating memory");
                    
                    snesConnectorService.MakeRequest(new SnesMemoryRequest()
                    {
                        RequestType = SnesMemoryRequestType.PutAddress,
                        SnesMemoryDomain = SnesMemoryDomain.Memory,
                        Address = 0x7E09C2,
                        Data = BitConverter.GetBytes((short)321).ToList()
                    });
                }*/
            },
            Filter = () =>
            {
                var datetime = DateTime.Now.Second;
                return datetime % 4 == 0;
            }
        });
        
        Console.ReadKey();
    }
}