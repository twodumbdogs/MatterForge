# Customer Provisioning

This folder contains the v1 repeatable customer provisioning command.

The public app still captures customer interest through `/Signup`. Submitters must accept the current CMIForge SaaS Terms, Legal Use, and License Agreement before the request is stored. Operators can review those requests in **System > Signup Requests**. When a request is ready to provision, run `provision-customer.ps1` from this repo.

## What It Does

For a customer subdomain such as `acme.cmiforge.com`, the script:

1. Validates and reserves the requested subdomain.
2. Creates or confirms the customer Azure SQL database.
3. Creates or confirms the private attachment Blob container.
4. Applies EF migrations through the dedicated VNet-integrated migrator WebJob.
5. Creates or confirms the App Service app and app settings.
6. Deploys the current CMIForge build.
7. Adds Azure DNS records:
   - `acme -> cmiforge-acme-web.azurewebsites.net`
   - `asuid.acme -> <App Service customDomainVerificationId>`
8. Waits for the CNAME and TXT records to resolve.
9. Binds `acme.cmiforge.com` to the App Service app.
10. Creates and binds an App Service managed certificate.
11. Adds Entra redirect URIs for both the custom hostname and Azure fallback hostname.
12. Optionally creates the initial Entra admin user.
13. Optionally creates matching CMIForge user `00000001` and assigns Administrator.
14. Optionally emails the temporary password after the tenant hostname is ready.
15. Grants tenant app managed identity access to Blob Storage and runtime Azure SQL roles where possible.
16. Restarts the web app.
17. Optionally updates a `TenantProvisioningRequest` to `Provisioning`, `Ready`, or `Failed`.
18. Stamps tenant outbound email settings for workflow notifications.

The originating signup request may also have a linked `LegalAgreementAcceptances` record. That acceptance is part of the customer onboarding/audit trail and should be preserved when provisioning, closing, or later exporting a customer history.

The script is intentionally idempotent: it checks for existing resources where practical so it can be rerun after a partial failure.

## Provisioning And Deploy Posture

The long-term goal is for online signup to create a `TenantProvisioningRequest`, queue a provisioning job, and let an operator-safe worker run this same path automatically. Until that worker exists, this script is the repeatable v1 provisioning command.

Gabe's working preference is still deploy-as-we-work: when provisioning code or shared app behavior changes, build/verify, deploy affected app surfaces, and smoke-check the live tenant URLs unless he explicitly says not to deploy.

The current low-cost private-networking pattern uses shared private endpoints for the shared Azure SQL server, shared attachment storage account, and shared Key Vault. That keeps early tenant cost down. Higher-sensitivity tenants can later move to dedicated storage accounts, dedicated SQL servers, or dedicated private endpoints if the customer budget and risk profile justify it.

The provisioning script now defaults new tenant apps into the shared private network:

- `VNetName=cmiforge-vnet`
- `VNetIntegrationSubnetName=appsvc-integration`
- `KeyVaultName=cmiforge-kv-gw`

It also creates blob containers through Azure Resource Manager with `az storage container-rm`, so storage public network access can stay disabled. EF migrations run through `deploy/migrations/run-tenant-migrations.ps1`, using the `cmiforge-migrator-mi` identity and the `cmiforge-db-migrator` triggered WebJob host inside the shared VNet path. If local SQL data-plane work is needed while SQL public access is disabled, pass `-AllowTemporarySqlPublicAccess`; the script opens a narrow firewall rule for the current public IP and restores SQL public access to disabled during cleanup.

For ad hoc SQL querying after private endpoints are enabled, the SQL tool is flexible but the network path is not. SSMS, Azure Data Studio, `sqlcmd`, `Invoke-Sqlcmd`, or scripts can all work from a VM/jumpbox in the VNet, a Bastion session, a VPN-connected workstation, or a short controlled temporary public-access window. Do not assume a local workstation can query private-endpoint SQL just because the DNS name is public-looking.

## Example

From `c#/CMIForge`:

```powershell
.\deploy\customer\provision-customer.ps1 `
  -Subdomain "acme" `
  -FirmName "Acme Law Group" `
  -Plan "Professional" `
  -BootstrapAdminEmail "admin@acmelaw.example" `
  -EntraTenantId "<directory-tenant-id>" `
  -EntraClientId "<app-registration-client-id>" `
  -EntraClientSecret "<client-secret-value>" `
  -AssignManagedIdentity `
  -CreateInitialAdminUser `
  -InitialAdminFirstName "Avery" `
  -InitialAdminLastName "Admin" `
  -AllowTemporarySqlPublicAccess
```

If the request already exists in CMIForge and you want the script to update its status, also pass:

```powershell
  -SignupRequestId "<tenant-provisioning-request-guid>" `
  -AdminDbConnectionString "<connection-string-for-the-admin/customer0-database>"
```

`Invoke-Sqlcmd` must be installed for automatic request-status updates and SQL managed-identity grants. The EF migration itself runs in Azure through the migrator WebJob; `Invoke-Sqlcmd` is still needed for local operator SQL grant/update steps.

## Initial Admin Email

To email the first admin a temporary password, add SMTP settings:

```powershell
.\deploy\customer\provision-customer.ps1 `
  -Subdomain "acme" `
  -FirmName "Acme Law Group" `
  -Plan "Professional" `
  -BootstrapAdminEmail "admin@acmelaw.example" `
  -EntraTenantId "<directory-tenant-id>" `
  -EntraClientId "<app-registration-client-id>" `
  -EntraClientSecret "<client-secret-value>" `
  -AssignManagedIdentity `
  -CreateInitialAdminUser `
  -SendInitialAdminEmail `
  -SmtpHost "smtp.example.com" `
  -SmtpPort 587 `
  -SmtpUsername "smtp-user" `
  -SmtpPassword "<smtp-password>" `
  -SmtpFromEmail "support@cmiforge.com" `
  -SmtpFromName "CMIForge"
