# MCP Timeout Resolution - Complete Troubleshooting Guide

## Executive Summary

This document describes the challenges encountered and solutions implemented to handle long-running AI operations (MCP with Ollama) in a .NET Aspire application with Vonage webhooks.

**Final Solution:**
1. **Manual HttpClient creation** to bypass Aspire's automatic Polly injection
2. **Fire-and-forget pattern** for webhook processing to avoid Vonage timeouts
3. **New DI scope** in background task to avoid ObjectDisposedException

---

## The Problem

### Initial Symptoms

```
Polly.Timeout.TimeoutRejectedException: The operation didn't complete within
the allowed timeout of '00:00:30'.
```

- MCP service consistently timing out after 30 seconds
- Vonage webhooks being retried, causing duplicate processing
- ObjectDisposedException when trying to process webhooks asynchronously

### Root Causes

1. **.NET Aspire's AddServiceDefaults()** automatically injects Polly resilience handlers into ALL HttpClients
2. **Multiple nested timeouts** that cannot be fully overridden:
   - AttemptTimeout: 10 seconds (default)
   - TotalRequestTimeout: 30 seconds (default)
   - Hidden timeouts in RateLimiter and other Polly strategies
3. **Vonage webhook timeout** of ~10 seconds
4. **Ollama AI processing** taking 30-60+ seconds for transcript summarization

---

## Architecture Context

### The MCP Service Flow

```
WebApi (Webhook)
    ↓
HandleTranscriptionCommand
    ↓
McpService
    ↓
HTTP POST → MCP Server (localhost:5000)
    ↓
Ollama AI (phi3.5:3.8b model)
    ↓
Response (30-60+ seconds later)
```

### Why Timeouts Were a Problem

1. **Vonage → WebApi**: Vonage expects webhook response in < 10 seconds
2. **WebApi → MCP Server**: Aspire's default timeout = 30 seconds
3. **MCP Server → Ollama**: AI generation takes 30-60+ seconds
4. **Result**: Timeouts at multiple levels causing failures

---

## Solutions Attempted (Chronological)

### ❌ Attempt 1: Configure Polly Timeouts

**File:** `src/Infrastructure/DependencyInjection.cs`

```csharp
services.AddHttpClient<IMcpService, McpService>(client => {
    client.Timeout = TimeSpan.FromMinutes(2);
})
.AddStandardResilienceHandler(options => {
    options.AttemptTimeout = new HttpTimeoutStrategyOptions {
        Timeout = TimeSpan.FromMinutes(2)
    };
    options.TotalRequestTimeout = new HttpTimeoutStrategyOptions {
        Timeout = TimeSpan.FromMinutes(3)
    };
});
```

**Result:** ❌ Failed - Still timing out after 30 seconds

**Why it failed:**
- Polly has multiple timeout layers (RateLimiter, CircuitBreaker, etc.)
- Each layer can have its own timeout
- Configuration only affected AttemptTimeout and TotalRequestTimeout
- Hidden timeouts in other strategies were still active

---

### ❌ Attempt 2: Increase Circuit Breaker Sampling Duration

```csharp
options.CircuitBreaker = new HttpCircuitBreakerStrategyOptions {
    SamplingDuration = TimeSpan.FromMinutes(5)
};
```

**Result:** ❌ Failed - Validation error

**Error:**
```
The sampling duration of circuit breaker strategy needs to be at least double
of an attempt timeout strategy's timeout interval
```

**Fix:** Increased SamplingDuration to 5 minutes (≥ 2× AttemptTimeout)

**Result:** ❌ Still timing out after 30 seconds

---

### ❌ Attempt 3: Remove AddStandardResilienceHandler()

```csharp
services.AddHttpClient<IMcpService, McpService>(client => {
    client.BaseAddress = new Uri("http://localhost:5000/mcp/");
    client.Timeout = TimeSpan.FromMinutes(5);
});
// No .AddStandardResilienceHandler() call
```

