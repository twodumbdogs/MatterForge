# CMIForge

CMIForge is a C# / ASP.NET Core prototype for configurable legal intake, workflow approvals, operational entity management, and conflict searches.

Current version: `20260623.1`.

The product currently has three public-facing surfaces:

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
- Form sections/tabs, basic field display conditions, and workflow-step editability rules
- Runtime form rendering
- Form submissions stored against the exact published form version
- Secure external form invites for saved contacts to complete client-facing intake links
- Submission queue and detail view
- Role-oriented dashboard views for firm admins, individual users, and matter partners
- Dashboard visibility grants by user, team, or role from Security
- Clients, matters, contacts, users, parties, aliases, and relationships
- Native conflicts search with deterministic AI-style explanations
- Azure SQL full-text candidate indexing for larger conflict-search datasets, backed by `ConflictSearchDocuments`
- Azure AI Search Basic proof-of-tech indexes for demo and Customer 0 conflict-search documents plus entity-directory search
- In-app conflict search-term help with examples for names, aliases, punctuation, history, and match strength
- Conflict result escalation to another active firm user, with escalation notes, approval notes, and audit history
- Conflict result detail paging at 50 hits per page
- Floating live conflict preview on submission forms
- Workflow definitions, approval queues, outcomes, and answer-based routing
- Submission details with a right-side workflow rail for open actions and recent workflow history
- Workflow notification steps with recipient/template configuration
- Inbound email intake groundwork for trusted firm mailboxes to create reviewed submissions from `Client:` / optional `Matter:` subjects
- CSV imports/exports for clients, matters, parties, contacts, users, and time entries
- Client photo OCR import drafting
- Private submission attachments
- Time recording, approval, locking, phase/task codes, timer capture, built-in operational reports, and a basic report builder
- Team, role, permission, and audit-log foundation
- Admin-only user impersonation for support, workflow-routing, and approval testing
- Personal user profile settings, including app-wide font size
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

## 2026-06-20 Nightly Wrap-Up

This wrap-up records the current live-app working agreement and the latest polish pass:

- Gabe's standing preference is now documented: after CMIForge changes, build/verify and deploy affected live surfaces unless Gabe explicitly says not to deploy.
- The Help page now explains how conflict search terms are split, normalized, matched, scored, and reported, with user-facing examples.
- Conflict search now treats `and` like an ampersand connector for names such as `Elm & Vine` vs. `Elm and Vine Capital`, while keeping acronym matching conservative.
- Conflict search create and re-run screens now show a staged progress bar so users get immediate feedback while the server scans entities, history, scores candidates, and opens the results.
- Conflict search matching was tuned so acronym-style names such as `A.A.W.` normalize sensibly and do not get overconfident one-letter matches against unrelated words.
- Clients, parties, conflicts, and submissions lists were tightened into compact one-line rows to match the cleaner Users list style.
- Enhancement requests now behave as a tenant-local firm queue: all signed-in users in the same firm can see the active firm requests and add/remove a support vote, while admins can still triage status and internal notes.
- Client aliases are now first-class records on client create/detail screens, editable after creation, shown on the client list, and included in conflict matching. Party aliases are also editable/removable from party details.
- Entity detail screens are better aligned: client, matter, party, and contact pages now surface related children/relationships in consistent side panels, including matter time entries, related parties, related contacts, client/matter links, and matter roles.
- Direct-created clients, matters, and parties now default to `Compliance Review` and show guidance/warnings until reviewed. Moving a client to `Active` or a matter to `Open` requires change-request notes and writes a separate compliance-reviewed audit event when approved.
- Published forms can now be sent to saved contacts as secure, expiring external intake links. The raw invite token is never stored, the external page uses a minimal client-safe layout, completed links become normal form submissions, and email-enabled tenants can queue the invite through the Email Outbox while non-email tenants get a copyable link. Recent send panels now show queued/sent/opened/completed tracking on the form, contact, client, and matter pages, and resend creates a fresh tracked link while revoking the old uncompleted invite.
- Inbound email intake now has the first real app lane: tenant settings define the Graph mailbox, tenant inbound address, allowed sender domains, and default form key; unread trusted messages with `Client: Acme Corp` or `Client: Acme Corp; Matter: Lease Review` create normal reviewable submissions and copy supported attachments into the private submission attachment store. Blank or omitted `Matter:` is allowed so the reviewer can create/link only a client.
- Form definitions now support named sections that render as tabs on internal and external intake pages. Fields can also have a basic "show if field equals value" condition and an optional workflow-step edit rule so returned submissions can be locked down by step.
- Submission detail pages now use a left-side Submission Workspace tab set for form sections, linked conflict searches, and attachments, while keeping a sticky right-side activity/audit rail for submitter context, workflow actions, tasks, history, recent audit events, and attachment summaries.
- Older submissions whose saved form-version JSON predates explicit sections get display-only inferred tabs on the detail page, so historical records can still read as Client Details, Matter Details, Related Parties, Conflicts Search, Compliance Review, or Review without mutating stored submission history.
- Customer 0 startup seeding now keeps demo/sample conflict reference parties behind `CMIForge:SeedSampleData=true`, so core production-style tenants do not try to seed demo data by accident.
- The security hardening direction is to keep moving toward managed identity, Key Vault-backed secrets, private storage, least-privilege SQL/Graph access, Defender alerts, private networking where cost-appropriate, and explicit audit/retention controls.

