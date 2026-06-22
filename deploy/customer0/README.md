# Customer 0 Deployment

Customer 0 is Gabe's real CMIForge tenant. It is separate from the public demo and should be treated like the first production-style customer space.

## Shape

Customer 0 uses the same CMIForge codebase as every other environment, but separate cloud configuration:

- Web app: `cmiforge-customer0-web`
- SQL database: `cmiforge-customer0`
- Blob container: `customer0-attachments`
- Entra app registration: `CMIForge Customer 0`
- Entra client ID: `cb05ca25-c8ce-42a1-a3aa-226a8e487c73`
- Entra user provisioning domain: `cmiforge.com`
- Demo mode: off
- Demo resets: off
- Sample data: off
- Bootstrap admin: Gabe's Entra sign-in email
- Public signup: anonymous request capture with mandatory legal-agreement acceptance
- Legal agreement records: stored in `LegalAgreementAcceptances`

Provisioned status:

- Provisioned on: `2026-06-17`
- App URL: `https://app.cmiforge.com`
- Azure fallback URL: `https://cmiforge-customer0-web.azurewebsites.net`
- App Service plan: `cmiforge-customer0-plan`
- App Service SKU: `B1`
- SQL tier: `Basic`
- Shared VNet: `cmiforge-vnet`
- App Service integration subnet: `appsvc-integration`
- SQL private endpoint: `pe-cmiforge-sql`
- Blob private endpoint: `pe-cmiforge-blob`
- Key Vault private endpoint: `pe-cmiforge-keyvault`
- DNS host: Azure DNS zone `cmiforge.com`
- Bootstrap admin email: `gwms@twodumbdogs.com`
- Entra redirect URIs:
  - `https://cmiforge-customer0-web.azurewebsites.net/signin-oidc`
  - `https://app.cmiforge.com/signin-oidc`
- Entra custom domain verification TXT value: `MS=ms36377677`

This keeps the disposable public demo away from real testing data.

## Standing Deploy Behavior

Customer 0 should stay in step with shared app behavior unless Gabe explicitly says to hold deployment. After CMIForge app changes, deploy Customer 0 after the demo app and smoke-check `https://app.cmiforge.com` or the affected Customer 0 route.

For code/UI/docs-only releases, keep database migrations skipped or disabled. For schema changes, apply migrations deliberately with the dedicated migrator lane, not normal Customer 0 web-app startup. Customer 0 should keep `CMIForge__SeedSampleData=false`; demo conflict fixtures and other public-demo filler data belong only in sample/demo environments.

Customer 0 data-plane access now runs through the shared private-networking slice. Azure SQL public network access is disabled, and the attachment storage account has public network access disabled with default action `Deny`. EF migrations should run through `deploy/migrations/run-tenant-migrations.ps1`, which uses `cmiforge-migrator-mi` and the VNet-integrated `cmiforge-db-migrator` triggered WebJob host.

The Customer 0 deployment wrapper now keeps the app VNet-integrated through `cmiforge-vnet/appsvc-integration` when it calls the shared `deployme.ps1` path. It also creates/ensures the attachment container through Azure Resource Manager instead of the storage data plane, so storage public access can remain disabled.

## Volume Test Data

Customer 0 currently includes a realistic generated dataset for early volume, dashboard, workflow, and search testing:

- 400 generated clients with mixed company and individual names
- 400 generated matters with realistic matter names and practice areas
- 400 generated parties with organization, individual, and government names
- 10 active users total, including generated fake users for volume submissions
- 1,000 volume-test submissions with `"volumeTest": true` in `FormSubmissions.DataJson`
- 300 open volume workflow tasks

The records use marker notes and the `volumeTest` submission flag so they can be searched, measured, renamed, or removed later without mixing them up with real firm data. Use `tools/rename-customer0-volume-data.ps1 -VerifyOnly` from the project root to inspect the current generated dataset.

## Signup And Legal Acceptance

Customer 0 hosts the real public request-capture route at:

```text
https://app.cmiforge.com/Signup
```

The page remains anonymous even though the rest of Customer 0 uses Microsoft Entra login. The submitter must accept the current CMIForge SaaS Terms, Legal Use, and License Agreement before the request can be submitted.

