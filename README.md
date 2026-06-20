# CMIForge

CMIForge is a C# / ASP.NET Core prototype for configurable legal intake, workflow approvals, operational entity management, and conflict searches.

Current version: `20260619.1`.

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
- Time recording, approval, locking, phase/task codes, timer capture, built-in operational reports, and a basic report builder
- Team, role, permission, and audit-log foundation
- Conversation-style notes on clients, matters, and parties
- Archive/unarchive support for clients, matters, parties, and users
- Admin-editable system settings for operational configuration
- Optional Microsoft Entra ID sign-in and first-pass Entra user provisioning
- Anonymous workspace request form for prospective/customer setup
- Mandatory signup acceptance of the current CMIForge SaaS Terms, Legal Use, and License Agreement
- Legal agreement acceptance audit table linked to each tenant provisioning request
- Admin signup-request triage and customer onboarding checklist
- Demo-mode scheduled/manual reset controls with reset run history
- Hardcoded Community/Professional/Enterprise plan limiter
- Azure SQL-ready EF Core model and migration

## 2026-06-19 Session Snapshot

This session moved CMIForge from "prototype with a lot of features" toward "early SaaS with real tenant and legal/commercial plumbing." The main shipped changes were:

- Signup now requires a checked legal agreement box before a workspace request can be submitted.
- `/System/Terms` now reads as a stronger SaaS terms, legal-use, and license agreement page rather than draft product notes.
- Agreement acceptance is stored in `LegalAgreementAcceptances` with customer, signer, agreement version, product version, timestamp, IP address, and user agent.
- `/System/Terms` is anonymous so prospective customers can open it from `/Signup` before they have an account.
- Dev and Customer 0 were deployed with migration `20260619232000_AddLegalAgreementAcceptances` applied.
- Dev signup smoke testing verified that submission without consent is blocked and submission with consent succeeds.

Other 2026-06-19 product work already present in the codebase and test plan includes dynamic firm branding, CMIForge 1.0 footer/version display, configurable live conflict preview, address autocomplete plumbing, searchable list boxes, sortable tables, conflict-result filters, row-level multi-select clearance, conflict archive/index storage, match-strength wording and scoring improvements, form-builder drag handles and field caps, enhancement requests under System, stronger archive/restore behavior for cancellations, and Customer 0 volume-test data for performance walkthroughs.

## 2026-06-19 Time And Rename Follow-up

This follow-up renamed the previous local app identity to CMIForge, including the project file, DbContext, user class, namespaces, config keys, and path references. Existing Azure SQL resource names that still contain `matterforge` were left alone because they are real cloud resource names, not app branding.

Time recording now has the v1 approval backbone:

- System default and matter-level time increments support actual minutes, 6-minute rounding, and 15-minute rounding.
- Matters can require time approval and can be assigned a lead partner, increment rule, and time code set.
- Time entries now use Draft, Submitted, and Approved statuses.
- Submitted time auto-approves when the matter does not require approval.
- Submitted time waits for lead-partner approval when the matter requires approval.
- Approved or exported entries are locked from normal editing.
- Time entries split client narrative from internal notes.
- Starter UTBMS-style phase/task code sets are seeded and selectable per matter.
- The Record Time screen includes a simple local start/stop timer that can fill elapsed time into a draft or submitted entry.
- Paginated list sorting now runs server-side before pagination so column sorts apply to the full result set.

Migration `20260620003102_AddTimeApprovalCodeSetsAndSorting` carries the database model changes for this slice.

## Current Architecture

CMIForge is still intentionally simple: a server-rendered ASP.NET Core Razor Pages app backed by Azure SQL. That keeps the product easy to inspect and evolve while the domain model is still moving quickly.

Current dev architecture:

- ASP.NET Core Razor Pages app on Azure App Service
- Azure SQL database for operational data, workflow state, submissions, conflicts, conflict archives, imports, audit logs, reports, time entries, tenant provisioning requests, and legal agreement acceptance records
- Azure Blob Storage for private submission attachment files
- Managed identity from App Service to Azure SQL
- Managed identity from App Service to Blob Storage in the cloud dev environment
- Static Web App for the public marketing site at `cmiforge.com`
- Azure DNS hosts `cmiforge.com` so app/customer/demo subdomains can be provisioned from Azure instead of manually through the registrar
- `demo.cmiforge.com` points at the public demo App Service
- `app.cmiforge.com` points at the Customer 0 App Service
- Future Azure Function App and Static Web App split-architecture resources are provisioned, but the current production-worthy app host remains App Service

The live demo currently runs in demo mode with the seeded `Ima User` context. Microsoft Entra authentication is supported in the app, but demo mode keeps the public demo experience frictionless while the product is still being shaped.

Real tenants should run with `CMIForge:DemoMode=false`, `Authentication:Microsoft:Enabled=true`, and a configured `CMIForge:BootstrapAdminEmail`. Customer-managed Entra user creation also needs a verified Entra custom domain, `EntraProvisioning:Enabled=true`, and `EntraProvisioning:Domain=<verified-domain>`.

Public requests can be collected through `/Signup`. The request cannot be submitted until the signer accepts the current CMIForge SaaS Terms, Legal Use, and License Agreement. Admins can triage requests in `System -> Signup Requests` and work through `System -> Onboarding` after the customer workspace is provisioned.

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

`cmiforge.com` email DNS is currently configured in Azure DNS for Microsoft 365 / Exchange Online:

- `MX @ -> cmiforge-com.mail.protection.outlook.com`
- `TXT @ -> v=spf1 include:spf.protection.outlook.com -all`
- `TXT @ -> MS=ms36377677`

The Entra identity/mailbox direction is now Microsoft-owned for `cmiforge.com`. Keep mailbox licensing, shared mailbox delegation, MFA, password resets, and mail-flow policy in Microsoft 365 / Entra rather than inside CMIForge.

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

Customer 0 has been loaded with realistic generated volume-test data for early performance and search testing:

- 400 generated clients with mixed company and individual names
- 400 generated matters with realistic matter names and practice areas
- 400 generated parties with organization, individual, and government names
- 10 active users total, including generated fake users for volume submissions
- 1,000 volume-test submissions marked in submission JSON with `"volumeTest": true`
- 300 open volume workflow tasks for queue testing

The generated records use marker notes and the `volumeTest` submission flag so they can be filtered, measured, renamed, or removed later without confusing them with real customer data. Use `tools/rename-customer0-volume-data.ps1 -VerifyOnly` to confirm the current generated dataset.
