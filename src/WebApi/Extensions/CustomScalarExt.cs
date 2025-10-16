using Scalar.AspNetCore;

namespace SSW_x_Vonage_Clean_Architecture.WebApi.Extensions;

public static class CustomScalarExt
{
    public static void MapCustomScalarApiReference(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapScalarApiReference(options => options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));
    }
}