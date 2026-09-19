using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DuetHQ.Modules.Actions;

// The only public type in this assembly (besides .Contracts): the composition root calls it instead of relying on bare project references.
public static class ActionsModule
{
    public static IServiceCollection AddActions(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services;
    }
}
