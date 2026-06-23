# CMIForge Project Summary

CMIForge is a homegrown ASP.NET Core Razor Pages prototype for configurable legal intake, entity management, workflow automation, and conflict searches. The long-term idea is a law-firm intake/workflow platform in the spirit of tools like Intapp Open, but built in focused slices so the data model and user experience can grow together.

Current version: `20260621.1`.

## Original Direction

The first build path was intentionally practical:

1. Dynamic form definitions and submissions
2. Workflow steps and approval queues
3. Rules-based routing and notifications
4. Matter/client record creation
5. Reporting, permissions, and integrations
6. Fancy admin designer UX

We deliberately did not over-engineer hosting first. The app still runs locally with Kestrel for development, but the project now also has a live Azure dev environment and a public marketing site.

## Current Stack

- ASP.NET Core Razor Pages
- .NET 10
- Entity Framework Core
- Azure SQL for persistent development data
- Azure Blob Storage for private submission attachment files
- Azure App Service for the live dev app/demo
- Azure App Service for Customer 0
- Azure Static Web Apps for the public marketing site
- Azure DNS for `cmiforge.com`
- A future Azure Function App/API resource is provisioned for a possible split architecture later
- In-memory database fallback when the configured connection string is missing or still has the placeholder server
- Bootstrap-style UI with custom CMIForge styling
- Local development URL: `http://localhost:5153`

## 2026-06-20 Nightly Wrap-Up

This pass brought the repo docs current with the latest app behavior and Gabe's preferred working loop.

Recorded operating rule:

- Gabe wants Codex to deploy as it works on CMIForge. Unless Gabe explicitly says not to deploy, the expected loop is implement, build/verify, deploy affected live surfaces, and smoke-check the live URLs.
- Shared app behavior should generally be deployed to both `https://demo.cmiforge.com` and `https://app.cmiforge.com`; public-site-only changes go to `https://cmiforge.com`.
- Schema-changing work should deploy code and database together. Code/UI/docs-only work can skip migrations, but should still deploy the affected surface.

Latest product/docs state:

- The Help page now includes a user-facing explanation of conflict search term handling, including punctuation cleanup, suffix trimming, aliases, matter context, related parties, prior-history matching, and match-strength examples.
- Conflict matching was tuned so acronym-style values such as `A.A.W.` normalize to useful initials without creating strong matches against unrelated single-letter containment.
- Conflict normalization now treats `and` like the ampersand connector, and phrase containment is token-boundary based so names like `Elm & Vine` can match `Elm and Vine Capital` without reopening loose substring matches.
- Clients, parties, and conflicts lists have been compacted into cleaner one-line rows.
- Submissions now use the same compact one-line list treatment: client and matter values render in one row without the extra `Submitted value` caption, and long names truncate cleanly.
- Enhancement requests now act as a tenant-local firm queue. All signed-in users in a firm can see active requests and add/remove one support vote per request; admins still own status triage and internal notes.
- Client aliases are now first-class records. They can be captured on client creation, edited/deleted from client details, shown on the client list, and searched by conflict checks. Party aliases are now editable/deletable from party details.
- Entity detail screens are now more consistent. Clients show matters, contacts, and related parties; matters show time entries, parties, and contacts in the side rail; contacts show client links, matter links, and related parties; parties show matter roles, related contacts, and party relationships.
- Direct-created clients, matters, and parties now default to `Compliance Review` instead of looking fully reviewed on creation. Create/detail/edit screens explain the direct-create path, and approving a client move to `Active` or matter move to `Open` writes a dedicated `EntityComplianceReviewed` audit entry.
- Published forms can now be sent to saved contacts through secure, expiring external intake links. The invite token is generated once and only its hash is stored, the external page uses a minimal client-safe layout, completed links create normal `FormSubmissions`, and the invite queues through `EmailOutboxMessages` when tenant mail is configured or falls back to a copyable link when it is not. Invite email tracking now links outbox messages back to external invites so the app can show queued, Graph-accepted/sent, opened, and completed states on form, contact, client, and matter detail pages, with resend creating a fresh tracked link and revoking the previous uncompleted invite.
- Inbound email intake now has a V1 processing lane. Tenant settings control the Graph mailbox anchor, tenant inbound address, allowed sender domains, and default intake form. Trusted unread mailbox messages with `Client:` and optional `Matter:` subject segments create normal reviewable submissions, copy supported attachments into the private submission attachment store, record the source email, and write audit history. Blank or omitted `Matter:` is intentionally allowed so conversion can create/link only the client.
- Conflict result bulk work now supports the expected multi-row escalation flow from the details page: checked result rows can all be escalated to one reviewer with shared notes using `Escalate selected`.
- Conflict clearance, escalation, and escalation approval now preserve impersonation attribution by storing the real actor and the effective impersonated user separately. Conflict result history can display `actual user impersonating effective user` instead of incorrectly saying `System`.
- Dashboard access can now be assigned by user, team, or role from `Security -> Dashboard access`. Seeded defaults restrict Firm Admin to Administrators and Matter Partner to Partners, while My Work remains broadly visible.
- Imports is now Imports/Exports. Customers can download core operational data to CSV in fixed 1,000-row batches, with the batch size shown as a read-only setting and Enterprise-only SQL data access surfaced as CMIForge-controlled read-only settings.
- User profiles now have a first personal setting surface. The top navigation links to `My Profile`, where each signed-in user can choose Small, Standard, Large, or Extra Large app text and toggle the browser-local dark mode preference; font size is stored on `Users.FontScalePercent` and applied app-wide.
- Public-site positioning now includes supported regional hosting/data residency language, and the in-app terms include a `Data Hosting Location` clause with commercially reasonable regional hosting language plus support, backup, security, disaster-recovery, and service-management exceptions. Current legal agreement version: `2026-06-22`.
- Entity discussion notes now preserve impersonation attribution by storing the real author and the impersonated user separately. The notes thread shows newest-first, hides older notes behind an expander, scrolls instead of taking over the page, and allows the author to delete their own note.
- Client/party alias add/edit controls now live together in the main Aliases panel. Alias adds use action-specific validation so valid new aliases save from detail pages, while duplicate normalized aliases show a clear validation error instead of silently no-oping.
- Matter partner time approval is reachable from the dashboard/time list, and the Time detail permission path now allows lead partners with approval rights to open submitted entries waiting on them.
- Customer 0 startup recovered after demo conflict reference parties were kept behind the sample-data seed switch. `CMIForge:SeedSampleData=false` should seed core production structure without public-demo filler data.
- Production schema changes now run through the dedicated migrator lane: `cmiforge-migrator-mi` plus the stopped-when-idle `cmiforge-db-migrator` triggered WebJob host, using `CMIForge.Migrator` and `deploy/migrations/run-tenant-migrations.ps1`.
- The near-term security direction is a "Fort Knox but practical" posture: managed identity first, Key Vault for secrets, private blob containers, least-privilege SQL and Graph grants, audit logging, Defender/alerting, backup/retention policy, and private networking as the customer risk profile justifies the spend.
- Brand assets now use Gabe's supplied square CMIForge raster logo across app/public-site chrome, favicons, and social previews. The repo keeps the web/app logo in `wwwroot/img/cmiforge-logo.png` and business/social exports in `artifacts/brand/`, including 400px, 1200px, and 2400px PNGs plus the source PNG.

Private-networking hardening was implemented after that planning note:

