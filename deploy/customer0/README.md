# Customer 0 Deployment

Customer 0 is Gabe's real CMIForge tenant. It is separate from the public demo and should be treated like the first production-style customer space.

## Shape

Customer 0 uses the same CMIForge codebase as every other environment, but separate cloud configuration:

- Web app: `cmiforge-customer0-web`
- SQL database: `cmiforge-customer0`
- Blob container: `customer0-attachments`
- Entra app registration: `CMIForge Customer 0`
- Entra client ID: `cb05ca25-c8ce-42a1-a3aa-226a8e487c73`
- Entra user provisioning domain: `cmiforge.com` pending verification
- Demo mode: off
- Demo resets: off
- Sample data: off
- Bootstrap admin: Gabe's Entra sign-in email

Provisioned status:

- Provisioned on: `2026-06-17`
- App URL: `https://cmiforge-customer0-web.azurewebsites.net`
- App Service plan: `cmiforge-customer0-plan`
- App Service SKU: `F1`
- SQL tier: `Basic`
- Bootstrap admin email: `gwms@twodumbdogs.com`
- Entra redirect URI: `https://cmiforge-customer0-web.azurewebsites.net/signin-oidc`
- Entra custom domain verification TXT value: `MS=ms36377677`

This keeps the disposable public demo away from real testing data.

## Entra User Provisioning

The app now has the foundation for creating a Microsoft Entra user when an administrator creates a CMIForge user.

Important behavior:

- The user's work/contact email in CMIForge can be different from the Entra login.
- The Entra login is the user's UPN, such as `first.last@cmiforge.com`.
- A UPN can look like an email address without having a real mailbox behind it.
- New Entra users receive a generated temporary password and must change it on first sign-in.
- Created Entra identity values are stored on the CMIForge user record for future login matching.

Current Customer 0 status:

- The `cmiforge.com` custom domain exists in Entra but is not verified yet.
- The Customer 0 web app managed identity has the Microsoft Graph `User.ReadWrite.All` application permission.
- App setting `EntraProvisioning__Domain=cmiforge.com` is configured.
- App setting `EntraProvisioning__Enabled=false` should stay disabled until the domain verifies.

To verify `cmiforge.com`, add this TXT record at the DNS host for the domain:

```text
Type: TXT
Name: @
Value: MS=ms36377677
TTL: 3600
```

If the DNS provider will not allow a TXT record at the root because the apex already points at the public Static Web App, Microsoft also provided this alternate MX verification record:

```text
Type: MX
Name: @
Mail exchange: ms36377677.msv1.invalid
Priority: 32767
TTL: 3600
```

After DNS propagation, verify the domain:

```powershell
az rest --method POST --uri "https://graph.microsoft.com/v1.0/domains/cmiforge.com/verify"
```

Then enable in-app Entra user creation:

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

## Prerequisites

- Azure CLI logged into the target subscription.
- `dotnet` SDK available locally.
- EF tool restored by `dotnet tool restore`.
- Existing Azure SQL server: `gwmatterforge`.
- Existing storage account: `cmiforgeattachasgmt7`.
- Entra app registration created from `ENTRA_CHECKLIST.md`.

## Run

From `c#/MatterForge`:

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
3. Apply EF migrations.
4. Create or confirm the App Service plan and web app.
5. Configure Entra login app settings.
6. Configure managed-identity SQL and blob settings.
7. Deploy the current CMIForge build.
8. Grant blob access to the web app managed identity.
9. Try to grant SQL access to the web app managed identity if `Invoke-Sqlcmd` is available.
10. Restart the app after role assignments.

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

1. Browse to `https://cmiforge-customer0-web.azurewebsites.net`.
2. Confirm Microsoft sign-in appears.
3. Sign in with the configured bootstrap admin email.
4. Confirm there is no demo banner.
5. Confirm **System > Security** is visible.
6. Confirm **Entities > Users** contains the signed-in admin user.
7. Create one test client, matter, form submission, conflict search, and time entry.
8. Upload a small attachment to a submission.
9. Confirm the data persists after app restart.

## Important App Settings

See `appservice-settings.sample.json` for the full shape. The most important switches are:

```text
Authentication__Microsoft__Enabled=true
MatterForge__BootstrapAdminEmail=<your-admin-email>
MatterForge__DemoMode=false
MatterForge__DemoResetEnabled=false
MatterForge__RunSeedDataOnStartup=true
MatterForge__SeedSampleData=false
```

After the first successful boot, `MatterForge__RunSeedDataOnStartup` can be changed to `false`. The seed operation is designed to be idempotent, but turning it off removes a little startup work.

## Future Customer Provisioning Button

Yes, it is practical to make customer provisioning happen from a button after subscription.

The safe version should not create Azure resources directly inside a normal web request. The better pattern is:

1. Customer subscribes.
2. CMIForge creates a `TenantProvisioningRequest` record.
3. The app queues a provisioning job.
4. A background worker or Azure Function picks up the job.
5. The worker creates the database, storage container, app settings, Entra configuration, and seed data.
6. Status flows through `Pending`, `Provisioning`, `Ready`, or `Failed`.
7. Admin/support can retry failed jobs safely.

The provisioning code should eventually move from ad hoc PowerShell into a declarative template such as Bicep or Terraform plus a small orchestration worker. The script in this folder is the prototype of that repeatable process.