**Result:** ❌ Still timing out

**Why it failed:**
- Aspire's `AddServiceDefaults()` in `Program.cs` globally configures ALL HttpClients
- Even without explicit `.AddStandardResilienceHandler()`, Aspire injects it automatically
- No way to opt-out specific HttpClients from this behavior

---

### ✅ Solution 1: Manual HttpClient Creation

**File:** `src/Infrastructure/DependencyInjection.cs`

```csharp
// Create HttpClient manually - bypasses Aspire's automatic injection
services.AddScoped<IMcpService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<McpService>>();

    var httpClient = new HttpClient
    {
        BaseAddress = new Uri("http://localhost:5000/mcp/"),
        Timeout = Timeout.InfiniteTimeSpan  // No timeout
    };

    return new McpService(httpClient, logger);
});
```

**Why it works:**
- Completely bypasses `AddHttpClient()` and Aspire's interception
- No Polly handlers injected
- Full control over timeout behavior
- HttpClient created directly, not through DI pipeline

**Trade-offs:**
- ✅ Complete control over timeout
- ✅ No hidden Polly behaviors
- ❌ No automatic resilience (retry, circuit breaker)
- ❌ HttpClient not pooled (but okay for single service)

---

### ✅ Solution 2: Fire-and-Forget Pattern for Webhooks

**Problem:** Even with infinite timeout on HttpClient, Vonage webhooks timeout after 10 seconds

**Symptoms:**
- Webhook received twice for same recording
- Vonage retrying due to slow response
- Logs showing duplicate processing

**File:** `src/WebApi/Endpoints/CallEndpoints.cs`

#### Initial (Blocking) Implementation

```csharp
group.MapPost("/transcribed", async (
    ISender sender,
    TranscriptionCallbackRequest request) =>
{
    var command = new HandleTranscriptionCommand(request);
    var result = await sender.Send(command, ct);  // BLOCKS for 30-60s

    return result.Match(
        _ => TypedResults.NoContent(),
        CustomResult.Problem);
});
```

**Problem:** Blocks for 30-60 seconds → Vonage timeout → Retries

#### Solution: Fire-and-Forget Pattern

```csharp
group.MapPost("/transcribed", (
    TranscriptionCallbackRequest request,
    ILogger<Program> logger,
    IServiceProvider serviceProvider) =>  // ← Inject IServiceProvider, not ISender
{
    var command = new HandleTranscriptionCommand(request);

    // Fire-and-forget: Process in background
    _ = Task.Run(async () =>
    {
        try
        {
            // Create NEW scope for background processing
            using var scope = serviceProvider.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            await sender.Send(command, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background processing failed");
        }
    }, CancellationToken.None);

    // Respond immediately (< 100ms)
    return TypedResults.Ok(new {
        message = "Transcription received and processing in background"
    });
});
```

**Why it works:**
1. **Immediate response** (< 100ms) → Vonage doesn't timeout
2. **No retries** → No duplicate processing
3. **Background processing** → MCP can take as long as needed

---

### ❌ Problem 3: ObjectDisposedException in Background Task

**Error:**
```
System.ObjectDisposedException: Cannot access a disposed object.
Object name: 'IServiceProvider'.
```

**Why it happened:**
```
1. Request arrives → ASP.NET creates request scope
2. Webhook returns 200 OK immediately
3. ASP.NET disposes request scope (including ISender)
4. Background Task.Run() tries to use ISender
5. ObjectDisposedException thrown
```

**Initial (Broken) Code:**
```csharp
.MapPost("/transcribed", async (
    ISender sender,  // ← Scoped to request
    TranscriptionCallbackRequest request) =>
{
    _ = Task.Run(async () =>
    {
        await sender.Send(command);  // ← ERROR: sender is disposed
    });

    return TypedResults.Ok(...);  // ← Request scope disposed here
});
```

