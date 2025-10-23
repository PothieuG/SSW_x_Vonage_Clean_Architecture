# MCP Client-Server Architecture - Documentation Complète

## Table des Matières

1. [Vue d'Ensemble](#vue-densemble)
2. [Architecture Globale](#architecture-globale)
3. [Protocole JSON-RPC 2.0](#protocole-json-rpc-20)
4. [Le Serveur MCP](#le-serveur-mcp)
5. [Le Client MCP](#le-client-mcp)
6. [Flux de Communication](#flux-de-communication)
7. [Intégration dans Clean Architecture](#intégration-dans-clean-architecture)
8. [Diagrammes de Séquence](#diagrammes-de-séquence)
9. [Configuration et Démarrage](#configuration-et-démarrage)
10. [Outils MCP Disponibles](#outils-mcp-disponibles)
11. [Traitement AI avec Ollama](#traitement-ai-avec-ollama)

---

## Vue d'Ensemble

### Qu'est-ce que MCP (Model Context Protocol) ?

**MCP** est un protocole basé sur **JSON-RPC 2.0** qui permet à des applications d'interagir avec des serveurs qui exposent des "outils" (tools). Dans ce projet, MCP est utilisé pour :

1. **Exécuter des traitements AI** (traduction, résumé, analyse de sentiment) via Ollama
2. **Découpler le traitement AI** de l'application principale
3. **Permettre la réutilisation** du serveur MCP par plusieurs clients (Claude Desktop, applications C#, etc.)

### Architecture en un Coup d'Œil

```
┌─────────────────────────────────────────────────────────────────────┐
│                      APPLICATION PRINCIPALE                         │
│                  (SSW_x_Vonage_Clean_Architecture)                  │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  WebApi (Webhook Vonage)                                            │
│      ↓                                                              │
│  HandleTranscriptionCommandHandler                                  │
│      ↓                                                              │
│  McpService (CLIENT MCP)                                            │
│      │                                                              │
│      │ HTTP POST: JSON-RPC 2.0                                      │
│      │ http://localhost:5000/mcp/                                   │
│      │ { "method": "tools/call", "params": { ... } }                │
│      ↓                                                              │
└─────────────────────────────────────────────────────────────────────┘
         │
         │ HTTP (JSON-RPC 2.0)
         ↓
┌─────────────────────────────────────────────────────────────────────┐
│                       SERVEUR MCP                                   │
│                  (Vonage_MCP/McpServer)                             │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ASP.NET Core Minimal API (http://localhost:5000)                   │
│      ↓                                                              │
│  JSON-RPC Handler                                                   │
│      ↓                                                              │
│  Tool Executor                                                      │
│      - process_transcript (détection + traduction + résumé)         │
│      - summarize_text                                               │
│      - translate_text                                               │
│      - analyze_sentiment                                            │
│      ↓                                                              │
│  OllamaSharp Client                                                 │
│      │                                                              │
│      │ HTTP POST                                                    │
│      │ http://localhost:11434/api/generate                          │
│      ↓                                                              │
└─────────────────────────────────────────────────────────────────────┘
         │
         ↓
┌─────────────────────────────────────────────────────────────────────┐
│                      OLLAMA (Modèle AI)                             │
│                    http://localhost:11434                           │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Modèle: gemma3:4b                                                  │
│  - Génération de texte                                              │
│  - Traduction naturelle                                             │
│  - Résumé intelligent                                               │
│  - Analyse de sentiment                                             │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Architecture Globale

### Composants Principaux

| Composant | Emplacement | Rôle | Port |
|-----------|-------------|------|------|
| **Application Principale** | `SSW_x_Vonage_Clean_Architecture/` | API Vonage, webhooks, orchestration | 7255 (HTTPS) |
| **Client MCP** | `src/Infrastructure/MCP/McpService.cs` | Envoie des requêtes JSON-RPC au serveur MCP | - |
| **Serveur MCP** | `Vonage_MCP/McpServer/Program.cs` | Expose des outils AI via JSON-RPC 2.0 | 5000 (HTTP) |
| **Ollama** | Service externe | Exécute les modèles AI (gemma3:4b) | 11434 (HTTP) |

### Pourquoi Cette Architecture ?

#### ✅ Avantages

1. **Découplage** : Le serveur MCP peut être utilisé par plusieurs clients (Claude Desktop, C#, Python, etc.)
2. **Réutilisabilité** : Les outils AI sont centralisés et réutilisables
3. **Scalabilité** : Le serveur MCP peut être déployé séparément et scalé indépendamment
4. **Maintenance** : Modifications des prompts AI sans toucher à l'application principale
5. **Test** : Possibilité de tester les outils MCP indépendamment

#### 🔄 Alternatives Considérées

| Alternative | Pourquoi Rejetée |
|-------------|------------------|
| **Appeler Ollama directement depuis l'application** | Couplage fort, difficile de partager les prompts avec Claude Desktop |
| **Bibliothèque partagée** | Ne permet pas l'utilisation par Claude Desktop (qui nécessite stdio/HTTP) |
| **Message Queue (RabbitMQ, etc.)** | Trop complexe pour ce cas d'usage synchrone |

---

## Protocole JSON-RPC 2.0

### Qu'est-ce que JSON-RPC 2.0 ?

**JSON-RPC 2.0** est un protocole léger de Remote Procedure Call (RPC) encodé en JSON. Il définit :

- **Requêtes** : Appels de méthodes avec paramètres
- **Réponses** : Résultats ou erreurs
- **Notifications** : Requêtes sans réponse attendue (non utilisé ici)

### Format des Messages

#### Requête JSON-RPC

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "process_transcript",
    "arguments": {
      "transcript": "Hello, this is a test message..."
    }
  }
}
```

**Champs** :
- `jsonrpc` : **Toujours "2.0"** (version du protocole)
- `id` : Identifiant de la requête (pour matcher la réponse)
- `method` : Nom de la méthode à appeler
- `params` : Paramètres de la méthode (objet ou tableau)

#### Réponse JSON-RPC (Succès)

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "Une personne a appelé pour tester le système de transcription."
      }
    ]
  }
}
```

**Champs** :
- `jsonrpc` : "2.0"
- `id` : Même ID que la requête
- `result` : Résultat de l'appel (structure MCP avec tableau `content`)

#### Réponse JSON-RPC (Erreur)

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32603,
    "message": "Tool execution failed: Ollama is not responding"
  }
}
```

**Codes d'erreur standard JSON-RPC** :
- `-32700` : Parse error (JSON invalide)
- `-32600` : Invalid request
- `-32601` : Method not found
- `-32602` : Invalid params
- `-32603` : Internal error

### Méthodes MCP Supportées

| Méthode | Description | Paramètres |
|---------|-------------|------------|
| `initialize` | Initialise la connexion MCP | Aucun |
| `tools/list` | Liste tous les outils disponibles | Aucun |
| `tools/call` | Exécute un outil spécifique | `name`, `arguments` |

---

## Le Serveur MCP

**Emplacement** : `C:\DataGillesPothieu\ProjectsGitHub\Vonage_MCP\McpServer\Program.cs`

### Architecture du Serveur

```
┌──────────────────────────────────────────────────────────────┐
│                      Program.cs (Entry Point)                │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌────────────────┐         ┌──────────────────┐            │
│  │  STDIO Mode    │         │   HTTP Mode      │            │
│  │  (Claude)      │         │   (C# Client)    │            │
│  └────────┬───────┘         └────────┬─────────┘            │
│           │                          │                      │
│           ├──────────────────────────┤                      │
│                      │                                      │
│         ┌────────────▼────────────┐                         │
│         │  JSON-RPC Handler       │                         │
│         │  - initialize           │                         │
│         │  - tools/list           │                         │
│         │  - tools/call           │                         │
│         └────────────┬────────────┘                         │
│                      │                                      │
│         ┌────────────▼────────────┐                         │
│         │  Tool Executor          │                         │
│         │  - process_transcript   │                         │
│         │  - summarize_text       │                         │
│         │  - translate_text       │                         │
│         │  - analyze_sentiment    │                         │
│         └────────────┬────────────┘                         │
│                      │                                      │
│         ┌────────────▼────────────┐                         │
│         │  OllamaSharp Client     │                         │
│         │  http://localhost:11434 │                         │
│         └─────────────────────────┘                         │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

### Dual Mode : STDIO vs HTTP

Le serveur MCP supporte **deux modes de transport** pour le protocole JSON-RPC :

#### 1. STDIO Mode (pour Claude Desktop)

**Activation** : `dotnet run --stdio`

**Fonctionnement** :
- Lit les requêtes JSON-RPC depuis **stdin** (entrée standard)
- Écrit les réponses JSON-RPC sur **stdout** (sortie standard)
- Redirige les logs vers **stderr** (pour ne pas polluer stdout)

**Utilisation** :
```json
// Dans claude_desktop_config.json
{
  "mcpServers": {
    "ollama-text-processor": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/McpServer", "--stdio"]
    }
  }
}
```

**Code Source** (lignes 25-71 de Program.cs) :

```csharp
async Task RunStdioModeAsync(OllamaApiClient ollamaClient)
{
    // CRITIQUE : Rediriger Console.WriteLine vers stderr pour garder stdout propre
    // stdout est réservé UNIQUEMENT pour les réponses JSON-RPC
    var stdoutOriginal = Console.Out;
    Console.SetOut(Console.Error);

    Console.WriteLine("[MCP Server] Running in stdio mode");

    while (true)
    {
        // Lire depuis stdin
        using var stdin = new StreamReader(Console.OpenStandardInput());
        var line = await stdin.ReadLineAsync();

        if (string.IsNullOrEmpty(line)) break;

        try
        {
            var request = JsonNode.Parse(line)!;
            var method = request["method"]?.ToString() ?? "";
            var id = request["id"]?.GetValue<int>() ?? 0;

            JsonObject response = method switch
            {
                "initialize" => CreateInitializeResponse(id),
                "tools/list" => CreateToolsListResponse(id),
                "tools/call" => await HandleToolCallStdioAsync(request, id, ollamaClient),
                _ => CreateErrorResponse(id, -32601, $"Method not found: {method}")
            };

            // Écrire la réponse JSON-RPC sur stdout (l'original)
            await stdoutOriginal.WriteLineAsync(response.ToJsonString());
            await stdoutOriginal.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MCP stdio] Error: {ex.Message}");
            var errorResponse = CreateErrorResponse(0, -32603, ex.Message);
            await stdoutOriginal.WriteLineAsync(errorResponse.ToJsonString());
        }
    }
}
```

#### 2. HTTP Mode (pour C# Client)

**Activation** : `dotnet run` (par défaut, sans `--stdio`)

**Fonctionnement** :
- Démarre un serveur HTTP ASP.NET Core Minimal API sur `http://localhost:5000`
- Expose un endpoint unique **POST /mcp** qui accepte JSON-RPC 2.0
- Les logs vont sur la console standard (stdout)

**Configuration Kestrel** :
```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
});
```

**Endpoint HTTP** (lignes 92-122 de Program.cs) :

```csharp
app.MapPost("/mcp", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync();

    Console.WriteLine($"[MCP HTTP] Received request: {body.Substring(0, Math.Min(200, body.Length))}...");

    try
    {
        var jsonRequest = JsonNode.Parse(body)!;
        var method = jsonRequest["method"]?.ToString() ?? "";
        var id = jsonRequest["id"]?.GetValue<int>() ?? 0;

        JsonObject response = method switch
        {
            "initialize" => CreateInitializeResponse(id),
            "tools/list" => CreateToolsListResponse(id),
            "tools/call" => await HandleToolCallAsync(jsonRequest, id, ollamaClient),
            _ => CreateErrorResponse(id, -32601, $"Method not found: {method}")
        };

        Console.WriteLine($"[MCP HTTP] Sending response for method: {method}");

        return Results.Json(response.AsObject());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[MCP HTTP] Error: {ex.Message}");
        return Results.Json(CreateErrorResponse(0, -32603, ex.Message).AsObject());
    }
});
```

### Handlers JSON-RPC

#### 1. `initialize` - Initialisation

**Requête** :
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize"
}
```

**Réponse** :
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "protocolVersion": "2024-11-05",
    "serverInfo": {
      "name": "ollama-text-processor",
      "version": "1.0.0"
    },
    "capabilities": {
      "tools": {}
    }
  }
}
```

**Code Source** (lignes 131-151) :
```csharp
JsonObject CreateInitializeResponse(int id)
{
    return new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id,
        ["result"] = new JsonObject
        {
            ["protocolVersion"] = "2024-11-05",
            ["serverInfo"] = new JsonObject
            {
                ["name"] = "ollama-text-processor",
                ["version"] = "1.0.0"
            },
            ["capabilities"] = new JsonObject
            {
                ["tools"] = new JsonObject()
            }
        }
    };
}
```

#### 2. `tools/list` - Liste des Outils

**Requête** :
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/list"
}
```

**Réponse** :
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "tools": [
      {
        "name": "process_transcript",
        "description": "Détecte automatiquement la langue d'un transcript, le traduit en français si nécessaire, puis le résume de manière professionnelle en français à la troisième personne.",
        "inputSchema": {
          "type": "object",
          "properties": {
            "transcript": {
              "type": "string",
              "description": "Le texte transcrit à traiter"
            }
          },
          "required": ["transcript"]
        }
      },
      {
        "name": "summarize_text",
        "description": "Résume un texte de manière intelligente en français...",
        "inputSchema": { ... }
      },
      {
        "name": "translate_text",
        "description": "Traduit un texte vers une langue cible...",
        "inputSchema": { ... }
      },
      {
        "name": "analyze_sentiment",
        "description": "Analyse le sentiment d'un texte...",
        "inputSchema": { ... }
      }
    ]
  }
}
```

**Code Source** (lignes 153-244) : Voir section [Outils MCP Disponibles](#outils-mcp-disponibles)

#### 3. `tools/call` - Exécution d'Outil

**Requête** :
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "process_transcript",
    "arguments": {
      "transcript": "Hello, this is a test message. I want to order 100 baguettes."
    }
  }
}
```

**Réponse** :
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "Une personne a appelé pour commander 100 baguettes."
      }
    ]
  }
}
```

**Code Source** (lignes 281-314) :
```csharp
async Task<JsonObject> HandleToolCallAsync(JsonNode request, int id, OllamaApiClient ollamaClient)
{
    try
    {
        var toolName = request["params"]?["name"]?.ToString() ?? "";
        var arguments = request["params"]?["arguments"];

        Console.WriteLine($"[MCP HTTP] Executing tool: {toolName}");

        // Délégation à ExecuteToolAsync (logique métier partagée)
        string resultText = await ExecuteToolAsync(toolName, arguments, ollamaClient);

        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = new JsonObject
            {
                ["content"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = resultText
                    }
                }
            }
        };
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[MCP HTTP] Tool call error: {ex.Message}");
        return CreateErrorResponse(id, -32603, $"Tool execution failed: {ex.Message}");
    }
}
```

---

## Le Client MCP

**Emplacement** : `src/Infrastructure/MCP/McpService.cs`

### Responsabilités

Le `McpService` est le **client MCP** qui :

1. **Envoie des requêtes JSON-RPC** au serveur MCP via HTTP
2. **Parse les réponses JSON-RPC** et extrait le contenu
3. **Gère les erreurs** (timeout, erreurs serveur, etc.)
4. **Abstrait le protocole MCP** pour l'Application Layer

### Interface

**Emplacement** : `src/Application/Common/Interfaces/IMcpService.cs`

```csharp
public interface IMcpService
{
    Task<ErrorOr<string>> ProcessTranscriptWithMcpAsync(
        string transcript,
        CancellationToken cancellationToken = default);
}
```

**Pourquoi une interface ?**
- ✅ **Dependency Inversion Principle** : L'Application Layer ne dépend pas de l'Infrastructure
- ✅ **Testabilité** : Possibilité de mocker le service MCP dans les tests
- ✅ **Flexibilité** : Possibilité de changer l'implémentation (ex: appeler un MCP distant via gRPC)

### Implémentation

**Code Source Complet** (`McpService.cs`) :

```csharp
public class McpService : IMcpService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<McpService> _logger;

    public McpService(HttpClient httpClient, ILogger<McpService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ErrorOr<string>> ProcessTranscriptWithMcpAsync(
        string transcript,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Début du traitement MCP du transcript ({Length} caractères)",
                transcript.Length);

            // Construction de la requête JSON-RPC 2.0
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

            _logger.LogInformation("Envoi du transcript au serveur MCP pour traitement complet");

            // POST HTTP vers http://localhost:5000/mcp/
            var response = await _httpClient.PostAsJsonAsync("", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Parse de la réponse JSON-RPC
            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);

            // Extraction du contenu depuis la structure MCP
            // { "result": { "content": [{ "type": "text", "text": "..." }] } }
            var content = jsonResponse.GetProperty("result").GetProperty("content");

            if (content.ValueKind == JsonValueKind.Array && content.GetArrayLength() > 0)
            {
                var firstContent = content[0];
                var result = firstContent.GetProperty("text").GetString() ?? transcript;

                _logger.LogInformation(
                    "Traitement MCP terminé avec succès. Résultat: {Length} caractères",
                    result.Length);

                return result;
            }

            _logger.LogWarning("MCP a retourné une réponse vide, utilisation du transcript original");
            return transcript;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du traitement MCP du transcript");
            return Error.Failure("Mcp.ProcessingError", ex.Message);
        }
    }
}
```

### Configuration HttpClient

**Emplacement** : `src/Infrastructure/DependencyInjection.cs` (lignes 110-125)

```csharp
services.AddScoped<IMcpService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<McpService>>();

    // Création manuelle de HttpClient pour bypasser Polly d'Aspire
    var httpClient = new HttpClient
    {
        BaseAddress = new Uri("http://localhost:5000/mcp/"),
        // Timeout infini pour les opérations AI longues (30-60s)
        Timeout = Timeout.InfiniteTimeSpan
    };

    return new McpService(httpClient, logger);
});
```

**Pourquoi création manuelle ?**

Voir [docs/MCP_TIMEOUT_RESOLUTION.md](MCP_TIMEOUT_RESOLUTION.md) pour les détails, mais en résumé :

- ❌ **Problème** : .NET Aspire injecte automatiquement Polly dans tous les HttpClients créés via `AddHttpClient()`
- ❌ Polly ajoute des timeouts (10s, 30s) qui ne peuvent pas être complètement désactivés
- ✅ **Solution** : Créer HttpClient manuellement pour bypasser Polly
- ✅ Timeout infini pour permettre à Ollama de prendre le temps nécessaire (30-60s)

---

## Flux de Communication

### Scénario Complet : Vonage → MCP → Ollama

#### Étape 1 : Réception du Webhook Vonage

**Fichier** : `src/WebApi/Endpoints/CallEndpoints.cs` (lignes 71-111)

```
1. Vonage envoie webhook POST /api/calls/transcribed
   - URL de transcription
   - UUID de l'enregistrement

2. Endpoint répond immédiatement 200 OK (fire-and-forget)

3. Traitement en arrière-plan :
   - Task.Run() avec nouveau scope DI
   - Évite ObjectDisposedException
```

#### Étape 2 : Téléchargement et Traitement

**Fichier** : `src/Application/UseCases/Calls/Commands/HandleTranscription/HandleTranscriptionCommandHandler.cs`

```
4. Téléchargement de la transcription depuis Vonage
   → VonageService.DownloadTranscriptionAsync()

5. Extraction du texte transcrit
   → transcriptionResult.Channels[0].ExtractTranscript()

6. Traitement MCP
   → McpService.ProcessTranscriptWithMcpAsync(transcriptText)
```

#### Étape 3 : Requête JSON-RPC au Serveur MCP

**Fichier** : `src/Infrastructure/MCP/McpService.cs`

```
7. Construction de la requête JSON-RPC :
   {
     "jsonrpc": "2.0",
     "id": 1,
     "method": "tools/call",
     "params": {
       "name": "process_transcript",
       "arguments": { "transcript": "..." }
     }
   }

8. POST HTTP → http://localhost:5000/mcp/
   - Timeout infini (bypass Polly)
   - Peut prendre 30-60 secondes
```

#### Étape 4 : Exécution du Serveur MCP

**Fichier** : `Vonage_MCP/McpServer/Program.cs`

```
9. Réception de la requête HTTP POST /mcp
   → HandleToolCallAsync()

10. Routage vers l'outil "process_transcript"
    → ExecuteToolAsync("process_transcript", arguments, ollamaClient)

11. Exécution de ProcessTranscriptFullAsync() :
    a) Détection de langue (via Ollama)
    b) Traduction si nécessaire (via Ollama)
    c) Résumé professionnel (via Ollama)
```

#### Étape 5 : Appels à Ollama

**Serveur MCP utilise OllamaSharp** :

```
12. Détection de langue :
    Prompt: "Détecte la langue du texte suivant..."
    → Ollama génère : "anglais"

13. Traduction (si non français) :
    Prompt: "Reformulate this voice message in français..."
    → Ollama génère : "Quelqu'un a appelé pour..."

14. Résumé :
    Prompt: "Create a clear, professional summary in French..."
    → Ollama génère : "Une personne a appelé pour commander 100 baguettes."
```

#### Étape 6 : Retour de la Réponse

**Remontée de la chaîne** :

```
15. Ollama → MCP Server
    Streaming response (OllamaSharp)

16. MCP Server → MCP Client
    Réponse JSON-RPC :
    {
      "jsonrpc": "2.0",
      "id": 1,
      "result": {
        "content": [{
          "type": "text",
          "text": "Une personne a appelé pour commander 100 baguettes."
        }]
      }
    }

17. McpService → HandleTranscriptionCommandHandler
    ErrorOr<string> avec le résumé

18. Sauvegarde dans OneDrive
    - transcription_YYYYMMDD_HHMMSS_{uuid}.txt (original)
    - processed_YYYYMMDD_HHMMSS_{uuid}.txt (résumé AI)
```

### Diagramme de Séquence Détaillé

```
┌────────┐    ┌────────┐    ┌──────────┐    ┌──────────┐    ┌────────┐
│ Vonage │    │ WebApi │    │ Handler  │    │   MCP    │    │ Ollama │
│        │    │        │    │          │    │  Server  │    │        │
└───┬────┘    └───┬────┘    └────┬─────┘    └────┬─────┘    └───┬────┘
    │             │              │               │              │
    │ POST /api   │              │               │              │
    │ /calls/     │              │               │              │
    │ transcribed │              │               │              │
    ├────────────>│              │               │              │
    │             │              │               │              │
    │ 200 OK      │              │               │              │
    │<────────────┤              │               │              │
    │             │              │               │              │
    │             │ Task.Run()   │               │              │
    │             │ (background) │               │              │
    │             ├─────────────>│               │              │
    │             │              │               │              │
    │             │              │ Download      │              │
    │             │              │ Transcript    │              │
    │             │              │ (Vonage)      │              │
    │             │              ├───────┐       │              │
    │             │              │       │       │              │
    │             │              │<──────┘       │              │
    │             │              │               │              │
    │             │              │ POST /mcp     │              │
    │             │              │ JSON-RPC      │              │
    │             │              │ tools/call    │              │
    │             │              ├──────────────>│              │
    │             │              │               │              │
    │             │              │               │ POST /api/   │
    │             │              │               │ generate     │
    │             │              │               │ (detect lang)│
    │             │              │               ├─────────────>│
    │             │              │               │              │
    │             │              │               │ Stream "en"  │
    │             │              │               │<─────────────┤
    │             │              │               │              │
    │             │              │               │ POST /api/   │
    │             │              │               │ generate     │
    │             │              │               │ (translate)  │
    │             │              │               ├─────────────>│
    │             │              │               │              │
    │             │              │               │ Stream FR    │
    │             │              │               │<─────────────┤
    │             │              │               │              │
    │             │              │               │ POST /api/   │
    │             │              │               │ generate     │
    │             │              │               │ (summarize)  │
    │             │              │               ├─────────────>│
    │             │              │               │              │
    │             │              │               │ Stream résumé│
    │             │              │               │<─────────────┤
    │             │              │               │              │
    │             │              │ JSON-RPC      │              │
    │             │              │ response      │              │
    │             │              │<──────────────┤              │
    │             │              │               │              │
    │             │              │ Upload to     │              │
    │             │              │ OneDrive      │              │
    │             │              ├───────┐       │              │
    │             │              │       │       │              │
    │             │              │<──────┘       │              │
    │             │              │               │              │
    │             │<─────────────┤               │              │
    │             │ (completed)  │               │              │
    │             │              │               │              │
```

---

## Intégration dans Clean Architecture

### Respect de la Clean Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        DOMAIN LAYER                         │
│                    (Pas de MCP ici)                         │
│                  Logique métier pure                        │
└─────────────────────────────────────────────────────────────┘
                              ↑
                              │ (dépendance)
                              │
┌─────────────────────────────────────────────────────────────┐
│                     APPLICATION LAYER                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  IMcpService (Interface)                                    │
│  ↑                                                          │
│  │ Appelé par HandleTranscriptionCommandHandler            │
│  │                                                          │
│  │ "Je veux traiter un transcript avec l'AI, mais je ne    │
│  │  sais pas comment c'est implémenté"                     │
│                                                             │
└─────────────────────────────────────────────────────────────┘
                              ↑
                              │ (implémente)
                              │
┌─────────────────────────────────────────────────────────────┐
│                   INFRASTRUCTURE LAYER                      │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  McpService (Implémentation concrète)                       │
│  - Sait comment parler JSON-RPC 2.0                         │
│  - Sait qu'il faut appeler http://localhost:5000/mcp/       │
│  - Gère les erreurs HTTP, timeouts, etc.                   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Pourquoi MCP est dans Infrastructure ?

| Critère | Raison |
|---------|--------|
| **Détail d'implémentation** | Le protocole JSON-RPC, HTTP, Ollama sont des détails techniques |
| **Dépendance externe** | Le serveur MCP est un service externe (comme une base de données) |
| **Substituable** | On pourrait remplacer MCP par OpenAI API sans toucher au Domain/Application |
| **Configuration** | Nécessite configuration (URL, ports, timeouts) |

### Flux de Dépendances

```
WebApi
  ↓
HandleTranscriptionCommandHandler (Application)
  ↓
IMcpService (Interface dans Application)
  ↑
  │ (implémentation injectée par DI)
  │
McpService (Infrastructure)
  ↓
HttpClient → Serveur MCP externe
```

**Inversion de dépendance** : L'Application définit l'interface `IMcpService`, l'Infrastructure l'implémente.

---

## Diagrammes de Séquence

### Séquence 1 : Appel Normal (Succès)

```
Client → Server: POST /mcp { "method": "tools/call", "params": { "name": "process_transcript", ... } }
Server → Ollama: POST /api/generate (detect language)
Ollama → Server: Stream "anglais"
Server → Ollama: POST /api/generate (translate to français)
Ollama → Server: Stream translated text
Server → Ollama: POST /api/generate (summarize)
Ollama → Server: Stream summary
Server → Client: { "result": { "content": [{ "text": "..." }] } }
```

**Durée totale** : 30-60 secondes (3 appels Ollama × 10-20s chacun)

### Séquence 2 : Gestion d'Erreur

```
Client → Server: POST /mcp { "method": "tools/call", ... }
Server → Ollama: POST /api/generate
Ollama → Server: ERROR 500 (Ollama down)
Server → Client: { "error": { "code": -32603, "message": "Tool execution failed: ..." } }
Client → User: Fallback au transcript original (sans traitement AI)
```

**Resilience** : L'application continue même si MCP/Ollama échoue.

---

## Configuration et Démarrage

### Prérequis

1. **Ollama installé et démarré**
   ```bash
   # Télécharger Ollama : https://ollama.ai

   # Démarrer Ollama
   ollama serve

   # Télécharger le modèle gemma3:4b
   ollama pull gemma3:4b
   ```

2. **Vérifier qu'Ollama fonctionne**
   ```bash
   curl http://localhost:11434/api/version
   # Devrait retourner : {"version":"0.x.x"}
   ```

### Démarrage du Serveur MCP

#### Mode HTTP (pour l'application C#)

```bash
cd C:\DataGillesPothieu\ProjectsGitHub\Vonage_MCP\McpServer
dotnet run
```

**Output attendu** :
```
[MCP Server] Running MCP JSON-RPC protocol over HTTP on http://localhost:5000
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

#### Mode STDIO (pour Claude Desktop)

```bash
cd C:\DataGillesPothieu\ProjectsGitHub\Vonage_MCP\McpServer
dotnet run --stdio
```

**Output attendu** (sur stderr) :
```
[MCP Server] Running in stdio mode
[MCP Server] All logs redirected to stderr, stdout reserved for JSON-RPC
```

### Test du Serveur MCP

#### Test avec cURL (HTTP Mode)

```bash
# Test initialize
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize"
  }'

# Test tools/list
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/list"
  }'

# Test tools/call (process_transcript)
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 3,
    "method": "tools/call",
    "params": {
      "name": "process_transcript",
      "arguments": {
        "transcript": "Hello, I want to order 100 baguettes for this weekend."
      }
    }
  }'
