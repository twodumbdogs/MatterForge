# CMIForge Dev Environment

This folder is the tracked dev deployment slice for CMIForge.

It captures the current Azure dev resources, the expected app settings shape, and the repo-based deployment path for the live dev app.

## Current Azure Resources

### Razor Pages app host

- App Service plan: `cmiforge-customer0-plan` (`B1`, shared with Customer 0 to keep custom-domain cost down)
- App Service app: `cmiforge-dev-web-06161223`
- Public demo URL: `https://demo.cmiforge.com`
- Azure fallback URL: `https://cmiforge-dev-web-06161223.azurewebsites.net`
- Authentication model to Azure SQL: system-assigned managed identity
- Attachment storage model: system-assigned managed identity to private Blob Storage
- Demo mode: enabled for the live public dev demo

### Future split-app API host

- Function App: `cmiforge-api-06161219`
- URL: `https://cmiforge-api-06161219.azurewebsites.net`
- Function storage account: `cmiforgefunc06161219`
- Managed identity: enabled

### Future split-app front end

- Static Web App: `cmiforge-web-06161219`
- URL: `https://happy-smoke-052d7f610.7.azurestaticapps.net`
- SKU: `Standard`
- Managed identity: enabled
- Linked backend: `cmiforge-api-06161219`

### Shared supporting services

- Shared paid App Service plan for custom-domain app hosts: `cmiforge-customer0-plan` (`B1`)
- Legacy/free dev App Service plan resource: `cmiforge-dev-plan` (`F1`)
- Azure SQL server: `gwmatterforge.database.windows.net`
- Azure SQL prototype/dev database: `matterforge-prototype`
- Azure SQL public demo database: `cmiforge-demo`
- Attachment storage account: `cmiforgeattachasgmt7`
- Attachment container: `submission-attachments`

## App Service Configuration

The live dev App Service currently expects:

- `ConnectionStrings__DefaultConnection`
- `SubmissionAttachments__ContainerName`
- `SubmissionAttachments__UseManagedIdentity`
- `SubmissionAttachments__AccountName`
- `ASPNETCORE_ENVIRONMENT`
- `Authentication__Microsoft__Enabled`
- `MatterForge__CurrentUserEmail`
- `MatterForge__CurrentUserDisplayName`
- `MatterForge__BootstrapAdminEmail`
- `MatterForge__DemoMode`
- `MatterForge__DemoResetEnabled`
- `MatterForge__DemoResetIntervalHours`
- `MatterForge__RunMigrationsOnStartup`
- `MatterForge__RunSeedDataOnStartup`
- `MatterForge__SeedSampleData`

The intended Azure SQL connection string for the public demo App Service is:

```text
Server=tcp:gwmatterforge.database.windows.net,1433;Initial Catalog=cmiforge-demo;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;
```

The App Service managed identity already has a database user in `cmiforge-demo` with:

- `db_datareader`
- `db_datawriter`

The same App Service managed identity has Blob access to the attachment storage account. The app should not need `SubmissionAttachments__ConnectionString` in the cloud dev environment.

Current attachment settings:

```text
SubmissionAttachments__ContainerName=submission-attachments
SubmissionAttachments__UseManagedIdentity=true
SubmissionAttachments__AccountName=cmiforgeattachasgmt7
```

## Repo Deployment Path

The GitHub Actions workflow for the live dev web app is:

- `.github/workflows/cmiforge-dev-appservice.yml`
- `deploy/dev/deploy-dev.ps1`

That workflow publishes the ASP.NET Core app from:

- `c#/MatterForge/MatterForge.csproj`

and deploys it to:

- `cmiforge-dev-web-06161223`

## GitHub Secret Needed

The workflow expects this repository secret:

- `AZURE_WEBAPP_PUBLISH_PROFILE_CMIFORGE_DEV`

Typical setup flow:

1. Download the publish profile from the Azure Portal for `cmiforge-dev-web-06161223`.
2. Add it as the GitHub repository secret `AZURE_WEBAPP_PUBLISH_PROFILE_CMIFORGE_DEV`.
3. Push changes to `main` that touch `c#/MatterForge/**`, or run the workflow manually.

## Manual Dev Deploy

From `c#/MatterForge`:

```powershell
.\deploy\dev\deploy-dev.ps1
```

That wrapper deploys to the fixed dev App Service resources and keeps the Azure SQL and Blob managed-identity settings consistent.

## Public Site

The tracked public website lives in:

- `sites/cmiforge-web`

The GitHub Actions workflow for the Static Web App is:

- `.github/workflows/cmiforge-public-site.yml`

The custom domain `cmiforge.com` is already bound to:

- `cmiforge-web-06161219`

Azure DNS now hosts the `cmiforge.com` zone. Namecheap remains the registrar, but DNS records are managed in Azure.

The workflow expects this repository secret:

- `AZURE_STATIC_WEB_APPS_API_TOKEN_CMIFORGE_WEB`

That value can be retrieved with:

```powershell
az staticwebapp secrets list --name cmiforge-web-06161219 --resource-group gw-rg
```

The public site can also be deployed directly with the Static Web Apps CLI:

```powershell
$token = (az staticwebapp secrets list --name cmiforge-web-06161219 --resource-group gw-rg | ConvertFrom-Json).properties.apiKey
npx @azure/static-web-apps-cli deploy "D:\00-mega\dad\scripts\codex\sites\cmiforge-web" --deployment-token $token --env production
```

The most recent public-site deploy was verified at:

- `https://cmiforge.com`
- `https://happy-smoke-052d7f610.7.azurestaticapps.net`

The live site currently includes the pain-focused intake messaging, Community/Professional/Enterprise pricing, `support@cmiforge.com`, and prominent links for request access, customer login, and the live demo.

## Current App Domains

Current subdomain map:

- `demo.cmiforge.com` -> `cmiforge-dev-web-06161223.azurewebsites.net`
- `app.cmiforge.com` -> `cmiforge-customer0-web.azurewebsites.net`
- `www.cmiforge.com` -> `happy-smoke-052d7f610.7.azurestaticapps.net`

Both `demo.cmiforge.com` and `app.cmiforge.com` are bound in Azure App Service with managed certificates. The public demo app was moved onto the existing `cmiforge-customer0-plan` (`B1`) to avoid paying for a second paid App Service plan just to support custom domains.

## Notes

- The current production-worthy host for the existing CMIForge app is App Service, not Static Web Apps.
- The Static Web App and Function App are in place as future split-architecture scaffolding.
- The live demo remains App Service-hosted and uses `MatterForge__DemoMode=true` so the app resolves to `Ima User` without requiring visitors to sign in.
- Public demo data is isolated in `cmiforge-demo`, resets every 12 hours while the App Service process is awake, and can be manually reset from `System -> Demo Mode`.
- Microsoft Entra login is supported by the app, but public demo mode intentionally bypasses the sign-in requirement for now.
- `az staticwebapp functions link` still fails in this environment with `API version 2020-12-01 does not have operation group 'static_sites'`.
- The working Azure CLI path was the newer backend-link flow:

```powershell
az staticwebapp backends link `
  --name cmiforge-web-06161219 `
  --resource-group gw-rg `
  --backend-resource-id "/subscriptions/3575a51c-2a8d-47df-91c9-e19d8e962fc5/resourceGroups/gw-rg/providers/Microsoft.Web/sites/cmiforge-api-06161219" `
  --backend-region "Central US"
```

- You can verify the link with:

```powershell
az staticwebapp backends show --name cmiforge-web-06161219 --resource-group gw-rg
az staticwebapp functions show --name cmiforge-web-06161219 --resource-group gw-rg
```

- The public demo app shares the existing `B1` App Service plan with Customer 0 for custom-domain support while keeping hosting cost controlled.