### ✅ Solution 3: Create New Scope in Background Task

```csharp
.MapPost("/transcribed", (
    IServiceProvider serviceProvider,  // ← Singleton, never disposed
    TranscriptionCallbackRequest request) =>
{
    _ = Task.Run(async () =>
    {
        // Create NEW scope that lives for duration of background task
        using var scope = serviceProvider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(command);  // ← Works: sender from new scope
    });

    return TypedResults.Ok(...);
});
```

**Why it works:**
- `IServiceProvider` is a singleton → Never disposed
- `CreateScope()` creates independent scope for background task
- New scope lives until `using` statement completes
- Services resolved from new scope are valid throughout background task

---

## Final Architecture

### 1. HttpClient Configuration (Manual)

**File:** [src/Infrastructure/DependencyInjection.cs](../src/Infrastructure/DependencyInjection.cs)

```csharp
services.AddScoped<IMcpService>(sp =>
{
    var httpClient = new HttpClient
    {
        BaseAddress = new Uri("http://localhost:5000/mcp/"),
        Timeout = Timeout.InfiniteTimeSpan
    };
    return new McpService(httpClient, logger);
});
```

### 2. Webhook Endpoint (Fire-and-Forget)

**File:** [src/WebApi/Endpoints/CallEndpoints.cs](../src/WebApi/Endpoints/CallEndpoints.cs)

```csharp
.MapPost("/transcribed", (
    TranscriptionCallbackRequest request,
    IServiceProvider serviceProvider) =>
{
    _ = Task.Run(async () =>
    {
        using var scope = serviceProvider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new HandleTranscriptionCommand(request));
    });

    return TypedResults.Ok(new {
        message = "Processing in background"
    });
});
```

### 3. Request Flow (Success Path)

```
1. Vonage → POST /api/calls/transcribed
   └─ Webhook received (time: 0ms)

2. WebApi → Return 200 OK immediately
   └─ Vonage receives response (time: 50ms)
   └─ No retry needed

3. Background Task.Run() starts
   ├─ Create new DI scope
   ├─ Resolve ISender from new scope
   └─ Send HandleTranscriptionCommand

4. HandleTranscriptionCommandHandler
   └─ Download transcript from Vonage

5. McpService.ProcessTranscriptWithMcpAsync()
   └─ POST to MCP Server (localhost:5000)
   └─ No timeout - can take as long as needed

6. MCP Server → Ollama AI
   └─ Process transcript (30-60 seconds)
   └─ Return summarized text

7. OneDriveService.UploadFileFromStreamAsync()
   └─ Save to OneDrive

8. Background task completes
   └─ Dispose scope
   └─ Log success/failure
```

---

## Testing the Solution

### Manual Test with curl

```bash
# Test MCP Server directly (measure actual processing time)
time curl -X POST http://localhost:5000/mcp/ \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc":"2.0",
    "id":1,
    "method":"tools/call",
    "params":{
      "name":"process_transcript",
      "arguments":{
        "transcript":"Bonjour, je voudrais commander une pizza"
      }
    }
  }'

# Typical response time: 30-60 seconds
```

### End-to-End Test

1. Make a test call via Vonage
2. Observe logs:
   ```
   info: Transcription webhook received
   info: Début du traitement MCP
   info: Envoi du transcript au serveur MCP
   [... 30-60 seconds later ...]
   info: Traitement MCP terminé avec succès
   info: File uploaded successfully to OneDrive
   ```

3. Verify:
   - ✅ Webhook received only once (no duplicates)
   - ✅ MCP processing completed
   - ✅ File uploaded to OneDrive
   - ✅ No timeout errors in logs

---

## Key Learnings

### 1. .NET Aspire's Automatic Behavior

**What we learned:**
- `AddServiceDefaults()` globally configures ALL HttpClients
- Cannot opt-out specific HttpClients from resilience policies
- Only way to bypass: Don't use `AddHttpClient()` at all