## 2026-06-21 Late-Night Closeout

This closeout captures the final polish and operator notes from the latest build session:

- Submission lists now match the compact one-line entity-list style. Client and matter values no longer show the extra `Submitted value` helper row, and long values truncate cleanly in the table.
- Entity-style list polish now covers clients, parties, conflicts, and submissions, with users already in the simpler one-line table pattern.
- Submission detail pages keep the sticky activity/audit rail while the main workspace uses tabs for form sections, conflict searches, attachments, and older inferred sections.
- Conflict searches now show progress feedback while the app moves through search setup, entity scanning, history checks, scoring, and results.
- The product logo now uses Gabe's supplied square CMIForge raster mark across app navigation, external form pages, favicons, public-site navigation, and public-site link previews. The web/app logo source is `wwwroot/img/cmiforge-logo.png`, with touch/favicon variants alongside it. High-resolution square exports for LinkedIn/company-profile use were regenerated under `artifacts/brand/`:
  - `cmiforge-linkedin-logo-400.png`
  - `cmiforge-linkedin-logo-1200.png`
  - `cmiforge-linkedin-logo-2400.png`
  - `cmiforge-logo-source.png`
- Recent UI-only releases were built and deployed to both `https://demo.cmiforge.com` and `https://app.cmiforge.com` with database migrations skipped where no schema changed.
- Workflow definitions now support an explicit saved-draft vs published-live state. Drafts can be saved before they are publish-ready, and a failed publish attempt keeps the latest edits as an unpublished draft instead of discarding them. Only active published workflows appear in form workflow pickers or start new submissions.
- Forms can now be copied from their latest published version into a new active form, and workflows can be copied into unpublished drafts for safe revision before going live. Workflow steps can be moved up/down in the designer before saving or publishing.
- Submission activity rails now keep Open Actions at the top, show workflow task/history detail newest-first, and use a tighter compact layout.
- Entity change approvals now show before/after values for each changed field, including resolved matter references where possible, so reviewers can see the actual data they are approving.
- Conflict result filters now sit directly above the results table, and row-level review/escalation controls are collapsed behind compact row actions to keep high-volume result screens easier to scan.
- Demo reset now clears the newer approval, request, alias, notes, archive, signup, and time-code tables before reseeding. The scheduler checks the last successful reset and catches up when the app starts overdue instead of waiting a fresh 12 hours after each cold start.
- Conflict result review now makes bulk escalation explicit: select rows, pick `Escalate to`, then use `Escalate selected` to escalate all checked results in one action.
- Conflict clearance and escalation attribution now keeps the actual signed-in actor separate from the impersonated/effective user, so audit-facing conflict rows can show values such as `Rowena Bekker impersonating Owen Bennett` instead of falling back to `System`.
- Client, matter, and party discussion notes now show newest-first in a compact scrollable thread, tuck older notes behind an expander, show the actual author when a note is added during impersonation, and let the author delete their own note.
- Client and party alias add/edit controls now live together in the main Aliases panel, and alias adds use action-specific validation so valid new aliases save while duplicate aliases show a clear validation error.
- Matter partner time approvals now expose approval actions from the dashboard/time list, and lead partners with approval rights can open submitted time entries waiting on them even when they did not create the time.
- This batch was deployed to both demo and Customer 0 with migrations applied through the dedicated migrator lane.

## 2026-06-22 Data Access

- The former Imports area is now `Imports/Exports`.
- Customers can download core operational data to CSV in fixed 1,000-row batches.
- The export batch size is shown under Settings as a CMIForge-controlled read-only value.
- Settings also shows Enterprise-only SQL data access fields for the CMIForge-controlled Entra principal and customer SQL connection string. Those fields are read-only and are intended to be provisioned by CMIForge, with database read/write scope only and no Azure control-plane permissions.
- Enterprise is now modeled at `$599/month` for 1,000 users, 5,000 clients, 5,000 matters, and SQL data access.

## 2026-06-22 Dashboard Access

