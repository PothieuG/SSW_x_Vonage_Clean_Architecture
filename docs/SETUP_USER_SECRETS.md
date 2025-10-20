# Setting Up User Secrets for OneDrive Integration

User Secrets is a secure way to store sensitive configuration values during local development without committing them to Git.

## Quick Setup

### Step 1: Navigate to WebApi Project

```bash
cd src/WebApi
```

### Step 2: Initialize User Secrets (Already Done)

The `UserSecretsId` has already been added to `WebApi.csproj`, so you can skip the init step.

### Step 3: Add OneDrive Configuration

Run these commands to add your OneDrive settings:

```bash
# Azure AD Tenant ID (from Azure Portal > Azure AD > Overview > Tenant ID)
dotnet user-secrets set "OneDrive:TenantId" "your-tenant-id-here"

# Azure AD Application (Client) ID (from App registrations > Your App > Overview)
dotnet user-secrets set "OneDrive:ClientId" "your-client-id-here"

# Client Secret VALUE (from App registrations > Your App > Certificates & secrets > Value)
dotnet user-secrets set "OneDrive:ClientSecret" "your-secret-value-here"

# Your OneDrive account email (from Azure AD > Users > Your User > User principal name)
dotnet user-secrets set "OneDrive:UserId" "your-email@yourcompany.com"

# Folder name for recordings (you choose this)
dotnet user-secrets set "OneDrive:UploadFolderPath" "CallRecordings"
```

### Step 4: (Optional) Add Vonage Settings

If you haven't set up Vonage secrets yet:

```bash
dotnet user-secrets set "Vonage:ApplicationId" "your-vonage-app-id"
dotnet user-secrets set "Vonage:ApplicationKey" "your-vonage-private-key-content"
dotnet user-secrets set "Vonage:FromNumber" "your-vonage-phone-number"
dotnet user-secrets set "Vonage:WebhookBaseUrl" "https://your-public-domain.com"
```

### Step 5: Verify Your Secrets

```bash
dotnet user-secrets list
```

**Expected output:**
```
OneDrive:ClientId = xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
OneDrive:ClientSecret = abc~123XYZ...
OneDrive:TenantId = yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy
OneDrive:UploadFolderPath = CallRecordings
OneDrive:UserId = your-email@yourcompany.com
Vonage:ApplicationId = ...
Vonage:ApplicationKey = ...
Vonage:FromNumber = ...
Vonage:WebhookBaseUrl = ...
```

## Example with Real Values

Here's what the commands look like with example values:

```bash
cd src/WebApi

# OneDrive configuration
dotnet user-secrets set "OneDrive:TenantId" "12345678-1234-1234-1234-123456789abc"
dotnet user-secrets set "OneDrive:ClientId" "abcdefab-abcd-abcd-abcd-abcdefabcdef"
dotnet user-secrets set "OneDrive:ClientSecret" "abc123XYZ~def456ABC.ghi789DEF"
dotnet user-secrets set "OneDrive:UserId" "john.doe@yourcompany.com"
dotnet user-secrets set "OneDrive:UploadFolderPath" "CallRecordings"
```

## How User Secrets Work

### Storage Location

Your secrets are stored in:
- **Windows**: `%APPDATA%\Microsoft\UserSecrets\ssw-vonage-clean-architecture-webapi\secrets.json`
- **macOS/Linux**: `~/.microsoft/usersecrets/ssw-vonage-clean-architecture-webapi/secrets.json`

This file is **NOT** in your project directory and **NOT** committed to Git.

### How It Overrides appsettings.json

When you run the app in Development:

1. `appsettings.json` is loaded first
2. User Secrets override any matching keys
3. Environment variables can override both

**Priority (lowest to highest):**
```
appsettings.json < User Secrets < Environment Variables
```

### View the Secrets File Directly

**Windows PowerShell:**
```powershell
notepad $env:APPDATA\Microsoft\UserSecrets\ssw-vonage-clean-architecture-webapi\secrets.json
```

