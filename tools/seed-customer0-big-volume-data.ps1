param(
    [string]$ResourceGroup = "gw-rg",
    [string]$ServerName = "gwmatterforge",
    [string]$Server = "gwmatterforge.database.windows.net",
    [string]$Database = "cmiforge-customer0",
    [int]$TargetUsers = 700,
    [int]$TargetClients = 10000,
    [int]$TargetParties = 10000,
    [int]$TargetMatters = 20000,
    [switch]$Apply,
    [switch]$AllowTemporarySqlPublicAccess
)

$ErrorActionPreference = "Stop"
$env:POWERSHELL_TELEMETRY_OPTOUT = "1"

$projectRoot = Split-Path -Parent $PSScriptRoot
$binCandidates = @(
    (Join-Path $projectRoot "bin\Release\net10.0"),
    (Join-Path $projectRoot "bin\Debug\net10.0")
) | Where-Object { Test-Path (Join-Path $_ "runtimes\win\lib\net9.0\Microsoft.Data.SqlClient.dll") }

if (-not $binCandidates) {
    throw "Microsoft.Data.SqlClient runtime DLL was not found under bin\Release\net10.0 or bin\Debug\net10.0. Build the app once, then rerun this script."
}

$binDir = $binCandidates[0]
$nativeDir = Join-Path $binDir "runtimes\win-x64\native"
$env:PATH = "$nativeDir;$binDir;$env:PATH"
$sqlServerServerDll = Join-Path $binDir "Microsoft.SqlServer.Server.dll"
if (Test-Path $sqlServerServerDll) {
    [void][System.Reflection.Assembly]::LoadFrom($sqlServerServerDll)
}

$sqlClientDll = Join-Path $binDir "runtimes\win\lib\net9.0\Microsoft.Data.SqlClient.dll"
[void][System.Reflection.Assembly]::LoadFrom($sqlClientDll)

$markerClient = "Generated Customer 0 big-volume client for performance testing."
$markerMatter = "Generated Customer 0 big-volume matter for performance testing."
$markerParty = "Generated Customer 0 big-volume party for conflict/search testing."
$markerMatterParty = "Generated Customer 0 big-volume matter-party link for search testing."
$tempFirewallRuleName = "cmiforge-customer0-big-volume-seed"
$originalPublicNetworkAccess = $null
$temporaryAccessEnabled = $false