- Dashboard visibility is now configurable from `Security -> Dashboard access`.
- Visibility grants can target an individual user, a team, or a security role.
- If a dashboard has no grants, it remains visible to everyone who can reach the dashboard. Once grants exist, only matching users, team members, role holders, and system admins can choose that dashboard.
- Seeded defaults restrict `Firm Admin` to the Administrator role and `Matter Partner` to the Partner role. `My Work` remains broadly available.

## 2026-06-22 User Profiles

- Signed-in users now have a `My Profile` page reachable from the top navigation user control.
- The first personal preference is app-wide font size, stored on the user's `Users.FontScalePercent` value; the profile page also hosts the browser-local dark mode switch.
- Available sizes are Small, Standard, Large, and Extra Large, and the selected size is applied by the shared layout before the page styles render.

## 2026-06-22 Regional Hosting Positioning

- The public marketing site now includes a concise data-residency section: `Choose where your data lives`.
- The in-app terms now include a guarded `Data Hosting Location` clause covering supported geographic regions, commercially reasonable regional storage/processing efforts, and operational exceptions for support, backup, security, disaster recovery, and service management.
- The legal agreement acceptance version is now `2026-06-22`.

## 2026-06-23 Search Scaling And Private Data Access

- Conflict search now uses a denormalized `ConflictSearchDocuments` table plus Azure SQL full-text search as the candidate finder when SQL Server full-text is available.
- CMIForge still applies its own deterministic scorer to returned candidates; SQL full-text rank is not the final displayed score.
- Large seeded or imported tenants should run `tools/rebuild-conflict-search-documents.ps1` after data loads so the first user-facing conflict preview/search does not have to build the document corpus.
- Customer 0 was warmed with `40,437` conflict-search documents and direct full-text hit checks against the generated volume data.
- Conflict search details now page result hits at 50 rows per page, using the same `RecordPage` pattern as entity lists.
- Azure AI Search Basic is provisioned as a proof-of-tech layer in shared service `gw-ai-srch-basic`, with separate tenant indexes `cmiforge-demo-conflict-documents` and `cmiforge-customer0-conflict-documents`. SQL full-text remains the active cheaper near-term conflict-search candidate path in the app; Search is available for semantic/vector/richer-ranking experiments behind trusted tenant configuration later.
- The Basic indexers use user-assigned managed identity `gw-index` with read access to `dbo.ConflictSearchDocuments`. SQL public access was temporarily enabled for initial indexer setup and then restored to disabled.
- System Settings now includes `Use Azure AI Search candidates`; when enabled, the configured tenant index is used as the candidate finder and CMIForge still hydrates SQL rows, applies its own scorer, and falls back to SQL full-text if Search is unavailable.
- Azure AI Search also has one tenant entity-directory index per environment: `cmiforge-demo-entity-directory` and `cmiforge-customer0-entity-directory`, sourced from `dbo.EntitySearchDocuments`. The view flattens clients, matters, parties, client aliases, party aliases, and contacts into one searchable document surface with a `sourceType` field. Users are intentionally excluded until there is a clear user-search use case.
- Initial entity-directory indexing completed with `57` demo documents and `40,081` Customer 0 documents. Search-only checks returned Customer 0 hits for `manufacturing`, `walker`, and `alder`.
- With private endpoints enabled and Azure SQL public access disabled, direct database querying requires a network path into the VNet, such as a jumpbox VM, Bastion, VPN, or a controlled temporary public-access maintenance window. SSMS is optional; Azure Data Studio, `sqlcmd`, `Invoke-Sqlcmd`, or scripts are also fine once network and Entra/SQL permissions are correct.

## 2026-06-20 Private Networking Hardening

CMIForge now has a shared private-networking slice for the current demo and Customer 0 apps:

- VNet: `cmiforge-vnet` in `gw-rg` / Central US.
- App Service integration subnet: `appsvc-integration` (`10.42.1.0/26`), delegated to `Microsoft.Web/serverFarms`.
- Private endpoint subnet: `private-endpoints` (`10.42.2.0/27`).
- Private endpoints:
  - SQL server `gwmatterforge` -> `pe-cmiforge-sql` / `10.42.2.4`
  - Blob storage account `cmiforgeattachasgmt7` -> `pe-cmiforge-blob` / `10.42.2.5`
  - Key Vault `cmiforge-kv-gw` -> `pe-cmiforge-keyvault` / `10.42.2.6`
- Private DNS zones are linked for SQL, Blob, and Key Vault private-link names.
- `cmiforge-dev-web-06161223` and `cmiforge-customer0-web` are VNet-integrated.
- Public network access is disabled for Azure SQL server `gwmatterforge`.
- Public network access is disabled for storage account `cmiforgeattachasgmt7`, with default network action `Deny`.
- Key Vault `cmiforge-kv-gw` is created with RBAC authorization and purge protection. Its private endpoint is ready, but public network access intentionally remains enabled until app secrets are actually moved into Key Vault and an operator access path is confirmed.

