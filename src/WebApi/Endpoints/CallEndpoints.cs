using MediatR;
using Microsoft.Extensions.Options;
using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;
using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleRecording;
using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleTranscription;
using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.InitiateCall;
using SSW_x_Vonage_Clean_Architecture.Infrastructure.OneDrive;
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

        // Authentication and test endpoint for OneDrive connection
        // IMPORTANT: This endpoint must be called FIRST to authenticate with OneDrive
        // It triggers the device code flow which caches the access token for subsequent uploads
        group
            .MapGet("/connection-to-one-drive", async (
                IOneDriveService oneDriveService,
                IOptions<OneDriveSettings> settings) =>
            {
                var config = settings.Value;

                // Create a test stream to verify connection
                var testContent = $"OneDrive Connection Test - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n" +
                                  "This file was created to test and authenticate the OneDrive connection.\n" +
                                  "Subsequent recording uploads will use the cached authentication token.";
                var testStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testContent));

                var fileName = $"connection_test_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";

                var result = await oneDriveService.UploadFileFromStreamAsync(
                    testStream,
                    fileName,
                    "ConnectionTests");

                return result.Match<IResult>(
                    fileId => TypedResults.Ok(new
                    {
                        Success = true,
                        Message = "OneDrive authentication successful! Token cached for future uploads.",
                        FileId = fileId,
                        FileName = fileName,
                        UploadedTo = $"{config.UploadFolderPath}/ConnectionTests/{fileName}",
                        NextSteps = new[]
                        {
                            "Authentication token is now cached",
                            "Recording uploads will work automatically",
                            "Token will be refreshed automatically when needed"
                        },
                        Configuration = new
                        {
                            config.TenantId,
                            config.ClientId,
                            config.UploadFolderPath,
                            AuthMethod = "Device Code (Interactive - Delegated Permissions)"
                        }
                    }),
                    errors => TypedResults.BadRequest(new
                    {
                        Success = false,
                        Message = "OneDrive authentication failed",
                        Errors = errors.Select(e => new { e.Code, e.Description }),
                        Configuration = new
                        {
                            config.TenantId,
                            config.ClientId,
                            config.UploadFolderPath,
                            AuthMethod = "Device Code (Interactive)"
                        },
                        TroubleshootingSteps = new[]
                        {
                            "1. Verify Azure AD App Registration exists with correct Client ID",
                            "2. Check API Permissions: Files.ReadWrite (Delegated type) + Admin consent granted",
                            "3. Enable 'Allow public client flows' in Authentication settings",
                            "4. Follow the device code authentication prompt (code will appear in console)",
                            "5. Ensure you have OneDrive provisioned (login to onedrive.com at least once)",
                            "6. Check that TenantId in appsettings.json matches your Azure AD tenant"
                        }
                    }));
            })
            .WithName("AuthenticateOneDrive")
            .WithDescription("Authenticate to OneDrive using device code flow. Call this endpoint BEFORE making test calls to cache the authentication token.")
            .Produces(200)
            .Produces(400)
            .AllowAnonymous(); // For debugging purposes
    }
}
