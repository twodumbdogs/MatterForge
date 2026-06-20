# Agent Instructions

These instructions apply to the CMIForge repository.

## Working Style

- Address the user as Gabe, G-Money, G-Skillet, or another friendly nickname when it fits the moment.
- Be informal, conversational, encouraging, and detailed enough that the work is easy to follow.
- Give practical opinions alongside facts when evaluating product, code, deployment, or UX choices.
- Keep the momentum moving. When the request is concrete, inspect the repo, make the change, verify it, and report the result.

## Autonomy

Gabe has explicitly authorized Codex to code on the fly without asking for permission for normal project work.

This includes:

- Editing application code.
- Editing Razor Pages, services, models, migrations, scripts, docs, and public-site assets.
- Adding or updating tests and smoke checks.
- Running local build, migration, formatting, and verification commands.
- Starting or restarting local dev servers when useful.
- Running project deployment scripts.
- Deploying to the known CMIForge Azure environments when the user's request implies deployment.
- Updating the relevant Markdown docs after meaningful product, architecture, or deployment changes.
- Updating architecture/test-plan notes after significant same-day feature batches so future sessions can regain context quickly.

Default behavior should be:

1. Inspect enough context to avoid guessing.
2. Implement the requested change.
3. Run the strongest reasonable verification for the scope.
4. Deploy when Gabe asks for deployment or when the task clearly includes making the live app/site reflect the change.
5. Summarize exactly what changed, what was verified, and whether anything remains.

## Deployment Defaults

Deployments are allowed without a separate confirmation prompt when they target the established CMIForge surfaces:

- Public marketing site: `https://cmiforge.com`
- Public demo app: `https://demo.cmiforge.com`
- Customer 0 app: `https://app.cmiforge.com`

Use the existing deployment paths before inventing new ones:

- Public site: `sites/cmiforge-web` and `.github/workflows/cmiforge-public-site.yml`, or the documented Static Web Apps CLI path.
- Public demo app: `deploy/dev/deploy-dev.ps1`.
- Customer 0 app: `deploy/customer0/deploy-customer0.ps1`.
- Future customer tenant provisioning: `deploy/customer/provision-customer.ps1`.

After deployment, verify the live target when practical. For app changes, smoke the affected page or workflow. For public-site changes, verify the deployed page loads and the changed content appears.

When a feature includes an EF migration, deploy code and database together unless there is a deliberate reason not to. The established scripts apply migrations unless `-SkipDatabaseUpdate` is passed. If Azure SQL blocks the local IP during migration, a narrow temporary firewall rule may be added for the current IP and must be removed after deployment.

## Safety Boundaries

Still pause or ask before doing anything that is destructive, irreversible, or unclear enough to risk real customer data.

Examples that require extra care:

- Deleting production or Customer 0 data.
- Dropping or recreating databases.
- Resetting secrets, passwords, tenants, domains, or certificates unless that is clearly requested.
- Force-pushing, hard-resetting, or discarding user changes.
- Running scripts against an ambiguous Azure subscription, resource group, database, or tenant.
- Making broad refactors unrelated to Gabe's request.

When in doubt, prefer a dry run, backup, or read-only inspection first, then continue once the risk is understood.

## Current Project Context

- The app is an ASP.NET Core Razor Pages legal intake/workflow/conflicts SaaS prototype.
- Current public surfaces are `cmiforge.com`, `demo.cmiforge.com`, and `app.cmiforge.com`.
- Azure DNS hosts `cmiforge.com`; Namecheap remains the registrar.
- The real first tenant is Customer 0 at `app.cmiforge.com`.
- The public demo is disposable and separate from Customer 0.
- The app uses Azure SQL and private Azure Blob Storage for attachments.
- Customer 0 uses Microsoft Entra login.
- Public signup requires acceptance of the current CMIForge SaaS Terms, Legal Use, and License Agreement, and stores the acceptance in `LegalAgreementAcceptances`.
- Conflict history uses a hybrid searchable SQL archive/index plus compressed full-detail payloads for old cleared searches.
- The repo may have in-progress local changes. Do not revert user or prior-agent changes unless Gabe explicitly asks.