```

Default initial admin behavior:

- Entra username defaults to `<subdomain>.admin@<EntraProvisioningDomain>`, for example `acme.admin@cmiforge.com`.
- The CMIForge user is created as system ID `00000001`.
- The CMIForge contact email is `BootstrapAdminEmail`, so the welcome email can go to the customer's real address even when the Entra UPN is under `cmiforge.com`.
- The Entra temporary password is generated by the script and forces password change on first sign-in.
- If the Entra user already exists, the script will not reset or email a password unless `-ResetInitialAdminPassword` is supplied.

## Tenant Notification Email

Provisioning now passes tenant email settings through to the new app environment:

- `Email.NotificationsEnabled` defaults to `true`.
- `Email.MailboxAddress` defaults to `intake@cmiforge.com`.
- `Email.FromEmail` defaults to `<subdomain>@cmiforge.com`, for example `acme@cmiforge.com`.
- `Email.ReplyToEmail` defaults to the same tenant address.
- `Email.FromName` defaults to `FirmName` or the subdomain.

Override these with `-EmailNotificationsEnabled`, `-EmailMailboxAddress`, `-EmailFromEmail`, `-EmailReplyToEmail`, or `-EmailFromName`.

Before a newly provisioned tenant can send live workflow notifications, the platform mailbox alias must exist on the shared mailbox and the tenant app managed identity must have Microsoft Graph mail access scoped to the shared mailbox policy.

## Tenant Inbound Email

Provisioning can also stamp the first inbound email intake settings:

- `InboundEmail.Enabled` defaults to `false`.
- `InboundEmail.MailboxAddress` defaults to the outbound mailbox anchor, usually `intake@cmiforge.com`.
- `InboundEmail.InboundAddress` defaults to the tenant sender address, such as `acme@cmiforge.com`.
- `InboundEmail.DefaultFormKey` defaults to `new-matter-intake`.
- `InboundEmail.AllowedSenderDomains` is intentionally blank by default so no inbound sender is trusted until the firm domain is known.

Override these with `-InboundEmailEnabled`, `-InboundEmailMailboxAddress`, `-InboundEmailAddress`, `-InboundEmailAllowedSenderDomains`, or `-InboundEmailDefaultFormKey`.

Before enabling live inbound intake, the platform mailbox alias must exist, allowed sender domains must be set, and the tenant app managed identity must have Graph mailbox read/write access scoped to the intake mailbox. The V1 subject format is `Client: Acme Corp` or `Client: Acme Corp; Matter: Lease Review`; blank `Matter:` creates a client-only intake for review.

`CreateInitialAdminUser` requires `Invoke-Sqlcmd` because it writes the initial CMIForge user and Administrator assignment into the new customer database.

## Default Resource Names

For `-Subdomain acme`, defaults are:

- Web app: `cmiforge-acme-web`
- Database: `cmiforge-acme`
- Attachment container: `acme-attachments`
- Hostname: `acme.cmiforge.com`

Override these with `-WebAppName`, `-SqlDatabaseName`, or `-StorageContainerName` only when there is a good reason.

## Useful Skips

Use skip switches for retries or controlled manual intervention:

- `-SkipDns`
- `-SkipHostnameBinding`
- `-SkipManagedCertificate`
- `-SkipEntraRedirectUri`
- `-SkipDatabaseCreate`
- `-SkipDatabaseUpdate`
- `-SkipStorageContainerCreate`
- `-SkipRoleAssignments`
- `-SkipVNetIntegration`
- `-SkipKeyVaultRoleAssignment`
- `-SkipPublish`
- `-SkipDeploy`

Use `-WhatIfProvision` for a dry run of the planned steps without making changes.

Use `-AllowTemporarySqlPublicAccess` only for local operator runs that must perform SQL managed-identity grants or other direct SQL admin work while Azure SQL public network access is disabled. Normal EF schema migrations should run through the dedicated migrator WebJob. The script opens a firewall rule for the current public IP and restores SQL public access to disabled in cleanup.

For a quick private-networking sanity check without changing Azure resources:

```powershell
.\deploy\customer\provision-customer.ps1 `
  -Subdomain "codextest" `
  -FirmName "Codex Test Firm" `
  -BootstrapAdminEmail "admin@example.com" `
  -EntraTenantId "<directory-tenant-id>" `
  -EntraClientId "<app-registration-client-id>" `
  -EntraClientSecret "<client-secret-value>" `
  -SkipDatabaseUpdate `
  -SkipRoleAssignments `
  -SkipDns `
  -SkipHostnameBinding `
  -SkipManagedCertificate `
  -SkipEntraRedirectUri `
  -WhatIfProvision
```

The WhatIf output should show `VNet integration: cmiforge-vnet/appsvc-integration`.

Use `-DnsWaitSeconds <seconds>` to control how long the script waits for new CNAME/TXT records before binding the hostname. The default is `120`.

## Notes

- DNS is expected to live in the Azure DNS zone `cmiforge.com`.
- The target App Service plan must be a paid tier such as `B1`; Free does not support custom domains.
- The script adds redirect URIs to the provided Entra app registration. If each customer gets its own app registration later, pass that customer's client ID and secret.
- The app itself should not create Azure resources inside a normal web request. This command is the v1 operator path; a later Azure Function or worker can call the same provisioning logic from a queue.