- Created shared VNet `cmiforge-vnet` in `gw-rg` / Central US.
- Added App Service integration subnet `appsvc-integration` (`10.42.1.0/26`) and private endpoint subnet `private-endpoints` (`10.42.2.0/27`).
- Added private endpoints and private DNS for Azure SQL `gwmatterforge`, Blob storage `cmiforgeattachasgmt7`, and Key Vault `cmiforge-kv-gw`.
- Integrated both `cmiforge-dev-web-06161223` and `cmiforge-customer0-web` with the VNet.
- Disabled public network access on SQL server `gwmatterforge`.
- Disabled public network access on storage account `cmiforgeattachasgmt7` and set storage network default action to `Deny`.
- Left Key Vault public network access enabled for now because secrets have not yet been moved into Key Vault and operator data-plane access still needs a clean private path.
- Updated the repeatable customer provisioning path so new tenant App Services can be VNet-integrated automatically, tenant attachment containers are created through ARM rather than blocked storage data-plane calls, tenant identities can receive Key Vault Secrets User, and EF schema updates run through the VNet-integrated migration WebJob instead of live web-app startup.

## 2026-06-19 Session Update

This session was a broad product-hardening pass across UX, conflicts, tenant onboarding, legal acceptance, deployment, and agent behavior.

Implemented product changes now reflected in the app:

- Navigation branding now uses the configured firm name without overlapping the menu.
- The footer displays `CMIForge 1.0` before the build version.
- Dashboard KPI cards link into the relevant lists.
- Entity, submission, workflow, and other row-heavy pages have inline search boxes where practical.
- Standard read-only paginated tables expose sortable column headers that sort the full result set before page slicing.
- Conflict searches can be named dynamically from selected matter context.
- Conflict results use `Match strength` language instead of treating string similarity as legal risk.
- Conflict scoring is less flat, with exact matches, contains matches, token overlap, fuzzy similarity, role boosts, and context boosts separated from legal/contextual risk.
- Conflict result pages support filters such as clearance status, role/match context, and risk level.
- Conflict results support multi-select row clearance, including select-all.
- New conflict searches include searchable prior/completed/cleared search history.
- Cleared conflict search history uses a hybrid SQL archive/index design: slim searchable SQL rows plus compressed full payload details for hydration when opened.
- Live conflict preview can be turned on/off from System Settings.
- Form editing caps forms at 50 fields and supports friendlier reorder controls.
- Form-builder reorder controls use icon/handle-style affordances instead of wordy buttons.
- Forms can be copied from the latest published version into a new active form with a unique key, so teams can branch a similar intake definition without rebuilding it by hand.
- Forms list rows show open/completed client invite counts, and each published form has a `Send` action for creating a saved-contact external intake link. The send page also acts as the recent-sends dashboard for that form, including resend, Graph send state, secure-link open time, and completion/submission links.
- Cancelled submissions behave more like archived records: hidden from daily lists, visible from System Archive, and restorable.
- Submission list/detail work now shows linked client and matter context earlier and more visibly.
- Address autocomplete is wired through Geoapify-backed address lookup where static address fields exist.
- Enhancement requests can be submitted from inside the product under System.
- A CMIForge brand icon was added to the app/public-site visual language.
- Customer 0 volume-test data was added for realistic dashboard, search, queue, and list-performance testing.
- Public signup now requires acceptance of the current CMIForge SaaS Terms, Legal Use, and License Agreement before submission.
- Legal agreement acceptance is recorded per tenant provisioning request in `LegalAgreementAcceptances`.

Deployed and verified during the session:

- Dev/demo app at `https://demo.cmiforge.com`.
- Customer 0 app at `https://app.cmiforge.com`.
- Migration `20260619232000_AddLegalAgreementAcceptances` was applied to both `cmiforge-demo` and `cmiforge-customer0`.
- Signup was smoke tested on dev with both missing-consent and accepted-consent POST flows.
- The public Customer 0 signup and terms pages were smoke tested anonymously.
- A temporary Azure SQL firewall rule was used for EF migrations from the local machine and removed after deployment.

Follow-up time-entry decisions now built into the app:

- Export marks approved exported records with exported metadata and locks them from normal editing.
- Time increment behavior is configurable at system and matter level, supporting 6-minute, 15-minute, and actual-minute entry.
- Time statuses are Draft, Submitted, and Approved.
- Approved time is immutable from normal edit screens.
- Matters have a `Requires time approval` flag and lead-partner approval path for submitted time.
- Record Time includes a simple local start/stop timer for assigning elapsed time to a matter.
- UTBMS-style phase/task code sets are modeled through reusable code sets, phases, and tasks.
- Time narratives are split into Client Narrative and Internal Notes.

Backlog/product decisions captured but not fully built yet:

- Time rejection/return statuses may be useful so approvers can send time back without deleting it.
- Historical corrections should eventually use explicit adjustment/reversal entries rather than mutating approved history.
- Multiple named persisted timers remain a nice-to-have.
- Future AI/search replicas can be revisited if archive/search volume grows beyond the hybrid SQL archive/index design.

## Current Cloud Architecture

Current public surfaces:

- Public marketing site: `https://cmiforge.com`
- Static Web App default host: `https://happy-smoke-052d7f610.7.azurestaticapps.net`
- Live dev app/demo: `https://demo.cmiforge.com`
- Live dev app/demo Azure fallback: `https://cmiforge-dev-web-06161223.azurewebsites.net`
- Customer app doorway: `https://app.cmiforge.com`
- Customer app Azure fallback: `https://cmiforge-customer0-web.azurewebsites.net`

Current Azure resources:

- Resource group: `gw-rg`
- App Service plan: `cmiforge-customer0-plan` (`B1`) hosts both the Customer 0 app and the public demo app to avoid paying for a second paid plan.
- Former dev App Service plan: `cmiforge-dev-plan` (`F1`) remains as a legacy/free plan resource.
- Public demo App Service app: `cmiforge-dev-web-06161223`
- Customer 0 App Service app: `cmiforge-customer0-web`
- Azure SQL server: `gwmatterforge.database.windows.net`
- Azure SQL prototype/dev database: `matterforge-prototype`
- Azure SQL public demo database: `cmiforge-demo`
- Azure SQL Customer 0 database: `cmiforge-customer0`
- Attachment storage account: `cmiforgeattachasgmt7`
- Attachment container: `submission-attachments`
- Customer 0 attachment container: `customer0-attachments`
- Shared private-networking VNet: `cmiforge-vnet`
- App Service VNet integration subnet: `appsvc-integration`
- Private endpoint subnet: `private-endpoints`
- Private endpoints: `pe-cmiforge-sql`, `pe-cmiforge-blob`, `pe-cmiforge-keyvault`
- Shared Key Vault landing zone: `cmiforge-kv-gw`
- Dedicated migration identity: `cmiforge-migrator-mi`
- Dedicated migration WebJob host: `cmiforge-db-migrator`
- Static Web App: `cmiforge-web-06161219`
- Future Function App/API resource: `cmiforge-api-06161219`
- Azure DNS zone: `cmiforge.com`

Current hosting stance:

- The Razor Pages app is the real product host today.
- The public marketing site is separate and deployed through Azure Static Web Apps.
- The Static Web App + Function App split is scaffolded for later, but the app has not been rewritten into that model.
- The live dev app uses demo mode so visitors can explore with the seeded `Ima User` context.
- The live demo app points at the separate `cmiforge-demo` database so public test data stays away from the prototype/dev database.
- `app.cmiforge.com` is bound to the Customer 0 app as the real tenant doorway.
- `demo.cmiforge.com` is bound to the public demo app.
- DNS for `cmiforge.com`, `app.cmiforge.com`, and `demo.cmiforge.com` is managed by Azure DNS after the Namecheap nameserver delegation.
- Namecheap remains the registrar.

