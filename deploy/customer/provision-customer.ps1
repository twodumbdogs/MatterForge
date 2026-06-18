param(
    [Parameter(Mandatory = $true)]
    [string]$Subdomain,

    [Parameter(Mandatory = $true)]
    [string]$BootstrapAdminEmail,

    [Parameter(Mandatory = $true)]
    [string]$EntraTenantId,

    [Parameter(Mandatory = $true)]
    [string]$EntraClientId,

    [Parameter(Mandatory = $true)]
    [string]$EntraClientSecret,

    [string]$FirmName,
    [string]$Plan = "Community",
    [string]$SignupRequestId,
    [string]$AdminDbConnectionString,

    [string]$ResourceGroup = "gw-rg",
    [string]$Location = "centralus",
    [string]$PlanName = "cmiforge-customer0-plan",
    [string]$PlanSku = "B1",
    [string]$SqlServerName = "gwmatterforge",
    [string]$SqlDatabaseSku = "Basic",
    [string]$StorageAccountName = "cmiforgeattachasgmt7",
    [string]$DnsZoneResourceGroup = "gw-rg",
    [string]$DnsZoneName = "cmiforge.com",
    [int]$DnsWaitSeconds = 120,
    [string]$EntraProvisioningDomain = "cmiforge.com",
    [bool]$EntraProvisioningEnabled = $false,

    [switch]$CreateInitialAdminUser,
    [switch]$ResetInitialAdminPassword,
    [string]$InitialAdminFirstName,
    [string]$InitialAdminLastName,
    [string]$InitialAdminDisplayName,
    [string]$InitialAdminUserName,
    [string]$InitialAdminTitle = "Administrator",
    [switch]$SendInitialAdminEmail,
    [string]$SmtpHost,
    [int]$SmtpPort = 587,
    [string]$SmtpUsername,
    [string]$SmtpPassword,
    [string]$SmtpFromEmail = "support@cmiforge.com",
    [string]$SmtpFromName = "CMIForge",
    [bool]$SmtpUseSsl = $true,

    [string]$WebAppName,
    [string]$SqlDatabaseName,
    [string]$StorageContainerName,

    [switch]$AssignManagedIdentity,
    [switch]$SkipDatabaseCreate,
    [switch]$SkipDatabaseUpdate,
    [switch]$SkipStorageContainerCreate,
    [switch]$SkipRoleAssignments,
    [switch]$SkipPublish,
    [switch]$SkipDeploy,
    [switch]$SkipDns,
    [switch]$SkipHostnameBinding,
    [switch]$SkipManagedCertificate,
    [switch]$SkipEntraRedirectUri,
    [switch]$WhatIfProvision
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
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    if ([string]::IsNullOrWhiteSpace($output)) {
        return $null
    }

    return $output | ConvertFrom-Json
}

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host $Name -ForegroundColor Cyan
    if ($WhatIfProvision.IsPresent) {
        Write-Host "  WhatIf: skipped" -ForegroundColor DarkYellow
        return $null
    }

    & $Action
}

function Escape-SqlLiteral {
    param([string]$Value)

    return $Value.Replace("'", "''")
}

function Escape-SqlIdentifier {
    param([string]$Value)

    return $Value.Replace("]", "]]")
}

function ConvertTo-GraphJson {
    param($Value)

    return $Value | ConvertTo-Json -Depth 20 -Compress
}

function New-TemporaryPassword {
    $chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!#$%*-_+".ToCharArray()
    $password = [System.Text.StringBuilder]::new("Cmi1!")
    while ($password.Length -lt 18) {
        [void]$password.Append($chars[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32($chars.Length)])
    }

    return $password.ToString()
}

