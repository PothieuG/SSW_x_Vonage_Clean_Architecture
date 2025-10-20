# OneDrive Integration for Recording Upload

This guide walks you through configuring Microsoft Graph API integration to upload Vonage call recordings to your personal OneDrive for Business account.

## Table of Contents

1. [Overview](#overview)
2. [Azure AD App Registration](#azure-ad-app-registration)
3. [Configuration](#configuration)
4. [Architecture](#architecture)
5. [Usage](#usage)
6. [Troubleshooting](#troubleshooting)
7. [Security Best Practices](#security-best-practices)

---

## Overview

The OneDrive integration allows your application to automatically upload call recordings from Vonage to a specific folder in your OneDrive for Business account.

### Key Features

- Automatic upload of recording files to OneDrive
- Support for large files (using chunked upload)
- Progress tracking during upload
- Organized folder structure (e.g., `/CallRecordings/YYYY-MM-DD/`)
- Secure authentication via Azure AD

### Authentication Method

This implementation uses **Client Credentials Flow** (OAuth 2.0) with Application Permissions:
- Best for background services/daemons
- No user interaction required
- Uses Client ID + Client Secret
- Requires admin consent

---

## Azure AD App Registration

Follow these steps to register your application in Azure AD and obtain the necessary credentials.

### Step 1: Access Azure Portal

1. Go to [Azure Portal](https://portal.azure.com/)
2. Sign in with your Microsoft 365 account (must have admin permissions)
3. Navigate to **Azure Active Directory** (or search for "Azure AD" in the top search bar)

### Step 2: Register a New Application

1. In the left sidebar, click **App registrations**
2. Click **+ New registration** at the top
3. Fill in the registration form:

   | Field | Value |
   |-------|-------|
   | **Name** | `SSW Vonage Recording Uploader` (or any descriptive name) |
   | **Supported account types** | **Accounts in this organizational directory only** (Single tenant) |
   | **Redirect URI** | Leave blank (not needed for daemon apps) |

4. Click **Register**

### Step 3: Note Your Application IDs

After registration, you'll see the **Overview** page. Copy these values (you'll need them later):

- **Application (client) ID**: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` → This becomes your **`ClientId`**
- **Directory (tenant) ID**: `yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy` → This becomes your **`TenantId`**

> **Azure Portal Path**: Azure Active Directory > App registrations > *Your App* > Overview

### Step 4: Create a Client Secret

1. In the left sidebar, click **Certificates & secrets**
2. Under **Client secrets**, click **+ New client secret**
3. Fill in:
   - **Description**: `OneDrive Upload Secret`
   - **Expires**: Choose based on your security policy (recommended: 6 months or 12 months)
4. Click **Add**
5. **CRITICAL**: Copy the **Value** immediately (it will only be shown once!) → This becomes your **`ClientSecret`**
   - Example: `abc123XYZ~def456ABC.ghi789DEF`
   - Store this securely (you'll add it to `appsettings.json` later)

> **Azure Portal Path**: Azure Active Directory > App registrations > *Your App* > Certificates & secrets
>
> ⚠️ **IMPORTANT**: Copy the **Value** column, NOT the **Secret ID**!
>
> | What you see | What to use |
> |--------------|-------------|
> | Secret ID: `12345678-abcd-...` | ❌ **DON'T use this** |
> | Value: `abc~XYZ123...` | ✅ **USE THIS for `ClientSecret`** |
>
> If you navigate away, you'll need to create a new secret!

### Step 5: Configure API Permissions

1. In the left sidebar, click **API permissions**
2. Click **+ Add a permission**
3. Select **Microsoft Graph**
4. Select **Application permissions** (not Delegated permissions)
5. Search for and select these permissions:

   | Permission | Type | Reason |
   |------------|------|--------|
   | **Files.ReadWrite.All** | Application | Allows app to read and write files in all site collections |

6. Click **Add permissions**
7. Click **Grant admin consent for [Your Organization]** (requires admin rights)
8. Click **Yes** to confirm

You should see a green checkmark under **Status** for all permissions.

#### Permission Options Comparison

You have several permission options for OneDrive access. Choose based on your security and usability requirements:

| Permission | Type | Scope | Best For | Pros | Cons |
|------------|------|-------|----------|------|------|
| **Files.ReadWrite.AppFolder** ✅ | Application | `/Apps/{AppName}/` folder only | **Recommended for production** | ✅ **Least privilege**<br>✅ Works with daemon apps<br>✅ Auto-sandboxed<br>✅ No PowerShell setup | ⚠️ Folder in `/Apps/` path<br>⚠️ Users need to navigate to Apps folder |
| **Files.ReadWrite.All** | Application | All OneDrive accounts | Development/Testing | ✅ Easy setup<br>✅ Files in root OneDrive<br>✅ Easy for users to find | ❌ Too broad access<br>❌ Not least privilege |
| **Sites.Selected** ✅ | Application | Specific OneDrive(s) | Production (if root folder needed) | ✅ Granular control<br>✅ Files in root OneDrive | ⚙️ Requires PowerShell setup |

#### Option 1: `Files.ReadWrite.AppFolder` (Recommended - Least Privilege)

**Best choice for production!** This permission creates a sandboxed folder that only your app can access.

**What is AppFolder?**
- Special folder path: `/Apps/{Your App Name}/` (e.g., `/Apps/SSW Vonage Recording Uploader/`)
- **Automatically sandboxed** - your app can ONLY access this one folder
- Works with **Application** permissions (no user interaction needed)
- Follows the principle of least privilege

**To use AppFolder:**

1. **In Step 5 above**, select **`Files.ReadWrite.AppFolder`** instead of `Files.ReadWrite.All`
2. **No code changes needed!** The current implementation works with both permissions
3. Files will be uploaded to: `/Apps/SSW Vonage Recording Uploader/CallRecordings/2025-01-17/recording.mp3`

**How users access their recordings:**
```
OneDrive Web UI:
→ Click "My files"
→ Scroll down to "Apps" section
→ Click "SSW Vonage Recording Uploader"
→ Navigate to "CallRecordings"
```

Or share a direct link like:
```
https://yourcompany-my.sharepoint.com/personal/user_yourcompany_com/_layouts/15/onedrive.aspx?id=%2Fpersonal%2Fuser%5Fyourcompany%5Fcom%2FDocuments%2FApps%2FSSW%20Vonage%20Recording%20Uploader
```

**Folder structure with AppFolder:**
```
OneDrive/
└── Apps/
    └── SSW Vonage Recording Uploader/  (automatically created, app-only access)
        └── CallRecordings/
            ├── 2025-01-17/
            │   ├── recording_20250117_093022.mp3
            │   └── recording_20250117_143512.mp3
            └── 2025-01-18/
                └── recording_20250118_101520.mp3
```

#### Option 2: `Files.ReadWrite.All` (Easier for users, broader permissions)

Use this if you want recordings in the root OneDrive (easier for users to find), but be aware it grants broader access.

**Folder structure with Files.ReadWrite.All:**
```
OneDrive/
└── CallRecordings/  (in root - easy to find)
    ├── 2025-01-17/
    │   └── recording_20250117_093022.mp3
    └── 2025-01-18/
        └── recording_20250118_101520.mp3
```

**To use Files.ReadWrite.All:**
- Already configured in Step 5 above
- No additional changes needed

#### Option 3: `Sites.Selected` (Advanced - Granular Control)

Use **Sites.Selected** if you need root folder access AND want to restrict to specific OneDrive accounts (see Step 6 below).

---

**Our Recommendation:** Use **`Files.ReadWrite.AppFolder`** for the best security posture!

### Step 6: (Optional) Restrict Access to Specific OneDrive

By default, `Files.ReadWrite.All` grants access to all OneDrive accounts. To restrict to a specific user's OneDrive:

1. Use **Sites.Selected** permission instead
2. Grant explicit access using Microsoft Graph PowerShell:

```powershell
# Install Microsoft Graph PowerShell (if not already installed)
Install-Module Microsoft.Graph -Scope CurrentUser

# Connect with admin account
Connect-MgGraph -Scopes "Sites.FullControl.All"

# Get the site ID for the user's OneDrive
$userId = "your-email@yourcompany.com"
$site = Get-MgUserDefaultDrive -UserId $userId

# Grant the app access to this specific OneDrive
# (Replace {appId} with your Application (client) ID)
$appId = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
$params = @{
    roles = @("write")
    grantedToIdentities = @(
        @{
            application = @{
                id = $appId
                displayName = "SSW Vonage Recording Uploader"
            }
        }
    )
}
New-MgSitePermission -SiteId $site.Id -BodyParameter $params
```

---

## Configuration

### Step 1: Add NuGet Packages

The following packages are added to the **Infrastructure** project:

```xml
<PackageReference Include="Microsoft.Graph" Version="5.70.0" />
<PackageReference Include="Azure.Identity" Version="1.13.1" />
```

These are already configured in the project via:

```bash
cd src/Infrastructure
dotnet add package Microsoft.Graph
dotnet add package Azure.Identity
```

### Step 2: Configure appsettings.json

Add the OneDrive configuration section to [src/WebApi/appsettings.json](../src/WebApi/appsettings.json):

```json
{
  "OneDrive": {
    "TenantId": "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy",
    "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ClientSecret": "abc123XYZ~def456ABC.ghi789DEF",
    "UploadFolderPath": "CallRecordings",
    "UserId": "your-email@yourcompany.com"
  }
}
```

#### Configuration Values - Azure Portal Mapping

| Setting | Where to find it in Azure Portal | Example | Format |
|---------|----------------------------------|---------|--------|
| `TenantId` | **Azure AD** > **Overview** > **Tenant ID** | `12345678-1234-...` | GUID (always) |
| `ClientId` | **App registrations** > *Your App* > **Overview** > **Application (client) ID** | `abcdefab-abcd-...` | GUID (always) |
| `ClientSecret` | **App registrations** > *Your App* > **Certificates & secrets** > *Your Secret* > **Value** | `xyz~ABC123...` | String with special chars (`~`, `.`) |
| `UserId` | **Azure AD** > **Users** > *Your User* > **User principal name** | `john@company.com` | Email address |
| `UploadFolderPath` | *Not in Azure* (you choose this) | `CallRecordings` | Simple folder name |

> **Common Mistakes:**
> - ❌ Using the **Secret ID** instead of **Value** for `ClientSecret`
> - ❌ Using the domain name (e.g., `company.onmicrosoft.com`) for `TenantId` - must be a GUID
> - ❌ Using **Display Name** instead of **User principal name** for `UserId`

### Step 3: User Secrets for Development (Recommended)

For local development, use **User Secrets** instead of storing secrets in `appsettings.json`:

```bash
cd src/WebApi

# Initialize user secrets
dotnet user-secrets init

# Set OneDrive configuration
dotnet user-secrets set "OneDrive:TenantId" "your-tenant-id"
dotnet user-secrets set "OneDrive:ClientId" "your-client-id"
dotnet user-secrets set "OneDrive:ClientSecret" "your-client-secret"
dotnet user-secrets set "OneDrive:UserId" "your-email@company.com"
dotnet user-secrets set "OneDrive:UploadFolderPath" "CallRecordings"
```

### Step 4: Production Configuration

For production deployments:

**Option A: Azure Key Vault** (Recommended)
```json
{
  "OneDrive": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "ClientSecret": "", // Leave empty, use Key Vault
    "UploadFolderPath": "CallRecordings",
    "UserId": "service-account@company.com"
  }
}
```

Configure Key Vault reference in Azure App Service:
- `@Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/OneDriveClientSecret/)`

**Option B: Environment Variables**
```bash
export OneDrive__ClientSecret="your-secret-here"
```

---

## Architecture

The OneDrive integration follows the same Clean Architecture pattern as the Vonage integration.

### File Structure

```
src/
├── Application/
│   ├── Common/Interfaces/
│   │   └── IOneDriveService.cs              # Service abstraction
│   └── UseCases/
│       └── Recordings/
│           └── Commands/
│               └── HandleRecording/
│                   ├── HandleRecordingCommand.cs
│                   └── HandleRecordingCommandHandler.cs
│
├── Infrastructure/
│   ├── OneDrive/
│   │   ├── OneDriveService.cs               # Microsoft Graph implementation
│   │   └── OneDriveSettings.cs              # Configuration model
│   └── DependencyInjection.cs               # Service registration
│
└── WebApi/
    ├── appsettings.json                     # Configuration
    └── Endpoints/
        └── RecordingEndpoints.cs            # Callback endpoint
```

### IOneDriveService Interface

Located at [src/Application/Common/Interfaces/IOneDriveService.cs](../src/Application/Common/Interfaces/IOneDriveService.cs):

```csharp
public interface IOneDriveService
{
    /// <summary>
    /// Uploads a file to OneDrive from a URL
    /// </summary>
    /// <param name="fileUrl">URL of the file to download and upload</param>
    /// <param name="fileName">Desired file name in OneDrive</param>
    /// <param name="folderPath">Optional subfolder path (e.g., "2025-01-15")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OneDrive file ID on success</returns>
    Task<ErrorOr<string>> UploadFileFromUrlAsync(
        string fileUrl,
        string fileName,
        string? folderPath = null,
        CancellationToken cancellationToken = default);
}
```

### OneDriveService Implementation

Located at [src/Infrastructure/OneDrive/OneDriveService.cs](../src/Infrastructure/OneDrive/OneDriveService.cs):

Key features:
- Uses `ClientSecretCredential` for authentication
- Creates folder structure automatically
- Supports large file uploads (chunked upload for files > 4MB)
- Progress tracking during upload
- Proper error handling with `ErrorOr<T>` pattern

---

## Usage

### Example: Upload Recording in HandleRecordingCommandHandler

```csharp
public async Task<ErrorOr<Success>> Handle(
    HandleRecordingCommand request,
    CancellationToken cancellationToken)
{
    // 1. Get recording URL from Vonage
    var recordingUrl = request.RecordingUrl;

    // 2. Generate file name
    var fileName = $"recording_{DateTime.UtcNow:yyyyMMdd_HHmmss}.mp3";

    // 3. Create dated subfolder
    var folderPath = DateTime.UtcNow.ToString("yyyy-MM-dd");

    // 4. Upload to OneDrive
    var uploadResult = await _oneDriveService.UploadFileFromUrlAsync(
        recordingUrl,
        fileName,
        folderPath,
        cancellationToken);

    if (uploadResult.IsError)
    {
        _logger.LogError("Failed to upload recording to OneDrive: {Error}",
            uploadResult.FirstError.Description);
        return uploadResult.FirstError;
    }

    _logger.LogInformation("Recording uploaded to OneDrive with ID: {FileId}",
        uploadResult.Value);

    return Result.Success;
}
```

### Folder Structure in OneDrive

```
OneDrive/
└── CallRecordings/               (root folder from config)
    ├── 2025-01-15/               (dated subfolders)
    │   ├── recording_20250115_093022.mp3
    │   └── recording_20250115_143512.mp3
    └── 2025-01-16/
        └── recording_20250116_101520.mp3
```

---

## Troubleshooting

### Common Errors

#### 1. `Authorization_RequestDenied`

**Error Message**:
```
Insufficient privileges to complete the operation.
```

**Solutions**:
- Verify you granted **Admin Consent** in Azure AD (Step 5, item 7)
- Ensure you selected **Application permissions** (not Delegated)
- Wait 5-10 minutes for permissions to propagate

#### 2. `InvalidAuthenticationToken`

**Error Message**:
```
Access token validation failure. Invalid audience.
```

**Solutions**:
- Verify `TenantId` and `ClientId` are correct
- Check that `ClientSecret` is copied correctly (no extra spaces)
- Ensure the secret hasn't expired (check Azure AD > Certificates & secrets)

#### 3. `ResourceNotFound` (User's OneDrive)

**Error Message**:
```
The user's OneDrive is not available.
```

**Solutions**:
- Ensure the user has logged into OneDrive for Business at least once
- Verify `UserId` email is correct
- Check the user has a valid Microsoft 365 license

#### 4. `itemNotFound` (Folder Path)

**Error Message**:
```
The resource could not be found.
```

**Solutions**:
- The service automatically creates folders, but ensure the root path is valid
- Check OneDrive storage quota isn't exceeded

### Testing the Integration

#### Test 1: Verify Authentication

```bash
# This will attempt to authenticate and list the user's drive
curl -X GET "https://localhost:7255/api/recordings/test-auth"
```

#### Test 2: Upload a Test File

```bash
curl -X POST "https://localhost:7255/api/recordings/test-upload" \
  -H "Content-Type: application/json" \
  -d '{
    "fileUrl": "https://example.com/sample.mp3",
    "fileName": "test-recording.mp3"
  }'
```

### Logging

Enable detailed logging in `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Graph": "Debug",
      "SSW_x_Vonage_Clean_Architecture.Infrastructure.OneDrive": "Debug"
    }
  }
}
```

---

## Security Best Practices

### 1. Secret Management

| Environment | Recommended Method |
|-------------|-------------------|
| **Local Development** | User Secrets (`dotnet user-secrets`) |
| **CI/CD** | Environment variables with secret masking |
| **Azure App Service** | Azure Key Vault integration |
| **Docker** | Docker secrets or environment variables |

### 2. Least Privilege Principle

- Use **Sites.Selected** instead of `Files.ReadWrite.All` for production
- Create a dedicated service account for OneDrive uploads
- Rotate client secrets every 6-12 months

### 3. Client Secret Rotation

To rotate your client secret:

1. In Azure AD > App registrations > Your App > Certificates & secrets
2. Click **+ New client secret** (create new secret)
3. Update your configuration with the new secret
4. Deploy the updated configuration
5. Delete the old secret after verifying the new one works

### 4. Monitoring

Track these metrics:
- Upload success/failure rates
- Authentication failures
- API rate limiting (Microsoft Graph has throttling limits)

### 5. Rate Limiting

Microsoft Graph API limits:
- **Requests per second**: ~10,000 per app per tenant
- **Upload limits**: Files up to 250 GB

For high-volume scenarios, implement retry logic with exponential backoff.

---

## Cost Considerations

### Microsoft 365 Licensing

- OneDrive for Business is included in most Microsoft 365 plans
- Storage limits vary by plan (typically 1TB+ per user)

### Microsoft Graph API

- **Pricing**: Free (included with Microsoft 365)
- **Quotas**: Subject to throttling limits (see above)

### Azure AD

- **Basic features**: Free (app registration, authentication)
- **Premium features**: Not required for this integration

---

## Future Enhancements

1. **Metadata Tagging**: Add custom properties to uploaded files
   ```csharp
   // Store call metadata in file properties
   var properties = new Dictionary<string, object>
   {
       { "CallId", callId },
       { "Duration", duration },
       { "PhoneNumber", phoneNumber }
   };
   ```

2. **SharePoint Integration**: Upload to shared team libraries instead of OneDrive

3. **Automatic Transcription**: Use Azure Cognitive Services to transcribe recordings

4. **Retention Policies**: Automatically delete recordings older than X days

5. **Webhook Notifications**: Subscribe to OneDrive changes via Microsoft Graph webhooks

---

## Quick Reference - Configuration Checklist

Before running your application, use this checklist to verify your configuration:

### Azure Portal Checklist

- [ ] App registered in Azure AD
- [ ] **Application (client) ID** copied (this is your `ClientId`)
- [ ] **Directory (tenant) ID** copied (this is your `TenantId`)
- [ ] Client secret created and **Value** copied (this is your `ClientSecret` - NOT the Secret ID!)
- [ ] **Files.ReadWrite.All** permission added as **Application** permission (not Delegated)
- [ ] **Admin consent granted** for the permission (green checkmark visible)
- [ ] **User principal name** noted for the target OneDrive owner (this is your `UserId`)

### Configuration Checklist

- [ ] `TenantId` is a GUID format: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`
- [ ] `ClientId` is a GUID format: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`
- [ ] `ClientSecret` contains special characters like `~` or `.` (NOT a GUID)
- [ ] `UserId` is an email address (contains `@`)
- [ ] `UploadFolderPath` is set (e.g., `CallRecordings`)
- [ ] User secrets configured (for local dev) OR Key Vault configured (for production)

### Verification Test

```bash
cd src/WebApi
dotnet user-secrets list

# You should see:
# OneDrive:TenantId = xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
# OneDrive:ClientId = yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy
# OneDrive:ClientSecret = abc~123XYZ...
# OneDrive:UserId = your-email@company.com
# OneDrive:UploadFolderPath = CallRecordings
```

---

## Additional Resources

- [Microsoft Graph Documentation](https://learn.microsoft.com/en-us/graph/)
- [OneDrive API Reference](https://learn.microsoft.com/en-us/onedrive/developer/)
- [Azure AD App Registration Guide](https://learn.microsoft.com/en-us/azure/active-directory/develop/quickstart-register-app)
- [Microsoft Graph SDK for .NET](https://github.com/microsoftgraph/msgraph-sdk-dotnet)
- [Client Credentials Flow](https://learn.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-client-creds-grant-flow)

---

## Support

For issues or questions:
- Check [Troubleshooting](#troubleshooting) section
- Review Azure AD app registration settings
- Verify all values in the [Configuration Checklist](#quick-reference---configuration-checklist)
- Check Application Insights logs (if deployed to Azure)
- Open a GitHub issue with error details
