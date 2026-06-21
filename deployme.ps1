param(
    [string]$ResourceGroup,
    [string]$PlanName,
    [string]$WebAppName,
    [string]$Location = "centralus",
    [string]$PlanSku = "F1",
    [string]$Runtime = "dotnet:10",
    [string]$SqlConnectionString,
    [string]$BlobConnectionString,
    [string]$BlobContainerName = "submission-attachments",
    [string]$BlobAccountName,
    [bool]$UseManagedIdentityForBlobStorage = $false,
    [string]$CurrentUserEmail,
    [string]$CurrentUserDisplayName,
    [string]$BootstrapAdminEmail,
    [bool]$EntraEnabled = $false,
    [string]$EntraTenantId,
    [string]$EntraClientId,
    [string]$EntraClientSecret,
    [string]$EntraCallbackPath = "/signin-oidc",
    [bool]$EntraProvisioningEnabled = $false,
    [string]$EntraProvisioningDomain,
    [bool]$DemoMode = $false,
    [bool]$DemoResetEnabled = $true,
    [double]$DemoResetIntervalHours = 12,
    [bool]$RunMigrationsOnStartup = $false,
    [bool]$RunSeedDataOnStartup = $false,
    [bool]$SeedSampleData = $false,
    [bool]$EmailNotificationsEnabled = $false,
    [string]$EmailMailboxAddress,
    [string]$EmailFromEmail,
    [string]$EmailReplyToEmail,
    [string]$EmailFromName,
    [bool]$InboundEmailEnabled = $false,
    [string]$InboundEmailMailboxAddress,
    [string]$InboundEmailAddress,
    [string]$InboundEmailAllowedSenderDomains,
    [string]$InboundEmailDefaultFormKey = "new-matter-intake",
    [bool]$NotificationsGraphEnabled = $true,
    [string]$NotificationsGraphTenantId,
    [string]$NotificationsGraphManagedIdentityClientId,
    [string]$VNetResourceGroup,
    [string]$VNetName,
    [string]$VNetIntegrationSubnetName,
    [switch]$AssignManagedIdentity,
    [switch]$SkipVNetIntegration,
    [switch]$SkipPublish,
    [switch]$SkipDeploy
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Show-Usage {
    param([string]$Message)

    if ($Message) {
        Write-Host $Message -ForegroundColor Red
        Write-Host ""
    }

    Write-Host "Usage:" -ForegroundColor Yellow
    Write-Host "  .\deployme.ps1 -ResourceGroup <rg> -PlanName <plan> -WebAppName <app> [-Location centralus] [-PlanSku F1|B1|S1] [-SqlConnectionString <value>] [-BlobConnectionString <value>] [-BlobAccountName <name>] [-UseManagedIdentityForBlobStorage `$true] [-EntraEnabled `$true] [-EntraTenantId <tenant>] [-EntraClientId <client>] [-BootstrapAdminEmail <email>] [-VNetName <name>] [-VNetIntegrationSubnetName <name>] [-AssignManagedIdentity]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Notes:" -ForegroundColor Yellow
    Write-Host "  - F1 is fine for a cheap dev cloud home, but App Service custom domains require a paid tier."
    Write-Host "  - If you use 'Authentication=Active Directory Default' for Azure SQL, assign a managed identity and grant that identity access in Azure SQL."
    exit 1
}

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

if ([string]::IsNullOrWhiteSpace($ResourceGroup) -or
    [string]::IsNullOrWhiteSpace($PlanName) -or
    [string]::IsNullOrWhiteSpace($WebAppName)) {
    Show-Usage "ResourceGroup, PlanName, and WebAppName are required."
}

Require-Command "az"
Require-Command "dotnet"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishRoot = Join-Path $projectRoot "artifacts\publish"
$publishDir = Join-Path $publishRoot "appservice"
$zipPath = Join-Path $publishRoot "cmiforge-appservice.zip"

Write-Host "Checking Azure resource group '$ResourceGroup'..." -ForegroundColor Cyan
$resourceGroupInfo = Invoke-AzJson group show --name $ResourceGroup
if (-not $resourceGroupInfo) {
    throw "Azure resource group '$ResourceGroup' was not found. Create it first or point this script at an existing group."
}

Write-Host "Ensuring App Service plan '$PlanName' exists..." -ForegroundColor Cyan
$plan = Invoke-AzJson appservice plan show --resource-group $ResourceGroup --name $PlanName
if (-not $plan) {
    az appservice plan create `
        --resource-group $ResourceGroup `
        --name $PlanName `
        --location $Location `
        --sku $PlanSku `
        --only-show-errors | Out-Null
    $plan = Invoke-AzJson appservice plan show --resource-group $ResourceGroup --name $PlanName
}

Write-Host "Ensuring web app '$WebAppName' exists..." -ForegroundColor Cyan
$webApp = Invoke-AzJson webapp show --resource-group $ResourceGroup --name $WebAppName
if (-not $webApp) {
    az webapp create `
        --resource-group $ResourceGroup `
        --plan $PlanName `
        --name $WebAppName `
        --runtime $Runtime `
        --https-only true `
        --only-show-errors | Out-Null
    $webApp = Invoke-AzJson webapp show --resource-group $ResourceGroup --name $WebAppName
}

if ($AssignManagedIdentity.IsPresent) {
    Write-Host "Assigning system-managed identity..." -ForegroundColor Cyan
    az webapp identity assign `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --only-show-errors | Out-Null
}

if (-not $SkipVNetIntegration.IsPresent -and
    -not [string]::IsNullOrWhiteSpace($VNetName) -and
    -not [string]::IsNullOrWhiteSpace($VNetIntegrationSubnetName)) {
    $effectiveVNetResourceGroup = if ([string]::IsNullOrWhiteSpace($VNetResourceGroup)) { $ResourceGroup } else { $VNetResourceGroup }
    Write-Host "Ensuring VNet integration '$VNetName/$VNetIntegrationSubnetName'..." -ForegroundColor Cyan
    $targetVNetId = az network vnet show `
        --resource-group $effectiveVNetResourceGroup `
        --name $VNetName `
        --query id `
        -o tsv
    if ([string]::IsNullOrWhiteSpace($targetVNetId)) {
        throw "Could not find VNet '$VNetName' in resource group '$effectiveVNetResourceGroup'."
    }

    $targetSubnetId = az network vnet subnet show `
        --resource-group $effectiveVNetResourceGroup `
        --vnet-name $VNetName `
        --name $VNetIntegrationSubnetName `
        --query id `
        -o tsv
    if ([string]::IsNullOrWhiteSpace($targetSubnetId)) {
        throw "Could not find subnet '$VNetIntegrationSubnetName' in VNet '$VNetName' / resource group '$effectiveVNetResourceGroup'."
    }

    $webApp = Invoke-AzJson webapp show --resource-group $ResourceGroup --name $WebAppName
    if ($webApp.virtualNetworkSubnetId -ne $targetSubnetId) {
        az webapp vnet-integration add `
            --resource-group $ResourceGroup `
            --name $WebAppName `
            --vnet $targetVNetId `
            --subnet $targetSubnetId `
            --only-show-errors | Out-Null
    }
}

