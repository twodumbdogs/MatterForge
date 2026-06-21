param(
    [string]$BlobConnectionString,
    [string]$MigrationWebAppName = "cmiforge-db-migrator",
    [string]$MigrationIdentityName = "cmiforge-migrator-mi",
    [switch]$AssignManagedIdentity,
    [switch]$SkipDatabaseUpdate,
    [switch]$AllowTemporarySqlPublicAccess,
    [switch]$SkipPublish,
    [switch]$SkipDeploy
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$deployScript = Join-Path $projectRoot "deployme.ps1"

if (-not (Test-Path $deployScript)) {
    throw "Could not find deploy script at '$deployScript'."
}

$migrationRunnerScript = Join-Path $projectRoot "deploy\migrations\run-tenant-migrations.ps1"
if (-not (Test-Path $migrationRunnerScript)) {
    throw "Could not find migration runner script at '$migrationRunnerScript'."
}

$sqlConnectionString = "Server=tcp:gwmatterforge.database.windows.net,1433;Initial Catalog=cmiforge-demo;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;"

if ([string]::IsNullOrWhiteSpace($BlobConnectionString)) {
    Write-Host "Tip: pass -BlobConnectionString when you want file uploads enabled in the cloud dev app." -ForegroundColor Yellow
}

if (-not $SkipDatabaseUpdate.IsPresent) {
    Write-Host "Applying EF migrations to Azure SQL with the dedicated migrator..." -ForegroundColor Cyan
    & $migrationRunnerScript `
        -ResourceGroup "gw-rg" `
        -Location "centralus" `
        -PlanName "cmiforge-customer0-plan" `
        -PlanSku "B1" `
        -MigrationWebAppName $MigrationWebAppName `
        -MigrationIdentityName $MigrationIdentityName `
        -SqlServerName "gwmatterforge" `
        -SqlDatabaseName "cmiforge-demo" `
        -VNetResourceGroup "gw-rg" `
        -VNetName "cmiforge-vnet" `
        -VNetIntegrationSubnetName "appsvc-integration" `
        -EnsureSqlUser `
        -AllowTemporarySqlPublicAccess:$AllowTemporarySqlPublicAccess `
        -SeedCoreData
}

& $deployScript `
    -ResourceGroup "gw-rg" `
    -PlanName "cmiforge-dev-plan" `
    -WebAppName "cmiforge-dev-web-06161223" `
    -Location "centralus" `
    -PlanSku "F1" `
    -Runtime "dotnet:10" `
    -SqlConnectionString $sqlConnectionString `
    -BlobConnectionString $BlobConnectionString `
    -BlobContainerName "submission-attachments" `
    -BlobAccountName "cmiforgeattachasgmt7" `
    -UseManagedIdentityForBlobStorage $true `
    -CurrentUserEmail "imauser@twodumbdogs.com" `
    -CurrentUserDisplayName "Ima User" `
    -DemoMode $true `
    -DemoResetEnabled $true `
    -DemoResetIntervalHours 12 `
    -RunMigrationsOnStartup $false `
    -RunSeedDataOnStartup $false `
    -SeedSampleData $true `
    -VNetResourceGroup "gw-rg" `
    -VNetName "cmiforge-vnet" `
    -VNetIntegrationSubnetName "appsvc-integration" `
    -AssignManagedIdentity:$AssignManagedIdentity `
    -SkipPublish:$SkipPublish `
    -SkipDeploy:$SkipDeploy
