using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SnesConnectorLibrary;

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

        var snesConnectorService = s_services.GetRequiredService<SnesConnectorService>();
        snesConnectorService.Connect(SnesConnectorType.Lua);
        snesConnectorService.AddScheduledRequest(new SnesScheduledMemoryRequest()
        {
            RequestType = SnesMemoryRequestType.GetAddress, 
            SnesMemoryDomain = SnesMemoryDomain.WRAM,
            Address = 0x7e09C2, 
            Length = 16,
            FrequencySeconds = 1,
            OnResponse = data =>
            {
                Log.Information("Response: {Data} {Data2}", data.ReadUInt16(0x7e09C2), data.Raw[0]);

                if (data.ReadUInt16(0x7e09C2) != 321)
                {
                    snesConnectorService.MakeRequest(new SnesMemoryRequest()
                    {
                        RequestType = SnesMemoryRequestType.PutAddress,
                        SnesMemoryDomain = SnesMemoryDomain.WRAM,
                        Address = 0x7E09C2,
                        Data = BitConverter.GetBytes((short)321).ToList()
                    });
                }
            },
            Filter = () =>
            {
                var datetime = DateTime.Now.Second;
                return datetime % 3 == 0;
            }
        });
        
        Console.ReadKey();
    }
}