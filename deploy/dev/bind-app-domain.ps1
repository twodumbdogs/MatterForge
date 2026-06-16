param(
    [string]$Hostname = "app.cmiforge.com",
    [string]$ResourceGroup = "gw-rg",
    [string]$PlanName = "cmiforge-dev-plan",
    [string]$WebAppName = "cmiforge-dev-web-06161223",
    [string]$PlanSku = "B1"
)

$ErrorActionPreference = "Stop"

Write-Host "Checking DNS records for $Hostname..." -ForegroundColor Cyan

$cname = Resolve-DnsName $Hostname -Type CNAME -ErrorAction SilentlyContinue
$txt = Resolve-DnsName ("asuid." + $Hostname) -Type TXT -ErrorAction SilentlyContinue

if (-not $cname) {
    throw "Missing CNAME for $Hostname. Create CNAME app -> $WebAppName.azurewebsites.net first."
}

if (-not $txt) {
    throw "Missing TXT verification record for asuid.$Hostname. Add the App Service verification ID first."
}

Write-Host "Upgrading App Service plan $PlanName to $PlanSku..." -ForegroundColor Cyan
az appservice plan update `
    --name $PlanName `
    --resource-group $ResourceGroup `
    --sku $PlanSku | Out-Null

Write-Host "Adding hostname binding for $Hostname..." -ForegroundColor Cyan
az webapp config hostname add `
    --resource-group $ResourceGroup `
    --webapp-name $WebAppName `
    --hostname $Hostname | Out-Null

Write-Host "Done. Verify the app at https://$Hostname" -ForegroundColor Green
