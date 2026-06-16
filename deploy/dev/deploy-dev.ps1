param(
    [string]$BlobConnectionString,
    [switch]$AssignManagedIdentity,
    [switch]$SkipDatabaseUpdate,
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

$sqlConnectionString = "Server=tcp:gwmatterforge.database.windows.net,1433;Initial Catalog=matterforge-prototype;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

if ([string]::IsNullOrWhiteSpace($BlobConnectionString)) {
    Write-Host "Tip: pass -BlobConnectionString when you want file uploads enabled in the cloud dev app." -ForegroundColor Yellow
}

if (-not $SkipDatabaseUpdate.IsPresent) {
    Write-Host "Applying EF migrations to Azure SQL from the local dev context..." -ForegroundColor Cyan
    dotnet tool restore | Out-Null
    dotnet tool run dotnet-ef database update --configuration Release
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
    -RunMigrationsOnStartup $false `
    -AssignManagedIdentity:$AssignManagedIdentity `
    -SkipPublish:$SkipPublish `
    -SkipDeploy:$SkipDeploy