$appSettings = @(
    "ASPNETCORE_ENVIRONMENT=Production",
    "SubmissionAttachments__ContainerName=$BlobContainerName",
    "SubmissionAttachments__UseManagedIdentity=$UseManagedIdentityForBlobStorage",
    "CMIForge__DemoMode=$DemoMode",
    "CMIForge__DemoResetEnabled=$DemoResetEnabled",
    "CMIForge__DemoResetIntervalHours=$DemoResetIntervalHours",
    "CMIForge__RunMigrationsOnStartup=$RunMigrationsOnStartup",
    "CMIForge__RunSeedDataOnStartup=$RunSeedDataOnStartup",
    "CMIForge__SeedSampleData=$SeedSampleData",
    "Authentication__Microsoft__Enabled=$EntraEnabled",
    "EntraProvisioning__Enabled=$EntraProvisioningEnabled"
)

if (-not [string]::IsNullOrWhiteSpace($BlobAccountName)) {
    $appSettings += "SubmissionAttachments__AccountName=$BlobAccountName"
}

if (-not [string]::IsNullOrWhiteSpace($CurrentUserEmail)) {
    $appSettings += "CMIForge__CurrentUserEmail=$CurrentUserEmail"
}

if (-not [string]::IsNullOrWhiteSpace($CurrentUserDisplayName)) {
    $appSettings += "CMIForge__CurrentUserDisplayName=$CurrentUserDisplayName"
}

if (-not [string]::IsNullOrWhiteSpace($BootstrapAdminEmail)) {
    $appSettings += "CMIForge__BootstrapAdminEmail=$BootstrapAdminEmail"
}

if (-not [string]::IsNullOrWhiteSpace($EntraTenantId)) {
    $appSettings += "Authentication__Microsoft__TenantId=$EntraTenantId"
}

if (-not [string]::IsNullOrWhiteSpace($EntraClientId)) {
    $appSettings += "Authentication__Microsoft__ClientId=$EntraClientId"
}

if (-not [string]::IsNullOrWhiteSpace($EntraClientSecret)) {
    $appSettings += "Authentication__Microsoft__ClientSecret=$EntraClientSecret"
}

if (-not [string]::IsNullOrWhiteSpace($EntraCallbackPath)) {
    $appSettings += "Authentication__Microsoft__CallbackPath=$EntraCallbackPath"
}

if (-not [string]::IsNullOrWhiteSpace($EntraProvisioningDomain)) {
    $appSettings += "EntraProvisioning__Domain=$EntraProvisioningDomain"
}

if ($EmailNotificationsEnabled) {
    $appSettings += "CMIForge__SystemSettings__Email.NotificationsEnabled=true"
}