function Normalize-AdminUserName {
    param([string]$Value)

    $normalized = $Value.Trim().Trim("@").ToLowerInvariant()
    $normalized = [regex]::Replace($normalized, "[^a-z0-9._-]", ".")
    $normalized = [regex]::Replace($normalized, "\.{2,}", ".").Trim(".", "_", "-")
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        $normalized = "$Subdomain.admin"
    }

    if ($normalized.Length -gt 64) {
        $normalized = $normalized.Substring(0, 64).Trim(".", "_", "-")
    }

    if ($normalized -notmatch '^[a-z0-9._-]{1,64}$') {
        throw "Initial admin username '$Value' is invalid after normalization."
    }

    return $normalized
}

function Get-GraphAccessToken {
    $token = az account get-access-token --resource-type ms-graph --query accessToken -o tsv 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($token)) {
        throw "Could not get a Microsoft Graph access token from Azure CLI."
    }

    return $token.Trim()
}

function Invoke-GraphJson {
    param(
        [string]$Method,
        [string]$Uri,
        $Body
    )

    $token = Get-GraphAccessToken
    $headers = @{
        Authorization = "Bearer $token"
        "Content-Type" = "application/json"
    }

    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $Uri -Headers $headers
    }

    return Invoke-RestMethod -Method $Method -Uri $Uri -Headers $headers -Body (ConvertTo-GraphJson $Body)
}

function Get-EntraUserByUpn {
    param([string]$UserPrincipalName)

    $encodedUpn = [System.Uri]::EscapeDataString($UserPrincipalName)
    try {
        return Invoke-GraphJson -Method "GET" -Uri "https://graph.microsoft.com/v1.0/users/$encodedUpn" -Body $null
    }
    catch {
        if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 404) {
            return $null
        }

        throw
    }
}

function Ensure-EntraInitialAdminUser {
    param(
        [string]$UserPrincipalName,
        [string]$MailNickname,
        [string]$DisplayName,
        [string]$FirstName,
        [string]$LastName,
        [string]$JobTitle
    )

    $existing = Get-EntraUserByUpn -UserPrincipalName $UserPrincipalName
    $temporaryPassword = $null
    if ($existing -and -not $ResetInitialAdminPassword.IsPresent) {
        return [pscustomobject]@{
            ObjectId = $existing.id
            UserPrincipalName = $existing.userPrincipalName
            TemporaryPassword = $null
            Created = $false
            PasswordReset = $false
        }
    }

    $temporaryPassword = New-TemporaryPassword
    $passwordProfile = @{
        forceChangePasswordNextSignIn = $true
        password = $temporaryPassword
    }

    if ($existing) {
        Invoke-GraphJson -Method "PATCH" -Uri "https://graph.microsoft.com/v1.0/users/$($existing.id)" -Body @{
            accountEnabled = $true
            passwordProfile = $passwordProfile
        } | Out-Null

        return [pscustomobject]@{
            ObjectId = $existing.id
            UserPrincipalName = $existing.userPrincipalName
            TemporaryPassword = $temporaryPassword
            Created = $false
            PasswordReset = $true
        }
    }

    $created = Invoke-GraphJson -Method "POST" -Uri "https://graph.microsoft.com/v1.0/users" -Body @{
        accountEnabled = $true
        displayName = $DisplayName
        givenName = $FirstName
        surname = $LastName
        mailNickname = $MailNickname
        userPrincipalName = $UserPrincipalName
        jobTitle = $JobTitle
        passwordProfile = $passwordProfile
    }

    return [pscustomobject]@{
        ObjectId = $created.id
        UserPrincipalName = $created.userPrincipalName
        TemporaryPassword = $temporaryPassword
        Created = $true
        PasswordReset = $false
    }
}