**Best Practice:**
```csharp
// For services with long-running operations, create HttpClient manually
services.AddScoped<IMyService>(sp => {
    var httpClient = new HttpClient { /* custom config */ };
    return new MyService(httpClient);
});
```

### 2. Webhook Timeout Patterns

**What we learned:**
- External webhooks (Vonage, Stripe, etc.) have strict timeouts (usually 10-30s)
- If you need > 10s processing, must use fire-and-forget pattern
- Always respond immediately, process in background

**Best Practice:**
```csharp
// Webhook pattern for long-running operations
.MapPost("/webhook", (Request request, IServiceProvider sp) => {
    _ = Task.Run(async () => {
        using var scope = sp.CreateScope();
        // Process in background
    });
    return TypedResults.Ok(); // Respond immediately
});
```

### 3. Dependency Injection Scope Management

**What we learned:**
- Request scope is disposed when endpoint returns
- Background tasks must create their own scope
- Always inject `IServiceProvider`, not scoped services

**Best Practice:**
```csharp
// ❌ DON'T: Inject scoped service for background task
.MapPost("/endpoint", (IScopedService service) => {
    _ = Task.Run(() => service.DoWork()); // ObjectDisposedException!
});

// ✅ DO: Inject IServiceProvider and create new scope
.MapPost("/endpoint", (IServiceProvider sp) => {
    _ = Task.Run(() => {
        using var scope = sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IScopedService>();
        service.DoWork();
    });
});
```

---

## Alternative Solutions Considered

### Option A: Message Queue (RabbitMQ, Azure Service Bus)

**Pros:**
- ✅ Durable - survives app restarts
- ✅ Retry logic built-in
- ✅ Horizontal scaling

**Cons:**
- ❌ Additional infrastructure
- ❌ Increased complexity
- ❌ Overkill for this use case

**Verdict:** Not needed for this application. Fire-and-forget is simpler and sufficient.

### Option B: BackgroundService with Channel

**Pros:**
- ✅ In-process queue
- ✅ More control over processing
- ✅ Can limit concurrency

**Cons:**
- ❌ Not durable (lost on restart)
- ❌ More code to maintain
- ❌ Complexity vs. Task.Run()

**Verdict:** Could be considered if we need concurrency control in the future.

### Option C: Keep Synchronous, Optimize Ollama

**Pros:**
- ✅ Simpler code
- ✅ Immediate feedback

**Cons:**
- ❌ Ollama cannot be made faster than 30s
- ❌ Would still timeout Vonage webhooks
- ❌ Not a viable solution

**Verdict:** Not feasible - AI operations are inherently slow.

---

## Production Considerations

### 1. Timeout Configuration

**Development:**
```csharp
Timeout = Timeout.InfiniteTimeSpan  // No timeout for debugging
```

**Production:**
```csharp
Timeout = TimeSpan.FromMinutes(5)  // Reasonable upper bound
```

**Rationale:**
- Prevent hung connections
- Allow operations to timeout eventually
- 5 minutes is generous for Ollama

### 2. Monitoring & Observability

**Add logging:**
```csharp
logger.LogInformation("MCP processing started for {RecordingId}", id);
var sw = Stopwatch.StartNew();
// ... processing ...
logger.LogInformation("MCP processing completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
```

**Add metrics:**
```csharp
var meter = new Meter("MyApp.MCP");
var processingDuration = meter.CreateHistogram<double>("mcp.processing.duration");

processingDuration.Record(sw.Elapsed.TotalSeconds);
```

### 3. Error Handling

**Current (fire-and-forget):**
```csharp
catch (Exception ex)
{
    logger.LogError(ex, "Background processing failed");
    // Error is logged but not propagated
}
```