```

**Réponse attendue** (après 20-30 secondes) :
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "Une personne a appelé pour commander 100 baguettes pour ce weekend."
      }
    ]
  }
}
```

### Démarrage de l'Application Principale

```bash
cd C:\DataGillesPothieu\ProjectsGitHub\SSW_x_Vonage_Clean_Architecture\tools\AppHost
dotnet run
```

**Vérifier** :
- ✅ Serveur MCP démarré sur http://localhost:5000
- ✅ Ollama démarré sur http://localhost:11434
- ✅ Application principale sur https://localhost:7255

---

## Outils MCP Disponibles

### 1. `process_transcript` (Outil Principal)

**Description** : Pipeline complet de traitement de transcript vocal.

**Workflow** :
1. **Détection de langue** (via Ollama)
2. **Traduction en français** si nécessaire (via Ollama)
3. **Résumé professionnel** à la 3ème personne (via Ollama)

**Input** :
```json
{
  "transcript": "Hello, this is a test message. I want to order 100 baguettes."
}
```

**Output** :
```json
{
  "content": [{
    "type": "text",
    "text": "Une personne a appelé pour commander 100 baguettes."
  }]
}
```

**Code Source** (lignes 359-393 de Program.cs) :

```csharp
async Task<string> ProcessTranscriptFullAsync(string transcript, OllamaApiClient ollamaClient)
{
    Console.WriteLine($"[ProcessTranscript] Starting with {transcript.Length} chars");

    // Step 1: Détection de langue
    string detectedLanguage = await DetectLanguageAsync(transcript, ollamaClient);
    Console.WriteLine($"[ProcessTranscript] Detected: {detectedLanguage}");

    string processedText = transcript;
    var steps = new List<string>();

    // Step 2: Traduction si nécessaire
    if (!detectedLanguage.Contains("français", StringComparison.OrdinalIgnoreCase) &&
        !detectedLanguage.Contains("french", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("[ProcessTranscript] Translating to French...");
        processedText = await TranslateWithAI(processedText, "français", ollamaClient);
        steps.Add("translation");
    }

    // Step 3: Résumé
    Console.WriteLine("[ProcessTranscript] Summarizing...");
    processedText = await SummarizeWithAI(processedText, ollamaClient);
    steps.Add("summary");

    Console.WriteLine($"[ProcessTranscript] Complete. Steps: {string.Join(", ", steps)}");

    return processedText;
}
```

