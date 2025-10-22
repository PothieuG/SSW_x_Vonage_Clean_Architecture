using Azure.Identity;
using EntityFramework.Exceptions.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;
using SSW_x_Vonage_Clean_Architecture.Infrastructure.OneDrive;
using SSW_x_Vonage_Clean_Architecture.Infrastructure.Persistence;
using SSW_x_Vonage_Clean_Architecture.Infrastructure.Persistence.Interceptors;
using SSW_x_Vonage_Clean_Architecture.Infrastructure.Services;
using SSW_x_Vonage_Clean_Architecture.Infrastructure.Vonage;

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

        // Configure Vonage settings from appsettings.json
        builder.Services.Configure<VonageSettings>(
            builder.Configuration.GetSection(VonageSettings.SectionName));

        services.AddScoped<IVonageService, VonageService>();

        // Configure OneDrive settings from appsettings.json
        builder.Services.Configure<OneDriveSettings>(
            builder.Configuration.GetSection(OneDriveSettings.SectionName));

        // Register DeviceCodeCredential as Singleton to share token cache across all requests
        services.AddSingleton<DeviceCodeCredential>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<OneDriveSettings>>().Value;
            var logger = sp.GetRequiredService<ILogger<DeviceCodeCredential>>();

            return new DeviceCodeCredential(
                new DeviceCodeCredentialOptions
                {
                    TenantId = settings.TenantId,
                    ClientId = settings.ClientId,
                    // Enable token caching to disk (persists across app restarts)
                    TokenCachePersistenceOptions = new TokenCachePersistenceOptions
                    {
                        Name = "SSW_Vonage_OneDrive_TokenCache"
                    },
                    DeviceCodeCallback = (code, cancellationToken) =>
                    {
                        logger.LogWarning("""

                            ============================================
                            MICROSOFT AUTHENTICATION REQUIRED
                            ============================================
                            To sign in, use a web browser to open the page:
                            {UserCodeUrl}

                            And enter the code: {UserCode}
                            ============================================

                            """, code.VerificationUri, code.UserCode);
                        Console.WriteLine($"\n⚠️  AUTHENTICATION REQUIRED ⚠️");
                        Console.WriteLine($"Open: {code.VerificationUri}");
                        Console.WriteLine($"Enter code: {code.UserCode}\n");
                        return Task.CompletedTask;
                    }
                });
        });

        services.AddScoped<IOneDriveService, OneDriveService>();
        services.AddHttpClient<ITranscriptSummarizer, OllamaTranscriptSummarizer>();


        services.AddSingleton(TimeProvider.System);
    }
}