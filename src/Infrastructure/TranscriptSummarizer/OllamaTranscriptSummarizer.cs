using System.Net.Http.Json;
using ErrorOr;
using Microsoft.Extensions.Logging;
using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;

namespace SSW_x_Vonage_Clean_Architecture.Infrastructure.Services;

public class OllamaTranscriptSummarizer : ITranscriptSummarizer
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaTranscriptSummarizer> _logger;
    private const string OllamaBaseUrl = "http://localhost:11434";
    private const string ModelName = "llama3.2:3b"; // Meilleur modèle

    public OllamaTranscriptSummarizer(HttpClient httpClient, ILogger<OllamaTranscriptSummarizer> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        _httpClient.BaseAddress = new Uri(OllamaBaseUrl);
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Augmenté à 5 minutes
    }

    public async Task<ErrorOr<string>> SummarizeAsync(string transcript, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Début du résumé du transcript avec Ollama. Longueur: {Length} caractères", transcript.Length);

            // Tronquer le transcript si trop long
            var truncatedTranscript = transcript.Length > 2000 
                ? transcript.Substring(0, 2000) + "... [texte tronqué]" 
                : transcript;

            var prompt = CreateSummaryPrompt(truncatedTranscript);
            
            var request = new OllamaGenerateRequest
            {
                Model = ModelName,
                Prompt = prompt,
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = 0.1,
                    TopP = 0.7,
                    NumPredict = 150, // Réduit pour plus de rapidité
                    Seed = 42
                }
            };

            // Utiliser un timeout séparé pour Ollama
            using var ollamaCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, ollamaCts.Token);

            var response = await _httpClient.PostAsJsonAsync(
                "/api/generate", 
                request, 
                linkedCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(linkedCts.Token);
                _logger.LogError("Erreur Ollama: {StatusCode} - {Error}", response.StatusCode, errorContent);
                
                // Vérifier si Ollama est démarré
                if (await IsOllamaRunningAsync(linkedCts.Token))
                {
                    return Error.Failure("Ollama.SummaryFailed", $"Erreur HTTP: {response.StatusCode}");
                }
                else
                {
                    return Error.Failure("Ollama.NotRunning", "Service Ollama non démarré");
                }
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(
                cancellationToken: linkedCts.Token);

            if (string.IsNullOrWhiteSpace(result?.Response))
            {
                _logger.LogWarning("Réponse vide reçue d'Ollama");
                return Error.Validation("Ollama.EmptyResponse", "Aucun résumé généré");
            }

            var summary = CleanSummary(result.Response);
            _logger.LogInformation("Résumé généré avec succès. Longueur: {Length} caractères", summary.Length);
            
            return summary;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout lors du résumé avec Ollama");
            return Error.Failure("Ollama.Timeout", "Timeout lors de la génération du résumé");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erreur de connexion à Ollama");
            return Error.Failure("Ollama.ConnectionFailed", "Impossible de se connecter au service Ollama");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Résumé annulé par l'utilisateur");
            return Error.Failure("Ollama.OperationCancelled", "Résumé annulé");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur inattendue lors du résumé");
            return Error.Failure("Ollama.UnexpectedError", $"Erreur lors du résumé: {ex.Message}");
        }
    }

    private async Task<bool> IsOllamaRunningAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string CreateSummaryPrompt(string transcript)
    {
        // Prompt plus simple et direct
        return @$"<|system|>
Tu résumes des conversations téléphoniques en français de manière concise (2-3 phrases).
Sois clair et capture les points principaux.
</s>
<|user|>
Résume cette conversation :

{transcript}
</s>
<|assistant|>
";
    }

    private static string CleanSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return "Résumé non disponible";

        return summary
            .Replace("<|assistant|>", "")
            .Replace("<|system|>", "")
            .Replace("<|user|>", "")
            .Replace("</s>", "")
            .Trim()
            .Trim('"', '\'', '`');
    }

    // Classes pour la sérialisation JSON
    private class OllamaGenerateRequest
    {
        public string Model { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public bool Stream { get; set; }
        public OllamaOptions? Options { get; set; }
    }

    private class OllamaOptions
    {
        public double Temperature { get; set; }
        public double TopP { get; set; }
        public int NumPredict { get; set; }
        public int Seed { get; set; }
    }

    private class OllamaGenerateResponse
    {
        public string Model { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public bool Done { get; set; }
    }
}