### 2. `summarize_text`

**Description** : Résume un texte en français avec un style professionnel à la 3ème personne.

**Input** :
```json
{
  "text": "Bonjour, c'est bien la boulangerie de la boustifaille ? Si oui, je voudrais commander 1450 baguettes pour ce weekend, merci"
}
```

**Output** :
```json
{
  "content": [{
    "type": "text",
    "text": "Une personne a appelé pour confirmer qu'il s'agissait bien de la boulangerie de la boustifaille, et souhaite commander 1450 baguettes pour ce weekend."
  }]
}
```

**Prompt Ollama** (lignes 417-443) :

```csharp
var prompt = $"""
You are an assistant that reformulates voice messages into professional third-person summaries.

ORIGINAL VOICE MESSAGE:
{input}

TASK:
Create a clear, professional summary in French using third-person narrative (2-3 sentences maximum).

RULES:
- Use third person (e.g., "L'appelant a mentionné...", "Une personne a appelé pour...")
- Extract ONLY the key information and main intent
- Remove filler words, repetitions, and transcription artifacts
- Write in natural, professional French as if reporting the call to someone else
- DO NOT copy the original text word-for-word
- DO NOT mention "transcription" or "summary"
- Focus on: WHO does WHAT, WHEN, WHERE, WHY

PROFESSIONAL SUMMARY IN FRENCH:
""";
```

