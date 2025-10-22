# OneDrive Integration for Recording Upload

This guide walks you through configuring Microsoft Graph API integration to upload Vonage call recordings to your personal OneDrive for Business account using **Device Code Authentication**.

## Table of Contents

1. [Overview](#overview)
2. [Azure AD App Registration](#azure-ad-app-registration)
3. [Configuration](#configuration)
4. [Authentication Setup (IMPORTANT)](#authentication-setup-important)
5. [Architecture & Implementation](#architecture--implementation)
6. [Usage & Testing](#usage--testing)
7. [Troubleshooting](#troubleshooting)
8. [Security Best Practices](#security-best-practices)

---

## Overview

The OneDrive integration allows your application to automatically upload call recordings from Vonage to your personal OneDrive for Business account.

### Key Features

- ✅ **Automatic upload** of recording files to OneDrive
- ✅ **Seekable stream handling** - converts HTTP streams to memory streams for Graph API compatibility
- ✅ **Smart upload strategy** - simple upload (<4MB) or chunked upload (>4MB) with progress tracking
- ✅ **Organized folder structure** - `/Vonage_Call_Recordings/2025-10-22/recording_xxx.mp3`
- ✅ **Secure authentication** via Azure AD Device Code Flow
- ✅ **Automatic token caching** - authenticate once, works forever (with automatic refresh)
- ✅ **Complete error handling** with `ErrorOr<T>` pattern

### Authentication Method

This implementation uses **Device Code Flow** (OAuth 2.0) with **Delegated Permissions**:

| Feature | Device Code Flow (Our Implementation) |
|---------|--------------------------------------|
| **Uploads to** | ✅ Your personal OneDrive (authenticated user) |
| **Setup** | ✅ One-time interactive authentication |
| **Secrets** | ✅ No client secret required |
| **Token** | ✅ Cached automatically, refreshed when needed |
| **Accounts** | ✅ Personal, work, or school accounts |
| **Permissions** | ✅ Least privilege (Files.ReadWrite - Delegated) |
| **User experience** | Interactive prompt first time, then automatic |

---

## Azure AD App Registration

Follow these steps to register your application in Azure AD.

### Step 1: Access Azure Portal

1. Go to [Azure Portal](https://portal.azure.com/)
2. Sign in with your Microsoft 365 account
3. Navigate to **Azure Active Directory** (search "Azure AD" in top bar)

### Step 2: Register a New Application

1. Click **App registrations** (left sidebar)
2. Click **+ New registration**
3. Fill in the registration form:

   | Field | Value |
   |-------|-------|
   | **Name** | `SSW Vonage Recording Uploader` |
   | **Supported account types** | **Accounts in this organizational directory only** (Single tenant) |
   | **Redirect URI** | Leave blank |

4. Click **Register**

### Step 3: Note Your Application IDs

After registration, copy these values from the **Overview** page:

- **Application (client) ID**: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` → Your **`ClientId`**
- **Directory (tenant) ID**: `yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy` → Your **`TenantId`**

### Step 4: Configure API Permissions

1. Click **API permissions** (left sidebar)
2. Click **+ Add a permission**
3. Select **Microsoft Graph**
4. Select **Delegated permissions** (⚠️ **NOT** Application permissions)
5. Search for and select:

   | Permission | Type | Reason |
   |------------|------|--------|
   | **Files.ReadWrite** | Delegated | Read and write files in user's OneDrive |

6. Click **Add permissions**
7. Click **Grant admin consent for [Your Organization]** (optional but recommended)

### Step 5: Enable Public Client Flow

**⚠️ CRITICAL STEP** - Required for Device Code Flow

1. Click **Authentication** (left sidebar)
2. Scroll down to **Advanced settings**
3. Find **Allow public client flows**
4. Toggle to **Yes**
5. Click **Save**

> **Without this setting**, device code authentication will fail with `invalid_client` error.

---

## Configuration

### Step 1: Configure appsettings.json

Add the OneDrive configuration to [src/WebApi/appsettings.json](../src/WebApi/appsettings.json):

```json
{
  "OneDrive": {
    "TenantId": "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy",
    "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "UploadFolderPath": "Vonage_Call_Recordings"
  }
}
```

#### Configuration Values

| Setting | Where to find it | Example | Notes |
|---------|-----------------|---------|-------|
| `TenantId` | Azure AD > Overview > Tenant ID | `12345678-1234-...` | GUID format |
| `ClientId` | App registrations > Your App > Application (client) ID | `abcdefab-abcd-...` | GUID format |
| `UploadFolderPath` | You choose this | `Vonage_Call_Recordings` | Root folder name in OneDrive |

> **Note**: No `ClientSecret` needed for Device Code Flow! ✅

### Step 2: User Secrets (Development - Recommended)

For local development, use **User Secrets**:

```bash
cd src/WebApi

# Initialize user secrets
dotnet user-secrets init

# Set OneDrive configuration
dotnet user-secrets set "OneDrive:TenantId" "your-tenant-id"
dotnet user-secrets set "OneDrive:ClientId" "your-client-id"
dotnet user-secrets set "OneDrive:UploadFolderPath" "Vonage_Call_Recordings"
```

Verify:
```bash
dotnet user-secrets list
```

### Step 3: Production Configuration (Azure)

For Azure App Service, use **Application Settings**:

```bash
# Via Azure CLI
az webapp config appsettings set --name your-app-name \
  --resource-group your-rg \
  --settings \
    OneDrive__TenantId="your-tenant-id" \
    OneDrive__ClientId="your-client-id" \
    OneDrive__UploadFolderPath="Vonage_Call_Recordings"
```

Or via Azure Portal: **App Service** > **Configuration** > **Application settings**

---

## Authentication Setup (IMPORTANT)

### ⚠️ Critical: Authenticate BEFORE First Recording

The Device Code Flow requires **interactive authentication** the first time. You must authenticate **before** receiving your first recording callback from Vonage.

### How to Authenticate

#### Step 1: Start Your Application

```bash
cd tools/AppHost
dotnet run
```

Wait for the application to start (Aspire Dashboard will open).

#### Step 2: Call the Authentication Endpoint

**PowerShell**:
```powershell
Invoke-RestMethod -Uri "https://localhost:7255/api/calls/connection-to-one-drive" -Method GET
```

**cURL (Bash/WSL)**:
```bash
curl -X GET "https://localhost:7255/api/calls/connection-to-one-drive" -k
```

**Browser**:
```
https://localhost:7255/api/calls/connection-to-one-drive
```

#### Step 3: Follow the Device Code Prompt

You'll see output in the **console** (not browser) like:

```
To sign in, use a web browser to open the page https://microsoft.com/devicelogin
and enter the code ABCD1234 to authenticate.
```

**What to do:**

1. Open https://microsoft.com/devicelogin in your browser
2. Enter the code shown (e.g., `ABCD1234`)
3. Sign in with your Microsoft account (the one that owns the OneDrive)
4. Grant consent when prompted
5. Return to the console - authentication will complete automatically

#### Step 4: Verify Success

The API endpoint will return:

```json
{
  "success": true,
  "message": "OneDrive authentication successful! Token cached for future uploads.",
  "fileId": "01JHZ5MQ...",
  "fileName": "connection_test_20251022_135530.txt",
  "uploadedTo": "Vonage_Call_Recordings/ConnectionTests/connection_test_20251022_135530.txt",
  "nextSteps": [
    "Authentication token is now cached",
    "Recording uploads will work automatically",
    "Token will be refreshed automatically when needed"
  ]
}
```

**Check OneDrive**: You should see a test file in `Vonage_Call_Recordings/ConnectionTests/`

### Authentication is Done! ✅

The access token is now cached. All future recording uploads will work automatically without user interaction.

---

## Architecture & Implementation

### High-Level Flow

```
┌─────────────────────────────────────────────────────────────────┐
│  1. Vonage Call Recording Callback                             │
│     POST /api/calls/recorded                                    │
│     { "recordingUrl": "https://api.vonage.com/...", ... }       │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────┐
│  2. HandleRecordingCommandHandler                               │
│     - Extracts recording URL                                    │
│     - Generates filename: recording_20251022_135530_{uuid}.mp3  │
│     - Creates dated folder: 2025-10-22                          │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────┐
│  3. VonageService.DownloadRecordingAsync()                      │
│     - Generates JWT token for Vonage authentication             │
│     - Downloads recording from Vonage URL with auth header      │
│     - Converts HTTP stream → MemoryStream (seekable!)           │
│     - Returns: ErrorOr<Stream> (seekable MemoryStream)          │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────┐
│  4. OneDriveService.UploadFileFromStreamAsync()                 │
│     - Authenticates with cached token (DeviceCodeCredential)    │
│     - Gets user's drive via /me/drive endpoint                  │
│     - Creates folder structure if needed:                       │
│       /Vonage_Call_Recordings/2025-10-22/                       │
│     - Checks stream.Length to decide upload method:             │
│         < 4MB: Simple PUT upload                                │
│         ≥ 4MB: Chunked upload with progress tracking            │
│     - Returns: ErrorOr<string> (OneDrive file ID)               │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────┐
│  5. Success! File uploaded to OneDrive                          │
│     /Vonage_Call_Recordings/2025-10-22/recording_xxx.mp3        │
└─────────────────────────────────────────────────────────────────┘
```

### Key Implementation Details

#### 1. Stream Handling Fix (Main Bug Fix)

**Problem**: HTTP response streams from Vonage don't support `stream.Length` property, which is required by Microsoft Graph API.

**Solution**: [VonageService.cs:148-160](../src/Infrastructure/Vonage/VonageService.cs#L148-L160)

```csharp
// Copy HTTP stream to MemoryStream to make it seekable
var memoryStream = new MemoryStream();
await using var httpStream = await response.Content.ReadAsStreamAsync(cancellationToken);
await httpStream.CopyToAsync(memoryStream, cancellationToken);
memoryStream.Position = 0; // Reset to beginning

_logger.LogInformation(
    "Recording downloaded successfully. Size: {Size} bytes",
    memoryStream.Length);

return memoryStream; // ✅ Seekable, supports Length property
```

#### 2. Authentication with Device Code Flow

**Implementation**: [DependencyInjection.cs](../src/Infrastructure/DependencyInjection.cs)

```csharp
// Register DeviceCodeCredential as singleton (token cache shared)
services.AddSingleton<DeviceCodeCredential>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<OneDriveSettings>>().Value;
    return new DeviceCodeCredential(new DeviceCodeCredentialOptions
    {
        TenantId = settings.TenantId,
        ClientId = settings.ClientId,
        DeviceCodeCallback = (code, cancellationToken) =>
        {
            Console.WriteLine(code.Message); // Shows device code prompt
            return Task.CompletedTask;
        }
    });
});
```

**Why Singleton?** Token cache is stored in the credential instance. Sharing it across requests prevents re-authentication.

#### 3. Upload Strategy

[OneDriveService.cs:122-143](../src/Infrastructure/OneDrive/OneDriveService.cs#L122-L143)

```csharp
const long maxSimpleUploadSize = 4 * 1024 * 1024; // 4MB

if (!fileStream.CanSeek)
{
    return Error.Failure("OneDrive.StreamNotSeekable",
        "Stream must be seekable");
}

if (fileStream.Length < maxSimpleUploadSize)
{
    // Simple upload - single PUT request
    return await SimpleUploadAsync(drive, fullPath, fileStream, ct);
}
else
{
    // Chunked upload - multiple requests with progress tracking
    return await ChunkedUploadAsync(drive, fullPath, fileStream, ct);
}
```

### File Structure

```
src/
├── Application/
│   ├── Common/Interfaces/
│   │   ├── IOneDriveService.cs             # Service abstraction
│   │   └── IVonageService.cs               # Vonage service abstraction
│   └── UseCases/
│       └── Calls/
│           └── Commands/
│               └── HandleRecording/
│                   ├── HandleRecordingCommand.cs
│                   ├── HandleRecordingCommandHandler.cs  # Orchestrates download + upload
│                   └── HandleRecordingCommandValidator.cs
│
├── Infrastructure/
│   ├── OneDrive/
│   │   ├── OneDriveService.cs              # Microsoft Graph implementation
│   │   └── OneDriveSettings.cs             # Configuration model
│   ├── Vonage/
│   │   └── VonageService.cs                # Downloads recording with JWT auth
│   └── DependencyInjection.cs              # Service registration
│
└── WebApi/
    ├── appsettings.json                    # Configuration
    └── Endpoints/
        └── CallEndpoints.cs                # API endpoints
```

---

## Usage & Testing

### Folder Structure in OneDrive

After successful uploads, your OneDrive will have:

```
OneDrive/
└── Vonage_Call_Recordings/          (root folder from config)
    ├── ConnectionTests/              (from authentication endpoint)
    │   └── connection_test_20251022_135530.txt
    ├── 2025-10-22/                   (dated subfolders)
    │   ├── recording_20251022_135530_2138b8cf-3ca8-4d5a-b4d8-1210355218f9.mp3
    │   └── recording_20251022_141045_f4f6ae90-b4dc-426e-9e8b-c9f4ead58ffc.mp3
    └── 2025-10-23/
        └── recording_20251023_093022_a5b1c02d-58fa-4fa5-b663-adab07e5293a.mp3
```

### Testing End-to-End

#### 1. Authenticate First (One-Time Setup)

```powershell
Invoke-RestMethod -Uri "https://localhost:7255/api/calls/connection-to-one-drive" -Method GET
```

Follow device code prompt in console.

#### 2. Make a Test Call

```powershell
$body = @{
    CallRequest = "+33123456789"
} | ConvertTo-Json

Invoke-RestMethod `
    -Uri "https://localhost:7255/api/calls/initiate" `
    -Method POST `
    -ContentType "application/json" `
    -Body $body
```

#### 3. Wait for Recording

After the call ends:
1. Vonage processes the recording (~30 seconds)
2. Vonage calls your webhook: `POST /api/calls/recorded`
3. Your app downloads the recording from Vonage (with JWT auth)
4. Your app uploads to OneDrive (with cached token)

#### 4. Check Logs

```
info: VonageService: Downloading recording from {url}
info: VonageService: Recording downloaded successfully. Size: 18125 bytes
info: HandleRecordingCommandHandler: Uploading recording to OneDrive...
info: OneDriveService: Using simple upload for file...
info: OneDriveService: File uploaded successfully with ID: xxx
info: HandleRecordingCommandHandler: Recording uploaded successfully!
```

#### 5. Verify in OneDrive

Open [OneDrive](https://onedrive.live.com) and navigate to:
```
Vonage_Call_Recordings → 2025-10-22 → recording_xxx.mp3
```

---

## Troubleshooting

### Common Errors

#### 1. `AADSTS7000218: The request body must contain the following parameter: 'client_assertion' or 'client_secret'`

**Cause**: "Allow public client flows" is disabled.

**Solution**:
1. Azure Portal → App registrations → Your App → **Authentication**
2. Scroll to **Advanced settings**
3. **Allow public client flows** → Set to **Yes**
4. Click **Save**

#### 2. `Stream does not support seeking`

**Cause**: Trying to upload an HTTP stream directly (before fix).

**Solution**: Already fixed! VonageService now converts to MemoryStream. If you see this error, verify you're using the latest code.

#### 3. `OneDrive.AccessDenied` or `Insufficient privileges`

**Causes**:
- Permission not granted
- Using Application permissions instead of Delegated
- Missing admin consent

**Solutions**:
- Verify **Files.ReadWrite** is added as **Delegated** permission
- Click "Grant admin consent" in API permissions
- Re-authenticate (delete cached token and call `/connection-to-one-drive` again)

#### 4. `OneDrive.UserDriveNotFound`

**Cause**: User hasn't provisioned OneDrive.

**Solution**:
- Log in to https://onedrive.com at least once with the Microsoft account
- Wait 5-10 minutes for OneDrive to provision

#### 5. Device code prompt not showing

**Causes**:
- DeviceCodeCredential not registered as singleton
- Console output not visible

**Solutions**:
- Check DependencyInjection.cs - credential must be singleton
- Run app in console mode (not as service) to see device code
- Check logs - device code message is logged

### Debugging Tips

#### Enable Detailed Logging

`appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Graph": "Debug",
      "SSW_x_Vonage_Clean_Architecture.Infrastructure": "Debug"
    }
  }
}
```

#### Clear Cached Token

If you need to re-authenticate:

**Windows**:
```
%LOCALAPPDATA%\.IdentityService
```

**Linux/macOS**:
```
~/.IdentityService/
```

Delete the cache folder and call `/connection-to-one-drive` again.

---

## Security Best Practices

### 1. Token Security

| Aspect | Implementation |
|--------|----------------|
| **Token storage** | Encrypted cache in user profile (`~/.IdentityService/`) |
| **Token refresh** | Automatic (handled by Azure.Identity SDK) |
| **Token lifetime** | 1 hour (access token), 90 days (refresh token) |
| **Token rotation** | Automatic on expiry |

### 2. Least Privilege

✅ **Current implementation**:
- Uses **Delegated** permissions (user context)
- Only **Files.ReadWrite** permission (not Files.ReadWrite.All)
- Uploads to authenticated user's OneDrive only
- No admin permissions required

### 3. Secrets Management

| Environment | Method |
|-------------|--------|
| **Local Dev** | User Secrets (`dotnet user-secrets`) |
| **Azure** | App Service Configuration |
| **CI/CD** | Environment variables |

**No secrets to manage!** Device Code Flow doesn't use client secrets.

### 4. Monitoring

Track these metrics:
- Upload success/failure rates
- Authentication failures
- File sizes and upload durations
- Microsoft Graph API errors

Use Application Insights (if deployed to Azure) or structured logging.

### 5. Rate Limiting

Microsoft Graph API limits:
- **Requests**: ~10,000 per app per tenant per second
- **Upload size**: Up to 250 GB per file
- **Throttling**: Exponential backoff recommended

For high-volume scenarios, implement retry with `Polly`.

---

## Future Enhancements

### 1. Metadata Tagging

Store call metadata in file properties:

```csharp
var driveItem = new DriveItem
{
    Name = fileName,
    AdditionalData = new Dictionary<string, object>
    {
        { "@microsoft.graph.conflictBehavior", "replace" },
        { "tags", new[] { "vonage", "recording", callId } }
    }
};
```

### 2. SharePoint Integration

Upload to shared team site instead of personal OneDrive:

```csharp
// Use site drive instead of user drive
var drive = await _graphClient.Sites["root"]
    .Drives["Documents"]
    .GetAsync();
```

### 3. Automatic Cleanup

Delete recordings older than 90 days:

```csharp
var cutoffDate = DateTime.UtcNow.AddDays(-90);
var oldFiles = await _graphClient.Me.Drive.Root
    .ItemWithPath($"{folderPath}")
    .Children
    .Request()
    .Filter($"createdDateTime lt {cutoffDate:yyyy-MM-ddTHH:mm:ssZ}")
    .GetAsync();
```

### 4. Shared Access Links

Generate temporary download links for recordings:

```csharp
var permission = await _graphClient.Me.Drive.Items[fileId]
    .CreateLink("view", "organization")
    .Request()
    .PostAsync();

return permission.Link.WebUrl; // Shareable link
```

---

## Quick Reference - Configuration Checklist

### Azure Portal Checklist

- [ ] App registered in Azure AD
- [ ] **Application (client) ID** copied → `ClientId`
- [ ] **Directory (tenant) ID** copied → `TenantId`
- [ ] **Files.ReadWrite** permission added as **Delegated** type (not Application)
- [ ] **Allow public client flows** enabled (Authentication > Advanced settings)
- [ ] (Optional) Admin consent granted

### Configuration Checklist

- [ ] `TenantId` set in appsettings.json or user secrets
- [ ] `ClientId` set in appsettings.json or user secrets
- [ ] `UploadFolderPath` set (e.g., `Vonage_Call_Recordings`)
- [ ] User secrets configured (local dev) OR App Settings configured (Azure)

### Authentication Checklist (First Time)

- [ ] Application started (`dotnet run` in tools/AppHost)
- [ ] Called `/connection-to-one-drive` endpoint
- [ ] Followed device code prompt in console
- [ ] Authenticated with Microsoft account
- [ ] Received success response with file ID
- [ ] Verified test file uploaded to OneDrive

---

## Additional Resources

- [Microsoft Graph Documentation](https://learn.microsoft.com/en-us/graph/)
- [OneDrive API Reference](https://learn.microsoft.com/en-us/onedrive/developer/)
- [Device Code Flow](https://learn.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-device-code)
- [Azure.Identity DeviceCodeCredential](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.devicecodecredential)
- [Microsoft Graph SDK for .NET](https://github.com/microsoftgraph/msgraph-sdk-dotnet)

---

## Support

For issues:
1. Check [Troubleshooting](#troubleshooting) section
2. Review [Configuration Checklist](#quick-reference---configuration-checklist)
3. Enable debug logging
4. Check Application Insights (if deployed)
5. Open GitHub issue with logs

---

**Last Updated**: 2025-10-22
**Authentication Method**: Device Code Flow (Delegated Permissions)
**Microsoft Graph SDK**: v5.70.0
**Azure.Identity**: v1.13.1