Acceptance is stored in `LegalAgreementAcceptances` and linked one-to-one to the `TenantProvisioningRequest`. The stored snapshot includes customer name, signer name/email, agreement key/version/title, product version, timestamp, IP address, and user agent.

## Entra User Provisioning

The app now has the foundation for creating a Microsoft Entra user when an administrator creates a CMIForge user.

Important behavior:

- The user's work/contact email in CMIForge can be different from the Entra login.
- The Entra login is the user's UPN, such as `first.last@cmiforge.com`.
- A UPN can look like an email address without having a real mailbox behind it.
- New Entra users receive a generated temporary password and must change it on first sign-in.
- Created Entra identity values are stored on the CMIForge user record for future login matching.

Current Customer 0 status:

- The `cmiforge.com` custom domain exists in Entra and is verified.
- The Customer 0 web app managed identity has the Microsoft Graph `User.ReadWrite.All` application permission.
- App setting `EntraProvisioning__Domain=cmiforge.com` is configured.
- App setting `EntraProvisioning__Enabled` can be enabled for tenants after the managed identity and Graph permissions are confirmed.
- `gabe@cmiforge.com` exists as an Entra identity and can become a Microsoft-hosted mailbox after a Microsoft 365 Business Basic or Exchange Online license is purchased and assigned.

Enable in-app Entra user creation with:

```powershell
az webapp config appsettings set `
  --resource-group gw-rg `
  --name cmiforge-customer0-web `
  --settings EntraProvisioning__Enabled=true EntraProvisioning__Domain=cmiforge.com

az webapp restart --resource-group gw-rg --name cmiforge-customer0-web
```

## Recommended First Tenant Pattern

Use a database-per-customer model for now:

- easier backup and restore
- easier customer export/delete later
- lower risk of cross-tenant data mistakes
- simpler support and debugging
- good fit for small-firm SaaS during early growth

The app code stays shared. Customer isolation comes from separate app settings, SQL database, and attachment container.

Use subdomains for tenant workspaces rather than path-based tenancy:

```text
app.cmiforge.com       Customer 0 / internal tenant
firm.cmiforge.com      future customer tenant
```

Azure DNS now hosts `cmiforge.com`, so future customer provisioning can create `firm.cmiforge.com` and `asuid.firm.cmiforge.com` records directly through Azure rather than the Namecheap DNS UI/API.

## Prerequisites

- Azure CLI logged into the target subscription.
- `dotnet` SDK available locally.
- EF tool restored by `dotnet tool restore`.
- Existing Azure SQL server: `gwmatterforge`.
- Existing storage account: `cmiforgeattachasgmt7`.
- Entra app registration created from `ENTRA_CHECKLIST.md`.

## Run

From `c#/CMIForge`:

```powershell
.\deploy\customer0\deploy-customer0.ps1 `
  -EntraTenantId "<directory-tenant-id>" `
  -EntraClientId "<app-registration-client-id>" `
  -EntraClientSecret "<client-secret-value>" `
  -BootstrapAdminEmail "<your-entra-email>" `
  -AssignManagedIdentity
```

The script will:

1. Create or confirm the Customer 0 SQL database.
2. Create or confirm the private blob container.
3. Apply EF migrations through the dedicated migrator WebJob.
4. Create or confirm the App Service plan and web app.
5. Configure Entra login app settings.
6. Configure managed-identity SQL and blob settings.
7. Deploy the current CMIForge build.
8. Grant blob access to the web app managed identity.
9. Try to grant SQL access to the web app managed identity if `Invoke-Sqlcmd` is available.
10. Restart the app after role assignments.

For schema-changing app releases, do not pass `-SkipDatabaseUpdate` unless the database migration has already been applied another way. If the migrator SQL user needs a one-time grant from a local operator machine, use `-AllowTemporarySqlPublicAccess`; normal EF schema updates should run through the migrator WebJob and keep the live web app's `CMIForge__RunMigrationsOnStartup=false`.

## SQL Managed Identity Grant

If the script says `Invoke-Sqlcmd` is not installed, run this against the `cmiforge-customer0` database from a SQL tool logged in with an Entra admin-capable account:

```sql
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'cmiforge-customer0-web')
BEGIN
    CREATE USER [cmiforge-customer0-web] FROM EXTERNAL PROVIDER;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members drm
    INNER JOIN sys.database_principals role_principal ON role_principal.principal_id = drm.role_principal_id
    INNER JOIN sys.database_principals member_principal ON member_principal.principal_id = drm.member_principal_id
    WHERE role_principal.name = N'db_datareader'
      AND member_principal.name = N'cmiforge-customer0-web')