**Paramètres Ollama** :
```csharp
Options = new RequestOptions
{
    Temperature = 0.3f,      // Plus déterministe (moins créatif)
    TopP = 0.9f,             // Limite la diversité
    RepeatPenalty = 1.2f,    // Évite les répétitions
    NumPredict = 150         // Limite la longueur (150 tokens ≈ 2-3 phrases)
}
```

### 3. `translate_text`

**Description** : Traduit un texte en reformulant de manière naturelle à la 3ème personne.

**Input** :
```json
{
  "text": "Hello, is this the right bakery? I need 100 baguettes",
  "target_language": "français"
}
```

**Output** :
```json
{
  "content": [{
    "type": "text",
    "text": "Quelqu'un a appelé pour vérifier qu'il s'agissait bien de la bonne boulangerie, et souhaite commander 100 baguettes."
  }]
}
```

**Prompt Ollama** (lignes 468-492) :

```csharp
var prompt = $"""
You are an assistant that reformulates voice messages in a natural, third-person narrative style.

ORIGINAL VOICE MESSAGE (transcribed):
{input}

TASK:
Reformulate this voice message in {targetLanguage} using a natural, third-person perspective.

RULES:
- Use third person (e.g., "Someone called to...", "The caller asked...")
- Provide context and clarity
- Keep the same information but make it sound like a message summary for someone else
- Remove filler words and transcription artifacts
- Write in clear, professional {targetLanguage}
- DO NOT translate word-for-word, REFORMULATE naturally

REFORMULATED MESSAGE IN {targetLanguage}:
""";
```

