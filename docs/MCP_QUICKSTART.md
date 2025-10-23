# MCP Integration - Quick Start Guide

## Vue d'Ensemble

L'application intègre un serveur MCP externe pour le traitement AI (via Ollama) des transcriptions vocales Vonage.

## Architecture Simplifiée

```
Vonage Webhook → WebApi
                   ↓
        HandleTranscriptionCommand
                   ↓
              McpService (Client)
                   ↓ HTTP JSON-RPC 2.0
                   ↓ http://localhost:5000/mcp/
                   ↓
        Serveur MCP (Vonage_MCP/McpServer)
                   ↓ OllamaSharp
                   ↓
              Ollama AI (gemma3:4b)
                   ↓
        Résumé professionnel en français
                   ↓
              OneDrive (2 fichiers)
```

## Workflow Complet

1. **Vonage** envoie webhook `/api/calls/transcribed` avec URL de transcription
2. **Endpoint** répond 200 OK immédiatement (fire-and-forget)
3. **Tâche arrière-plan** :
   - Télécharge transcription depuis Vonage
   - Envoie au serveur MCP (30-60s)
4. **Serveur MCP** traite avec Ollama :
   - Détecte langue (`DetectLanguageAsync`)
   - Traduit en français si nécessaire (`TranslateWithAI`)
   - Résume professionnellement à la 3ème personne (`SummarizeWithAI`)
5. **OneDrive** sauvegarde 2 fichiers :
   - `transcription_YYYYMMDD_HHMMSS_{uuid}.txt` (original)
   - `processed_YYYYMMDD_HHMMSS_{uuid}.txt` (résumé AI)

## Démarrage Rapide

### Prérequis

1. **Ollama installé et démarré**
   ```bash
   ollama serve
   ollama pull gemma3:4b
   ```

2. **Vérifier Ollama**
   ```bash
   curl http://localhost:11434/api/version
   # Devrait retourner: {"version":"0.x.x"}
   ```

### Démarrer le Serveur MCP

```bash
cd C:\DataGillesPothieu\ProjectsGitHub\Vonage_MCP\McpServer
dotnet run
```

**Output attendu** :
```
[MCP Server] Running MCP JSON-RPC protocol over HTTP on http://localhost:5000
```

### Démarrer l'Application

```bash
cd C:\DataGillesPothieu\ProjectsGitHub\SSW_x_Vonage_Clean_Architecture\tools\AppHost
dotnet run
```

## Fichiers Clés

| Fichier | Rôle | Lignes Clés |
|---------|------|-------------|
| [src/Infrastructure/MCP/McpService.cs](../src/Infrastructure/MCP/McpService.cs) | Client MCP | 81-200 |
| [src/Infrastructure/DependencyInjection.cs](../src/Infrastructure/DependencyInjection.cs) | Config HttpClient (bypass Polly) | 110-125 |
| [src/WebApi/Endpoints/CallEndpoints.cs](../src/WebApi/Endpoints/CallEndpoints.cs) | Fire-and-forget webhook | 71-111 |
| [src/Application/Common/Interfaces/IMcpService.cs](../src/Application/Common/Interfaces/IMcpService.cs) | Interface (Application) | 4-6 |
| `Vonage_MCP/McpServer/Program.cs` | Serveur MCP + Ollama | Complet |

## Points Clés de l'Implémentation

### 1. HttpClient Manuel (Bypass Polly)

**Problème** : .NET Aspire injecte automatiquement Polly avec timeouts (10s, 30s) dans tous les HttpClients.

**Solution** : Créer HttpClient manuellement au lieu d'utiliser `AddHttpClient()`.

**Fichier** : [DependencyInjection.cs](../src/Infrastructure/DependencyInjection.cs:110-125)

```csharp
services.AddScoped<IMcpService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<McpService>>();

    // Création manuelle - bypass Aspire's AddServiceDefaults()
    var httpClient = new HttpClient
    {
        BaseAddress = new Uri("http://localhost:5000/mcp/"),
        Timeout = Timeout.InfiniteTimeSpan // Ollama prend 30-60s
    };

    return new McpService(httpClient, logger);
});
```

### 2. Fire-and-Forget Pattern

**Problème** : Vonage webhooks timeout après ~10 secondes, mais MCP prend 30-60s.

**Solution** : Répondre 200 OK immédiatement, traiter en arrière-plan avec `Task.Run()`.

**Fichier** : [CallEndpoints.cs](../src/WebApi/Endpoints/CallEndpoints.cs:71-111)

```csharp
.MapPost("/transcribed", (
    TranscriptionCallbackRequest request,
    ILogger<Program> logger,
    IServiceProvider serviceProvider) =>  // IServiceProvider (singleton)
{
    var command = new HandleTranscriptionCommand(request);

    // Fire-and-forget: traitement en arrière-plan
    _ = Task.Run(async () =>
    {
        using var scope = serviceProvider.CreateScope(); // Nouveau scope
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(command, CancellationToken.None);
    }, CancellationToken.None);

    // Réponse immédiate (< 100ms)
    return TypedResults.Ok(new { message = "Processing..." });
})
```

