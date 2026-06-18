# CMIForge

CMIForge is a C# / ASP.NET Core prototype for configurable legal intake, workflow approvals, operational entity management, and conflict searches.

Current version: `20260618.1`.

The product currently has two public-facing surfaces:

- Marketing site: `https://cmiforge.com`
- Public live demo: `https://demo.cmiforge.com`
- Customer 0 / real app doorway: `https://app.cmiforge.com`

Azure fallback hosts remain available for troubleshooting:

- Demo fallback: `https://cmiforge-dev-web-06161223.azurewebsites.net`
- Customer 0 fallback: `https://cmiforge-customer0-web.azurewebsites.net`

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
- Clients, matters, contacts, users, parties, aliases, and relationships
- Native conflicts search with deterministic AI-style explanations
- Floating live conflict preview on submission forms
- Workflow definitions, approval queues, outcomes, and answer-based routing
- Workflow notification steps with recipient/template configuration
- CSV import center for clients, matters, and parties
- Client photo OCR import drafting
- Private submission attachments
- Time recording, built-in operational reports, and a basic report builder
- Team, role, permission, and audit-log foundation
- Conversation-style notes on clients, matters, and parties
- Archive/unarchive support for clients, matters, parties, and users
- Admin-editable system settings for operational configuration
- Optional Microsoft Entra ID sign-in and first-pass Entra user provisioning
- Anonymous workspace request form for prospective/customer setup
- Admin signup-request triage and customer onboarding checklist
- Demo-mode scheduled/manual reset controls with reset run history
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
- Azure DNS hosts `cmiforge.com` so app/customer/demo subdomains can be provisioned from Azure instead of manually through the registrar
- `demo.cmiforge.com` points at the public demo App Service
- `app.cmiforge.com` points at the Customer 0 App Service
- Future Azure Function App and Static Web App split-architecture resources are provisioned, but the current production-worthy app host remains App Service

The live demo currently runs in demo mode with the seeded `Ima User` context. Microsoft Entra authentication is supported in the app, but demo mode keeps the public demo experience frictionless while the product is still being shaped.

Real tenants should run with `MatterForge:DemoMode=false`, `Authentication:Microsoft:Enabled=true`, and a configured `MatterForge:BootstrapAdminEmail`. Customer-managed Entra user creation also needs a verified Entra custom domain, `EntraProvisioning:Enabled=true`, and `EntraProvisioning:Domain=<verified-domain>`.

Public requests can be collected through `/Signup`. Admins can triage requests in `System -> Signup Requests` and work through `System -> Onboarding` after the customer workspace is provisioned.

## Current Cloud Surfaces

| Surface | URL | Purpose |
| --- | --- | --- |
| Marketing | `https://cmiforge.com` | Public product site, pricing, FAQ, request-access links |
| Demo | `https://demo.cmiforge.com` | Public sandbox with demo mode, guardrails, and scheduled/manual reset |
| Customer 0 | `https://app.cmiforge.com` | Gabe's real non-demo tenant with Entra login |
| Demo fallback | `https://cmiforge-dev-web-06161223.azurewebsites.net` | Direct App Service hostname for troubleshooting |
| Customer 0 fallback | `https://cmiforge-customer0-web.azurewebsites.net` | Direct App Service hostname for troubleshooting |

The near-term customer model is subdomain-per-tenant plus database-per-customer:

```text
cmiforge.com           public marketing site
demo.cmiforge.com      public disposable demo
app.cmiforge.com       Customer 0 / internal real tenant
firm.cmiforge.com      future customer tenant
```

DNS is now managed in Azure DNS while the domain registration remains at Namecheap.

## Email Status

`cmiforge.com` email DNS is currently configured in Azure DNS for Namecheap Private Email:

- `MX @ -> mx1.privateemail.com`
- `MX @ -> mx2.privateemail.com`
- `TXT @ -> v=spf1 include:spf.privateemail.com ~all`
- `mail`, `autodiscover`, and `autoconfig` CNAMEs point to `privateemail.com`

An Entra user identity exists for `gabe@cmiforge.com`, but it does not become a Microsoft-hosted mailbox until a Microsoft 365 Business Basic or Exchange Online license is purchased and assigned. If mail is moved from Namecheap Private Email to Microsoft 365, Azure DNS must be updated to the Microsoft 365 Exchange records shown in the Microsoft admin center.

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

- Demo app URL: `https://demo.cmiforge.com`
- Demo fallback URL: `https://cmiforge-dev-web-06161223.azurewebsites.net`
- Dev deployment notes: `deploy/dev/README.md`
- Dev app settings sample: `deploy/dev/appservice-settings.sample.json`
- Repo deployment workflow: `.github/workflows/cmiforge-dev-appservice.yml`

The dev App Service uses a system-assigned managed identity to reach Azure SQL with Entra-based authentication.

Submission attachments in the dev App Service use private Azure Blob Storage through managed identity. Downloads flow through the app instead of exposing public blob URLs.

## Current Build Path

1. Dynamic form definitions + submissions
2. Workflow steps + approval queues
3. Rules-based routing + workflow notifications
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
- Price: `$149/month`

Community and Enterprise tiers are visible in the app on `/Billing`, but payment handling is intentionally deferred.

## Customer 0 Volume Test Data

Customer 0 has been loaded with recognizable volume-test data for early performance testing:

- 400 clients named `Volume Test Client ###`
- 400 matters named `Volume Test Matter ###`
- 400 parties named `Volume Test Party ###`
- 10 active users total
- 1,000 volume-test submissions marked in submission JSON with `"volumeTest": true`
- 300 open volume workflow tasks for queue testing

The generated records are intentionally labeled so they can be filtered, measured, or removed later without confusing them with real customer data.