### 4. `analyze_sentiment`

**Description** : Analyse le sentiment d'un texte (positif, négatif, neutre).

**Input** :
```json
{
  "text": "This is amazing! I love it!"
}
```

**Output** :
```json
{
  "content": [{
    "type": "text",
    "text": "positif"
  }]
}
```

**Prompt Ollama** (lignes 516-525) :

```csharp
var prompt = $"""
Analyse le sentiment du texte suivant.
Réponds uniquement par: "positif", "négatif" ou "neutre".

Texte: {input}

Sentiment:
""";
```

---

## Traitement AI avec Ollama

### OllamaSharp Client

Le serveur MCP utilise **OllamaSharp** (bibliothèque .NET pour Ollama) :

```csharp
using OllamaSharp;

var ollama = new OllamaApiClient("http://localhost:11434", "gemma3:4b");
```

**Modèle utilisé** : `gemma3:4b` (Google Gemma 3, 4 milliards de paramètres)

**Pourquoi gemma3:4b ?**
- ✅ **Rapide** : 10-20 secondes par génération sur CPU
- ✅ **Léger** : ~2.5 GB de RAM
- ✅ **Performant** : Bonne qualité de traduction/résumé
- ✅ **Multilingue** : Supporte français, anglais, espagnol, etc.

### Génération Streaming