Current Azure DNS records intentionally include:

- Apex `cmiforge.com` as an Azure DNS alias-style record to the Static Web App.
- `www` as a CNAME to the Static Web App default host.
- `app` as a CNAME to `cmiforge-customer0-web.azurewebsites.net`.
- `demo` as a CNAME to `cmiforge-dev-web-06161223.azurewebsites.net`.
- `asuid.app` and `asuid.demo` TXT records for App Service custom-domain verification.
- Entra, Google Search Console, and Static Web App verification TXT records.
- Microsoft 365 / Exchange Online MX and SPF records for `cmiforge.com` mail.

## Customer 0 Tenant Slice

The repo now includes a deployment slice for Gabe's real non-demo tenant, called Customer 0.

Customer 0 is intended to be the first production-style CMIForge space:

- Separate App Service app: `cmiforge-customer0-web`.
- Separate Azure SQL database: `cmiforge-customer0`.
- Separate private Blob container: `customer0-attachments`.
- Microsoft Entra login enabled.
- Demo mode disabled.
- Demo resets disabled.
- Sample/demo data disabled.
- Bootstrap admin configured through `CMIForge:BootstrapAdminEmail`.

Customer 0 was provisioned on `2026-06-17` at `https://cmiforge-customer0-web.azurewebsites.net` using the `CMIForge Customer 0` Entra app registration. It now uses the `Basic B1` App Service SKU so `app.cmiforge.com` can serve as the cleaner customer app doorway.

The Customer 0 slice now includes the first Entra user-provisioning foundation:

- `cmiforge.com` has been added to Entra as a verified custom domain.
- `gabe@cmiforge.com` exists as an Entra user identity, pending mailbox licensing if Microsoft 365 email is adopted.
- Customer 0 stores Entra tenant ID, object ID, and UPN on user records.
- The create-user flow can optionally create an Entra user, generate a temporary password, and link the Entra identity back to the CMIForge user record.
- The user's CMIForge work/contact email can differ from the Entra UPN.
- A UPN such as `first.last@cmiforge.com` does not require a real mailbox.
- The Customer 0 web app managed identity has the Microsoft Graph `User.ReadWrite.All` application permission needed for user creation.
- `EntraProvisioning__Enabled` can be enabled per tenant when the managed identity and Graph permissions are in place.

The onboarding/provisioning slice now includes:

- Anonymous `/Signup` request capture for prospective/customer workspace requests.
- `TenantProvisioningRequests` table for firm, admin, plan, desired domain/subdomain, notes, status, and internal notes.
- Mandatory signup acceptance of the current CMIForge SaaS Terms, Legal Use, and License Agreement.
- `LegalAgreementAcceptances` table for customer name, signer name/email, agreement key/version/title, product version, accepted timestamp, IP address, and user agent.
- `System -> Signup Requests` for admin triage of new, contacted, provisioning, ready, and closed requests.
- `System -> Onboarding` for a first-customer checklist covering Entra login, Entra provisioning, support contact, admins, users, teams, forms, workflows, clients, matters, contacts, parties, conflicts, imports, time, attachments, and settings review.
- The public marketing site now links request-access CTAs to the Customer 0 `/Signup` page so real requests land outside the disposable demo database.

Tracked Customer 0 files:

- `deploy/customer0/deploy-customer0.ps1`
- `deploy/customer0/appservice-settings.sample.json`
- `deploy/customer0/ENTRA_CHECKLIST.md`
- `deploy/customer0/README.md`

The reusable v1 customer provisioning command now lives at:

- `deploy/customer/provision-customer.ps1`
- `deploy/customer/README.md`

For a requested `firm.cmiforge.com` workspace, the command validates the subdomain, creates the customer database and private attachment container, deploys/configures the App Service app, creates the Azure DNS CNAME and `asuid` TXT verification record, binds the hostname, creates/binds an App Service managed certificate, adds Entra redirect URIs, applies migrations, enables core seed startup, grants managed identity access where possible, and can mark the signup request `Provisioning`, `Ready`, or `Failed`.

The command now defaults new tenant app environments into the shared private-networking slice by passing `cmiforge-vnet/appsvc-integration` through `deployme.ps1`. Since SQL and storage public access are disabled, EF migrations run through `deploy/migrations/run-tenant-migrations.ps1`, which deploys/runs the `CMIForge.Migrator` triggered WebJob under `cmiforge-migrator-mi`. The current operator scripts still support an explicit `-AllowTemporarySqlPublicAccess` switch for controlled one-time SQL grant/admin maintenance, then restore SQL public access to disabled during cleanup.

The command can also create the initial Entra admin user, create the matching CMIForge user as `00000001`, assign the Administrator role, generate a temporary password, and email that password to the admin after the tenant hostname is ready.

The app now supports `CMIForge:BootstrapAdminEmail`. When Microsoft Entra login is enabled and demo mode is off, a matching authenticated user is activated if needed and granted the seeded `Administrator` role. This prevents a fresh tenant from being easy to lock yourself out of.

Startup seeding is now split between core platform seed data and sample/demo seed data:

- `CMIForge:RunSeedDataOnStartup=true` allows startup seed execution.
- `CMIForge:SeedSampleData=false` seeds core platform structure without public-demo filler data.
- `CMIForge:SeedSampleData=true` keeps local/demo-style sample data available when needed.
- Demo conflict reference parties are sample data. They should stay behind `CMIForge:SeedSampleData=true` so Customer 0 and future real tenants do not pick up public-demo search fixtures during startup.

The near-term tenant model is database-per-customer. That is simpler for support, backup/restore, customer export/delete, and early legal-data isolation. A future subscription flow can queue a provisioning job that creates the customer database, attachment container, app settings, Entra configuration, seed data, and first admin.

The near-term URL model is subdomain-per-tenant rather than path-based tenancy:

```text
cmiforge.com           public marketing site
demo.cmiforge.com      public disposable demo
app.cmiforge.com       Customer 0 / internal real tenant
firm.cmiforge.com      future customer tenant
```

Path-based tenancy such as `app.cmiforge.com/customer1` is intentionally avoided for now because it would require tenant-aware route prefixes, auth redirects, per-request tenant resolution, and more cross-tenant isolation testing.

## Email And Microsoft 365

Azure DNS currently carries Microsoft 365 / Exchange Online mail records for `cmiforge.com`:

- `MX @ -> cmiforge-com.mail.protection.outlook.com`
- `TXT @ -> v=spf1 include:spf.protection.outlook.com -all`
- `TXT @ -> MS=ms36377677`

The intended Microsoft 365 mailbox shape remains:

- `gabe@cmiforge.com` as the licensed user mailbox.
- `support@cmiforge.com` as a shared mailbox delegated to `gabe@cmiforge.com`.

Microsoft 365 / Entra should own mailbox licensing, shared mailbox delegation, MFA, password resets, and sign-in policy. CMIForge owns app-level authorization, teams, roles, and permissions.

## Public Demo Safety

The live demo now has dedicated guardrails for public visitors:

- `CMIForge:DemoMode=true` enables demo behavior.
- `CMIForge:DemoResetEnabled=true` enables scheduled demo resets.
- `CMIForge:DemoResetIntervalHours=12` configures the reset interval.
- A visible banner appears at the top of every page explaining public demo behavior.
- `/System/Demo` shows demo behavior and exposes a manual `Reset demo now` button.
- Manual reset clears demo-created records, including newer approvals, enhancement requests/votes, notes, aliases, signup/legal records, conflict archives, invites, inbound email records, workflow notification templates, and time-code tables, then reruns starter seed data.
- Scheduled reset checks the last successful reset and catches up shortly after app startup if the demo is overdue instead of waiting a fresh 12 hours after every cold start.
- `DemoResetRuns` records manual and scheduled reset attempts with trigger, status, deleted row count, message, error, start time, and completion time.
- `/System/Demo` now shows last reset, next estimated reset, and recent reset run history.
- Demo mode blocks administrative/designer POST actions such as Security, System Settings, Imports, Form designer, and Workflow designer changes.
- Demo mode blocks destructive handlers with `Delete` or `Remove` in the handler name.
- User `00000001` remains protected.
- Submitted form text is screened for obvious abusive/unsafe public demo content before handlers write data.

## In-App Help

CMIForge now has a first-pass Help page with formatted documentation for how the current build works.

The Help page covers:

- Big-picture intake-to-conversion flow
- Forms and form versioning
- Submissions and approval-gated conversion
- Workflow definitions, queues, outcomes, and routing conditions
- Clients, matters, contacts, parties, and users
- Entity note threads and archive/unarchive behavior
- Conflicts search behavior, prior-history matching, and result-level clearance
- Conflict search term handling with examples for aliases, punctuation, suffixes, matter context, related parties, prior search history, and why match strength is not the same thing as legal risk
- Floating live conflict preview during submission entry
- CSV imports and validation mode
- Client photo OCR import drafting
- Teams, roles, permissions, and optional Entra login
- Workflow notification steps
- System settings for operational configuration
- Hardcoded product plans
- Current limits and likely next slices

The Help page lives at:

```text
http://localhost:5153/Help
```

## Database Setup

The app uses `ConnectionStrings:DefaultConnection`.

For local development, the Azure SQL connection string is stored with .NET user secrets rather than committed to source control.

Useful commands:

```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<azure-sql-connection-string>"
dotnet tool restore
dotnet tool run dotnet-ef database update
dotnet run --urls http://localhost:5153
```

The current Azure SQL databases have the full entity, workflow, conflicts, import, attachment, time/reporting, audit-log, archive/index, enhancement-request, legal-agreement, conflict-result-escalation, enhancement-request-vote, and client-alias migration chain applied through `AddEnhancementVotesAndClientAliases`.

## Plan Limiter

CMIForge now has a configured plan limiter. This is not a payment system yet; it is a product-gating layer that gives the app realistic tier behavior while billing is deferred.

The default plan is `Professional`, and tenants can override it with `CMIForge:Plan` / `CMIForge__Plan`. The active plan is shown in the shared footer next to the product version.

Current plan tiers:

- `Community`
  - Free
  - 3 users
  - 50 matters
  - 50 clients
  - All features
  - Community support
- `Professional`
  - `$149/month`
  - Includes 10 users
  - 500 matters
  - 500 clients
  - All features
  - Email support
- `Enterprise`
  - `$599/month`
  - Includes 1,000 users
  - Unlimited matters
  - Unlimited clients
  - Customer SQL data access

Current enforcement:

- User creation is blocked when the current plan reaches its user limit.
- Matter creation is blocked when the current plan reaches its matter limit.
- Submission-to-client/matter conversion also respects the matter limit.
- Feature gates still exist in code, but the current Community and Professional packaging includes most product features. Customer SQL data access is modeled as Enterprise-only. The practical plan limits are user, matter, and client counts.

Plan and usage details are shown at:

```text
http://localhost:5153/Billing
```

## Dynamic Forms

CMIForge supports dynamic form definitions and versioned form schemas.

Current capabilities:

- Create forms in the app.
- Edit forms in the app.
- Define fields with labels, keys, types, required flags, and select options.
- Group fields into named sections that render as tabs on intake and submission screens.
- Add first-pass display conditions so a field can appear only when another field has a matching answer.
- Mark fields as editable only during a named workflow step, giving returned submissions a simple read-only/editable rule without a full rules engine.
- Publish an initial version.
- Publish a new version when editing an existing form.
- Attach a workflow definition to a published form version.
- Submit forms at runtime.
- Store each submission against the exact published form version.
- Display submitted answers using the original form schema.

Important behavior:

- Editing a form creates the next `FormVersion` instead of mutating the historical version used by existing submissions.
- The workflow attached to a `FormVersion` is the workflow new submissions use.
- Blank rows in the form builder are ignored.
- Options are only required for field types that actually use options.
- Select-style fields remain static-option driven for now.
- Table-backed user picker fields are intentionally deferred.
- Display conditions are intentionally simple in v1: field key plus matching value, with boolean-style values normalized for common `true` inputs.
- Workflow-step editability is also intentionally simple in v1: a field is editable on returned submissions when its configured step name is currently open.
- Workflow attachment remains feature-gated in code, but the current Community and Professional packaging includes workflow.

## Submissions

Submissions have become the intake queue.

Current submission statuses:

- `Submitted`
- `In Review`
- `Approved`
- `Returned`
- `Converted`

Submission details now include:

- Stable numeric submission number
- Status display
- Submission Workspace tabs for submitted form sections, linked conflict searches, and attachments
- Legacy submission details infer display-only sections when the historical form-version schema only contains the old single `General` section
- Sticky right-side activity/audit rail with submitter context, current workflow actions, compact workflow tasks, workflow history, recent audit events, and attachment summaries
- `Create Client/Matter` conversion action after approval
- Links to the created client and matter after conversion

Converted submissions are linked back to operational records through nullable `ClientId` and `MatterId` columns on `FormSubmissions`.

Submission numbers start at `00000001`, increment upward, and appear as the leading linked column in the Submissions queue.

The Submissions queue now has:

- `My Submissions`
- Admin/permission-gated `All Submissions`

Conversion is locked until a submission reaches `Approved`. The detail page disables the conversion action before approval, and the conversion page also enforces the same rule server-side.

## Submission Attachments

Submissions now support first-pass attachment management.

Current attachment capabilities:

- Upload private files to Azure Blob Storage.
- Store attachment metadata in Azure SQL through `SubmissionAttachments`.
- Link attachments to a specific `FormSubmission`.
- Download uploaded files through the app instead of exposing public blob URLs.
- Add external hyperlinks as submission attachments.
- Remove file or hyperlink attachments from the submission detail page.
- Track who added the attachment when a CMIForge user is available.

Current file rules:

- Maximum file size: 25 MB
- Allowed file extensions: `.pdf`, `.doc`, `.docx`, `.xls`, `.xlsx`
- Allowed hyperlink schemes: `http://` and `https://`

Current Azure storage configuration:

- Storage account: `cmiforgeattachasgmt7`
- Container: `submission-attachments`
- Public blob access: disabled
- Local development can use a connection string stored in .NET user secrets under `SubmissionAttachments:ConnectionString`
- The live dev App Service uses managed identity with `SubmissionAttachments:UseManagedIdentity=true`
- Managed identity storage is configured with `SubmissionAttachments:AccountName=cmiforgeattachasgmt7`
- Container name is stored under `SubmissionAttachments:ContainerName`
- Blob downloads flow through CMIForge permission checks rather than public blob URLs

Attachments live directly on the submission detail page:

```text
http://localhost:5153/Submissions/Details/{submissionId}
```

## Entity Management