**macOS/Linux:**
```bash
cat ~/.microsoft/usersecrets/ssw-vonage-clean-architecture-webapi/secrets.json
```

**Expected contents:**
```json
{
  "OneDrive:TenantId": "12345678-1234-1234-1234-123456789abc",
  "OneDrive:ClientId": "abcdefab-abcd-abcd-abcd-abcdefabcdef",
  "OneDrive:ClientSecret": "abc123XYZ~def456ABC.ghi789DEF",
  "OneDrive:UserId": "john.doe@yourcompany.com",
  "OneDrive:UploadFolderPath": "CallRecordings"
}
```

## Removing Secrets

### Remove a single secret:
```bash
dotnet user-secrets remove "OneDrive:ClientSecret"
```

### Remove all secrets:
```bash
dotnet user-secrets clear
```

## Troubleshooting

### Error: "Could not find UserSecretsId"

**Solution:** The `UserSecretsId` property has already been added to `WebApi.csproj`. Make sure you're in the correct directory:

```bash
cd src/WebApi
pwd  # Should show: .../SSW_x_Vonage_Clean_Architecture/src/WebApi
```

### Secrets not being loaded

**Possible causes:**

1. **Wrong environment** - User Secrets only work in `Development` environment

   **Check:**
   ```bash
   # Windows PowerShell
   $env:ASPNETCORE_ENVIRONMENT

   # macOS/Linux
   echo $ASPNETCORE_ENVIRONMENT
   ```

   **Should be:** `Development` (or not set)

2. **Wrong directory** - Make sure you're in `src/WebApi` when running commands

3. **Typo in key names** - Use `dotnet user-secrets list` to verify exact key names

### How to verify secrets are loaded at runtime

Add this temporary code to `Program.cs` (remove after testing):

```csharp
var oneDriveConfig = builder.Configuration.GetSection("OneDrive");
Console.WriteLine($"OneDrive:TenantId = {oneDriveConfig["TenantId"]}");
Console.WriteLine($"OneDrive:ClientId = {oneDriveConfig["ClientId"]}");
Console.WriteLine($"OneDrive:ClientSecret = {(string.IsNullOrEmpty(oneDriveConfig["ClientSecret"]) ? "NOT SET" : "***SET***")}");
```

## Production Deployment

**Important:** User Secrets are **ONLY for local development**!

For production, use:
- **Azure App Service**: Application Settings or Key Vault references
- **Docker**: Environment variables or Docker secrets
- **Other**: Azure Key Vault, AWS Secrets Manager, etc.

See [ONEDRIVE_INTEGRATION.md](ONEDRIVE_INTEGRATION.md) for production configuration details.

## Security Best Practices

✅ **DO:**
- Use User Secrets for local development
- Keep secrets.json file secure
- Rotate secrets regularly (every 6-12 months)

❌ **DON'T:**
- Commit secrets to Git
- Share secrets via email/Slack/Teams
- Use the same secrets across environments
- Use User Secrets in production

## Quick Reference

| Command | Purpose |
|---------|---------|
| `dotnet user-secrets set "Key" "Value"` | Add/update a secret |
| `dotnet user-secrets list` | List all secrets |
| `dotnet user-secrets remove "Key"` | Remove a secret |
| `dotnet user-secrets clear` | Remove all secrets |
| `dotnet user-secrets init` | Initialize (already done) |

## Next Steps

After setting up User Secrets:

1. ✅ Verify secrets are set: `dotnet user-secrets list`
2. ✅ Check [ONEDRIVE_INTEGRATION.md](ONEDRIVE_INTEGRATION.md) for Azure AD setup
3. ✅ Run the application: `cd ../.. && cd tools/AppHost && dotnet run`
4. ✅ Test the OneDrive integration by uploading a test recording

---

For more details, see:
- [OneDrive Integration Guide](ONEDRIVE_INTEGRATION.md)
- [Microsoft Docs: Safe storage of app secrets in development](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