Toutes les réponses Ollama utilisent le **streaming** pour éviter de bloquer :

```csharp
var responseBuilder = new StringBuilder();
var responseStream = ollamaClient.GenerateAsync(new GenerateRequest
{
    Prompt = prompt,
    Options = new RequestOptions
    {
        Temperature = 0.3f,
        TopP = 0.9f,
        RepeatPenalty = 1.2f
    }
});

await foreach (var response in responseStream)
{
    if (response?.Response != null)
    {
        responseBuilder.Append(response.Response);
    }
}

return responseBuilder.ToString().Trim();
```

**Avantages du streaming** :
- ✅ Réactivité : Le texte se génère token par token
- ✅ Pas de timeout : Chaque chunk arrive avant le timeout HTTP
- ✅ Moins de mémoire : Pas besoin de tout garder en RAM

### Performance Typique

| Opération | Durée (gemma3:4b sur CPU) |
|-----------|---------------------------|
| Détection de langue | 5-10 secondes |
| Traduction | 10-20 secondes |
| Résumé | 10-20 secondes |
| **TOTAL (process_transcript)** | **30-60 secondes** |

**Sur GPU** : Diviser par 3-5 (10-15 secondes total)

### Choix des Paramètres Ollama

| Paramètre | Valeur | Raison |
|-----------|--------|--------|
| `Temperature` | 0.3 | Plus déterministe, moins créatif (bon pour traduction/résumé) |
| `TopP` | 0.9 | Limite la diversité du vocabulaire (évite les tokens improbables) |
| `RepeatPenalty` | 1.2 | Pénalise les répétitions (important pour résumés) |
| `NumPredict` | 150 (résumé) | Limite la longueur (150 tokens ≈ 2-3 phrases en français) |