$companyStarts = @(
    "Alder", "Apex", "Arbor", "Atlas", "Beacon", "Blue Harbor", "Brightline", "Brookstone", "Cedar", "Clearwater",
    "Copperline", "Crescent", "Crosswind", "Evergreen", "Fairmont", "First Valley", "Granite", "Greenfield", "Harborview", "Highland",
    "Ironwood", "Juniper", "Keystone", "Lakeside", "Magnolia", "Maple Street", "Mariner", "Meridian", "Northstar", "Oakmont",
    "Parkside", "Pioneer", "Prairie", "Redwood", "Ridgeline", "Riverbend", "Sagebrush", "Silvergate", "Stonebridge", "Summit",
    "Vantage", "Westbridge", "Willow Creek", "Windward", "Blackstone", "Foxglove", "Hawthorne", "Sierra", "Lowland", "Mesa"
)
$companyMiddles = @(
    "Advisory", "Aerospace", "Analytics", "Biotech", "Capital", "Construction", "Consulting", "Distribution", "Energy", "Equipment",
    "Foods", "Health", "Hospitality", "Insurance", "Logistics", "Manufacturing", "Media", "Medical", "Mobility", "Packaging",
    "Pharma", "Property", "Renewables", "Research", "Retail", "Robotics", "Security", "Software", "Systems", "Telecom"
)
$companyEndings = @(
    "Group", "Holdings", "Partners", "Industries", "Solutions", "Ventures", "Works", "Services", "Labs", "Enterprises",
    "Associates", "Networks", "Resources", "Management", "Development", "Company", "Operations", "Alliance", "Collective", "Corporation"
)
$matterTypes = @(
    "Asset Purchase", "Board Advisory", "Commercial Lease", "Compliance Review", "Construction Claim", "Contract Review",
    "Data Privacy Review", "Employment Counseling", "Insurance Coverage", "IP License", "Joint Venture", "Labor Advice",
    "Lease Negotiation", "M&A Diligence", "Privacy Assessment", "Regulatory Response", "Shareholder Dispute", "Supply Agreement",
    "Trademark Review", "Vendor Dispute"
)
$practiceAreas = @("Corporate", "Litigation", "Employment", "Real Estate", "Insurance", "Intellectual Property", "Regulatory", "Finance", "Privacy", "Tax")
$cities = @("Austin", "Denver", "Chicago", "Raleigh", "Phoenix", "Columbus", "Nashville", "Portland", "Atlanta", "Milwaukee", "Charlotte", "Seattle", "Dallas", "Madison", "Richmond")
$states = @("TX", "CO", "IL", "NC", "AZ", "OH", "TN", "OR", "GA", "WI", "NC", "WA", "TX", "WI", "VA")
$firstNames = @(
    "Aaliyah", "Aaron", "Abigail", "Adam", "Adrian", "Aisha", "Alex", "Amara", "Ana", "Andre",
    "Anika", "Anthony", "Ari", "Ava", "Ben", "Bianca", "Caleb", "Camila", "Caroline", "Carter",
    "Chloe", "Claire", "Daniel", "Daphne", "Diego", "Elena", "Eli", "Ella", "Elliot", "Emilia",
    "Ethan", "Fatima", "Gabriel", "Grace", "Hannah", "Harper", "Henry", "Imani", "Isaac", "Isla",
    "Jalen", "Jasmine", "Jonah", "Julian", "Kai", "Keira", "Lena", "Liam", "Lucas", "Maya",
    "Mia", "Miles", "Naomi", "Noah", "Nolan", "Nora", "Omar", "Owen", "Priya", "Quinn",
    "Rafael", "Riley", "Samira", "Sofia", "Tessa", "Theo", "Vivian", "Wesley", "Yara", "Zoe"
)
$lastNames = @(
    "Ahmed", "Anderson", "Bailey", "Baker", "Bennett", "Brooks", "Bryant", "Campbell", "Carter", "Chen",
    "Coleman", "Cooper", "Diaz", "Ellis", "Flores", "Foster", "Garcia", "Gonzalez", "Green", "Hayes",
    "Hernandez", "Hughes", "Jackson", "Johnson", "Kim", "Lawson", "Lee", "Martinez", "Mitchell", "Morgan",
    "Murphy", "Nguyen", "Patel", "Parker", "Powell", "Price", "Reed", "Rivera", "Roberts", "Ross",
    "Sanders", "Santos", "Shah", "Singh", "Taylor", "Thomas", "Thompson", "Walker", "Williams", "Wright"
)
$titles = @("Partner", "Associate", "Senior Counsel", "Paralegal", "Conflicts Analyst", "Intake Coordinator", "Legal Assistant", "Records Specialist", "Practice Manager", "Compliance Specialist")
$partyRoles = @("Adverse Party", "Related Party", "Witness", "Opposing Counsel", "Affiliate/Subsidiary", "Other")

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

function Enable-TemporarySqlAccess {
    if (-not $AllowTemporarySqlPublicAccess.IsPresent) {
        return
    }

    Require-Command "az"
    $script:originalPublicNetworkAccess = az sql server show --resource-group $ResourceGroup --name $ServerName --query publicNetworkAccess -o tsv
    Write-Host "Opening temporary SQL access for this workstation..." -ForegroundColor Cyan
    az sql server update --resource-group $ResourceGroup --name $ServerName --set publicNetworkAccess=Enabled --only-show-errors | Out-Null

    $ip = (Invoke-RestMethod -Uri "https://api.ipify.org" -TimeoutSec 30).Trim()
    az sql server firewall-rule create `
        --resource-group $ResourceGroup `
        --server $ServerName `
        --name $tempFirewallRuleName `
        --start-ip-address $ip `
        --end-ip-address $ip `
        --only-show-errors | Out-Null

    $script:temporaryAccessEnabled = $true
}

function Restore-TemporarySqlAccess {
    if (-not $script:temporaryAccessEnabled) {
        return
    }

    Write-Host "Restoring SQL public access state..." -ForegroundColor Cyan
    az sql server firewall-rule delete `
        --resource-group $ResourceGroup `
        --server $ServerName `
        --name $tempFirewallRuleName `
        --only-show-errors 2>$null | Out-Null

    if ($script:originalPublicNetworkAccess) {
        az sql server update `
            --resource-group $ResourceGroup `
            --name $ServerName `
            --set publicNetworkAccess=$script:originalPublicNetworkAccess `
            --only-show-errors | Out-Null
    }
}

