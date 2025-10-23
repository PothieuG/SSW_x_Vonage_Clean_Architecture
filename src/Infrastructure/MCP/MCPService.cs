// ============================================================================
// MCP CLIENT SERVICE
// ============================================================================
//
// Ce service est le CLIENT MCP qui communique avec le serveur MCP via HTTP.
//
// ARCHITECTURE:
//
//   McpService (ce fichier)
//          │
//          │ HTTP POST http://localhost:5000/mcp/
//          │ Content-Type: application/json
//          │ Body: { "jsonrpc": "2.0", "method": "tools/call", "params": {...} }
//          ↓
//   MCP Server (Vonage_MCP/McpServer/Program.cs)
//          │
//          │ Exécute l'outil "process_transcript"
//          │ - Détection langue (Ollama)
//          │ - Traduction si nécessaire (Ollama)
//          │ - Résumé professionnel (Ollama)
//          ↓
//   Réponse: { "jsonrpc": "2.0", "result": { "content": [{ "text": "..." }] } }
//
// PROTOCOLE JSON-RPC 2.0:
// Le serveur MCP utilise le protocole JSON-RPC 2.0 (standard pour RPC en JSON).
// Voir: https://www.jsonrpc.org/specification
//
// TIMEOUT HANDLING:
// Ce service utilise un HttpClient créé MANUELLEMENT (pas via AddHttpClient)
// pour bypasser les timeouts Polly de .NET Aspire (voir DependencyInjection.cs).
// Timeout configuré: Infinite (car Ollama prend 30-60s pour traiter)
//
// RÉFÉRENCE COMPLÈTE:
// - Architecture MCP: docs/MCP_ARCHITECTURE.md
// - Résolution timeouts: docs/MCP_TIMEOUT_RESOLUTION.md
//
// ============================================================================

using System.Text.Json;
using System.Net.Http.Json;
using ErrorOr;
using Microsoft.Extensions.Logging;
using SSW_x_Vonage_Clean_Architecture.Application.Common.Interfaces;

namespace SSW_x_Vonage_Clean_Architecture.Infrastructure.MCP;

public class McpService : IMcpService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<McpService> _logger;

    public McpService(HttpClient httpClient, ILogger<McpService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // ========================================================================
    // MÉTHODE PRINCIPALE: ProcessTranscriptWithMcpAsync
    // ========================================================================
    // Envoie un transcript au serveur MCP pour traitement AI complet.
    //
    // WORKFLOW:
    // 1. Construction requête JSON-RPC 2.0 avec l'outil "process_transcript"
    // 2. POST HTTP vers http://localhost:5000/mcp/
    // 3. Le serveur MCP exécute (30-60s avec Ollama):
    //    - Détection de langue
    //    - Traduction en français (si nécessaire)
    //    - Résumé professionnel (toujours)
    // 4. Parse de la réponse JSON-RPC et extraction du texte
    //
    // GESTION D'ERREURS:
    // En cas d'erreur (serveur MCP down, Ollama down, timeout, etc.),
    // retourne ErrorOr<string> avec l'erreur. L'appelant (Handler) peut
    // décider de sauvegarder le transcript original sans traitement AI.
    //
    // TIMEOUT:
    // HttpClient configuré avec Timeout.InfiniteTimeSpan (voir DependencyInjection)
    // pour permettre à Ollama de prendre 30-60s sans timeout.
    // ========================================================================
    public async Task<ErrorOr<string>> ProcessTranscriptWithMcpAsync(string transcript, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Début du traitement MCP du transcript ({Length} caractères)", transcript.Length);

            // ================================================================
            // ÉTAPE 1: Construction de la requête JSON-RPC 2.0
            // ================================================================
            // Format JSON-RPC 2.0:
            // {
            //   "jsonrpc": "2.0",              // Version du protocole (obligatoire)
            //   "id": 1,                       // ID de la requête (pour matcher réponse)
            //   "method": "tools/call",        // Méthode MCP à appeler
            //   "params": {                    // Paramètres de la méthode
            //     "name": "process_transcript", // Nom de l'outil à exécuter
            //     "arguments": {               // Arguments de l'outil
            //       "transcript": "..."        // Le transcript à traiter
            //     }
            //   }
            // }
            //
            // L'outil "process_transcript" côté serveur fait:
            // 1. Détection de langue (via Ollama)
            // 2. Traduction en français si nécessaire (via Ollama)
            // 3. Résumé professionnel à la 3ème personne (via Ollama)
            // ================================================================
            var request = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/call",
                @params = new
                {
                    name = "process_transcript",
                    arguments = new
                    {
                        transcript
                    }
                }
            };

            // ================================================================
            // ÉTAPE 2: Envoi de la requête HTTP POST au serveur MCP
            // ================================================================
            // POST http://localhost:5000/mcp/
            // Content-Type: application/json
            // Body: <requête JSON-RPC ci-dessus>
            //
            // DURÉE ATTENDUE: 30-60 secondes (3 appels Ollama × 10-20s chacun)
            // HttpClient configuré avec Timeout.InfiniteTimeSpan pour éviter timeout
            // ================================================================
            _logger.LogInformation("Envoi du transcript au serveur MCP pour traitement complet");
            var response = await _httpClient.PostAsJsonAsync("", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            // ================================================================
            // ÉTAPE 3: Parse de la réponse JSON-RPC
            // ================================================================
            // Format de réponse MCP (succès):
            // {
            //   "jsonrpc": "2.0",
            //   "id": 1,
            //   "result": {
            //     "content": [
            //       {
            //         "type": "text",
            //         "text": "Une personne a appelé pour..."  ← Le résumé AI
            //       }
            //     ]
            //   }
            // }
            //
            // Format de réponse JSON-RPC (erreur):
            // {
            //   "jsonrpc": "2.0",
            //   "id": 1,
            //   "error": {
            //     "code": -32603,
            //     "message": "Tool execution failed: ..."
            //   }
            // }
            // ================================================================
            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

            // Navigation dans la structure MCP pour extraire le texte
            // jsonResponse["result"]["content"][0]["text"]
            var content = jsonResponse.GetProperty("result").GetProperty("content");
            if (content.ValueKind == JsonValueKind.Array && content.GetArrayLength() > 0)
            {
                var firstContent = content[0];
                var result = firstContent.GetProperty("text").GetString() ?? transcript;

                _logger.LogInformation("Traitement MCP terminé avec succès. Résultat: {Length} caractères", result.Length);
                return result;
            }

            // Cas rare: réponse vide (ne devrait jamais arriver)
            _logger.LogWarning("MCP a retourné une réponse vide, utilisation du transcript original");
            return transcript;
        }
        catch (Exception ex)
        {
            // ================================================================
            // GESTION D'ERREURS
            // ================================================================
            // Erreurs possibles:
            // - HttpRequestException: Serveur MCP inaccessible (port 5000 fermé)
            // - TaskCanceledException: CancellationToken annulé
            // - JsonException: Réponse JSON invalide
            // - KeyNotFoundException: Structure JSON-RPC incorrecte
            //
            // En cas d'erreur, on retourne ErrorOr avec l'erreur.
            // L'appelant (HandleTranscriptionCommandHandler) peut décider
            // de sauvegarder uniquement le transcript original sans traitement AI.
            // ================================================================
            _logger.LogError(ex, "Erreur lors du traitement MCP du transcript");
            return Error.Failure("Mcp.ProcessingError", ex.Message);
        }
    }
}
