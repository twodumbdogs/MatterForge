# CMIForge Database Migration Runner

CMIForge production schema changes should run through the dedicated migrator lane, not through normal web app startup.

The runner uses:

- a dedicated user-assigned managed identity, `cmiforge-migrator-mi`
- a dedicated triggered WebJob host, `cmiforge-db-migrator`
- VNet integration through `cmiforge-vnet/appsvc-integration`
- SQL permissions granted to the migrator identity, not to the live web app identity

The live tenant web apps should keep:

```text
CMIForge__RunMigrationsOnStartup=false
```

## Existing Tenant Database

```powershell
.\deploy\migrations\run-tenant-migrations.ps1 `
  -SqlDatabaseName cmiforge-customer0 `
  -EnsureSqlUser `
  -AllowTemporarySqlPublicAccess
```

Use `-AllowTemporarySqlPublicAccess` only for a local operator run when SQL public access is disabled and the migrator SQL user has not been granted yet. After the first grant, normal migration runs should not need SQL public access.

## Multiple Tenant Databases

```powershell
.\deploy\migrations\run-tenant-migrations.ps1 `
  -SqlDatabaseName cmiforge-demo, cmiforge-customer0 `
  -EnsureSqlUser
```

## Future Provisioning

`deploy/customer/provision-customer.ps1` calls this runner for EF migrations. That keeps the future signup/provisioning path aligned with production security:

1. create tenant database
2. grant the migrator identity schema-change access
3. run EF migrations from the VNet-integrated WebJob host
4. deploy the tenant web app with startup migrations disabled

