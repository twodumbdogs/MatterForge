# CMIForge

CMIForge is a C# / ASP.NET Core prototype for configurable legal intake, workflow approvals, operational entity management, and conflict searches.

Current version: `20260616.1`.

The product currently has two public-facing surfaces:

- Marketing site: `https://cmiforge.com`
- Live dev app / demo: `https://cmiforge-dev-web-06161223.azurewebsites.net`

The repo also now includes a Customer 0 deployment slice for a real, non-demo tenant with Entra login:

- Customer 0 notes: `deploy/customer0/README.md`
- Entra setup checklist: `deploy/customer0/ENTRA_CHECKLIST.md`
- Customer 0 app settings sample: `deploy/customer0/appservice-settings.sample.json`

## Current Scope

- Dynamic form definitions
- Versioned JSON form schemas
- Runtime form rendering
- Form submissions stored against the exact published form version
- Submission queue and detail view
- Clients, matters, users, parties, aliases, and relationships
- Native conflicts search with deterministic AI-style explanations
- Workflow definitions, approval queues, outcomes, and answer-based routing
- CSV import center for clients, matters, and parties
- Private submission attachments
- Time recording and operational reports
- Team, role, permission, and audit-log foundation
- Admin-editable system settings for operational configuration
- Optional Microsoft Entra ID sign-in and first-pass Entra user provisioning
- Hardcoded Community/Professional/Enterprise plan limiter
- Azure SQL-ready EF Core model and migration

## Current Architecture

CMIForge is still intentionally simple: a server-rendered ASP.NET Core Razor Pages app backed by Azure SQL. That keeps the product easy to inspect and evolve while the domain model is still moving quickly.

Current dev architecture:

- ASP.NET Core Razor Pages app on Azure App Service
- Azure SQL database for operational data, workflow state, submissions, conflicts, imports, audit logs, reports, and time entries
- Azure Blob Storage for private submission attachment files
- Managed identity from App Service to Azure SQL
- Managed identity from App Service to Blob Storage in the cloud dev environment
- Static Web App for the public marketing site at `cmiforge.com`
- Future Azure Function App and Static Web App split-architecture resources are provisioned, but the current production-worthy app host remains App Service

The live demo currently runs in demo mode with the seeded `Ima User` context. Microsoft Entra authentication is supported in the app, but demo mode keeps the public dev experience frictionless while the product is still being shaped.

Real tenants should run with `MatterForge:DemoMode=false`, `Authentication:Microsoft:Enabled=true`, and a configured `MatterForge:BootstrapAdminEmail`. Customer-managed Entra user creation also needs a verified Entra custom domain, `EntraProvisioning:Enabled=true`, and `EntraProvisioning:Domain=<verified-domain>`.

## Run Locally

```powershell
dotnet restore
dotnet run --urls http://localhost:5153
```

Open:

```text
http://localhost:5153
```

If `ConnectionStrings:DefaultConnection` still contains the placeholder `YOUR_SERVER`, the app uses an in-memory development database and seeds a sample New Matter Intake form.

## Configure Azure SQL

For local development, prefer user secrets so the real connection string is not committed:

```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
```

Then apply the schema:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update
```

In Azure App Service, the dev app uses a managed-identity Azure SQL connection string rather than a password-based SQL login.

## Dev Cloud Environment

CMIForge now has a tracked dev App Service environment in Azure:

- Dev app URL: `https://cmiforge-dev-web-06161223.azurewebsites.net`
- Dev deployment notes: `deploy/dev/README.md`
- Dev app settings sample: `deploy/dev/appservice-settings.sample.json`
- Repo deployment workflow: `.github/workflows/cmiforge-dev-appservice.yml`

The dev App Service uses a system-assigned managed identity to reach Azure SQL with Entra-based authentication.

Submission attachments in the dev App Service use private Azure Blob Storage through managed identity. Downloads flow through the app instead of exposing public blob URLs.

## Current Build Path

1. Dynamic form definitions + submissions
2. Workflow steps + approval queues
3. Rules-based routing + notifications
4. Matter/client record creation
5. Reporting, permissions, integrations
6. Fancy admin designer UX

## Current Hardcoded Plan

The app currently runs as `Professional`:

- Includes 10 users
- 500 matters
- 500 clients
- All features
- Email support
- Additional users priced at `$10/user/month`

Community and Enterprise tiers are visible in the app on `/Billing`, but payment handling is intentionally deferred.