CMIForge now has a first-class `Entities` area with sub-tabs for:

- Clients
- Matters
- Parties
- Contacts
- Users

Entity creation is separate from form submission until a submission is intentionally converted.

Clients, matters, parties, and users now support archive/unarchive behavior from the front end. Archived records are hidden from the default operational lists but remain available for search/history and can be restored.

Clients, matters, and parties now include conversation-style note threads on their detail pages, so reviewers can leave follow-up context directly on the operational record.

### Clients

Clients have:

- Stable numeric client number
- Display format like `00000001`
- Name
- Aliases for DBA names, prior names, abbreviations, and alternate spellings
- Status
- Primary contact
- Email
- Phone
- Notes
- Created/updated timestamps

Client numbers start at `00000001` and increment upward.

Client list and detail pages link to related matters, contacts, and parties connected through the client's matters.

Client aliases can be added during client creation and added, edited, or deleted later from the unified Aliases panel on client details by users with entity edit rights. The client list shows the alias count, alias text is searchable, and conflict searches score client aliases directly so DBA/prior-name checks do not depend only on client-party sync.

Direct-created clients default to `Compliance Review`. Moving a client from `Compliance Review` to `Active` requires change-request notes and, when approved, logs both the normal change approval and a dedicated compliance-reviewed audit entry.

### Matters

Matters have:

- Stable numeric matter number
- Display format like `00000001`
- Matter name
- Required client reference
- Practice area
- Status
- Opened date
- Optional responsible user
- Notes
- Created/updated timestamps

Matter numbers start at `00000001` and increment upward.

Matter detail pages link back to the client record.

Matter detail pages use the same detail/child layout as other entity screens. Core matter facts stay on the left, while time entries, linked parties, and linked contacts appear in the right-side related-record rail.

Direct-created matters default to `Compliance Review`. Moving a matter from `Compliance Review` to `Open` requires change-request notes and, when approved, logs both the normal change approval and a dedicated compliance-reviewed audit entry. The entity approval queue shows actual current and proposed values for changed fields so reviewers can approve the data change, not just the field names.

### Contacts

Contacts are first-class address-book records, separate from application users and conflict-search parties.

Contacts have:

- Stable numeric contact number
- Display format like `00000001`
- First, middle, and last name
- Organization
- Title
- Email
- Phone and mobile phone
- Mailing address
- Notes
- Client links with role, primary flag, and notes
- Matter links with role, primary flag, and notes

Contacts are for people who belong in the firm's operational address book but should not necessarily log into CMIForge.

Contact detail pages show linked clients, linked matters, and related parties connected through those matter links.

### Parties

Parties are first-class searchable conflict entities.

Parties have:

- Stable numeric party number
- Display format like `00000001`
- Name
- Normalized name
- Party type
- Status
- Notes
- Aliases
- Matter-role links
- Party-to-party relationships

Party aliases support alternate spellings, DBAs, former names, abbreviations, and other names that should match in a conflict search.

Party aliases can be added, edited, or deleted from the unified Aliases panel on party details by users with entity edit rights. Alias changes are normalized for searching and written to the audit log.

Party detail pages show matter roles, related contacts connected through those matters, and direct party-to-party relationships.

Direct-created parties default to `Compliance Review` and show detail-page guidance until conflicts/compliance checks have been completed. A full party status-change approval path is still a later hardening item.

Matter-party roles currently include:

- Client
- Adverse Party
- Related Party
- Witness
- Opposing Counsel
- Affiliate/Subsidiary
- Other

Party relationship types currently include:

- Parent
- Subsidiary
- Affiliate
- Acquired By
- Contact
- Related

### Users

Users are stored in the SQL table named `Users`, while the C# model remains `CMIForgeUser`.

Users have:

- Stable numeric system ID
- Display format like `00000001`
- First name
- Middle name
- Last name
- Display name
- Email
- Title/role
- Active flag
- Created/updated timestamps

User system IDs were added after users already existed, so the migration safely assigned existing users sequential IDs before enforcing uniqueness.

Users are now:

- Clickable from the Users list
- Viewable on a details page
- Editable from an edit page
- Linked from responsible matter relationships
- Linked to team memberships and direct security roles

## Submission Conversion

The current conversion flow is the bridge between intake submissions and operational records.

From a submission detail page:

1. Click `Create Client/Matter`.
2. CMIForge reads the submission JSON answers.
3. The conversion page pre-fills likely client and matter fields.
4. The user reviews and edits the proposed values.
5. CMIForge creates a client and matter.
6. CMIForge links the submission to both records.
7. The submission status changes to `Converted`.

Currently recognized prefill fields include common keys such as:

- `clientName`
- `matterName`
- `practiceArea`
- `summary`
- `matterSummary`
- `assignedUser`

The conversion flow has been tested against a real submitted form and created linked client/matter records successfully.

Conversion now also syncs the created client into the party index and links that party to the created matter as `Client`.

## Conflicts Slice

CMIForge now has the first native conflicts-search slice.

Current conflicts capabilities:

- `Conflicts` top navigation item.
- Conflict search list page.
- Conflict search create/run page.
- Conflict search create and re-run forms show a staged progress bar while the synchronous server search completes.
- Conflict search detail page.
- Reviewer decision capture.
- Conflict searches can be linked to submissions.
- Conflict searches can be linked to matters.
- Submission detail pages show linked conflict searches.
- Submission detail pages can start a conflict search from submitted answers.
- Matter detail pages can start a conflict search from matter context.
- Submission forms can show a floating live conflict preview while client or matter names are being typed.
- Live conflict preview can be enabled/disabled through `Conflicts.LivePreviewEnabled` in System Settings.
- Live conflict preview uses the same matcher but skips prior-history scanning so high-volume tenants get fast on-the-fly feedback while typing.
- Results can be filtered by clearance status, role/match context, and risk level.
- Results can be selected in bulk for row-level clearance updates.
- Results can be escalated individually or in bulk to another active firm user for approval, with escalation notes, assigned reviewer approval notes, and audit log entries on the conflict search.
- Cleared search history remains searchable through the archive/index design.
- Existing clients/matters are synced into parties during startup seeding.
- New converted clients/matters sync into parties during conversion.

Current conflicts data model:

- `Parties`
- `PartyAliases`
- `MatterParties`
- `PartyRelationships`
- `Conflicts`
- `ConflictsResults`
- `ConflictSearchDocuments`
- `ConflictsSearchArchives`
- `ConflictsHitArchives`

Current search behavior:

- Normalizes party/search names.
- Removes punctuation and common corporate suffixes such as LLC, Inc, Corp, LLP, Ltd, and similar.
- Searches party names.
- Searches party aliases.
- Searches client names and client aliases.
- Expands matches through party relationships.
- Includes matter/client context when a party is linked to matters.
- Searches prior conflict search names, terms, review notes, and AI summaries.
- Searches prior conflict result clearance notes.
- Uses a denormalized `ConflictSearchDocuments` table with Azure SQL full-text search as a candidate finder when SQL Server full-text is available, then applies the existing CMIForge scorer to the narrowed candidates.
- Falls back to the original in-memory scan/scoring path when full-text search is unavailable, such as local in-memory test runs.
- Labels string similarity as match strength, separate from legal/contextual risk.
- Scores exact normalized matches at the top of the scale.
- Scores contains/full-phrase matches based on token coverage.
- Scores token overlap proportionally.
- Scores fuzzy trigram similarity and edit-distance similarity proportionally.
- Applies boosts for adverse/opposing-counsel style roles and linked matter/client context.
- Produces risk labels: Low, Medium, High, Critical.
- Produces per-result explanations.
- Tracks per-result clearance status, notes, reviewer, and timestamp.
- Tracks per-result escalation assignee, escalation notes, approval notes, and approval timestamps.

