using MediatR;
using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleRecording;
using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleTranscription;
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

        group
            .MapPost("/recorded", async (
                ISender sender,
                RecordingCallbackRequest request,
                CancellationToken ct) =>
            {
                var command = new HandleRecordingCommand(request);
                var result = await sender.Send(command, ct);
                return result.Match(
                    _ => TypedResults.Ok(new { Message = "Recording callback processed successfully" }),
                    CustomResult.Problem);
            })
            .WithName("RecordingCallback")
            .ProducesPost()
            .AllowAnonymous(); // Vonage webhooks don't use bearer tokens

        group
            .MapPost("/transcribed", async (
                ISender sender,
                TranscriptionCallbackRequest request,
                CancellationToken ct) =>
            {
                var command = new HandleTranscriptionCommand(request);
                var result = await sender.Send(command, ct);
                return result.Match(
                    _ => TypedResults.Ok(new { Message = "Transcription callback processed successfully" }),
                    CustomResult.Problem);
            })
            .WithName("TranscriptionCallback")
            .ProducesPost()
            .AllowAnonymous(); // Vonage webhooks don't use bearer tokens
    }
}