### Alternative : Autres Modèles Ollama

Si vous voulez changer de modèle :

```csharp
// Dans Program.cs, ligne 10
var ollama = new OllamaApiClient("http://localhost:11434", "llama3:8b");
// ou
var ollama = new OllamaApiClient("http://localhost:11434", "mistral:7b");
```

**Comparaison** :

| Modèle | Taille | Vitesse | Qualité | Langues |
|--------|--------|---------|---------|---------|
| `gemma3:4b` | 2.5 GB | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ |
| `llama3:8b` | 4.7 GB | ⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| `mistral:7b` | 4.1 GB | ⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐ |
| `phi3.5:3.8b` | 2.2 GB | ⭐⭐⭐ | ⭐⭐ | ⭐⭐ |

---

## Résumé de l'Architecture

### Points Clés

1. **MCP = JSON-RPC 2.0** : Protocole standard pour appeler des outils distants
2. **Dual Transport** : STDIO (Claude Desktop) + HTTP (C# Client)
3. **Client Simple** : McpService envoie requêtes, parse réponses
4. **Serveur Modulaire** : 4 outils (process_transcript, summarize, translate, sentiment)
5. **AI Backend** : Ollama avec modèle gemma3:4b (local, rapide, gratuit)
6. **Clean Architecture** : Interface dans Application, implémentation dans Infrastructure
7. **Timeout Handling** : HttpClient manuel pour bypasser Polly d'Aspire
8. **Fire-and-Forget** : Webhook répond immédiatement, traitement en arrière-plan

### Fichiers Principaux

| Fichier | Rôle | Lignes Clés |
|---------|------|-------------|
| `src/Infrastructure/MCP/McpService.cs` | Client MCP | 20-70 (ProcessTranscriptWithMcpAsync) |
| `src/Infrastructure/DependencyInjection.cs` | Config HttpClient | 110-125 (bypass Polly) |
| `Vonage_MCP/McpServer/Program.cs` | Serveur MCP | 76-124 (HTTP mode), 25-71 (STDIO mode) |
| `src/Application/Common/Interfaces/IMcpService.cs` | Interface | 3-6 (contrat) |
| `src/Application/UseCases/Calls/Commands/HandleTranscription/HandleTranscriptionCommandHandler.cs` | Orchestration | 78 (appel MCP) |
| `src/WebApi/Endpoints/CallEndpoints.cs` | Webhook | 85-103 (fire-and-forget) |

### Diagramme Final : Vue d'Ensemble

```
┌──────────────────────────────────────────────────────────────────────┐
│                         CLEAN ARCHITECTURE                           │
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌────────────────────────────────────────────────────────────┐     │
│  │                    APPLICATION LAYER                       │     │
│  │  - HandleTranscriptionCommandHandler                       │     │
│  │  - IMcpService (interface)                                 │     │
│  └────────────────────┬───────────────────────────────────────┘     │
│                       │                                             │
│  ┌────────────────────▼───────────────────────────────────────┐     │
│  │                  INFRASTRUCTURE LAYER                      │     │
│  │  - McpService (implémentation)                             │     │
│  │  - HttpClient (manuel, bypass Polly)                       │     │
│  │  - Base URL: http://localhost:5000/mcp/                    │     │
│  └────────────────────┬───────────────────────────────────────┘     │
│                       │                                             │
└───────────────────────┼─────────────────────────────────────────────┘
                        │ HTTP POST (JSON-RPC 2.0)
                        │ { "method": "tools/call", ... }
                        ▼
┌──────────────────────────────────────────────────────────────────────┐
│                       MCP SERVER (Externe)                           │
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌────────────────────────────────────────────────────────────┐     │
│  │  ASP.NET Core Minimal API (http://localhost:5000)          │     │
│  │  - Endpoint: POST /mcp                                     │     │
│  │  - JSON-RPC Handler                                        │     │
│  └────────────────────┬───────────────────────────────────────┘     │
│                       │                                             │
│  ┌────────────────────▼───────────────────────────────────────┐     │
│  │  Tool Executor                                             │     │
│  │  - process_transcript (langue + traduction + résumé)       │     │
│  │  - summarize_text                                          │     │
│  │  - translate_text                                          │     │
│  │  - analyze_sentiment                                       │     │
│  └────────────────────┬───────────────────────────────────────┘     │
│                       │                                             │
│  ┌────────────────────▼───────────────────────────────────────┐     │
│  │  OllamaSharp Client                                        │     │
│  │  - URL: http://localhost:11434                             │     │
│  │  - Modèle: gemma3:4b                                       │     │
│  └────────────────────┬───────────────────────────────────────┘     │
│                       │                                             │
└───────────────────────┼─────────────────────────────────────────────┘
                        │ HTTP POST /api/generate
                        │ { "prompt": "...", "model": "gemma3:4b" }
                        ▼
┌──────────────────────────────────────────────────────────────────────┐
│                     OLLAMA AI (http://localhost:11434)               │
│  - Génération de texte (streaming)                                  │
│  - Modèle: gemma3:4b (2.5 GB)                                       │
│  - Durée: 10-20s par génération                                     │
└──────────────────────────────────────────────────────────────────────┘
```

---

## Références

- **MCP Specification** : [https://modelcontextprotocol.io](https://modelcontextprotocol.io)
- **JSON-RPC 2.0 Spec** : [https://www.jsonrpc.org/specification](https://www.jsonrpc.org/specification)
- **OllamaSharp** : [https://github.com/awaescher/OllamaSharp](https://github.com/awaescher/OllamaSharp)
- **Ollama** : [https://ollama.ai](https://ollama.ai)
- **Timeout Resolution** : [docs/MCP_TIMEOUT_RESOLUTION.md](MCP_TIMEOUT_RESOLUTION.md)

---

## Prochaines Étapes / Améliorations Possibles

### 1. Production Readiness

- [ ] Déployer le serveur MCP sur un serveur dédié
- [ ] Ajouter authentification (API Key, JWT)
- [ ] Implémenter rate limiting (éviter abus Ollama)
- [ ] Ajouter monitoring (Application Insights, Prometheus)
- [ ] Configurer URL MCP via `appsettings.json` (au lieu de hardcoder localhost:5000)

### 2. Fonctionnalités

- [ ] Ajouter outil `extract_entities` (noms, dates, numéros)
- [ ] Ajouter outil `categorize_call` (support, vente, réclamation)
- [ ] Supporter plusieurs modèles Ollama (gemma3, llama3, mistral)
- [ ] Cache des résumés (éviter de re-traiter les mêmes transcripts)

### 3. Performance

- [ ] Utiliser GPU pour Ollama (10x plus rapide)
- [ ] Paralléliser les appels Ollama (détection + traduction en même temps)
- [ ] Compression des requêtes/réponses HTTP (GZIP)

### 4. Tests

- [ ] Tests unitaires du serveur MCP (tools/list, tools/call)
- [ ] Tests d'intégration McpService → MCP Server
- [ ] Mock d'Ollama pour tests rapides
- [ ] Tests de charge (combien de requêtes simultanées ?)

### 5. Documentation

- [x] Documentation architecture MCP client-server (ce fichier)
- [x] Documentation résolution des timeouts
- [ ] Tutoriel vidéo de déploiement
- [ ] Guide de contribution pour ajouter de nouveaux outils MCP