function Send-InitialAdminEmail {
    param(
        [string]$To,
        [string]$DisplayName,
        [string]$UserPrincipalName,
        [string]$TemporaryPassword,
        [string]$TenantUrl
    )

    if ([string]::IsNullOrWhiteSpace($SmtpHost) -or
        [string]::IsNullOrWhiteSpace($SmtpFromEmail)) {
        throw "SmtpHost and SmtpFromEmail are required when SendInitialAdminEmail is used."
    }

    if ([string]::IsNullOrWhiteSpace($TemporaryPassword)) {
        throw "No temporary password is available to email. Use ResetInitialAdminPassword if the Entra user already exists and you need a new password."
    }

    $body = @"
Hello $DisplayName,

Your CMIForge workspace is ready.

Workspace: $TenantUrl
Username: $UserPrincipalName
Temporary password: $TemporaryPassword

You will be asked to change this password the first time you sign in.

CMIForge Support
"@

    $message = [System.Net.Mail.MailMessage]::new()
    $message.From = [System.Net.Mail.MailAddress]::new($SmtpFromEmail, $SmtpFromName)
    $message.To.Add($To)
    $message.Subject = "Your CMIForge workspace is ready"
    $message.Body = $body

    $client = [System.Net.Mail.SmtpClient]::new($SmtpHost, $SmtpPort)
    $client.EnableSsl = $SmtpUseSsl
    if (-not [string]::IsNullOrWhiteSpace($SmtpUsername)) {
        $client.Credentials = [System.Net.NetworkCredential]::new($SmtpUsername, $SmtpPassword)
    }

    try {
        $client.Send($message)
    }
    finally {
        $message.Dispose()
        $client.Dispose()
    }
}

function Test-ValidSubdomain {
    param([string]$Value)

    if ($Value -cne $Value.ToLowerInvariant()) {
        return $false
    }

    if ($Value.Length -lt 2 -or $Value.Length -gt 40) {
        return $false
    }

    return $Value -match '^[a-z0-9]([a-z0-9-]*[a-z0-9])?$'
}

function Update-SignupRequest {
    param(
        [string]$Status,
        [string]$Message
    )

    if ([string]::IsNullOrWhiteSpace($SignupRequestId) -or [string]::IsNullOrWhiteSpace($AdminDbConnectionString)) {
        return
    }

    if (-not (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue)) {
        Write-Host "Invoke-Sqlcmd is not installed, so signup request status was not updated automatically." -ForegroundColor Yellow
        return
    }

    $escapedStatus = Escape-SqlLiteral $Status
    $escapedMessage = Escape-SqlLiteral $Message
    $escapedPlan = Escape-SqlLiteral $Plan
    $firmNameValue = if ([string]::IsNullOrWhiteSpace($FirmName)) { "" } else { $FirmName }
    $escapedFirmName = Escape-SqlLiteral $firmNameValue
    $escapedHostname = Escape-SqlLiteral $Hostname
    $escapedWebAppName = Escape-SqlLiteral $WebAppName
    $escapedDatabase = Escape-SqlLiteral $SqlDatabaseName
    $escapedContainer = Escape-SqlLiteral $StorageContainerName
    $query = @"
UPDATE TenantProvisioningRequests
SET Status = N'$escapedStatus',
    Plan = CASE WHEN LEN(N'$escapedPlan') > 0 THEN N'$escapedPlan' ELSE Plan END,
    FirmName = CASE WHEN LEN(N'$escapedFirmName') > 0 THEN N'$escapedFirmName' ELSE FirmName END,
    InternalNotes =
        CONCAT(
            COALESCE(NULLIF(InternalNotes, N''), N''),
            CASE WHEN COALESCE(NULLIF(InternalNotes, N''), N'') = N'' THEN N'' ELSE CHAR(13) + CHAR(10) END,
            FORMAT(SYSUTCDATETIME(), N'yyyy-MM-dd HH:mm:ss'),
            N' UTC - ',
            N'$escapedMessage',
            N' Hostname: $escapedHostname; Web app: $escapedWebAppName; Database: $escapedDatabase; Container: $escapedContainer.'
        ),
    UpdatedAt = SYSDATETIMEOFFSET()
WHERE Id = TRY_CONVERT(uniqueidentifier, N'$SignupRequestId');
"@

    Invoke-Sqlcmd -ConnectionString $AdminDbConnectionString -Query $query | Out-Null
}

