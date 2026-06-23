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
- Azure SQL public demo database: `cmiforge-demo`
- Attachment storage account: `cmiforgeattachasgmt7`
- Attachment container: `submission-attachments`
- Shared VNet: `cmiforge-vnet`
- App Service integration subnet: `appsvc-integration`
- Private endpoint subnet: `private-endpoints`
- SQL private endpoint: `pe-cmiforge-sql`
- Blob private endpoint: `pe-cmiforge-blob`
- Key Vault private endpoint: `pe-cmiforge-keyvault`

## App Service Configuration

The live dev App Service currently expects:

- `ConnectionStrings__DefaultConnection`
- `SubmissionAttachments__ContainerName`
- `SubmissionAttachments__UseManagedIdentity`
- `SubmissionAttachments__AccountName`
- `ASPNETCORE_ENVIRONMENT`
- `Authentication__Microsoft__Enabled`
- `CMIForge__CurrentUserEmail`
- `CMIForge__CurrentUserDisplayName`
- `CMIForge__BootstrapAdminEmail`
- `CMIForge__DemoMode`
- `CMIForge__DemoResetEnabled`
- `CMIForge__DemoResetIntervalHours`
- `CMIForge__RunMigrationsOnStartup`
- `CMIForge__RunSeedDataOnStartup`
- `CMIForge__SeedSampleData`

The intended Azure SQL connection string for the public demo App Service is:

```text
Server=tcp:gwmatterforge.database.windows.net,1433;Initial Catalog=cmiforge-demo;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;
```

The App Service managed identity already has a database user in `cmiforge-demo` with:

- `db_datareader`
- `db_datawriter`

The same App Service managed identity has Blob access to the attachment storage account. The app should not need `SubmissionAttachments__ConnectionString` in the cloud dev environment.

The dev/demo app is VNet-integrated through `cmiforge-vnet/appsvc-integration`. Azure SQL and Blob Storage public network access are disabled, so cloud data access depends on the private endpoints and private DNS zones. Cloud database migrations should run through `deploy/migrations/run-tenant-migrations.ps1` and the dedicated `cmiforge-db-migrator` WebJob, not through normal web-app startup.

## Querying Azure SQL With Private Endpoints

SSMS is not mandatory. The SQL client can be SSMS, Azure Data Studio, `sqlcmd`, `Invoke-Sqlcmd`, or a small scripted Microsoft.Data.SqlClient tool. The gating issue is network reachability and permissions:

- A local workstation cannot reach the SQL private endpoint unless it is connected to the VNet path.
- Durable operator options include a small VM in `cmiforge-vnet` reached through Azure Bastion, a Point-to-Site VPN, or a future site-to-site/ExpressRoute path.
- Cloud migrations should continue to use the VNet-integrated migrator WebJob.
- For short local maintenance, repo scripts can use `-AllowTemporarySqlPublicAccess` to open a narrow firewall rule for the current public IP, perform the SQL action, then restore public access to disabled.

Use temporary public access only as an explicit maintenance exception, not as the normal app or migration path.

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

- `c#/CMIForge/CMIForge.csproj`

and deploys it to:

- `cmiforge-dev-web-06161223`

## GitHub Secret Needed

The workflow expects this repository secret:

- `AZURE_WEBAPP_PUBLISH_PROFILE_CMIFORGE_DEV`

Typical setup flow:

1. Download the publish profile from the Azure Portal for `cmiforge-dev-web-06161223`.
2. Add it as the GitHub repository secret `AZURE_WEBAPP_PUBLISH_PROFILE_CMIFORGE_DEV`.
3. Push changes to `main` that touch `c#/CMIForge/**`, or run the workflow manually.

## Manual Dev Deploy

From `c#/CMIForge`:

```powershell
.\deploy\dev\deploy-dev.ps1
```

That wrapper deploys to the fixed dev App Service resources and keeps the Azure SQL and Blob managed-identity settings consistent.

By default the wrapper also applies EF migrations to `cmiforge-demo` before publishing. Use `-SkipDatabaseUpdate` only when the code change does not require schema changes or when the database update is being handled separately.

If Azure SQL rejects the local migration connection because the current IP is not allowed, add a narrow temporary firewall rule for the current public IP, run the migration/deploy, then remove that rule immediately after verification.

## Standing Deploy Behavior

Gabe wants CMIForge deployed as work lands. For dev/demo app changes, the normal path is to build or otherwise verify locally, run this deploy wrapper, and smoke-check `https://demo.cmiforge.com`.

Use `-SkipDatabaseUpdate` for docs, UI, CSS, Razor-only, or service changes that do not need EF schema changes. Do not use that switch for model/migration work unless the database update has already been applied another way.

The dev deploy wrapper passes `cmiforge-vnet/appsvc-integration` to the shared `deployme.ps1` path so recreated or reconfigured demo apps stay on the private-networking path.

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

The live site currently includes the pain-focused intake messaging, Community/Professional/Enterprise pricing including the `$599/month` Enterprise SQL-access tier with 1,000 users, 5,000 clients, and 5,000 matters, `support@cmiforge.com`, and prominent links for request access, customer login, and the live demo.

## Current App Domains

Current subdomain map:

- `demo.cmiforge.com` -> `cmiforge-dev-web-06161223.azurewebsites.net`
- `app.cmiforge.com` -> `cmiforge-customer0-web.azurewebsites.net`
- `www.cmiforge.com` -> `happy-smoke-052d7f610.7.azurestaticapps.net`

Both `demo.cmiforge.com` and `app.cmiforge.com` are bound in Azure App Service with managed certificates. The public demo app was moved onto the existing `cmiforge-customer0-plan` (`B1`) to avoid paying for a second paid App Service plan just to support custom domains.

## Current App Feature Smoke

The current app build includes dashboard charts, role-oriented dashboard views, user profile font-size preferences, workflow notification step fields, workflow copy/draft and step move controls, form copy/versioning controls, secure external form invites with recent-send tracking, inbound email intake settings/logging, client photo OCR import drafting, floating live conflict previews, conflict-search progress feedback, configurable conflict preview settings, conflict filters directly above the results table, bulk conflict escalation, compact/collapsible row actions, searchable/sortable/compact lists, compact attributed entity notes, archive/unarchive behavior, enhancement requests, address autocomplete, time approval actions, demo reset catch-up, and signup legal-agreement acceptance. After manual deploys, smoke these in the demo app before relying on the public demo for walkthroughs.

Recent UI-only polish, including compact submission rows and brand/logo exports, can deploy with `-SkipDatabaseUpdate` after a successful build because no EF schema change is involved.

## Brand Assets

The app/public-site logo source is `wwwroot/img/cmiforge-logo.png`, generated from Gabe's supplied square CMIForge raster mark. Favicon, touch-icon, and social-preview sizes are kept alongside the app/public-site assets. High-resolution business/profile exports are kept under `artifacts/brand/`:

- `cmiforge-linkedin-logo-400.png`
- `cmiforge-linkedin-logo-1200.png`
- `cmiforge-linkedin-logo-2400.png`
- `cmiforge-logo-source.png`

## Notes

- The current production-worthy host for the existing CMIForge app is App Service, not Static Web Apps.
- The Static Web App and Function App are in place as future split-architecture scaffolding.
- The live demo remains App Service-hosted and uses `CMIForge__DemoMode=true` so the app resolves to `Ima User` without requiring visitors to sign in.
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
