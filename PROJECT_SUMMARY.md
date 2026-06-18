# CMIForge Project Summary

CMIForge is a homegrown ASP.NET Core Razor Pages prototype for configurable legal intake, entity management, workflow automation, and conflict searches. The long-term idea is a law-firm intake/workflow platform in the spirit of tools like Intapp Open, but built in focused slices so the data model and user experience can grow together.

Current version: `20260617.2`.

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
- Namecheap Private Email MX/SPF/autodiscover records until Microsoft 365 mail is fully licensed and cut over.

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
- Bootstrap admin configured through `MatterForge:BootstrapAdminEmail`.

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
- `System -> Signup Requests` for admin triage of new, contacted, provisioning, ready, and closed requests.
- `System -> Onboarding` for a first-customer checklist covering Entra login, Entra provisioning, support contact, admins, users, teams, forms, workflows, clients, matters, contacts, parties, conflicts, imports, time, attachments, and settings review.
- The public marketing site now links request-access CTAs to the Customer 0 `/Signup` page so real requests land outside the disposable demo database.

Tracked Customer 0 files:

- `deploy/customer0/deploy-customer0.ps1`
- `deploy/customer0/appservice-settings.sample.json`
- `deploy/customer0/ENTRA_CHECKLIST.md`
- `deploy/customer0/README.md`

The app now supports `MatterForge:BootstrapAdminEmail`. When Microsoft Entra login is enabled and demo mode is off, a matching authenticated user is activated if needed and granted the seeded `Administrator` role. This prevents a fresh tenant from being easy to lock yourself out of.

Startup seeding is now split between core platform seed data and sample/demo seed data:

- `MatterForge:RunSeedDataOnStartup=true` allows startup seed execution.
- `MatterForge:SeedSampleData=false` seeds core platform structure without public-demo filler data.
- `MatterForge:SeedSampleData=true` keeps local/demo-style sample data available when needed.

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

Azure DNS currently carries Namecheap Private Email records for `cmiforge.com`:

- `MX @ -> mx1.privateemail.com`
- `MX @ -> mx2.privateemail.com`
- `TXT @ -> v=spf1 include:spf.privateemail.com ~all`
- `mail`, `autodiscover`, and `autoconfig` CNAMEs point to `privateemail.com`

The planned Microsoft 365 shape is:

- `gabe@cmiforge.com` as the licensed user mailbox.
- `support@cmiforge.com` as a shared mailbox delegated to `gabe@cmiforge.com`.

After a Microsoft 365 Business Basic or Exchange Online license is purchased and assigned, the Microsoft admin center should provide the Exchange DNS records. At that point Azure DNS needs to be updated from Namecheap Private Email records to the Microsoft 365 Exchange records.

## Public Demo Safety

The live demo now has dedicated guardrails for public visitors:

- `MatterForge:DemoMode=true` enables demo behavior.
- `MatterForge:DemoResetEnabled=true` enables scheduled demo resets.
- `MatterForge:DemoResetIntervalHours=12` configures the reset interval.
- A visible banner appears at the top of every page explaining public demo behavior.
- `/System/Demo` shows demo behavior and exposes a manual `Reset demo now` button.
- Manual reset clears demo-created records and reruns starter seed data.
- Scheduled reset runs every 12 hours while the App Service process is awake.
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
- Conflicts search behavior, prior-history matching, and result-level clearance
- CSV imports and validation mode
- Teams, roles, permissions, and optional Entra login
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

The current Azure SQL database has the full entity, workflow, conflicts, import, attachment, time/reporting, and audit-log migration chain applied through `AddAuditLogsAndSecurityHardening`.

## Plan Limiter

CMIForge now has a hardcoded plan limiter. This is not a payment system yet; it is a product-gating layer that gives the app realistic tier behavior while billing is deferred.

Current hardcoded plan: `Professional`, so development can continue with the paid-plan limits and all features enabled.

Current plan tiers:

- `Community`
  - Free
  - 3 users
  - 50 matters
  - 50 clients
  - All features
  - Community support