function Add-EntraRedirectUri {
    param([string]$Uri)

    $currentUris = @(Invoke-AzJson ad app show --id $EntraClientId --query "web.redirectUris" | ForEach-Object { $_ })
    $uriSet = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($currentUri in $currentUris) {
        if (-not [string]::IsNullOrWhiteSpace($currentUri)) {
            [void]$uriSet.Add($currentUri)
        }
    }

    if ($uriSet.Contains($Uri)) {
        return
    }

    [void]$uriSet.Add($Uri)
    $uriArgs = @($uriSet)
    az ad app update --id $EntraClientId --web-redirect-uris $uriArgs --only-show-errors | Out-Null
}

function Get-CertificateForHost {
    param([string]$HostName)

    $certificates = @(Invoke-AzJson webapp config ssl list --resource-group $ResourceGroup)
    foreach ($certificate in $certificates) {
        $names = @($certificate.hostNames)
        if ($names -contains $HostName) {
            return $certificate
        }
    }

    return $null
}

function Get-HostnameBinding {
    param([string]$HostName)

    $bindings = @(Invoke-AzJson webapp config hostname list --resource-group $ResourceGroup --webapp-name $WebAppName)
    foreach ($binding in $bindings) {
        if ($binding.name -eq $HostName) {
            return $binding
        }
    }

    return $null
}

