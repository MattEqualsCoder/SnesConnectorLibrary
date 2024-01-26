using Microsoft.Extensions.DependencyInjection;
using SnesConnectorLibrary.Connectors;

namespace SnesConnectorLibrary;

public static class SnesConnectorServiceCollectionExtensions
{
    public static IServiceCollection AddSnesConnectorServices(this IServiceCollection services)
    {
        services.AddSingleton<SnesConnectorService>()
            .AddSingleton<Usb2SnesConnector>()
            .AddSingleton<LuaConnector>();

        return services;
    }
}