Current AI behavior:

- The app generates an `AI Assist Summary` for each conflict search.
- Each result gets an `AI assessment` explaining why it deserves reviewer attention.
- This is currently deterministic/heuristic and does not call an external LLM yet.
- The code is shaped so a real LLM-backed assistant can later replace or augment the deterministic explanation layer.

Conflict reviewer decisions:

- Pending
- Clear
- Potential Conflict
- Conflict
- Needs Info

Conflict decisions can be captured at the overall search level or on individual result rows. Row-level decisions roll up into the search status when the results collectively indicate clear, needs-info, potential-conflict, or conflict outcomes. Result rows can also be escalated to another active user for approval; the escalation does not overwrite clearance status, but records who escalated it, who it was assigned to, notes, approval notes, timestamps, and audit log history.

Conflict search pages live at:

```text
http://localhost:5153/Conflicts
http://localhost:5153/Conflicts/Create
```

## Imports/Exports

CMIForge now has an `Imports/Exports` area for CSV-based data movement. This is the practical "your data" workflow: bring an existing firm's operational data into the app, and download customer-owned records back out in predictable batches.

Current import types:

- Clients
- Matters
- Parties

Each import card now includes:

- Download Template
- Validate CSV
- Import CSV

Export cards are available for:

- Clients
- Matters
- Parties
- Contacts
- Users
- Time entries

Each export downloads one CSV batch at a time. The current fixed batch size is `1,000` rows and is shown under Settings as a CMIForge-controlled read-only value.

Validation mode uses the same parser and row checks as import mode, but it does not create or update clients, matters, parties, or matter-party links. It stores a validation batch with row-level messages so users can see issues before loading data.

Client imports:

- Upsert by `ClientNumber`
- Required columns: `ClientNumber`, `ClientName`
- Optional columns: `Status`, `PrimaryContact`, `Email`, `Phone`

Matter imports:

- Upsert by `MatterNumber`
- Required columns: `MatterNumber`, `MatterName`, `ClientNumber`
- Optional columns: `Status`, `ResponsibleAttorney`, `PracticeArea`
- The referenced client must already exist.
- `ResponsibleAttorney` can match an active user by display name, first/last name, or email.

Party imports:

- Link parties to existing matters by `MatterNumber`
- Required columns: `PartyName`, `Role`, `MatterNumber`
- Recognized party role values include client, adverse party, related party, witness, opposing counsel, affiliate/subsidiary, and other.
- Existing parties are reused by normalized name.
- New parties are added to the conflicts party index.
- Existing matter-party links are skipped instead of duplicated.

Each import creates:

- An `ImportBatch` summary row
- `ImportBatchRows` with row number, status, message, and source JSON
- Counts for imported, updated, skipped, and errored rows

Validation batches use the same tables and show counts for would-import, would-update, would-skip, and error rows.

Photo OCR import now supports a client-image workflow:

- Upload or drag a client photo/scan.
- Run in-browser OCR using the bundled open-source Tesseract worker.
- Review the recognized text.
- Let CMIForge draft likely client fields.
- Create the client only after user review.

The Imports/Exports center is permission-gated through:

- `Imports.View`
- `Imports.Run`

The Imports/Exports center lives at:

```text
http://localhost:5153/Imports
```

## Seed Data

The app seeds:

- Starter user:
  - `Ima User`
  - `imauser@twodumbdogs.com`
  - `Administrator`
- Starter form:
  - `New Matter Intake`
  - Includes client name, matter name, practice area, estimated fees, assigned user, and matter summary fields
- Demo conflict-search data:
  - `Arcadia Sample Holdings`
  - `Demo Conflicts - Legacy Supply Dispute`
  - Similar organizations such as `Stark & Stone Holdings LLC` and `Stark Stone LLP`
  - Alias-heavy organizations such as `Globex BioSystems North America LLC`, `Umbrella Risk Group plc`, and `Acme Anvil Works, Inc.`
  - Individuals such as `Mina Q. Caldera` and `Jonas van der Meer`
  - Relationships between demo parties for relationship-expansion testing

Seed data only fills gaps. It does not overwrite existing Azure SQL records.

Customer 0 additionally has a realistic generated volume-test dataset for performance and search testing:

- 700 generated active users with realistic names and no Entra IDs.
- 10,000 generated client records with mixed company and individual names.
- 10,000 generated party records with organization, individual, and government names.
- 20,000 generated matter records with realistic matter names and practice areas.
- 19,600 generated matter-party links for relationship and search-load testing.
- 1,000 volume-test submissions with `"volumeTest": true` in the submission JSON.
- 300 open volume workflow tasks.
- `tools/seed-customer0-big-volume-data.ps1 -AllowTemporarySqlPublicAccess` verifies the large generated dataset without adding rows; adding `-Apply` tops up Customer 0 to the target counts.

## Current Verification

Recent verification included:

```powershell
dotnet build
dotnet tool run dotnet-ef database update
dotnet tool run dotnet-ef migrations has-pending-model-changes --no-build
```

Also verified in the running app:

- Forms load.
- Submissions load.
- Submission details load.
- Submission status changes persist.
- Submission conversion creates client and matter records.
- Converted submissions link to the created client and matter.
- Clients, matters, contacts, and users list/detail pages load.
- User edit saves successfully.
- Users show first/last name columns.
- Dashboard shows tenant branding, role-oriented dashboard views, counts, charts, workflow load, conflict mix, and plan usage meters.
- Plan page shows Community, Professional, and Enterprise tiers.
- Parties show seeded conflict-test records.
- A rich conflict search against `Stark Stone`, `Globex Bio Systems`, and `Mina Caldera` produced multiple Critical/Medium hits with AI assist and relationship expansion.
- The original CSV import migration applied to Azure SQL.
- Import template downloads and validation-only CSV checks were added for clients, matters, and parties.
- Azure SQL contains the expected schema and records.
- The `AddSubmissionAttachments` migration applied to Azure SQL.
- Azure Blob attachment configuration was stored in user secrets.
- A smoke harness verified PDF upload to Blob Storage, SQL metadata save, Blob download, Blob delete, and SQL cleanup.
- The submission details page renders the attachment panel with optional display names and constrained file types.
- Managed-identity Blob upload/download/delete was verified in the live dev App Service.
- Storage account keys were rotated after managed-identity attachment access was verified.
- Public blob access is disabled.
- Blob soft delete is enabled.
- App Service FTP/SCM basic publishing credentials are disabled.
- The public marketing site was deployed to Azure Static Web Apps and verified at `https://cmiforge.com`.
- The public site now presents the current Community/Professional/Enterprise packaging, live demo link, support contact, and pain-focused platform messaging.

## Dashboard Slice

The dashboard now acts as a quick operational cockpit rather than a plain counter page.

Current dashboard capabilities:

- Tenant-branded header using the configured firm/customer display name.
- Dashboard selector with Firm Admin, My Work, and Matter Partner views.
- Dashboard selector only shows views the current user is allowed to use.
- Dashboard visibility grants can target users, teams, or roles from `Security -> Dashboard access`.
- A dashboard with no grants remains broadly visible; a dashboard with grants is restricted to matching users, team members, role holders, and system admins.
- Top-level counts for forms, submissions, published versions, and recorded hours.
- Submission-status donut chart.
- Conflict-status donut chart.
- Open workflow task bars grouped by workflow step.
- Last-14-days submission trend bars.
- Plan usage meters for users, matters, and clients.
- My Work view for assigned tasks, team queue counts, personal submissions, personal time, and conflict escalations assigned to the signed-in user.
- Matter Partner view for lead partner matters, submitted time awaiting approval, pending conflict results, and partner-related submissions.