function Wait-ForDnsRecord {
    param(
        [string]$Name,
        [string]$Type,
        [string]$ExpectedValue
    )

    if ($DnsWaitSeconds -le 0) {
        return
    }

    $deadline = (Get-Date).AddSeconds($DnsWaitSeconds)
    do {
        $records = Resolve-DnsName $Name -Type $Type -ErrorAction SilentlyContinue
        foreach ($record in @($records)) {
            if ($Type -eq "CNAME" -and $record.NameHost -and $record.NameHost.TrimEnd(".") -ieq $ExpectedValue.TrimEnd(".")) {
                return
            }

            if ($Type -eq "TXT" -and $record.Strings -contains $ExpectedValue) {
                return
            }
        }

        Start-Sleep -Seconds 5
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for DNS $Type record '$Name' to resolve to '$ExpectedValue'."
}

$Subdomain = $Subdomain.Trim().ToLowerInvariant()
$reservedSubdomains = @("app", "asuid", "autodiscover", "cmiforge", "demo", "mail", "support", "www")
if (-not (Test-ValidSubdomain $Subdomain)) {
    throw "Subdomain '$Subdomain' is invalid. Use 2-40 lowercase letters, numbers, or hyphens; do not start or end with a hyphen."
}

if ($reservedSubdomains -contains $Subdomain) {
    throw "Subdomain '$Subdomain' is reserved."
}

if ($SendInitialAdminEmail.IsPresent -and -not $CreateInitialAdminUser.IsPresent) {
    throw "SendInitialAdminEmail requires CreateInitialAdminUser."
}

if ($CreateInitialAdminUser.IsPresent -and [string]::IsNullOrWhiteSpace($EntraProvisioningDomain)) {
    throw "EntraProvisioningDomain is required when CreateInitialAdminUser is used."
}

if ($SendInitialAdminEmail.IsPresent -and [string]::IsNullOrWhiteSpace($SmtpHost)) {
    throw "SmtpHost is required when SendInitialAdminEmail is used."
}

if ([string]::IsNullOrWhiteSpace($WebAppName)) {
    $WebAppName = "cmiforge-$Subdomain-web"
}

if ([string]::IsNullOrWhiteSpace($SqlDatabaseName)) {
    $SqlDatabaseName = "cmiforge-$Subdomain"
}

if ([string]::IsNullOrWhiteSpace($StorageContainerName)) {
    $StorageContainerName = "$Subdomain-attachments"
}

$Hostname = "$Subdomain.$DnsZoneName"
$AsuidRecordName = "asuid.$Subdomain"
$appConnectionString = "Server=tcp:$SqlServerName.database.windows.net,1433;Initial Catalog=$SqlDatabaseName;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;"
$migrationConnectionString = "Server=tcp:$SqlServerName.database.windows.net,1433;Initial Catalog=$SqlDatabaseName;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;"
$script:InitialAdminEmailPayload = $null

Require-Command "az"
Require-Command "dotnet"

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$deployScript = Join-Path $projectRoot "deployme.ps1"
if (-not (Test-Path $deployScript)) {
    throw "Could not find deploy script at '$deployScript'."
}

Write-Host ""
Write-Host "CMIForge customer provisioning" -ForegroundColor Green
Write-Host "Firm: $FirmName"
Write-Host "Plan: $Plan"
Write-Host "Hostname: https://$Hostname"
Write-Host "Web app: $WebAppName"
Write-Host "SQL database: $SqlDatabaseName"
Write-Host "Blob container: $StorageContainerName"
Write-Host ""

try {
    Update-SignupRequest -Status "Provisioning" -Message "Provisioning started."

    Invoke-Step "Checking target subdomain and web app availability..." {
        $existingDns = Resolve-DnsName $Hostname -ErrorAction SilentlyContinue
        if ($existingDns) {
            $existingWebApp = Invoke-AzJson webapp show --resource-group $ResourceGroup --name $WebAppName
            if (-not $existingWebApp) {
                throw "$Hostname already resolves, but target web app '$WebAppName' does not exist. Choose a different subdomain or inspect DNS first."
            }
        }
    }

    if (-not $SkipDatabaseCreate.IsPresent) {
        Invoke-Step "Ensuring Azure SQL database '$SqlDatabaseName' exists..." {
            $database = Invoke-AzJson sql db show --resource-group $ResourceGroup --server $SqlServerName --name $SqlDatabaseName
            if (-not $database) {
                az sql db create `
                    --resource-group $ResourceGroup `
                    --server $SqlServerName `
                    --name $SqlDatabaseName `
                    --service-objective $SqlDatabaseSku `
                    --only-show-errors | Out-Null
            }
        }
    }

    if (-not $SkipStorageContainerCreate.IsPresent) {
        Invoke-Step "Ensuring private blob container '$StorageContainerName' exists..." {
            az storage container create `
                --account-name $StorageAccountName `
                --name $StorageContainerName `
                --auth-mode login `
                --public-access off `
                --only-show-errors | Out-Null
        }
    }

    if (-not $SkipDatabaseUpdate.IsPresent) {
        Invoke-Step "Applying EF migrations to '$SqlDatabaseName'..." {
            Push-Location $projectRoot
            try {
                dotnet tool restore | Out-Null
                dotnet tool run dotnet-ef database update --configuration Release --connection $migrationConnectionString
            }
            finally {
                Pop-Location
            }
        }
    }

    if ($CreateInitialAdminUser.IsPresent) {
        Invoke-Step "Creating initial Entra admin and CMIForge user 00000001..." {
            if (-not (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue)) {
                throw "Invoke-Sqlcmd is required for CreateInitialAdminUser because the script must create the CMIForge user row."
            }

            $adminUserName = if ([string]::IsNullOrWhiteSpace($InitialAdminUserName)) {
                Normalize-AdminUserName "$Subdomain.admin"
            }
            else {
                Normalize-AdminUserName $InitialAdminUserName
            }

            $adminUpn = "$adminUserName@$EntraProvisioningDomain"
            $adminFirstName = if ([string]::IsNullOrWhiteSpace($InitialAdminFirstName)) { "CMIForge" } else { $InitialAdminFirstName.Trim() }
            $adminLastName = if ([string]::IsNullOrWhiteSpace($InitialAdminLastName)) { "Admin" } else { $InitialAdminLastName.Trim() }
            $adminDisplayName = if ([string]::IsNullOrWhiteSpace($InitialAdminDisplayName)) {
                "$adminFirstName $adminLastName".Trim()
            }
            else {
                $InitialAdminDisplayName.Trim()
            }

            $entraAdmin = Ensure-EntraInitialAdminUser `
                -UserPrincipalName $adminUpn `
                -MailNickname $adminUserName `
                -DisplayName $adminDisplayName `
                -FirstName $adminFirstName `
                -LastName $adminLastName `
                -JobTitle $InitialAdminTitle

            $userId = [Guid]::NewGuid()
            $userRoleId = [Guid]::NewGuid()
            $escapedFirstName = Escape-SqlLiteral $adminFirstName
            $escapedLastName = Escape-SqlLiteral $adminLastName
            $escapedDisplayName = Escape-SqlLiteral $adminDisplayName
            $escapedContactEmail = Escape-SqlLiteral $BootstrapAdminEmail
            $escapedTenantId = Escape-SqlLiteral $EntraTenantId
            $escapedObjectId = Escape-SqlLiteral $entraAdmin.ObjectId
            $escapedUpn = Escape-SqlLiteral $entraAdmin.UserPrincipalName
            $escapedTitle = Escape-SqlLiteral $InitialAdminTitle
            $ensureUserSql = @"
DECLARE @UserId uniqueidentifier;
DECLARE @AdministratorRoleId uniqueidentifier;

SELECT @AdministratorRoleId = Id
FROM SecurityRoles
WHERE [Key] = N'administrator'
  AND IsActive = 1;

IF @AdministratorRoleId IS NULL
BEGIN
    SET @AdministratorRoleId = NEWID();

    INSERT INTO SecurityRoles (
        Id, Name, [Key], Description, IsSystem, IsActive, CreatedAt, UpdatedAt)
    VALUES (
        @AdministratorRoleId,
        N'Administrator',
        N'administrator',
        N'Full CMIForge administration.',
        1,
        1,
        SYSDATETIMEOFFSET(),
        SYSDATETIMEOFFSET());
END

SELECT @UserId = Id
FROM Users
WHERE SystemId = 1
   OR EntraUserPrincipalName = N'$escapedUpn'
   OR Email = N'$escapedContactEmail';

IF @UserId IS NULL
BEGIN
    SET @UserId = '$userId';

    INSERT INTO Users (
        Id, SystemId, FirstName, MiddleName, LastName, DisplayName, Email,
        EntraTenantId, EntraObjectId, EntraUserPrincipalName, Title, IsActive,
        CreatedAt, UpdatedAt, LastLoginAt)
    VALUES (
        @UserId, 1, N'$escapedFirstName', N'', N'$escapedLastName', N'$escapedDisplayName', N'$escapedContactEmail',
        N'$escapedTenantId', N'$escapedObjectId', N'$escapedUpn', N'$escapedTitle', 1,
        SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), NULL);
END
ELSE
BEGIN
    UPDATE Users
    SET SystemId = 1,
        FirstName = N'$escapedFirstName',
        MiddleName = N'',
        LastName = N'$escapedLastName',
        DisplayName = N'$escapedDisplayName',
        Email = N'$escapedContactEmail',
        EntraTenantId = N'$escapedTenantId',
        EntraObjectId = N'$escapedObjectId',
        EntraUserPrincipalName = N'$escapedUpn',
        Title = N'$escapedTitle',
        IsActive = 1,
        UpdatedAt = SYSDATETIMEOFFSET()
    WHERE Id = @UserId;
END

IF NOT EXISTS (
    SELECT 1
    FROM UserRoles
    WHERE UserId = @UserId
      AND SecurityRoleId = @AdministratorRoleId)
BEGIN
    INSERT INTO UserRoles (Id, UserId, SecurityRoleId, CreatedAt)
    VALUES ('$userRoleId', @UserId, @AdministratorRoleId, SYSDATETIMEOFFSET());
END
"@

            Invoke-Sqlcmd -ConnectionString $migrationConnectionString -Query $ensureUserSql | Out-Null

            $adminMessage = if ($entraAdmin.Created) {
                "Created initial admin user $($entraAdmin.UserPrincipalName)."
            }
            elseif ($entraAdmin.PasswordReset) {
                "Reset password for existing initial admin user $($entraAdmin.UserPrincipalName)."
            }
            else {
                "Initial admin user $($entraAdmin.UserPrincipalName) already existed; CMIForge user was ensured."
            }

            Update-SignupRequest -Status "Provisioning" -Message $adminMessage
            Write-Host $adminMessage -ForegroundColor Green
            if (-not [string]::IsNullOrWhiteSpace($entraAdmin.TemporaryPassword)) {
                Write-Host "Temporary password: $($entraAdmin.TemporaryPassword)" -ForegroundColor Yellow
            }

            $script:InitialAdminEmailPayload = [pscustomobject]@{
                To = $BootstrapAdminEmail
                DisplayName = $adminDisplayName
                UserPrincipalName = $entraAdmin.UserPrincipalName
                TemporaryPassword = $entraAdmin.TemporaryPassword
            }
        }
    }

    Invoke-Step "Deploying and configuring App Service '$WebAppName'..." {
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
            -DemoMode $false `
            -DemoResetEnabled $false `
            -DemoResetIntervalHours 12 `
            -RunMigrationsOnStartup $false `
            -RunSeedDataOnStartup $true `
            -SeedSampleData $false `
            -AssignManagedIdentity:($AssignManagedIdentity -or -not $SkipRoleAssignments) `
            -SkipPublish:$SkipPublish `
            -SkipDeploy:$SkipDeploy
    }

    if ($WhatIfProvision.IsPresent) {
        Write-Host ""
        Write-Host "WhatIf provisioning plan completed. No Azure resources were changed." -ForegroundColor Green
        Write-Host "Planned URL: https://$Hostname" -ForegroundColor Green
        return
    }

    $webApp = Invoke-AzJson webapp show --resource-group $ResourceGroup --name $WebAppName
    if (-not $webApp) {
        throw "Web app '$WebAppName' was not found after deployment."
    }

    $defaultHostName = $webApp.defaultHostName
    $verificationId = $webApp.customDomainVerificationId
    if ([string]::IsNullOrWhiteSpace($defaultHostName)) {
        throw "Web app '$WebAppName' did not report a default hostname."
    }

    if ([string]::IsNullOrWhiteSpace($verificationId)) {
        throw "Web app '$WebAppName' did not report a customDomainVerificationId."
    }

    if (-not $SkipDns.IsPresent) {
        Invoke-Step "Creating Azure DNS CNAME '$Subdomain' -> '$defaultHostName'..." {
            az network dns record-set cname set-record `
                --resource-group $DnsZoneResourceGroup `
                --zone-name $DnsZoneName `
                --record-set-name $Subdomain `
                --cname $defaultHostName `
                --ttl 300 `
                --only-show-errors | Out-Null
        }

        Invoke-Step "Creating Azure DNS TXT '$AsuidRecordName' for App Service verification..." {
            $txtRecordSet = Invoke-AzJson network dns record-set txt show `
                --resource-group $DnsZoneResourceGroup `
                --zone-name $DnsZoneName `
                --name $AsuidRecordName
            if (-not $txtRecordSet) {
                az network dns record-set txt create `
                    --resource-group $DnsZoneResourceGroup `
                    --zone-name $DnsZoneName `
                    --name $AsuidRecordName `
                    --ttl 300 `
                    --only-show-errors | Out-Null
            }

            $currentValues = @()
            if ($txtRecordSet -and $txtRecordSet.txtRecords) {
                foreach ($txtRecord in $txtRecordSet.txtRecords) {
                    $currentValues += @($txtRecord.value)
                }
            }

            if ($currentValues -notcontains $verificationId) {
                az network dns record-set txt add-record `
                    --resource-group $DnsZoneResourceGroup `
                    --zone-name $DnsZoneName `
                    --record-set-name $AsuidRecordName `
                    --value $verificationId `
                    --only-show-errors | Out-Null
            }
        }
    }

    if (-not $SkipHostnameBinding.IsPresent) {
        if (-not $SkipDns.IsPresent) {
            Invoke-Step "Waiting for DNS records to resolve..." {
                Wait-ForDnsRecord -Name $Hostname -Type "CNAME" -ExpectedValue $defaultHostName
                Wait-ForDnsRecord -Name "$AsuidRecordName.$DnsZoneName" -Type "TXT" -ExpectedValue $verificationId
            }
        }

        Invoke-Step "Binding hostname '$Hostname' to '$WebAppName'..." {
            $binding = Get-HostnameBinding -HostName $Hostname
            if (-not $binding) {
                az webapp config hostname add `
                    --resource-group $ResourceGroup `
                    --webapp-name $WebAppName `
                    --hostname $Hostname `
                    --only-show-errors | Out-Null
            }
        }
    }

    if (-not $SkipManagedCertificate.IsPresent) {
        Invoke-Step "Creating and binding App Service managed certificate for '$Hostname'..." {
            $certificate = Get-CertificateForHost -HostName $Hostname
            if (-not $certificate) {
                $certificate = Invoke-AzJson webapp config ssl create `
                    --resource-group $ResourceGroup `
                    --name $WebAppName `
                    --hostname $Hostname `
                    --only-show-errors
            }

            if (-not $certificate -or [string]::IsNullOrWhiteSpace($certificate.thumbprint)) {
                throw "Could not find or create a managed certificate for '$Hostname'."
            }

            $binding = Get-HostnameBinding -HostName $Hostname
            if (-not $binding -or $binding.sslState -ne "SniEnabled" -or $binding.thumbprint -ne $certificate.thumbprint) {
                az webapp config ssl bind `
                    --resource-group $ResourceGroup `
                    --name $WebAppName `
                    --hostname $Hostname `
                    --certificate-thumbprint $certificate.thumbprint `
                    --ssl-type SNI `
                    --only-show-errors | Out-Null
            }
        }
    }

    if (-not $SkipEntraRedirectUri.IsPresent) {
        Invoke-Step "Adding Microsoft Entra redirect URI for '$Hostname'..." {
            Add-EntraRedirectUri -Uri "https://$Hostname/signin-oidc"
            Add-EntraRedirectUri -Uri "https://$defaultHostName/signin-oidc"
        }
    }

    if (-not $SkipRoleAssignments.IsPresent) {
        Invoke-Step "Applying managed-identity access to SQL and Blob Storage..." {
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

            if (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue) {
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
            }

            az webapp restart --resource-group $ResourceGroup --name $WebAppName --only-show-errors | Out-Null
        }
    }

    if ($CreateInitialAdminUser.IsPresent -and $SendInitialAdminEmail.IsPresent) {
        Invoke-Step "Emailing initial admin credentials..." {
            if ($null -eq $script:InitialAdminEmailPayload) {
                throw "Initial admin user payload was not created."
            }

            Send-InitialAdminEmail `
                -To $script:InitialAdminEmailPayload.To `
                -DisplayName $script:InitialAdminEmailPayload.DisplayName `
                -UserPrincipalName $script:InitialAdminEmailPayload.UserPrincipalName `
                -TemporaryPassword $script:InitialAdminEmailPayload.TemporaryPassword `
                -TenantUrl "https://$Hostname"

            Update-SignupRequest -Status "Provisioning" -Message "Initial admin credentials emailed to $($script:InitialAdminEmailPayload.To)."
        }
    }

    Update-SignupRequest -Status "Ready" -Message "Provisioning completed."

    Write-Host ""
    Write-Host "Customer tenant is ready." -ForegroundColor Green
    Write-Host "URL: https://$Hostname" -ForegroundColor Green
    Write-Host "Fallback URL: https://$defaultHostName" -ForegroundColor Green
    Write-Host "Bootstrap admin: $BootstrapAdminEmail" -ForegroundColor Green
}
catch {
    $message = $_.Exception.Message
    Update-SignupRequest -Status "Failed" -Message "Provisioning failed: $message"
    Write-Host ""
    Write-Host "Provisioning failed: $message" -ForegroundColor Red
    throw
}
