using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;
using SSW_x_Vonage_Clean_Architecture.WebApi.HealthChecks;
using SSW_x_Vonage_Clean_Architecture.WebApi.Services;

namespace SSW_x_Vonage_Clean_Architecture.WebApi;
// TODO: Can we remove this?
// #pragma warning disable IDE0055

public static class DependencyInjection
{
    public static void AddWebApi(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddOpenApi();

        services.AddHealthChecks(config);
    }
}
// #pragma warning restore IDE0055