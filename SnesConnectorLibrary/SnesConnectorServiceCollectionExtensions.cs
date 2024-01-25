using Microsoft.Extensions.DependencyInjection;
using SnesConnectorLibrary.Usb2Snes;

namespace SnesConnectorLibrary;

public static class SnesConnectorServiceCollectionExtensions
{
    public static IServiceCollection AddSnesConnectorServices(this IServiceCollection services)
    {
        services.AddSingleton<SnesConnectorService>()
            .AddSingleton<Usb2SnesConnector>();

        return services;
    }
}