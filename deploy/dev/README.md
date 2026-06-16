# CMIForge Dev Environment

This folder is the tracked dev deployment slice for CMIForge.

It captures the current Azure dev resources, the expected app settings shape, and the repo-based deployment path for the live dev app.

## Current Azure Resources

### Razor Pages app host

- App Service plan: `cmiforge-dev-plan`
- App Service app: `cmiforge-dev-web-06161223`
- URL: `https://cmiforge-dev-web-06161223.azurewebsites.net`
- Authentication model to Azure SQL: system-assigned managed identity

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

- Azure SQL server: `gwmatterforge.database.windows.net`
- Azure SQL database: `matterforge-prototype`
- Attachment storage account: `cmiforgeattachasgmt7`
- Attachment container: `submission-attachments`

## App Service Configuration

The live dev App Service currently expects:

- `ConnectionStrings__DefaultConnection`
- `SubmissionAttachments__ConnectionString`
- `SubmissionAttachments__ContainerName`
- `ASPNETCORE_ENVIRONMENT`

The intended Azure SQL connection string for the dev App Service is:

```text
Server=tcp:gwmatterforge.database.windows.net,1433;Initial Catalog=matterforge-prototype;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

The App Service managed identity already has a database user in `matterforge-prototype` with:

- `db_datareader`
- `db_datawriter`

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
.\deploy\dev\deploy-dev.ps1 -BlobConnectionString "<storage-connection-string>"
```

That wrapper deploys to the fixed dev App Service resources and keeps the Azure SQL managed identity connection string consistent.

## Public Site

The tracked public website lives in:

- `sites/cmiforge-web`

The GitHub Actions workflow for the Static Web App is:

- `.github/workflows/cmiforge-public-site.yml`

The custom domain `cmiforge.com` is already bound to:

- `cmiforge-web-06161219`

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

## Pending App Domain

`cmiforge.com` DNS is not hosted in this Azure subscription, so `app.cmiforge.com` needs to be created at the domain registrar or external DNS host.

Recommended DNS records:

- `CNAME` for `app` -> `cmiforge-dev-web-06161223.azurewebsites.net`
- `TXT` for `asuid.app` -> `94D4C4E8F6246343B35E1DF4D7CF95E0BEDE95EF28AFF1FFE5EEDB257E4CBBF4`

Important note:

- The current App Service plan is `F1` (Free).
- Azure App Service custom domain binding requires a paid plan tier such as `B1`.
- After the DNS records are live, use `deploy/dev/bind-app-domain.ps1` to upgrade the plan and add the hostname binding.

## Notes

- The current production-worthy host for the existing CMIForge app is App Service, not Static Web Apps.
- The Static Web App and Function App are in place as future split-architecture scaffolding.
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

- Free App Service was chosen for the dev web app to keep costs down. Custom domain binding for `cmiforge.com` will need a paid App Service tier later.