if (-not [string]::IsNullOrWhiteSpace($EmailMailboxAddress)) {
    $appSettings += "CMIForge__SystemSettings__Email.MailboxAddress=$EmailMailboxAddress"
}

if (-not [string]::IsNullOrWhiteSpace($EmailFromEmail)) {
    $appSettings += "CMIForge__SystemSettings__Email.FromEmail=$EmailFromEmail"
}

if (-not [string]::IsNullOrWhiteSpace($EmailReplyToEmail)) {
    $appSettings += "CMIForge__SystemSettings__Email.ReplyToEmail=$EmailReplyToEmail"
}

if (-not [string]::IsNullOrWhiteSpace($EmailFromName)) {
    $appSettings += "CMIForge__SystemSettings__Email.FromName=$EmailFromName"
}

if ($InboundEmailEnabled) {
    $appSettings += "CMIForge__SystemSettings__InboundEmail.Enabled=true"
}

if (-not [string]::IsNullOrWhiteSpace($InboundEmailMailboxAddress)) {
    $appSettings += "CMIForge__SystemSettings__InboundEmail.MailboxAddress=$InboundEmailMailboxAddress"
}

if (-not [string]::IsNullOrWhiteSpace($InboundEmailAddress)) {
    $appSettings += "CMIForge__SystemSettings__InboundEmail.InboundAddress=$InboundEmailAddress"
}

if (-not [string]::IsNullOrWhiteSpace($InboundEmailAllowedSenderDomains)) {
    $appSettings += "CMIForge__SystemSettings__InboundEmail.AllowedSenderDomains=$InboundEmailAllowedSenderDomains"
}

if (-not [string]::IsNullOrWhiteSpace($InboundEmailDefaultFormKey)) {
    $appSettings += "CMIForge__SystemSettings__InboundEmail.DefaultFormKey=$InboundEmailDefaultFormKey"
}

$appSettings += "Notifications__Graph__Enabled=$NotificationsGraphEnabled"

if (-not [string]::IsNullOrWhiteSpace($NotificationsGraphTenantId)) {
    $appSettings += "Notifications__Graph__TenantId=$NotificationsGraphTenantId"
}

if (-not [string]::IsNullOrWhiteSpace($NotificationsGraphManagedIdentityClientId)) {
    $appSettings += "Notifications__Graph__ManagedIdentityClientId=$NotificationsGraphManagedIdentityClientId"
}

if (-not [string]::IsNullOrWhiteSpace($SqlConnectionString)) {
    $appSettings += "ConnectionStrings__DefaultConnection=$SqlConnectionString"
}
else {
    Write-Host "No SQL connection string supplied. The app will fall back to its in-memory database unless you set ConnectionStrings__DefaultConnection later." -ForegroundColor Yellow
}

if (-not [string]::IsNullOrWhiteSpace($BlobConnectionString)) {
    $appSettings += "SubmissionAttachments__ConnectionString=$BlobConnectionString"
}
else {
    Write-Host "No Blob connection string supplied. Existing Azure app setting will be left unchanged; if none exists, attachments stay disabled." -ForegroundColor Yellow
}

Write-Host "Applying app settings..." -ForegroundColor Cyan
az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --settings $appSettings `
    --only-show-errors | Out-Null

if (-not $SkipPublish.IsPresent) {
    Write-Host "Publishing CMIForge..." -ForegroundColor Cyan
    if (Test-Path $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }

    dotnet publish `
        (Join-Path $projectRoot "CMIForge.csproj") `
        -c Release `
        -o $publishDir | Out-Null
}

if (-not $SkipDeploy.IsPresent) {
    if (-not (Test-Path $publishDir)) {
        throw "Publish output '$publishDir' was not found. Run without -SkipPublish first, or publish the app before deploying."
    }

    Write-Host "Packaging published output..." -ForegroundColor Cyan
    if (Test-Path $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

    Write-Host "Deploying package to Azure App Service..." -ForegroundColor Cyan
    az webapp deploy `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --src-path $zipPath `
        --type zip `
        --clean true `
        --restart true `
        --only-show-errors | Out-Null
}

$finalWebApp = Invoke-AzJson webapp show --resource-group $ResourceGroup --name $WebAppName
$defaultUrl = if ($finalWebApp.defaultHostName) { "https://$($finalWebApp.defaultHostName)" } else { "(hostname unavailable)" }

Write-Host ""
Write-Host "CMIForge App Service is ready." -ForegroundColor Green
Write-Host "Default URL: $defaultUrl" -ForegroundColor Green
Write-Host "Plan SKU: $PlanSku" -ForegroundColor Green
Write-Host ""
Write-Host "Next likely steps:" -ForegroundColor Yellow
Write-Host "  1. Browse the default URL and confirm the app boots."
Write-Host "  2. If Azure SQL uses Entra/managed identity auth, create a database user for the web app identity and grant db_datareader/db_datawriter (or tighter) access."
Write-Host "  3. Scale to B1 or higher before wiring cmiforge.com as a custom domain on App Service."