## Workflow Slice

CMIForge now has the first native workflow, notification, and approval queue slice.

Current workflow capabilities:

- Workflow definitions can be created and edited in the app.
- Workflow definitions can be saved as unpublished drafts before they are ready to go live, and publish attempts that are not ready preserve the latest edits as an unpublished draft.
- Workflow definitions can be copied into unpublished drafts and then renamed, adjusted, reordered, and published when ready.
- Only active published workflow definitions are available for new form attachments and automatic submission starts.
- Workflow definitions can be used as a global fallback or scoped to a form definition.
- Workflow definitions can be attached directly to published form versions.
- Workflow definitions have ordered workflow steps.
- Workflow steps can be moved up or down in the designer before saving or publishing.
- Workflow steps can be created and edited in the app.
- Workflow steps can be typed as `Approval` or `Notification`.
- Workflow steps can be assigned to a CMIForge user.
- Workflow steps can be assigned to a team queue.
- Form submissions automatically start the workflow attached to their form version.
- External client form completions create normal form submissions and then start the attached workflow the same way internal submissions do.
- Workflow instances create open workflow tasks.
- Workflow tasks carry user and/or team assignment.
- The Workflow queue has `My Queue`, `Team Queue`, and admin-only `All Open` views.
- Queue users can choose configured outcomes for a task.
- Submission detail pages show current workflow action buttons for open tasks.
- Outcomes can advance, complete, or return a workflow.
- Outcomes can set the submission status.
- Outcomes can optionally route to a specific next step number.
- Completing the final matching task marks the workflow instance complete.
- Returned outcomes mark the submission returned.
- Workflow steps can include routing conditions based on submitted form answers.
- Routing conditions support always, equals, not equals, contains, present, and blank checks.
- Notification steps resolve recipients from `assigned`, `submitter`, or literal email addresses.
- Notification steps support subject/body templates with submission and workflow tokens.
- Notification steps continue automatically to the next matching step after sending or logging the notification event.
- If SMTP is disabled or incomplete, the notification step records a skipped/failed workflow event instead of blocking workflow progress.
- Workflow events preserve history on the submission.
- Submission detail pages expose a compact workflow rail with Open Actions pinned at the top and task/history detail ordered newest-first next to the intake data.
- Returned-submission editing respects field-level workflow-step edit rules; fields outside the active step stay read-only and are preserved server-side even if a browser posts a changed value.
- New submissions for the starter intake form automatically start the seeded workflow.
- Older unconverted submissions can still start workflow from the submission detail page when no instance exists.
- Existing open workflow tasks are refreshed from their step assignment during startup seeding.
- Form and workflow designers now share a cleaner table style.
- Workflow designer rows include outcomes and answer-based routing fields.
- Workflow designer rows include notification fields for recipients, subject, and body.
- Workflow create/edit designer rows now treat optional step cells as optional, so blank extra rows do not block saving.

Seeded starter workflow:

- `Standard Intake Review`
- Step 1: `Intake Review`
- Step 2: `Final Approval`

The starter `Intake Review` step is assigned to the seeded `Intake Team`. The starter `Final Approval` step remains assigned directly to Gabe.

The workflow queue lives at:

```text
http://localhost:5153/Workflow/Queue
```

Workflow definitions are managed at:

```text
http://localhost:5153/Workflow/Definitions
```

Workflow outcome designer format:

```text
Label|Action|Status|Next step
```

Examples:

```text
Approve|Advance|In Review
Final approve|Complete|Approved
Needs CDD|Advance|In Review|3
Return|Return|Returned
```

## Security Slice

CMIForge now has the first internal security and assignment layer.

Current security capabilities:

- Teams/groups are first-class records.
- Users can belong to teams.
- Teams can carry roles.
- Users can carry direct roles.
- Roles contain granular permissions.
- Dashboard visibility can be granted to users, teams, or roles.
- The main navigation hides form/workflow/entity/security areas when the current user lacks permission.
- Designer/admin pages also enforce server-side permission checks.
- Workflow queue task actions verify that the current user is allowed to act on the task.
- `System.ImpersonateUsers` lets authorized admins temporarily view CMIForge as another active user for support, workflow-routing, dashboard, and approval testing.
- Impersonation is session-based, shows a visible banner, can be stopped from the banner or Security area, and writes start/stop actions to the audit log.
- Audit logging keeps the actual signed-in user as the actor and includes the impersonated/effective user in audit details while impersonation is active.

Current seeded teams:

- `Admins`
- `Intake Team`
- `CDD Team`
- `Partner Approvers`

Current seeded roles:

- `Administrator`
- `Workflow Designer`
- `Intake Reviewer`
- `Entity Manager`
- `Submitter`

Current seeded permissions include:

- Form view/submit/design permissions
- Submission own/all/approve/convert permissions
- Workflow queue/all-queue/design permissions
- Entity view/create/edit permissions
- Conflict view/run/review permissions
- Security management permission

System user `00000001` is seeded as:

- Direct `Administrator`
- Member of `Admins`
- Member of `Intake Team`
- Member of `Partner Approvers`

The current-user abstraction defaults to `imauser@twodumbdogs.com` / `Ima User` while Microsoft Entra ID login is disabled for local prototype work. In live demo mode, user `00000001` is protected from profile and team-membership changes.

## Entra Login Slice

CMIForge now has optional Microsoft Entra ID authentication plumbing.

Current behavior:

- Entra login is controlled by `Authentication:Microsoft:Enabled`.
- Local development remains unchanged while Entra login is disabled.
- When enabled, the app uses OpenID Connect against Microsoft identity platform.
- The current user is resolved from authenticated email-style claims such as `preferred_username`, `upn`, or email.
- Authenticated Entra users are mapped to rows in the `Users` table.
- New authenticated users are auto-created as active CMIForge users with the next system ID.
- Permissions still come from CMIForge roles and teams, so a newly auto-created user starts with no magic admin access.
- The layout shows sign-in/sign-out controls only when Entra login is enabled.

Config shape:

```json
"Authentication": {
  "Microsoft": {
    "Enabled": false,
    "TenantId": "",
    "ClientId": "",
    "ClientSecret": "",
    "CallbackPath": "/signin-oidc"
  }
}
```

For Azure App Registration later, the local redirect URI should match:

```text
http://localhost:5153/signin-oidc
```

Security pages live at:

```text
http://localhost:5153/Security
http://localhost:5153/Security/Teams
http://localhost:5153/Security/Dashboards
http://localhost:5153/Security/Impersonation
http://localhost:5153/Security/Audit
```

## Time Recording and Reporting Slice

CMIForge now includes first-pass time recording, built-in operational reporting, and a basic report builder.

Time recording adds:

- `TimeEntries` with an 8-digit `TimeEntryNumber`
- Required links to user, client, and matter
- Work date, minutes, client narrative, internal notes, billable flag, phase/task codes, status, approval metadata, export metadata, and timestamps
- Statuses: Draft, Submitted, Approved
- Matter-level `RequiresTimeApproval`, time increment override, and assigned time code set
- System default time increment setting with actual-minute, 6-minute, and 15-minute options
- Submitted entries auto-approve when matter approval is not required
- Submitted entries wait for lead-partner approval when matter approval is required
- Approved and exported entries are locked from normal editing
- CSV export uses client narrative and stamps approved unexported entries as exported
- Starter UTBMS-style time code set with reusable phases and tasks
- Record Time screen with a local start/stop timer
- Top-level Time navigation
- Time list with filters and CSV export
- Record Time form
- Edit Time form for non-locked entries
- Time detail page
- Matter detail time section with a Record Time action

Reporting adds:

- Top-level Reports navigation
- Built-in Intake Pipeline report
- Built-in Approval Queue Aging report
- Built-in Conflicts Review report
- Built-in Matter Roster report
- Built-in Time Detail report
- Basic report builder with dataset selection, field selection, up to three simple filters, and CSV export
- Builder datasets for submissions, workflow tasks, conflicts, matters, time entries, clients, and contacts

New permissions:

- `Time.ViewOwn`
- `Time.ViewAll`
- `Time.Create`
- `Time.Edit`
- `Time.Approve`
- `Reporting.View`

The Professional plan remains hardcoded as the active development plan. The product ladder now models Community as free, Professional at `$149/month`, and Enterprise at `$599/month` for 1,000 users, unlimited clients, unlimited matters, and customer SQL data access.

## Security Hardening Slice

CMIForge now has the first low-cost SaaS hardening pass:

- Entra-required app mode is supported when Microsoft authentication is enabled and demo mode is off.
- The live demo can still use the configured `Ima User` context while `CMIForge:DemoMode` is enabled.
- Submission attachments can use Azure Blob Storage through App Service managed identity instead of a storage connection string.
- Blob containers are created with private access only, and file downloads continue to flow through the app permission checks.
- App Service basic FTP/SCM publishing credentials are disabled in the dev Azure app.
- The dev attachment storage account has public blob access disabled and blob soft delete enabled.
- Storage account keys were rotated after managed-identity upload/download was verified.
- Security headers are emitted for content type sniffing, framing, referrer behavior, browser permissions, and content security policy.
- `AuditLogs` records admin/destructive activity such as user edits, team/role changes, workflow/form designer changes, submission status changes, conversion, and attachment activity.
- Visible timestamps now render through the user's browser timezone when available, falling back to the configured default timezone.

The next infrastructure-hardening path should prioritize:

- Moving app secrets and Graph credentials into Key Vault or managed identity patterns instead of long-lived app settings.
- Keeping every tenant on private Blob containers with downloads mediated by app authorization.
- Using least-privilege SQL users/roles and narrowly scoped Microsoft Graph application access.
- Keeping live web app identities out of schema ownership; EF schema changes belong to the dedicated migrator identity/WebJob.
- Enabling Defender for Cloud / Defender for Storage / Defender for SQL alerts as cost allows.
- Adding explicit backup, restore, retention, and customer export/delete runbooks before onboarding outside customers.
- Private endpoints/VNet integration are now in place for SQL, Blob, and Key Vault. The next step is moving secrets into Key Vault references and deciding whether future tenants share this private endpoint set or get dedicated storage/account boundaries.

## System Settings Slice

CMIForge now has a first-pass `System -> Settings` area for admin-editable operational configuration.

Current settings behavior:

- Settings are stored in the `SystemSettings` table.
- Settings are grouped by category, including `Data Access`, `Conflicts`, `Address Lookup`, `Email`, and `Inbound Email`.
- Settings support value types for text, integer, boolean, email, and secret reference values.
- Secret-style settings are masked in the UI. The current implementation stores a secret reference/name rather than the raw secret value.
- Read-only data access settings show the CMIForge-controlled CSV export batch size and Enterprise-only SQL data access values.
- Settings updates are audit logged through `AuditLogs`.
- Settings are permission-gated through the existing `Security.Manage` permission for this first slice.
- Settings are hidden from the System menu in demo mode.
- Direct access to the Settings page in demo mode is read-only and server-side save attempts are blocked.

Initial seeded settings include:

- CSV export batch size
- Customer SQL access availability
- Customer SQL Entra principal
- Customer SQL connection string
- Support email
- Default timezone fallback
- Email notifications enabled flag
- Inbound email intake enabled flag
- Inbound mailbox anchor, tenant inbound address, allowed sender domains, default form key, and mark-as-read behavior
- SMTP host
- SMTP port
- SMTP SSL/TLS flag
- SMTP username
- SMTP password Key Vault secret reference
- From email
- From display name

Identity-management note:

- Microsoft Entra should own password resets, account verification, MFA, lockout, and sign-in policy.
- CMIForge owns app-level authorization, roles, teams, permissions, and operational settings.

## Near-Term Next Steps

The next major product slice should make workflow routing smarter now that workflow definitions are configurable in the app.

Recommended next sequence:

1. Add editable role/permission assignment screens beyond seeded defaults.
2. Add manual party dedupe/merge tools for conflicts data hygiene.
3. Add a real LLM-backed conflict narrative provider behind the current AI assist seam.
4. Add in-app notification badges and notification history views on top of the workflow notification events.
5. Add workflow versioning once workflows are used by enough historical submissions.
6. Continue improving designer UX beyond the current form copy/versioning and workflow draft/copy/reorder controls, especially richer inline validation and less table-like editing for complex workflows.
7. Add field-level permissions and better admin guardrails for non-admin users.

Good first workflow tables might be:

- `WorkflowDefinitions`
- `WorkflowSteps`
- `SubmissionWorkflowInstances`
- `SubmissionWorkflowTasks`
- `SubmissionWorkflowEvents`

The key design choice is that submissions should move through workflow before conversion, while converted records should preserve their link back to the original intake and approval history.

## Product Notes

CMIForge is now in a local prototype shape with the important spine:

- Configurable intake forms
- Secure external form invites with recent-send tracking, secure-link open/completion visibility, and one-click resend
- Versioned submissions
- Operational clients/matters/users
- Conversion from intake to records
- Early workflow status handling
- Personal and team workflow queues
- Saved workflow drafts, published workflow versions, copy-to-new workflows, and step move controls
- Workflow notification steps with email/event logging behavior
- Granular role/permission foundation
- Optional Entra-backed login
- Parties, aliases, relationships, and conflict searches
- Floating live conflict preview on submission forms
- Time recording tied to users, clients, and matters
- Built-in operational reports and a basic report builder
- Deterministic conflict scoring with AI-style explanations
- Match-strength scoring separated from legal/contextual risk labels
- Searchable conflict archive/index with compressed full-detail payloads
- CSV import center for clients, matters, and parties
- Client photo OCR import drafting
- Private submission attachments through Azure Blob Storage
- Managed-identity attachment storage support
- Audit log foundation for admin and destructive actions
- Admin-editable system settings with demo-mode protection
- Browser-local timezone display for visible timestamps
- Seeded conflict-test parties, aliases, relationships, and matter roles
- Hardcoded Community/Professional/Enterprise product-plan limiter
- Archive/unarchive support for clients, matters, parties, and users
- Compact conversation-style notes for clients, matters, and parties, including impersonation attribution and author deletion
- My/all submission views
- Approval-gated conversion
- Matter partner time approval actions from dashboards and time lists
- Demo reset controls with scheduled catch-up behavior
- Configurable workflow outcomes
- Answer-based workflow routing
- Azure SQL persistence

The next work should deepen workflow behavior without losing the simple, inspectable data model that has made the prototype easy to evolve.
