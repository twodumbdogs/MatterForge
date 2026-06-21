param(
    [string]$ResourceGroup = "gw-rg",
    [string]$Location = "centralus",
    [string]$PlanName = "cmiforge-customer0-plan",
    [string]$PlanSku = "B1",
    [string]$MigrationWebAppName = "cmiforge-db-migrator",
    [string]$MigrationIdentityName = "cmiforge-migrator-mi",
    [string]$WebAppName = "cmiforge-customer0-web",
    [string]$SqlServerName = "gwmatterforge",
    [string]$SqlDatabaseName = "cmiforge-customer0",
    [string]$SqlDatabaseSku = "Basic",
    [string]$StorageAccountName = "cmiforgeattachasgmt7",
    [string]$StorageContainerName = "customer0-attachments",
    [string]$VNetResourceGroup,
    [string]$VNetName = "cmiforge-vnet",
    [string]$VNetIntegrationSubnetName = "appsvc-integration",
    [string]$EntraTenantId,
    [string]$EntraClientId,
    [string]$EntraClientSecret,
    [bool]$EntraProvisioningEnabled = $false,
    [string]$EntraProvisioningDomain = "cmiforge.com",
    [string]$BootstrapAdminEmail,
    [switch]$AssignManagedIdentity,
    [switch]$SkipDatabaseCreate,
    [switch]$SkipDatabaseUpdate,
    [switch]$SkipStorageContainerCreate,
    [switch]$SkipRoleAssignments,
    [switch]$SkipVNetIntegration,
    [switch]$SkipPublish,
    [switch]$SkipDeploy,
    [switch]$AllowTemporarySqlPublicAccess
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Require-Command {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' is not installed or not on PATH."
    }
}

function Invoke-AzJson {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

    $output = az @Arguments --output json 2>$null
    if ([string]::IsNullOrWhiteSpace($output)) {
        return $null
    }

    return $output | ConvertFrom-Json
}

function Escape-SqlLiteral {
    param([string]$Value)

    return $Value.Replace("'", "''")
}

function Escape-SqlIdentifier {
    param([string]$Value)

    return $Value.Replace("]", "]]")
}

if ([string]::IsNullOrWhiteSpace($EntraTenantId) -or
    [string]::IsNullOrWhiteSpace($EntraClientId) -or
    [string]::IsNullOrWhiteSpace($EntraClientSecret) -or
    [string]::IsNullOrWhiteSpace($BootstrapAdminEmail)) {
    throw "EntraTenantId, EntraClientId, EntraClientSecret, and BootstrapAdminEmail are required for Customer 0."
}

Require-Command "az"
Require-Command "dotnet"

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$deployScript = Join-Path $projectRoot "deployme.ps1"
if (-not (Test-Path $deployScript)) {
    throw "Could not find deploy script at '$deployScript'."
}

$migrationRunnerScript = Join-Path $projectRoot "deploy\migrations\run-tenant-migrations.ps1"
if (-not (Test-Path $migrationRunnerScript)) {
    throw "Could not find migration runner script at '$migrationRunnerScript'."
}

if ([string]::IsNullOrWhiteSpace($VNetResourceGroup)) {
    $VNetResourceGroup = $ResourceGroup
}

$appConnectionString = "Server=tcp:$SqlServerName.database.windows.net,1433;Initial Catalog=$SqlDatabaseName;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;"
$migrationConnectionString = "Server=tcp:$SqlServerName.database.windows.net,1433;Initial Catalog=$SqlDatabaseName;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;"

Write-Host "Customer 0 target app: $WebAppName" -ForegroundColor Cyan
Write-Host "Customer 0 database: $SqlDatabaseName" -ForegroundColor Cyan
Write-Host "Entra redirect URI should include: https://$WebAppName.azurewebsites.net/signin-oidc" -ForegroundColor Yellow

if (-not $SkipDatabaseCreate.IsPresent) {
    Write-Host "Ensuring Azure SQL database '$SqlDatabaseName' exists..." -ForegroundColor Cyan
    $database = Invoke-AzJson sql db show `
        --resource-group $ResourceGroup `
        --server $SqlServerName `
        --name $SqlDatabaseName

    if (-not $database) {
        az sql db create `
            --resource-group $ResourceGroup `
            --server $SqlServerName `
            --name $SqlDatabaseName `
            --service-objective $SqlDatabaseSku `
            --only-show-errors | Out-Null
    }
}

