using MediatR;
using Microsoft.Extensions.Options;
using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;
using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleRecording;
using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.HandleTranscription;
using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.InitiateCall;
using SSW_x_Vonage_Clean_Architecture.Application.UseCases.Calls.Commands.SendSms;
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

        // ============================================================================
        // TRANSCRIPTION WEBHOOK - FIRE-AND-FORGET PATTERN
        // ============================================================================
        // PROBLEM: Vonage webhooks have a ~10 second timeout. If the webhook endpoint
        // doesn't respond within this time, Vonage retries the webhook, causing duplicates.
        //
        // WHY THIS WAS AN ISSUE:
        // - MCP processing (with Ollama AI) takes 30-60+ seconds
        // - Processing synchronously would timeout Vonage's webhook
        // - This caused webhook retries and duplicate processing
        //
        // SOLUTION: Fire-and-forget pattern
        // 1. Respond immediately (200 OK) to Vonage webhook (< 100ms)
        // 2. Process transcript in background using Task.Run()
        // 3. Create new DI scope to avoid ObjectDisposedException
        //
        // IMPORTANT: We inject IServiceProvider (not ISender) because:
        // - The request scope is disposed when we return 200 OK
        // - Using injected ISender directly would cause ObjectDisposedException
        // - We create a NEW scope inside Task.Run() that stays alive during processing
        //
        // REFERENCE: See docs/MCP_TIMEOUT_RESOLUTION.md for full details
        // ============================================================================
        group
            .MapPost("/transcribed", (
                TranscriptionCallbackRequest request,
                ILogger<Program> logger,
                IServiceProvider serviceProvider) =>  // IServiceProvider is a singleton - never disposed
            {
                logger.LogInformation(
                    "Transcription webhook received for conversation {ConversationUuid}, recording {RecordingUuid}",
                    request.ConversationUuid,
                    request.RecordingUuid);

                var command = new HandleTranscriptionCommand(request);

                // Fire-and-forget: Process in background without blocking webhook response
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Create NEW scope for background processing (avoids ObjectDisposedException)
                        // This scope stays alive for the entire duration of the background task
                        using var scope = serviceProvider.CreateScope();
                        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

                        // Process transcript with MCP (can take 30-60+ seconds with Ollama)
                        await sender.Send(command, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        // Log errors but don't propagate (fire-and-forget)
                        logger.LogError(ex, "Background transcription processing failed for {RecordingUuid}",
                            request.RecordingUuid);
                    }
                }, CancellationToken.None);  // Explicitly pass None to indicate intentional fire-and-forget

                // Respond immediately to Vonage webhook (< 100ms)
                // This prevents timeout and duplicate webhook deliveries
                return TypedResults.Ok(new { message = "Transcription received and processing in background" });
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

        // SMS sending endpoint for testing
        group
            .MapPost("/send-sms", async (
                ISender sender,
                SendSmsCommand command,
                CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return result.Match(
                    messageId => TypedResults.Ok(new
                    {
                        Success = true,
                        MessageId = messageId,
                        Message = "SMS sent successfully"
                    }),
                    CustomResult.Problem);
            })
            .WithName("SendSms")
            .WithDescription("Send an SMS message via Vonage. Useful for testing SMS functionality.")
            .ProducesPost()
            .AllowAnonymous(); // For testing purposes
    }
}