- `Professional`
  - `$99/month`
  - Includes 10 users
  - 500 matters
  - 500 clients
  - All features
  - Email support
  - Additional users at `$10/user/month`
- `Enterprise`
  - Coming soon

Current enforcement:

- User creation is blocked when the current plan reaches its user limit.
- Matter creation is blocked when the current plan reaches its matter limit.
- Submission-to-client/matter conversion also respects the matter limit.
- Feature gates still exist in code, but the current Community and Professional packaging includes all features. The practical plan limits are user, matter, and client counts.

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
- Workflow outcome buttons for current open tasks
- Submitted answer display
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

### Clients

Clients have:

- Stable numeric client number
- Display format like `00000001`
- Name
- Status
- Primary contact
- Email
- Phone
- Notes
- Created/updated timestamps

Client numbers start at `00000001` and increment upward.

Client list and detail pages link to related matters.

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

Matter detail pages also show linked parties and their roles.

Matter detail pages also show linked contacts and their matter-specific roles.

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

Users are stored in the SQL table named `Users`, while the C# model remains `MatterForgeUser`.

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
- Conflict search detail page.
- Reviewer decision capture.
- Conflict searches can be linked to submissions.
- Conflict searches can be linked to matters.
- Submission detail pages show linked conflict searches.
- Submission detail pages can start a conflict search from submitted answers.
- Matter detail pages can start a conflict search from matter context.
- Existing clients/matters are synced into parties during startup seeding.
- New converted clients/matters sync into parties during conversion.

Current conflicts data model:

- `Parties`
- `PartyAliases`
- `MatterParties`
- `PartyRelationships`
- `Conflicts`
- `ConflictsResults`

Current search behavior:

- Normalizes party/search names.
- Removes punctuation and common corporate suffixes such as LLC, Inc, Corp, LLP, Ltd, and similar.
- Searches party names.
- Searches party aliases.
- Expands matches through party relationships.
- Includes matter/client context when a party is linked to matters.
- Searches prior conflict search names, terms, review notes, and AI summaries.
- Searches prior conflict result clearance notes.
- Scores exact normalized matches.
- Scores contains/token matches.
- Scores fuzzy trigram similarity.
- Scores edit-distance similarity.
- Produces risk labels: Low, Medium, High, Critical.
- Produces per-result explanations.
- Tracks per-result clearance status, notes, reviewer, and timestamp.

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

Conflict decisions can be captured at the overall search level or on individual result rows. Row-level decisions roll up into the search status when the results collectively indicate clear, needs-info, potential-conflict, or conflict outcomes.

Conflict search pages live at:

```text
http://localhost:5153/Conflicts
http://localhost:5153/Conflicts/Create
```

## Import Center

CMIForge now has an `Imports` area for CSV-based data loading. This is the first pass at the practical "Drag CSV Here" workflow for bringing an existing firm's operational data into the app.

Current import types:

- Clients
- Matters
- Parties

Each import card now includes:

- Download Template
- Validate CSV
- Import CSV

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

The Import Center is permission-gated through:

- `Imports.View`
- `Imports.Run`

The Import Center lives at:

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
- Dashboard shows version `20260617.2`.
- Plan page shows Community, Professional, and Enterprise tiers.
- Parties show seeded conflict-test records.
- A rich conflict search against `Stark Stone`, `Globex Bio Systems`, and `Mina Caldera` produced multiple Critical/Medium hits with AI assist and relationship expansion.
- The Import Center migration applied to Azure SQL.
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

## Workflow Slice

CMIForge now has the first native workflow and approval queue slice.

Current workflow capabilities:

- Workflow definitions can be created and edited in the app.
- Workflow definitions can be used as a global fallback or scoped to a form definition.
- Workflow definitions can be attached directly to published form versions.
- Workflow definitions have ordered workflow steps.
- Workflow steps can be created and edited in the app.
- Workflow steps can be assigned to a CMIForge user.
- Workflow steps can be assigned to a team queue.
- Form submissions automatically start the workflow attached to their form version.
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
- Workflow events preserve history on the submission.
- New submissions for the starter intake form automatically start the seeded workflow.
- Older unconverted submissions can still start workflow from the submission detail page when no instance exists.
- Existing open workflow tasks are refreshed from their step assignment during startup seeding.
- Form and workflow designers now share a cleaner table style.
- Workflow designer rows include outcomes and answer-based routing fields.
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
- The main navigation hides form/workflow/entity/security areas when the current user lacks permission.
- Designer/admin pages also enforce server-side permission checks.
- Workflow queue task actions verify that the current user is allowed to act on the task.

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
```

## Time Recording and Reporting Slice

CMIForge now includes first-pass time recording, built-in operational reporting, and a basic report builder.

Time recording adds:

- `TimeEntries` with an 8-digit `TimeEntryNumber`
- Required links to user, client, and matter
- Work date, minutes, narrative, billable flag, status, and timestamps
- Statuses: Draft, Submitted, Approved, Billed, No Charge
- Top-level Time navigation
- Time list with filters and CSV export
- Record Time form
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

The Professional plan remains hardcoded as the active development plan. The product ladder now models Community as free, Professional at `$99/month`, additional users at `$10/user/month`, and Enterprise as coming soon.

## Security Hardening Slice

CMIForge now has the first low-cost SaaS hardening pass:

- Entra-required app mode is supported when Microsoft authentication is enabled and demo mode is off.
- The live demo can still use the configured `Ima User` context while `MatterForge:DemoMode` is enabled.
- Submission attachments can use Azure Blob Storage through App Service managed identity instead of a storage connection string.
- Blob containers are created with private access only, and file downloads continue to flow through the app permission checks.
- App Service basic FTP/SCM publishing credentials are disabled in the dev Azure app.
- The dev attachment storage account has public blob access disabled and blob soft delete enabled.
- Storage account keys were rotated after managed-identity upload/download was verified.
- Security headers are emitted for content type sniffing, framing, referrer behavior, browser permissions, and content security policy.
- `AuditLogs` records admin/destructive activity such as user edits, team/role changes, workflow/form designer changes, submission status changes, conversion, and attachment activity.
- Visible timestamps now render through the user's browser timezone when available, falling back to the configured default timezone.

## System Settings Slice

CMIForge now has a first-pass `System -> Settings` area for admin-editable operational configuration.

Current settings behavior:

- Settings are stored in the `SystemSettings` table.
- Settings are grouped by category, starting with `General` and `Email`.
- Settings support value types for text, integer, boolean, email, and secret reference values.
- Secret-style settings are masked in the UI. The current implementation stores a secret reference/name rather than the raw secret value.
- Settings updates are audit logged through `AuditLogs`.
- Settings are permission-gated through the existing `Security.Manage` permission for this first slice.
- Settings are hidden from the System menu in demo mode.
- Direct access to the Settings page in demo mode is read-only and server-side save attempts are blocked.

Initial seeded settings include:

- Support email
- Default timezone fallback
- Email notifications enabled flag
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
4. Add email/in-app notifications for new tasks and returned submissions.
5. Add workflow versioning once workflows are used by enough historical submissions.
6. Add a richer designer UX with add/remove/reorder rows instead of fixed blank rows.
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
- Versioned submissions
- Operational clients/matters/users
- Conversion from intake to records
- Early workflow status handling
- Personal and team workflow queues
- Granular role/permission foundation
- Optional Entra-backed login
- Parties, aliases, relationships, and conflict searches
- Time recording tied to users, clients, and matters
- Built-in operational reports and a basic report builder
- Deterministic conflict scoring with AI-style explanations
- CSV import center for clients, matters, and parties
- Private submission attachments through Azure Blob Storage
- Managed-identity attachment storage support
- Audit log foundation for admin and destructive actions
- Admin-editable system settings with demo-mode protection
- Browser-local timezone display for visible timestamps
- Seeded conflict-test parties, aliases, relationships, and matter roles
- Hardcoded Community/Professional/Enterprise product-plan limiter
- My/all submission views
- Approval-gated conversion
- Configurable workflow outcomes
- Answer-based workflow routing
- Azure SQL persistence

The next work should deepen workflow behavior without losing the simple, inspectable data model that has made the prototype easy to evolve.
