param(
    [string]$ResourceGroup = "gw-rg",
    [string]$Location = "centralus",
    [string]$PlanName = "cmiforge-customer0-plan",
    [string]$PlanSku = "B1",
    [string]$MigrationWebAppName = "cmiforge-db-migrator",
    [string]$MigrationIdentityName = "cmiforge-migrator-mi",
    [string]$SqlServerName = "gwmatterforge",
    [Parameter(Mandatory = $true)]
    [string[]]$SqlDatabaseName,
    [string]$VNetResourceGroup,
    [string]$VNetName = "cmiforge-vnet",
    [string]$VNetIntegrationSubnetName = "appsvc-integration",
    [switch]$EnsureSqlUser,
    [switch]$AllowTemporarySqlPublicAccess,
    [switch]$SeedCoreData,
    [switch]$SeedSampleData,
    [switch]$SkipPublish,
    [switch]$KeepMigrationAppRunning
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -Scope Global -ErrorAction SilentlyContinue) {
    $Global:PSNativeCommandUseErrorActionPreference = $false
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
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($output)) {
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

function Get-CurrentPublicIp {
    try {
        return (Invoke-RestMethod -Uri "https://api.ipify.org" -TimeoutSec 15).Trim()
    }
    catch {
        throw "Could not determine the current public IP for temporary SQL firewall access."
    }
}

function Enable-TemporarySqlMaintenanceAccess {
    $server = Invoke-AzJson sql server show --resource-group $ResourceGroup --name $SqlServerName
    if (-not $server) {
        throw "SQL server '$SqlServerName' was not found."
    }

    if ($server.publicNetworkAccess -eq "Disabled" -and -not $AllowTemporarySqlPublicAccess.IsPresent) {
        throw "Azure SQL public network access is disabled for '$SqlServerName'. Re-run from an Azure/VNet path, pass -AllowTemporarySqlPublicAccess for the one-time migrator SQL grant, or omit -EnsureSqlUser if the grant already exists."
    }

    if ($AllowTemporarySqlPublicAccess.IsPresent) {
        if ($server.publicNetworkAccess -eq "Disabled") {
            az sql server update `
                --resource-group $ResourceGroup `
                --name $SqlServerName `
                --enable-public-network true `
                --only-show-errors | Out-Null
            $script:RestoreSqlPublicNetworkDisabled = $true
        }

        $ip = Get-CurrentPublicIp
        az sql server firewall-rule create `
            --resource-group $ResourceGroup `
            --server $SqlServerName `
            --name $script:TemporarySqlFirewallRuleName `
            --start-ip-address $ip `
            --end-ip-address $ip `
            --only-show-errors | Out-Null
        $script:RemoveTemporarySqlFirewallRule = $true
    }
}

function Restore-TemporarySqlMaintenanceAccess {
    if ($script:RemoveTemporarySqlFirewallRule) {
        az sql server firewall-rule delete `
            --resource-group $ResourceGroup `
            --server $SqlServerName `
            --name $script:TemporarySqlFirewallRuleName `
            --only-show-errors 2>$null | Out-Null
    }

    if ($script:RestoreSqlPublicNetworkDisabled) {
        az sql server update `
            --resource-group $ResourceGroup `
            --name $SqlServerName `
            --enable-public-network false `
            --only-show-errors | Out-Null
    }
}

function Grant-MigratorSqlAccess {
    param(
        [string]$DatabaseName,
        [string]$IdentityName
    )

    if (-not (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue)) {
        throw "Invoke-Sqlcmd is required for -EnsureSqlUser."
    }

    Enable-TemporarySqlMaintenanceAccess

    $escapedIdentityName = Escape-SqlLiteral $IdentityName
    $identitySqlIdentifier = Escape-SqlIdentifier $IdentityName
    $grantSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$escapedIdentityName')
BEGIN
    CREATE USER [$identitySqlIdentifier] FROM EXTERNAL PROVIDER;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members drm
    INNER JOIN sys.database_principals role_principal ON role_principal.principal_id = drm.role_principal_id
    INNER JOIN sys.database_principals member_principal ON member_principal.principal_id = drm.member_principal_id
    WHERE role_principal.name = N'db_ddladmin'
      AND member_principal.name = N'$escapedIdentityName')
BEGIN
    ALTER ROLE db_ddladmin ADD MEMBER [$identitySqlIdentifier];
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members drm
    INNER JOIN sys.database_principals role_principal ON role_principal.principal_id = drm.role_principal_id
    INNER JOIN sys.database_principals member_principal ON member_principal.principal_id = drm.member_principal_id
    WHERE role_principal.name = N'db_datareader'
      AND member_principal.name = N'$escapedIdentityName')