**Production enhancement:**
```csharp
catch (Exception ex)
{
    logger.LogError(ex, "Background processing failed");

    // Optional: Save failed transcript to database for manual review
    await saveFailedTranscriptRepository.SaveAsync(transcript, ex.Message);

    // Optional: Send alert to monitoring system
    await alertService.NotifyAsync($"MCP processing failed: {ex.Message}");
}
```

### 4. Graceful Shutdown

**Current risk:**
- App shutdown → Background Task.Run() killed mid-processing

**Solution (optional):**
```csharp
public class BackgroundTaskTracker : IHostedService
{
    private readonly ConcurrentBag<Task> _runningTasks = new();

    public void Track(Task task) => _runningTasks.Add(task);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Wait for all tracked tasks to complete
        await Task.WhenAll(_runningTasks);
    }
}
```

---

## Troubleshooting Guide

### Issue: MCP Still Timing Out

**Symptom:**
```
Polly.Timeout.TimeoutRejectedException: The operation didn't complete...
```

**Check:**
1. ✅ HttpClient created manually (not via AddHttpClient)?
2. ✅ No `.AddStandardResilienceHandler()` call?
3. ✅ `Timeout = Timeout.InfiniteTimeSpan`?

**Debug:**
```csharp
// Add logging to verify configuration
logger.LogInformation("HttpClient Timeout: {Timeout}", httpClient.Timeout);
```

### Issue: Duplicate Webhook Deliveries

**Symptom:**
- Same recording processed multiple times
- Logs show same UUID twice

**Check:**
1. ✅ Webhook endpoint returns immediately (< 100ms)?
2. ✅ Processing moved to Task.Run()?
3. ✅ Returns 200 OK (not 204 NoContent)?

**Debug:**
```csharp
var sw = Stopwatch.StartNew();
// ... your code ...
logger.LogInformation("Webhook responded in {ElapsedMs}ms", sw.ElapsedMilliseconds);
// Should be < 100ms
```

### Issue: ObjectDisposedException

**Symptom:**
```
Cannot access a disposed object. Object name: 'IServiceProvider'
```

**Check:**
1. ✅ Injecting `IServiceProvider` (not `ISender`)?
2. ✅ Creating new scope with `CreateScope()`?
3. ✅ Using `using` statement for scope?

**Correct pattern:**
```csharp
_ = Task.Run(async () =>
{
    using var scope = serviceProvider.CreateScope();
    var sender = scope.ServiceProvider.GetRequiredService<ISender>();
    await sender.Send(command);
});
```

---

## Related Files

| File | Purpose |
|------|---------|
| [src/Infrastructure/DependencyInjection.cs](../src/Infrastructure/DependencyInjection.cs) | Manual HttpClient configuration |
| [src/WebApi/Endpoints/CallEndpoints.cs](../src/WebApi/Endpoints/CallEndpoints.cs) | Fire-and-forget webhook pattern |
| [src/Infrastructure/MCP/MCPService.cs](../src/Infrastructure/MCP/MCPService.cs) | MCP service implementation |
| [Vonage_MCP/McpServer/Program.cs](../../Vonage_MCP/McpServer/Program.cs) | MCP Server with Kestrel timeout config |

---

## References

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Polly Resilience Strategies](https://www.pollydocs.org/)
- [ASP.NET Core Dependency Injection Scopes](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection)
- [Vonage Webhook Best Practices](https://developer.vonage.com/en/getting-started/concepts/webhooks)

---

## Summary

**Problem:** Long-running AI operations (30-60s) timing out due to Aspire's automatic Polly injection and Vonage webhook timeouts.

**Solution:**
1. Manual HttpClient creation to bypass Aspire's Polly
2. Fire-and-forget pattern for webhook processing
3. New DI scope in background task

**Result:** ✅ No timeouts, no duplicates, no ObjectDisposedException

**Time invested:** ~4 hours of troubleshooting

**Lines of code changed:** ~50 lines

**Complexity reduction:** Removed ~200 lines of unused Polly configuration attempts