function New-Slug {
    param([string]$Value)

    $slug = $value.ToLowerInvariant() -replace "[^a-z0-9]+", "."
    return ($slug -replace "^\.+", "" -replace "\.+$", "")
}

function Normalize-Name {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($value)) {
        return ""
    }

    $suffixes = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    @("a", "an", "the", "co", "company", "corp", "corporation", "inc", "incorporated", "llc", "llp", "lp", "ltd", "limited", "plc", "pllc", "pc", "pa", "holdings", "holding", "group") | ForEach-Object { [void]$suffixes.Add($_) }
    $decomposed = $value.Trim().ToLowerInvariant().Normalize([Text.NormalizationForm]::FormD)
    $builder = [Text.StringBuilder]::new()
    foreach ($ch in $decomposed.ToCharArray()) {
        if ([System.Globalization.CharUnicodeInfo]::GetUnicodeCategory($ch) -eq [System.Globalization.UnicodeCategory]::NonSpacingMark) {
            continue
        }

        if ([char]::IsLetterOrDigit($ch)) {
            [void]$builder.Append($ch)
        }
        else {
            [void]$builder.Append(" ")
        }
    }

    $tokens = $builder.ToString().Normalize([Text.NormalizationForm]::FormC).Split(" ", [StringSplitOptions]::RemoveEmptyEntries -bor [StringSplitOptions]::TrimEntries) |
        Where-Object { -not $suffixes.Contains($_) }

    return ($tokens -join " ")
}

function Get-Person {
    param([int]$Index)

    $firstNameCount = $firstNames.Count
    $lastNameCount = $lastNames.Count
    $firstIndex = $Index % $firstNameCount
    $lastIndex = [int]([math]::Floor($Index / $firstNameCount)) % $lastNameCount
    $first = $firstNames[$firstIndex]
    $last = $lastNames[$lastIndex]
    [pscustomobject]@{ First = $first; Last = $last; Display = "$first $last" }
}

function Get-Company {
    param([int]$Index)

    $startCount = $companyStarts.Count
    $middleCount = $companyMiddles.Count
    $endingCount = $companyEndings.Count
    $cityCount = $cities.Count
    $start = $companyStarts[$($Index % $startCount)]
    $middle = $companyMiddles[$([int]([math]::Floor($Index / $startCount)) % $middleCount)]
    $ending = $companyEndings[$([int]([math]::Floor($Index / ($startCount * $middleCount))) % $endingCount)]
    $region = $cities[$($Index % $cityCount)]
    return "$start $middle $ending of $region $("{0:D5}" -f ($Index + 1))"
}

function New-Table {
    param(
        [string[]]$Columns,
        [type[]]$Types
    )

    $table = [System.Data.DataTable]::new()
    for ($i = 0; $i -lt $Columns.Count; $i++) {
        [void]$table.Columns.Add($Columns[$i], $Types[$i])
    }

    return ,$table
}

function Add-Row {
    param(
        [System.Data.DataTable]$TargetTable,
        [object[]]$Values
    )

    if ($Values.Count -gt 0 -and
        $Values[0] -isnot [guid] -and
        $Values[0] -is [System.Collections.IEnumerable] -and
        $Values[0] -isnot [string]) {
        $Values = [object[]]@($Values[0])
    }

    $row = $TargetTable.NewRow()
    for ($i = 0; $i -lt $Values.Count; $i++) {
        if ($TargetTable.Columns[$i].DataType -eq [bool] -and $Values[$i] -is [string]) {
            $debugValues = ($Values | ForEach-Object { if ($null -eq $_) { "<null>" } else { "[$_]" } }) -join ", "
            throw "Value shift while adding row to $($TargetTable.TableName): column $($TargetTable.Columns[$i].ColumnName) received string '$($Values[$i])'. Values: $debugValues"
        }
        if ($i -eq 0 -and $TargetTable.Columns[$i].DataType -eq [guid] -and $Values[$i] -isnot [guid]) {
            $debugValues = ($Values | ForEach-Object { if ($null -eq $_) { "<null>" } else { "[$_]<$($_.GetType().FullName)>" } }) -join ", "
            throw "Value shape while adding row to $($TargetTable.TableName): count $($Values.Count), first type $($Values[$i].GetType().FullName). Values: $debugValues"
        }

        $row[$i] = if ($null -eq $Values[$i]) { [DBNull]::Value } else { $Values[$i] }
    }

    [void]$TargetTable.Rows.Add($row)
}