BEGIN
    ALTER ROLE db_datareader ADD MEMBER [cmiforge-customer0-web];
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members drm
    INNER JOIN sys.database_principals role_principal ON role_principal.principal_id = drm.role_principal_id
    INNER JOIN sys.database_principals member_principal ON member_principal.principal_id = drm.member_principal_id
    WHERE role_principal.name = N'db_datawriter'
      AND member_principal.name = N'cmiforge-customer0-web')
BEGIN
    ALTER ROLE db_datawriter ADD MEMBER [cmiforge-customer0-web];
END
```

Then restart the web app:

```powershell
az webapp restart --resource-group gw-rg --name cmiforge-customer0-web
```

## First Smoke Test

1. Browse to `https://app.cmiforge.com`.
2. Confirm Microsoft sign-in appears.
3. Sign in with the configured bootstrap admin email.
4. Confirm there is no demo banner.
5. Confirm **System > Security** is visible.
6. Confirm **Entities > Users** contains the signed-in admin user.
7. Create one test client, matter, form submission, conflict search, and time entry.
8. Upload a small attachment to a submission.
9. Confirm the dashboard charts render with the Customer 0 volume dataset.
10. Confirm workflow notification step fields are visible in the workflow designer.
11. Confirm the submissions list uses compact one-line rows and the detail page keeps conflict searches/attachments in tabs with the activity rail on the side.
12. Confirm the inbound email settings/log page is visible to admins, but inbound processing remains disabled until mailbox aliases, allowed domains, and Graph permissions are ready.
13. Confirm the data persists after app restart.

## Important App Settings

See `appservice-settings.sample.json` for the full shape. The most important switches are:

```text
Authentication__Microsoft__Enabled=true
CMIForge__BootstrapAdminEmail=<your-admin-email>
CMIForge__DemoMode=false
CMIForge__DemoResetEnabled=false
CMIForge__RunSeedDataOnStartup=true
CMIForge__SeedSampleData=false
```

After the first successful boot, `CMIForge__RunSeedDataOnStartup` can be changed to `false`. The seed operation is designed to be idempotent, but turning it off removes a little startup work.

## Customer Provisioning Command

The repeatable v1 customer provisioning command now lives at `deploy/customer/provision-customer.ps1`.

That script is the operator-safe path for the next real customer. It validates a subdomain, creates the customer database and private attachment container, deploys/configures the App Service app, creates Azure DNS records, binds the custom hostname, creates/binds an App Service managed certificate, adds Entra redirect URIs, applies migrations through the dedicated migrator WebJob, configures core seed startup, and can update a `TenantProvisioningRequest` to `Provisioning`, `Ready`, or `Failed`.

## Future Customer Provisioning Button

Yes, it is practical to make customer provisioning happen from a button after subscription, but the button should enqueue this kind of work instead of doing Azure operations in a normal web request.

The safe version should not create Azure resources directly inside a normal web request. The better pattern is:

1. Customer subscribes.
2. CMIForge creates a `TenantProvisioningRequest` record.
3. The app queues a provisioning job.
4. A background worker or Azure Function picks up the job.
5. The worker runs the same provisioning path used by `deploy/customer/provision-customer.ps1`.
6. Status flows through `Pending`, `Provisioning`, `Ready`, or `Failed`.
7. Admin/support can retry failed jobs safely.

The provisioning code should eventually move from ad hoc PowerShell into a declarative template such as Bicep or Terraform plus a small orchestration worker. The script in this folder is the prototype of that repeatable process.
