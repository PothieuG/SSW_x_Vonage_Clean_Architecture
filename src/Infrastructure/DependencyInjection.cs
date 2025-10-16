using EntityFramework.Exceptions.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;
using SSW_x_Vonage_Clean_Architecture.Infrastructure.Persistence;
using SSW_x_Vonage_Clean_Architecture.Infrastructure.Persistence.Interceptors;

namespace SSW_x_Vonage_Clean_Architecture.Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddSqlServerDbContext<ApplicationDbContext>("CleanArchitecture",
            null,
            options =>
            {
                var serviceProvider = builder.Services.BuildServiceProvider();
                options.AddInterceptors(
                    serviceProvider.GetRequiredService<EntitySaveChangesInterceptor>(),
                    serviceProvider.GetRequiredService<DispatchDomainEventsInterceptor>());

                // Return strongly typed useful exceptions
                options.UseExceptionProcessor();
            });

        var services = builder.Services;

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<EntitySaveChangesInterceptor>();
        services.AddScoped<DispatchDomainEventsInterceptor>();

        services.AddSingleton(TimeProvider.System);
    }
}