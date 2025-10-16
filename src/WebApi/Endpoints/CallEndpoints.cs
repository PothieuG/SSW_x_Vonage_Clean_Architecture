using MediatR;
using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.InitiateCall;
using SSW_x_Vonage_Clean_Architecture.WebApi.Extensions;

namespace SSW_x_Vonage_Clean_Architecture.WebApi.Endpoints;

public static class CallEndpoints
{
    public static void MapCallEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup("calls");

        group
            .MapPost("/initiate", async (
                ISender sender,
                InitiateCallCommand command,
                CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return result.Match(
                    callId => TypedResults.Ok(new { CallId = callId }),
                    CustomResult.Problem);
            })
            .WithName("InitiateCall")
            .ProducesPost();
    }
}
