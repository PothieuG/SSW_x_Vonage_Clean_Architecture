using System.Text.Json;
using System.Net.Http.Json;
using ErrorOr;
using Microsoft.Extensions.Logging;
using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;
using OllamaSharp;

namespace SSW_x_Vonage_Clean_Architecture.Infrastructure.MCP;

public class McpService : IMcpService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaApiClient _ollama;
    private readonly ILogger<McpService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public McpService(HttpClient httpClient, ILogger<McpService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _ollama = new OllamaApiClient(
            uriString: "http://localhost:11434",
            defaultModel: "llama3.2:3b")
        {
            // Configure timeout to 60 seconds for AI generation
            SelectedModel = "llama3.2:3b"
        };
    }

    public async Task<ErrorOr<string>> ProcessTranscriptWithMcpAsync(string transcript, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Début du traitement MCP du transcript ({Length} caractères)", transcript.Length);

            // 1. Lister les outils disponibles du serveur MCP
            var tools = await GetAvailableToolsAsync(cancellationToken);
            if (tools.IsError)
                return tools.Errors;

            // 2. Demander à l'IA locale de choisir la stratégie de traitement
            var actionPlan = await CreateActionPlanAsync(transcript, tools.Value, cancellationToken);
            if (actionPlan.IsError)
                return actionPlan.Errors;

            // 3. Exécuter le plan d'action
            var result = await ExecuteActionPlanAsync(actionPlan.Value, cancellationToken);
            
            _logger.LogInformation("Traitement MCP terminé avec succès");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du traitement MCP du transcript");
            return Error.Failure("Mcp.ProcessingError", ex.Message);
        }
    }

    private async Task<ErrorOr<JsonElement>> GetAvailableToolsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools.list"
            };

            var response = await _httpClient.PostAsJsonAsync("", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            return jsonResponse.GetProperty("result").GetProperty("tools");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des outils MCP");
            return Error.Failure("Mcp.ToolsListError", ex.Message);
        }
    }

    private async Task<ErrorOr<ActionPlan>> CreateActionPlanAsync(string transcript, JsonElement tools, CancellationToken cancellationToken)
    {
        var serializedTools = JsonSerializer.Serialize(tools, JsonOptions);
        var prompt = string.Join(Environment.NewLine, new[]
        {
            "Tu dois analyser ce transcript et décider du meilleur traitement à appliquer.",
            "",
            "OUTILS DISPONIBLES :",
            serializedTools,
            "",
            "TRANSCRIPT À TRAITER :",
            $"\"{transcript}\"",
            "",
            "CONSIGNES :",
            "1. Si le transcript n'est pas en français, utilise D'ABORD \"translate_text\" pour le traduire en français",
            "2. Ensuite, utilise \"summarize_text\" pour créer un résumé concis",
            "3. Si le transcript est court (< 200 caractères), tu peux sauter l'étape de résumé",
            "4. Pour l'analyse de sentiment, utilise \"analyze_sentiment\" seulement si c'est pertinent",
            "",
            "Réponds UNIQUEMENT avec le JSON suivant :",
            "",
            "{",
            "    \"steps\": [",
            "        {",
            "            \"tool\": \"nom_de_l_outil\",",
            "            \"arguments\": {",
            "                \"param1\": \"valeur1\",",
            "                \"param2\": \"valeur2\"",
            "            },",
            "            \"reason\": \"Explication du choix\"",
            "        }",
            "    ]",
            "}",
        });

        try
        {
            _logger.LogInformation("Generating action plan with Ollama (transcript length: {Length})", transcript.Length);

            // Use a timeout source separate from the cancellation token
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // Collect the streaming response
            var responseBuilder = new System.Text.StringBuilder();
            var startTime = DateTime.UtcNow;

            await foreach (var chunk in _ollama.GenerateAsync(prompt, cancellationToken: linkedCts.Token))
            {
                if (chunk?.Response != null)
                {
                    responseBuilder.Append(chunk.Response);
                }
            }

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Ollama generation completed in {Duration}ms", duration.TotalMilliseconds);

            var planText = responseBuilder.ToString().Trim();

            if (string.IsNullOrWhiteSpace(planText))
            {
                _logger.LogWarning("Ollama returned empty response");
                return Error.Failure("Mcp.EmptyResponse", "Ollama returned an empty response");
            }

            _logger.LogDebug("Raw Ollama response: {Response}", planText);

            // Nettoyer la réponse (parfois Ollama ajoute du texte autour du JSON)
            var jsonStart = planText.IndexOf('{', StringComparison.Ordinal);
            var jsonEnd = planText.LastIndexOf('}') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                planText = planText[jsonStart..jsonEnd];
            }
            else
            {
                _logger.LogWarning("Could not find valid JSON in Ollama response");
                return Error.Validation("Mcp.InvalidJsonFormat", "Could not extract JSON from Ollama response");
            }

            _logger.LogDebug("Extracted JSON: {Json}", planText);

            var plan = JsonSerializer.Deserialize<ActionPlan>(planText);
            if (plan?.Steps == null || plan.Steps.Count == 0)
            {
                _logger.LogWarning("Action plan is empty or invalid");
                return Error.Validation("Mcp.InvalidPlan", "Le plan d'action généré est invalide");
            }

            _logger.LogInformation("Plan d'action généré: {StepsCount} étapes", plan.Steps.Count);
            return plan;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Action plan generation was cancelled or timed out");
            return Error.Failure("Mcp.Timeout", "AI generation timed out after 30 seconds");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création du plan d'action");
            return Error.Failure("Mcp.PlanningError", ex.Message);
        }
    }

    private async Task<string> ExecuteActionPlanAsync(ActionPlan plan, CancellationToken cancellationToken)
    {
        string currentInput = ""; // Sera mis à jour à chaque étape

        foreach (var step in plan.Steps)
        {
            _logger.LogInformation("Exécution de l'étape: {Tool} - {Reason}", step.Tool, step.Reason);

            var request = new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().GetHashCode(),
                method = "tools.call",
                @params = new
                {
                    name = step.Tool,
                    arguments = step.Arguments
                }
            };

            var response = await _httpClient.PostAsJsonAsync("", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            currentInput = jsonResponse.GetProperty("result").GetProperty("output").GetString() ?? "";

            _logger.LogInformation("Résultat de l'étape {Tool}: {ResultLength} caractères", step.Tool, currentInput.Length);
        }

        return currentInput;
    }
}

// Classes de support
public class ActionPlan
{
    public List<ProcessingStep> Steps { get; set; } = new();
}

public class ProcessingStep
{
    public string Tool { get; set; } = string.Empty;
    public Dictionary<string, object> Arguments { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
}