### 3. Nouveau Scope DI

**Problème** : Le request scope est disposé quand on retourne 200 OK, causant `ObjectDisposedException` dans Task.Run().

**Solution** : Créer un nouveau scope dans `Task.Run()` qui reste vivant pendant le traitement.

```csharp
// ❌ NE FONCTIONNE PAS - ISender disposé après retour
.MapPost("/transcribed", (request, ISender sender) => {
    _ = Task.Run(async () => await sender.Send(...)); // ObjectDisposedException!
    return Ok();
})

// ✅ FONCTIONNE - Nouveau scope créé
.MapPost("/transcribed", (request, IServiceProvider sp) => {
    _ = Task.Run(async () => {
        using var scope = sp.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(...); // ✓ Fonctionne
    });
    return Ok();
})
```

## Protocole JSON-RPC 2.0

Le serveur MCP utilise JSON-RPC 2.0 (standard pour RPC en JSON).

### Requête

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "process_transcript",
    "arguments": {
      "transcript": "Hello, I want 100 baguettes."
    }
  }
}
```

### Réponse (Succès)

```json
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
```

### Réponse (Erreur)

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

## Tests

### Test Serveur MCP avec cURL

```bash
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/call",
    "params": {
      "name": "process_transcript",
      "arguments": {
        "transcript": "Hello, I want to order 100 baguettes for this weekend."
      }
    }
  }'
```

**Durée** : 30-60 secondes

**Réponse attendue** :
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "content": [{
      "type": "text",
      "text": "Une personne a appelé pour commander 100 baguettes pour ce weekend."
    }]
  }
}
```

## Troubleshooting

### Erreur : "Connection refused" sur port 5000

**Cause** : Serveur MCP pas démarré

**Solution** :
```bash
cd Vonage_MCP/McpServer
dotnet run
```

---

### Erreur : "Ollama is not responding"

**Cause** : Ollama pas démarré

**Solution** :
```bash
ollama serve
ollama pull gemma3:4b
```

---

### Timeout après 30 secondes

**Cause** : Client HTTP avec timeout par défaut de Polly

**Solution** : Vérifier que HttpClient est créé manuellement (DependencyInjection.cs:110-125)

---

### Génération trop lente (> 60s)

**Solutions** :
1. Utiliser un modèle plus petit : `phi3.5:3.8b`
2. Utiliser un GPU NVIDIA avec CUDA (10x plus rapide)

## Documentation Complète

Pour une compréhension approfondie de l'architecture MCP :

### 📖 Documentation Principale

- **[MCP Architecture](MCP_ARCHITECTURE.md)** - Architecture complète client-server
  - Protocole JSON-RPC 2.0 détaillé
  - Flux de communication avec diagrammes de séquence
  - Intégration dans Clean Architecture
  - Outils MCP disponibles (4 outils)
  - Configuration Ollama et modèles
  - Performance et optimisations

- **[MCP Timeout Resolution](MCP_TIMEOUT_RESOLUTION.md)** - Guide troubleshooting timeouts
  - Historique complet des problèmes
  - Toutes les solutions tentées (avec raisons d'échec)
  - Solution finale avec bypass Polly
  - Fire-and-forget pattern pour webhooks
  - Tests et validation

- **[MCP Server README](../../Vonage_MCP/README.md)** - Guide serveur MCP
  - Installation et démarrage (HTTP + STDIO modes)
  - Tests avec cURL/PowerShell
  - Outils disponibles (process_transcript, summarize, translate, sentiment)
  - Configuration Ollama (modèles, paramètres)
  - Troubleshooting complet

## Résumé

### Composants

1. **McpService** (client) - Envoie requêtes JSON-RPC au serveur MCP
2. **Serveur MCP** - Traite avec Ollama (détection langue + traduction + résumé)
3. **Ollama** - Génération AI locale (modèle gemma3:4b)
4. **Fire-and-forget** - Pattern pour éviter timeout des webhooks Vonage

### Technologies

- **JSON-RPC 2.0** - Protocole de communication MCP
- **OllamaSharp** - Client .NET pour Ollama
- **gemma3:4b** - Modèle AI (Google Gemma 3, 4B paramètres)
- **.NET Aspire** - Orchestration (avec contournement Polly pour MCP)
- **ASP.NET Core** - Serveur HTTP du serveur MCP

### Durées Typiques

| Opération | Durée (CPU) |
|-----------|-------------|
| Détection langue | 5-10s |
| Traduction | 10-20s |
| Résumé | 10-20s |
| **process_transcript (complet)** | **30-60s** |

Avec GPU : 10-15s total