function Invoke-Scalar($connection, [string]$sql, [hashtable]$parameters = @{}) {
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.CommandTimeout = 180
    foreach ($key in $parameters.Keys) {
        $parameter = $command.Parameters.Add("@$key", [System.Data.SqlDbType]::NVarChar)
        $parameter.Value = $parameters[$key]
    }

    return $command.ExecuteScalar()
}

function Invoke-Rows($connection, [string]$sql, [hashtable]$parameters = @{}) {
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.CommandTimeout = 180
    foreach ($key in $parameters.Keys) {
        $parameter = $command.Parameters.Add("@$key", [System.Data.SqlDbType]::NVarChar)
        $parameter.Value = $parameters[$key]
    }

    $reader = $command.ExecuteReader()
    try {
        $rows = @()
        while ($reader.Read()) {
            $row = [ordered]@{}
            for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                $row[$reader.GetName($i)] = if ($reader.IsDBNull($i)) { $null } else { $reader.GetValue($i) }
            }
            $rows += [pscustomobject]$row
        }

        return $rows
    }
    finally {
        $reader.Close()
    }
}

function Write-Bulk($connection, [System.Data.DataTable]$table, [string]$destination) {
    if ($table.Rows.Count -eq 0) {
        return
    }

    $bulk = [Microsoft.Data.SqlClient.SqlBulkCopy]::new($connection)
    try {
        $bulk.DestinationTableName = $destination
        $bulk.BatchSize = 1000
        $bulk.BulkCopyTimeout = 600
        foreach ($column in $table.Columns) {
            [void]$bulk.ColumnMappings.Add($column.ColumnName, $column.ColumnName)
        }

        $bulk.WriteToServer($table)
    }
    finally {
        $bulk.Close()
    }
}

function Get-GeneratedCounts($connection) {
    Invoke-Rows $connection @"
SELECT
    (SELECT COUNT(*) FROM Users WHERE Email LIKE N'%@customer0.example') AS Users,
    (SELECT COUNT(*) FROM Clients WHERE Notes IN (N'$markerClient', N'Renamed generated volume-test client for realistic search testing.')) AS Clients,
    (SELECT COUNT(*) FROM Parties WHERE Notes IN (N'$markerParty', N'Renamed generated volume-test party for realistic conflict-search testing.')) AS Parties,
    (SELECT COUNT(*) FROM Matters WHERE Notes IN (N'$markerMatter', N'Renamed generated volume-test matter for realistic search testing.')) AS Matters,
    (SELECT COUNT(*) FROM MatterParties WHERE Notes = N'$markerMatterParty') AS MatterPartyLinks
"@ | Select-Object -First 1
}

function Get-GeneratedIds($connection, [string]$table, [string]$marker) {
    Invoke-Rows $connection "SELECT Id FROM $table WHERE Notes = @Marker ORDER BY CreatedAt, Id" @{ Marker = $marker } |
        ForEach-Object { [guid]$_.Id }
}

Require-Command "az"