Production EF migrations now use a dedicated migration lane instead of normal web-app startup:

- Migrator managed identity: `cmiforge-migrator-mi`
- Triggered WebJob host: `cmiforge-db-migrator`
- WebJob project: `CMIForge.Migrator`
- Runner script: `deploy/migrations/run-tenant-migrations.ps1`
- The migrator host is integrated with `cmiforge-vnet/appsvc-integration`, runs through the SQL private endpoint, and is stopped when idle.
- `cmiforge-dev-web-06161223` and `cmiforge-customer0-web` should keep `CMIForge__RunMigrationsOnStartup=false`.

Because Azure SQL public access is disabled, local EF migrations against the cloud databases should not be the default path. Use the dedicated migration runner. `-AllowTemporarySqlPublicAccess` is reserved for one-time/local SQL grant maintenance, then the scripts restore SQL public access to disabled.

For ad hoc database querying, the preferred durable operator path is to run the SQL client from a machine on the private network path, such as a small Azure VM in `cmiforge-vnet` reached through Bastion or a future VPN. A local workstation can still use the repo's controlled `-AllowTemporarySqlPublicAccess` maintenance switches when a short local query or grant is necessary, but that should remain an explicit exception.

The repeatable customer provisioning path is private-network aware:

- `deployme.ps1` can integrate a newly created App Service into `cmiforge-vnet/appsvc-integration`.
- `deploy/customer/provision-customer.ps1` passes that VNet integration by default for future customer environments.
- New tenant attachment containers are created through Azure Resource Manager (`az storage container-rm`) so provisioning is not blocked by storage public network access being disabled.
- The provisioning script can grant Key Vault secret-read access to the tenant app identity.
- EF migrations are applied through the dedicated VNet-integrated migrator WebJob. Local SQL grant steps require either an Azure/VNet execution path or the explicit `-AllowTemporarySqlPublicAccess` maintenance switch.

## Deployment Working Agreement

Codex should deploy as it works on CMIForge. The default loop is: inspect, implement, build or otherwise verify, deploy the affected app/site surface, and smoke-check the live URL. Schema-changing work should deploy code and database together. Code/UI/docs-only work can skip database migrations, but should still publish the app or site unless Gabe explicitly asks to hold deployment.

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
- Azure SQL database for operational data, workflow state, submissions, external form invites, conflicts, conflict archives, imports, audit logs, reports, time entries, tenant provisioning requests, and legal agreement acceptance records
- Azure Blob Storage for private submission attachment files
- Managed identity from App Service to Azure SQL
- Dedicated migrator managed identity/WebJob for EF schema changes
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

Inbound email intake is separate from outbound notifications. The app now has settings and a Microsoft Graph polling worker for reading a tenant mailbox, but it should stay disabled until the mailbox alias exists, allowed sender domains are set, and the managed identity has narrowly scoped Graph mail-read/write access to the intake mailbox. The current subject rule is:

```text
Client: Acme Corp
Client: Acme Corp; Matter:
Client: Acme Corp; Matter: Lease Review
```

Messages without `Client:` are rejected into the inbound email log. Messages from domains outside `InboundEmail.AllowedSenderDomains` are also rejected.

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
2. Secure client-facing form invites for saved contacts
3. Workflow steps + approval queues
4. Rules-based routing + workflow notifications
5. Matter/client record creation
6. Reporting, permissions, integrations
7. Fancy admin designer UX

## Current Configured Plan

The app defaults to `Professional` unless the tenant sets `CMIForge:Plan` / `CMIForge__Plan` to another tier:

- Includes 10 users
- 500 matters
- 500 clients
- All features
- Email support
- Price: `$149/month`

Community and Enterprise tiers are visible in the app on `/Billing`, but payment handling is intentionally deferred. The active plan is shown in the app footer next to the product version. Enterprise is modeled at `$599/month` for 1,000 users, 5,000 clients, 5,000 matters, and customer SQL data access.

## Customer 0 Volume Test Data

Customer 0 has been loaded with realistic generated volume-test data for early performance and search testing:

- 700 generated active users with realistic names and no Entra IDs
- 10,000 generated clients with mixed company and individual names
- 10,000 generated parties with organization, individual, and government names
- 20,000 generated matters with realistic matter names and practice areas
- 19,600 generated matter-party links for relationship and search-load testing
- 1,000 volume-test submissions marked in submission JSON with `"volumeTest": true`
- 300 open volume workflow tasks for queue testing

The generated records use marker notes and the `volumeTest` submission flag so they can be filtered, measured, renamed, or removed later without confusing them with real customer data. Use `tools/seed-customer0-big-volume-data.ps1 -AllowTemporarySqlPublicAccess` to verify the current large dataset without adding more rows, or add `-Apply` to top it up to the target counts.
