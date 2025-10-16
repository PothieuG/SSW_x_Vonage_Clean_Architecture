using SSW_x_Vonage_Clean_Architecture.Infrastructure.Middleware;

namespace SSW_x_Vonage_Clean_Architecture.WebApi.Extensions;

public static class EventualConsistencyMiddlewareExt
{
    public static IApplicationBuilder UseEventualConsistencyMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<EventualConsistencyMiddleware>();
        return app;
    }
}