BEGIN
    ALTER ROLE db_datareader ADD MEMBER [$identitySqlIdentifier];
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members drm
    INNER JOIN sys.database_principals role_principal ON role_principal.principal_id = drm.role_principal_id
    INNER JOIN sys.database_principals member_principal ON member_principal.principal_id = drm.member_principal_id
    WHERE role_principal.name = N'db_datawriter'
      AND member_principal.name = N'$escapedIdentityName')
BEGIN
    ALTER ROLE db_datawriter ADD MEMBER [$identitySqlIdentifier];
END
"@

    $connectionString = "Server=tcp:$SqlServerName.database.windows.net,1433;Initial Catalog=$DatabaseName;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;"
    Invoke-Sqlcmd -ConnectionString $connectionString -Query $grantSql | Out-Null
}

function Wait-ForWebJobCompletion {
    param(
        [string]$DatabaseName
    )

    $deadline = (Get-Date).AddMinutes(5)
    do {
        Start-Sleep -Seconds 8
        $jobs = Invoke-AzJson webapp webjob triggered list `
            --resource-group $ResourceGroup `
            --name $MigrationWebAppName

        $job = $jobs | Where-Object { $_.name -eq "$MigrationWebAppName/DbMigrate" } | Select-Object -First 1
        if ($job -and $job.latestRun) {
            $latest = $job.latestRun
            if ($latest.status -eq "Success") {
                Write-Host "Migration WebJob succeeded for '$DatabaseName'." -ForegroundColor Green
                return
            }

            if ($latest.status -eq "Failed") {
                throw "Migration WebJob failed for '$DatabaseName'. Inspect the WebJob log for '$MigrationWebAppName/DbMigrate'."
            }

            Write-Host "Migration WebJob status for '$DatabaseName': $($latest.status)" -ForegroundColor DarkYellow
        }
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for migration WebJob completion for '$DatabaseName'."
}

Require-Command "az"
Require-Command "dotnet"

if ([string]::IsNullOrWhiteSpace($VNetResourceGroup)) {
    $VNetResourceGroup = $ResourceGroup
}

$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))
$publishRoot = Join-Path $projectRoot "artifacts\publish"
$jobPackageRoot = Join-Path $publishRoot "migrator-webjob-package"
$jobPublishDir = Join-Path $jobPackageRoot "App_Data\jobs\triggered\DbMigrate"
$zipPath = Join-Path $publishRoot "cmiforge-db-migrator-webjob.zip"
$script:TemporarySqlFirewallRuleName = "cmiforge-migrator-setup"
$script:RemoveTemporarySqlFirewallRule = $false
$script:RestoreSqlPublicNetworkDisabled = $false

