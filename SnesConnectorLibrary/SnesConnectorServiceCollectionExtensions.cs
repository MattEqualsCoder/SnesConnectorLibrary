using Microsoft.Extensions.DependencyInjection;
using SnesConnectorLibrary.Connectors;

namespace SnesConnectorLibrary;

public static class SnesConnectorServiceCollectionExtensions
{
    public static IServiceCollection AddSnesConnectorServices(this IServiceCollection services)
    {
        services.AddSingleton<ISnesConnectorService, SnesConnectorService>()
            .AddSingleton<Usb2SnesConnector>()
            .AddSingleton<LuaConnectorDefault>()
            .AddSingleton<LuaConnectorEmoTracker>()
            .AddSingleton<LuaConnectorCrowdControl>()
            .AddSingleton<SniConnector>();

        return services;
    }
}