try {
    Enable-TemporarySqlAccess

    Write-Host "Getting Azure SQL access token..." -ForegroundColor Cyan
    $token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Azure CLI did not return a database token. Run az login and try again."
    }

    $connectionString = "Server=tcp:$Server,1433;Initial Catalog=$Database;Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;"
    $connection = [Microsoft.Data.SqlClient.SqlConnection]::new($connectionString)
    $connection.AccessToken = $token
    $connection.Open()

    try {
        $before = Get-GeneratedCounts $connection
        Write-Host "Current generated counts:" -ForegroundColor Cyan
        $before | Format-List

        $usersToAdd = [Math]::Max(0, $TargetUsers - [int]($before.Users))
        $clientsToAdd = [Math]::Max(0, $TargetClients - [int]($before.Clients))
        $partiesToAdd = [Math]::Max(0, $TargetParties - [int]($before.Parties))
        $mattersToAdd = [Math]::Max(0, $TargetMatters - [int]($before.Matters))

        Write-Host "Planned additions: $usersToAdd users, $clientsToAdd clients, $partiesToAdd parties, $mattersToAdd matters." -ForegroundColor Cyan
        if (-not $Apply) {
            Write-Host "Preview only. Rerun with -Apply to insert generated Customer 0 data." -ForegroundColor Yellow
            return
        }

        $now = [DateTimeOffset]::UtcNow
        $maxSystemId = [int](Invoke-Scalar $connection "SELECT ISNULL(MAX(SystemId), 0) FROM Users")
        $maxClientNumber = [int](Invoke-Scalar $connection "SELECT ISNULL(MAX(ClientNumber), 0) FROM Clients")
        $maxPartyNumber = [int](Invoke-Scalar $connection "SELECT ISNULL(MAX(PartyNumber), 0) FROM Parties")
        $maxMatterNumber = [int](Invoke-Scalar $connection "SELECT ISNULL(MAX(MatterNumber), 0) FROM Matters")

        $users = New-Table `
            -Columns `
            @("Id", "SystemId", "FirstName", "MiddleName", "LastName", "DisplayName", "Email", "EntraTenantId", "EntraObjectId", "EntraUserPrincipalName", "Title", "IsActive", "IsArchived", "CreatedAt", "UpdatedAt", "LastLoginAt", "FontScalePercent") `
            -Types `
            @([guid], [int], [string], [string], [string], [string], [string], [string], [string], [string], [string], [bool], [bool], [DateTimeOffset], [DateTimeOffset], [DateTimeOffset], [int])
        $newUserIds = [System.Collections.Generic.List[guid]]::new()
        $titleCount = $titles.Count
        for ($i = 0; $i -lt $usersToAdd; $i++) {
            $globalIndex = [int]($before.Users) + $i
            $person = Get-Person $globalIndex | Select-Object -Last 1
            $id = [guid]::NewGuid()
            $email = "$(New-Slug $person.First).$(New-Slug $person.Last).$("{0:D4}" -f ($globalIndex + 1))@customer0.example"
            $newUserIds.Add($id)
            Add-Row -TargetTable $users -Values @($id, ($maxSystemId + $i + 1), $person.First, "", $person.Last, $person.Display, $email, "", "", "", $titles[($globalIndex % $titleCount)], $true, $false, $now, $now, $null, 100)
        }

        Write-Bulk $connection $users "Users"
        Write-Host "Inserted $($users.Rows.Count) users." -ForegroundColor Green

        $clients = New-Table `
            -Columns `
            @("Id", "Name", "ClientNumber", "Status", "PrimaryContact", "Email", "Phone", "AddressLine1", "AddressLine2", "City", "State", "PostalCode", "Country", "Notes", "IsArchived", "CreatedAt", "UpdatedAt") `
            -Types `
            @([guid], [string], [int], [string], [string], [string], [string], [string], [string], [string], [string], [string], [string], [string], [bool], [DateTimeOffset], [DateTimeOffset])
        $newClientIds = [System.Collections.Generic.List[guid]]::new()
        $companyStartCount = $companyStarts.Count
        $cityCount = $cities.Count
        for ($i = 0; $i -lt $clientsToAdd; $i++) {
            $globalIndex = [int]($before.Clients) + $i
            $id = [guid]::NewGuid()
            $company = Get-Company $globalIndex | Select-Object -Last 1
            $contact = Get-Person ($globalIndex + 17) | Select-Object -Last 1
            $cityIndex = $globalIndex % $cityCount
            $streetStart = $companyStarts[($globalIndex % $companyStartCount)]
            $newClientIds.Add($id)
            $row = $clients.NewRow()
            $row["Id"] = $id
            $row["Name"] = $company
            $row["ClientNumber"] = $maxClientNumber + $i + 1
            $row["Status"] = "Active"
            $row["PrimaryContact"] = $contact.Display
            $row["Email"] = "legal@$(New-Slug $company).example"
            $row["Phone"] = "555-$("{0:D3}" -f (($globalIndex % 800) + 100))-$("{0:D4}" -f (($globalIndex * 37) % 10000))"
            $row["AddressLine1"] = "$(($globalIndex % 9000) + 100) $streetStart Parkway"
            $row["AddressLine2"] = ""
            $row["City"] = $cities[$cityIndex]
            $row["State"] = $states[$cityIndex]
            $row["PostalCode"] = "{0:D5}" -f (70000 + ($globalIndex % 9999))
            $row["Country"] = "United States"
            $row["Notes"] = $markerClient
            $row["IsArchived"] = $false
            $row["CreatedAt"] = $now
            $row["UpdatedAt"] = $now
            [void]$clients.Rows.Add($row)
        }

        Write-Bulk $connection $clients "Clients"
        Write-Host "Inserted $($clients.Rows.Count) clients." -ForegroundColor Green

        $parties = New-Table `
            -Columns `
            @("Id", "PartyNumber", "Name", "NormalizedName", "PartyType", "Status", "Notes", "IsArchived", "CreatedAt", "UpdatedAt") `
            -Types `
            @([guid], [int], [string], [string], [string], [string], [string], [bool], [DateTimeOffset], [DateTimeOffset])
        $newPartyIds = [System.Collections.Generic.List[guid]]::new()
        $partyCityCount = $cities.Count
        for ($i = 0; $i -lt $partiesToAdd; $i++) {
            $globalIndex = [int]($before.Parties) + $i
            $id = [guid]::NewGuid()
            if (($globalIndex % 17) -eq 0) {
                $name = "City of $($cities[($globalIndex % $partyCityCount)]) Office $("{0:D4}" -f ($globalIndex + 1))"
                $type = "Government"
            }
            elseif (($globalIndex % 7) -eq 0) {
                $personParty = Get-Person ($globalIndex + 251) | Select-Object -Last 1
                $name = $personParty.Display + " $("{0:D4}" -f ($globalIndex + 1))"
                $type = "Individual"
            }
            else {
                $name = Get-Company ($globalIndex + 10000) | Select-Object -Last 1
                $type = "Organization"
            }

            $newPartyIds.Add($id)
            $row = $parties.NewRow()
            $row["Id"] = $id
            $row["PartyNumber"] = $maxPartyNumber + $i + 1
            $row["Name"] = $name
            $row["NormalizedName"] = Normalize-Name $name
            $row["PartyType"] = $type
            $row["Status"] = "Active"
            $row["Notes"] = $markerParty
            $row["IsArchived"] = $false
            $row["CreatedAt"] = $now
            $row["UpdatedAt"] = $now
            [void]$parties.Rows.Add($row)
        }

        Write-Bulk $connection $parties "Parties"
        Write-Host "Inserted $($parties.Rows.Count) parties." -ForegroundColor Green

        $allGeneratedClientIds = [System.Collections.Generic.List[guid]]::new()
        (Get-GeneratedIds $connection "Clients" $markerClient) | ForEach-Object { $allGeneratedClientIds.Add($_) }
        $allGeneratedClientIds.AddRange($newClientIds)
        if ($allGeneratedClientIds.Count -eq 0) {
            $existingClientIds = Invoke-Rows $connection "SELECT TOP 1 Id FROM Clients ORDER BY ClientNumber"
            if ($existingClientIds.Count -eq 0) {
                throw "No clients are available to attach generated matters."
            }
            $allGeneratedClientIds.Add([guid]$existingClientIds[0].Id)
        }

        $allGeneratedUserIds = [System.Collections.Generic.List[guid]]::new()
        (Invoke-Rows $connection "SELECT Id FROM Users WHERE Email LIKE N'%@customer0.example' ORDER BY SystemId") | ForEach-Object { $allGeneratedUserIds.Add([guid]$_.Id) }

        $matters = New-Table `
            -Columns `
            @("Id", "Name", "MatterNumber", "ClientId", "PracticeArea", "Status", "OpenedDate", "ResponsibleUserId", "LeadPartnerId", "RequiresTimeApproval", "TimeIncrementMinutes", "TimeCodeSetId", "Notes", "IsArchived", "CreatedAt", "UpdatedAt") `
            -Types `
            @([guid], [string], [int], [guid], [string], [string], [DateTime], [guid], [guid], [bool], [int], [guid], [string], [bool], [DateTimeOffset], [DateTimeOffset])
        $newMatterIds = [System.Collections.Generic.List[guid]]::new()
        $generatedClientCount = $allGeneratedClientIds.Count
        $generatedUserCount = $allGeneratedUserIds.Count
        $matterTypeCount = $matterTypes.Count
        $practiceAreaCount = $practiceAreas.Count
        for ($i = 0; $i -lt $mattersToAdd; $i++) {
            $globalIndex = [int]($before.Matters) + $i
            $id = [guid]::NewGuid()
            $clientId = $allGeneratedClientIds[($globalIndex % $generatedClientCount)]
            $matterType = $matterTypes[($globalIndex % $matterTypeCount)]
            $name = "$matterType - $("{0:D5}" -f ($globalIndex + 1))"
            $responsibleUserId = if ($generatedUserCount -gt 0) { $allGeneratedUserIds[($globalIndex % $generatedUserCount)] } else { $null }
            $leadPartnerId = if ($generatedUserCount -gt 0) { $allGeneratedUserIds[(($globalIndex + 11) % $generatedUserCount)] } else { $null }
            $openedDate = [DateTime]::UtcNow.Date.AddDays(-1 * ($globalIndex % 720))
            $newMatterIds.Add($id)
            $row = $matters.NewRow()
            $row["Id"] = $id
            $row["Name"] = $name
            $row["MatterNumber"] = $maxMatterNumber + $i + 1
            $row["ClientId"] = $clientId
            $row["PracticeArea"] = $practiceAreas[($globalIndex % $practiceAreaCount)]
            $row["Status"] = "Open"
            $row["OpenedDate"] = $openedDate
            $row["ResponsibleUserId"] = if ($null -eq $responsibleUserId) { [DBNull]::Value } else { $responsibleUserId }
            $row["LeadPartnerId"] = if ($null -eq $leadPartnerId) { [DBNull]::Value } else { $leadPartnerId }
            $row["RequiresTimeApproval"] = (($globalIndex % 3) -eq 0)
            $row["TimeIncrementMinutes"] = [DBNull]::Value
            $row["TimeCodeSetId"] = [DBNull]::Value
            $row["Notes"] = $markerMatter
            $row["IsArchived"] = $false
            $row["CreatedAt"] = $now
            $row["UpdatedAt"] = $now
            [void]$matters.Rows.Add($row)
        }

        Write-Bulk $connection $matters "Matters"
        Write-Host "Inserted $($matters.Rows.Count) matters." -ForegroundColor Green

        $allGeneratedPartyIds = [System.Collections.Generic.List[guid]]::new()
        (Get-GeneratedIds $connection "Parties" $markerParty) | ForEach-Object { $allGeneratedPartyIds.Add($_) }
        $allGeneratedPartyIds.AddRange($newPartyIds)
        if ($newMatterIds.Count -gt 0 -and $allGeneratedPartyIds.Count -gt 0) {
            $links = New-Table `
                -Columns `
                @("Id", "MatterId", "PartyId", "Role", "Notes", "CreatedAt") `
                -Types `
                @([guid], [guid], [guid], [string], [string], [DateTimeOffset])
            $generatedPartyCount = $allGeneratedPartyIds.Count
            $partyRoleCount = $partyRoles.Count
            for ($i = 0; $i -lt $newMatterIds.Count; $i++) {
                $globalIndex = [int]($before.Matters) + $i
                $row = $links.NewRow()
                $row["Id"] = [guid]::NewGuid()
                $row["MatterId"] = $newMatterIds[$i]
                $row["PartyId"] = $allGeneratedPartyIds[($globalIndex % $generatedPartyCount)]
                $row["Role"] = $partyRoles[($globalIndex % $partyRoleCount)]
                $row["Notes"] = $markerMatterParty
                $row["CreatedAt"] = $now
                [void]$links.Rows.Add($row)
            }

            Write-Bulk $connection $links "MatterParties"
            Write-Host "Inserted $($links.Rows.Count) matter-party links." -ForegroundColor Green
        }

        $after = Get-GeneratedCounts $connection
        Write-Host "Final generated counts:" -ForegroundColor Green
        $after | Format-List

        Write-Host "Sample generated clients:" -ForegroundColor Cyan
        Invoke-Rows $connection "SELECT TOP 5 ClientNumber, Name FROM Clients WHERE Notes = @Marker ORDER BY ClientNumber DESC" @{ Marker = $markerClient } | Format-Table -AutoSize
        Write-Host "Sample generated matters:" -ForegroundColor Cyan
        Invoke-Rows $connection "SELECT TOP 5 MatterNumber, Name, PracticeArea FROM Matters WHERE Notes = @Marker ORDER BY MatterNumber DESC" @{ Marker = $markerMatter } | Format-Table -AutoSize
        Write-Host "Sample generated parties:" -ForegroundColor Cyan
        Invoke-Rows $connection "SELECT TOP 5 PartyNumber, Name, PartyType FROM Parties WHERE Notes = @Marker ORDER BY PartyNumber DESC" @{ Marker = $markerParty } | Format-Table -AutoSize
    }
    finally {
        if ($connection) {
            $connection.Dispose()
        }
    }
}
finally {
    Restore-TemporarySqlAccess
}