try {
    Write-Host "Ensuring migrator identity '$MigrationIdentityName'..." -ForegroundColor Cyan
    $identity = Invoke-AzJson identity show --resource-group $ResourceGroup --name $MigrationIdentityName
    if (-not $identity) {
        az identity create `
            --resource-group $ResourceGroup `
            --name $MigrationIdentityName `
            --location $Location `
            --only-show-errors | Out-Null
        $identity = Invoke-AzJson identity show --resource-group $ResourceGroup --name $MigrationIdentityName
    }

    if (-not $identity) {
        throw "Could not create or load managed identity '$MigrationIdentityName'."
    }

    Write-Host "Ensuring App Service plan '$PlanName'..." -ForegroundColor Cyan
    $plan = Invoke-AzJson appservice plan show --resource-group $ResourceGroup --name $PlanName
    if (-not $plan) {
        az appservice plan create `
            --resource-group $ResourceGroup `
            --name $PlanName `
            --location $Location `
            --sku $PlanSku `
            --only-show-errors | Out-Null
    }

    Write-Host "Ensuring migration WebJob host '$MigrationWebAppName'..." -ForegroundColor Cyan
    $migrationApp = Invoke-AzJson webapp show --resource-group $ResourceGroup --name $MigrationWebAppName
    if (-not $migrationApp) {
        az webapp create `
            --resource-group $ResourceGroup `
            --plan $PlanName `
            --name $MigrationWebAppName `
            --runtime "dotnet:10" `
            --https-only true `
            --only-show-errors | Out-Null
    }

    az webapp identity assign `
        --resource-group $ResourceGroup `
        --name $MigrationWebAppName `
        --identities $identity.id `
        --only-show-errors | Out-Null

    $targetVNetId = az network vnet show `
        --resource-group $VNetResourceGroup `
        --name $VNetName `
        --query id `
        -o tsv
    if ([string]::IsNullOrWhiteSpace($targetVNetId)) {
        throw "Could not find VNet '$VNetName' in resource group '$VNetResourceGroup'."
    }

    $targetSubnetId = az network vnet subnet show `
        --resource-group $VNetResourceGroup `
        --vnet-name $VNetName `
        --name $VNetIntegrationSubnetName `
        --query id `
        -o tsv
    if ([string]::IsNullOrWhiteSpace($targetSubnetId)) {
        throw "Could not find subnet '$VNetIntegrationSubnetName' in VNet '$VNetName' / resource group '$VNetResourceGroup'."
    }

    $migrationApp = Invoke-AzJson webapp show --resource-group $ResourceGroup --name $MigrationWebAppName
    if ($migrationApp.virtualNetworkSubnetId -ne $targetSubnetId) {
        Write-Host "Adding VNet integration to migration host..." -ForegroundColor Cyan
        az webapp vnet-integration add `
            --resource-group $ResourceGroup `
            --name $MigrationWebAppName `
            --vnet $targetVNetId `
            --subnet $targetSubnetId `
            --only-show-errors | Out-Null
    }

    if (-not $SkipPublish.IsPresent) {
        Write-Host "Publishing migrator WebJob..." -ForegroundColor Cyan
        if (Test-Path $jobPackageRoot) {
            Remove-Item -LiteralPath $jobPackageRoot -Recurse -Force
        }

        New-Item -ItemType Directory -Path $jobPublishDir -Force | Out-Null
        dotnet publish `
            (Join-Path $projectRoot "CMIForge.Migrator\CMIForge.Migrator.csproj") `
            -c Release `
            -o $jobPublishDir | Out-Null

        Set-Content `
            -LiteralPath (Join-Path $jobPublishDir "run.cmd") `
            -Value "@echo off`r`nCMIForge.Migrator.exe" `
            -Encoding ASCII

        if (Test-Path $zipPath) {
            Remove-Item -LiteralPath $zipPath -Force
        }

        Compress-Archive -Path (Join-Path $jobPackageRoot "*") -DestinationPath $zipPath -Force

        Write-Host "Deploying migrator WebJob package..." -ForegroundColor Cyan
        az webapp deploy `
            --resource-group $ResourceGroup `
            --name $MigrationWebAppName `
            --src-path $zipPath `
            --type zip `
            --clean false `
            --restart true `
            --only-show-errors | Out-Null
    }

    foreach ($databaseName in $SqlDatabaseName) {
        if ([string]::IsNullOrWhiteSpace($databaseName)) {
            continue
        }

        if ($EnsureSqlUser.IsPresent) {
            Write-Host "Ensuring migrator SQL access on '$databaseName'..." -ForegroundColor Cyan
            Grant-MigratorSqlAccess -DatabaseName $databaseName -IdentityName $MigrationIdentityName
        }

        $connectionString = "Server=tcp:$SqlServerName.database.windows.net,1433;Initial Catalog=$databaseName;Authentication=Active Directory Managed Identity;User Id=$($identity.clientId);Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;"
        $settings = @(
            "ConnectionStrings__DefaultConnection=$connectionString",
            "CMIForgeMigrator__SeedCoreData=$SeedCoreData",
            "CMIForgeMigrator__SeedSampleData=$SeedSampleData"
        )

        Write-Host "Configuring migration host for '$databaseName'..." -ForegroundColor Cyan
        az webapp config appsettings set `
            --resource-group $ResourceGroup `
            --name $MigrationWebAppName `
            --settings $settings `
            --only-show-errors | Out-Null

        az webapp start `
            --resource-group $ResourceGroup `
            --name $MigrationWebAppName `
            --only-show-errors | Out-Null

        Write-Host "Running migration WebJob for '$databaseName'..." -ForegroundColor Cyan
        az webapp webjob triggered run `
            --resource-group $ResourceGroup `
            --name $MigrationWebAppName `
            --webjob-name "DbMigrate" `
            --only-show-errors | Out-Null

        Wait-ForWebJobCompletion -DatabaseName $databaseName
    }
}
finally {
    Restore-TemporarySqlMaintenanceAccess

    if (-not $KeepMigrationAppRunning.IsPresent) {
        az webapp stop `
            --resource-group $ResourceGroup `
            --name $MigrationWebAppName `
            --only-show-errors 2>$null | Out-Null
    }
}

Write-Host ""
Write-Host "CMIForge tenant migrations are complete." -ForegroundColor Green
Write-Host "Migration identity: $MigrationIdentityName" -ForegroundColor Green
Write-Host "Migration host: $MigrationWebAppName" -ForegroundColor Green
