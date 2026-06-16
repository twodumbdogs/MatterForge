# CMIForge

CMIForge is a C# / ASP.NET Core prototype for configurable legal intake, workflow approvals, operational entity management, and conflict searches.

Current version: `20260616.1`.

## Current Scope

- Dynamic form definitions
- Versioned JSON form schemas
- Runtime form rendering
- Form submissions stored against the exact published form version
- Submission queue and detail view
- Clients, matters, users, parties, aliases, and relationships
- Native conflicts search with deterministic AI-style explanations
- Hardcoded Free/Standard/Professional plan limiter
- Azure SQL-ready EF Core model and migration

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

In Azure App Service later, put the same `ConnectionStrings:DefaultConnection` value in App Service configuration or Key Vault-backed configuration.

## Dev Cloud Environment

CMIForge now has a tracked dev App Service environment in Azure:

- Dev app URL: `https://cmiforge-dev-web-06161223.azurewebsites.net`
- Dev deployment notes: `deploy/dev/README.md`
- Dev app settings sample: `deploy/dev/appservice-settings.sample.json`
- Repo deployment workflow: `.github/workflows/cmiforge-dev-appservice.yml`

The dev App Service uses a system-assigned managed identity to reach Azure SQL with Entra-based authentication.

## Current Build Path

1. Dynamic form definitions + submissions
2. Workflow steps + approval queues
3. Rules-based routing + notifications
4. Matter/client record creation
5. Reporting, permissions, integrations
6. Fancy admin designer UX

## Current Hardcoded Plan

The app currently runs as `Professional`:

- Unlimited users
- Unlimited matters
- Workflow
- Email notifications
- Audit trail
- Advanced workflow
- Reporting
- Azure AD / SSO

Free and Standard tiers are visible in the app on `/Billing`, but payment handling is intentionally deferred.