if (-not $SkipStorageContainerCreate.IsPresent) {
    Write-Host "Ensuring private blob container '$StorageContainerName' exists..." -ForegroundColor Cyan
    az storage container-rm create `
        --resource-group $ResourceGroup `
        --storage-account $StorageAccountName `
        --name $StorageContainerName `
        --public-access off `
        --only-show-errors | Out-Null
}

if (-not $SkipDatabaseUpdate.IsPresent) {
    Write-Host "Applying EF migrations to '$SqlDatabaseName' with the dedicated migrator..." -ForegroundColor Cyan
    & $migrationRunnerScript `
        -ResourceGroup $ResourceGroup `
        -Location $Location `
        -PlanName $PlanName `
        -PlanSku $PlanSku `
        -MigrationWebAppName $MigrationWebAppName `
        -MigrationIdentityName $MigrationIdentityName `
        -SqlServerName $SqlServerName `
        -SqlDatabaseName $SqlDatabaseName `
        -VNetResourceGroup $VNetResourceGroup `
        -VNetName $VNetName `
        -VNetIntegrationSubnetName $VNetIntegrationSubnetName `
        -EnsureSqlUser `
        -AllowTemporarySqlPublicAccess:$AllowTemporarySqlPublicAccess `
        -SeedCoreData
}

& $deployScript `
    -ResourceGroup $ResourceGroup `
    -PlanName $PlanName `
    -WebAppName $WebAppName `
    -Location $Location `
    -PlanSku $PlanSku `
    -Runtime "dotnet:10" `
    -SqlConnectionString $appConnectionString `
    -BlobContainerName $StorageContainerName `
    -BlobAccountName $StorageAccountName `
    -UseManagedIdentityForBlobStorage $true `
    -BootstrapAdminEmail $BootstrapAdminEmail `
    -EntraEnabled $true `
    -EntraTenantId $EntraTenantId `
    -EntraClientId $EntraClientId `
    -EntraClientSecret $EntraClientSecret `
    -EntraCallbackPath "/signin-oidc" `
    -EntraProvisioningEnabled $EntraProvisioningEnabled `
    -EntraProvisioningDomain $EntraProvisioningDomain `
    -VNetResourceGroup $VNetResourceGroup `
    -VNetName $VNetName `
    -VNetIntegrationSubnetName $VNetIntegrationSubnetName `
    -DemoMode $false `
    -DemoResetEnabled $false `
    -DemoResetIntervalHours 12 `
    -RunMigrationsOnStartup $false `
    -RunSeedDataOnStartup $true `
    -SeedSampleData $false `
    -AssignManagedIdentity:($AssignManagedIdentity -or -not $SkipRoleAssignments) `
    -SkipVNetIntegration:$SkipVNetIntegration `
    -SkipPublish:$SkipPublish `
    -SkipDeploy:$SkipDeploy

if (-not $SkipRoleAssignments.IsPresent) {
    Write-Host "Applying managed-identity access to SQL and blob storage..." -ForegroundColor Cyan
    $webApp = Invoke-AzJson webapp show --resource-group $ResourceGroup --name $WebAppName
    $principalId = $webApp.identity.principalId
    if ([string]::IsNullOrWhiteSpace($principalId)) {
        throw "The web app does not have a system-assigned managed identity yet. Re-run with -AssignManagedIdentity."
    }

    $subscriptionId = (Invoke-AzJson account show).id
    $containerScope = "/subscriptions/$subscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.Storage/storageAccounts/$StorageAccountName/blobServices/default/containers/$StorageContainerName"
    $existingStorageRole = Invoke-AzJson role assignment list `
        --assignee $principalId `
        --scope $containerScope `
        --role "Storage Blob Data Contributor"
    if (-not $existingStorageRole -or $existingStorageRole.Count -eq 0) {
        az role assignment create `
            --assignee-object-id $principalId `
            --assignee-principal-type ServicePrincipal `
            --role "Storage Blob Data Contributor" `
            --scope $containerScope `
            --only-show-errors | Out-Null
    }

    if (-not $AllowTemporarySqlPublicAccess.IsPresent) {
        Write-Host "Skipping local SQL managed-identity grant because Azure SQL public network access is expected to be disabled." -ForegroundColor Yellow
        Write-Host "Use the dedicated migrator path for schema changes; run the SQL grant manually or with -AllowTemporarySqlPublicAccess only for first-time web-app identity setup." -ForegroundColor Yellow
    }
    elseif (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue) {
        $escapedWebAppName = Escape-SqlLiteral $WebAppName
        $webAppSqlIdentifier = Escape-SqlIdentifier $WebAppName
        $grantSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$escapedWebAppName')
BEGIN
    CREATE USER [$webAppSqlIdentifier] FROM EXTERNAL PROVIDER;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members drm
    INNER JOIN sys.database_principals role_principal ON role_principal.principal_id = drm.role_principal_id
    INNER JOIN sys.database_principals member_principal ON member_principal.principal_id = drm.member_principal_id
    WHERE role_principal.name = N'db_datareader'
      AND member_principal.name = N'$escapedWebAppName')
BEGIN
    ALTER ROLE db_datareader ADD MEMBER [$webAppSqlIdentifier];
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members drm
    INNER JOIN sys.database_principals role_principal ON role_principal.principal_id = drm.role_principal_id
    INNER JOIN sys.database_principals member_principal ON member_principal.principal_id = drm.member_principal_id
    WHERE role_principal.name = N'db_datawriter'
      AND member_principal.name = N'$escapedWebAppName')
BEGIN
    ALTER ROLE db_datawriter ADD MEMBER [$webAppSqlIdentifier];
END
"@

        Invoke-Sqlcmd -ConnectionString $migrationConnectionString -Query $grantSql | Out-Null
    }
    else {
        Write-Host "Invoke-Sqlcmd is not installed, so SQL managed-identity grants were not applied automatically." -ForegroundColor Yellow
        Write-Host "Run the SQL grant script from deploy/customer0/README.md before first real use." -ForegroundColor Yellow
    }

    Write-Host "Restarting '$WebAppName' after role assignments..." -ForegroundColor Cyan
    az webapp restart --resource-group $ResourceGroup --name $WebAppName --only-show-errors | Out-Null
}

Write-Host ""
Write-Host "Customer 0 deployment is ready to test." -ForegroundColor Green
Write-Host "URL: https://$WebAppName.azurewebsites.net" -ForegroundColor Green
Write-Host "Bootstrap admin: $BootstrapAdminEmail" -ForegroundColor Green
