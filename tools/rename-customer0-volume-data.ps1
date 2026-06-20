param(
    [string]$Server = "gwmatterforge.database.windows.net",
    [string]$Database = "cmiforge-customer0",
    [switch]$VerifyOnly,
    [switch]$Apply
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

function New-Slug([string]$value) {
    $slug = $value.ToLowerInvariant() -replace "[^a-z0-9]+", "-"
    return ($slug -replace "^-+", "" -replace "-+$", "")
}

function Normalize-Name([string]$value) {
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

function Invoke-Rows($connection, [string]$sql, [hashtable]$parameters = @{}) {
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.CommandTimeout = 120
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

function Invoke-NonQuery($connection, [string]$sql, [hashtable]$parameters) {
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.CommandTimeout = 120
    foreach ($key in $parameters.Keys) {
        $value = $parameters[$key]
        $parameter = $command.Parameters.Add("@$key", [System.Data.SqlDbType]::NVarChar)
        if ($value -is [guid]) {
            $parameter.SqlDbType = [System.Data.SqlDbType]::UniqueIdentifier
        }
        elseif ($value -is [int]) {
            $parameter.SqlDbType = [System.Data.SqlDbType]::Int
        }

        $parameter.Value = if ($null -eq $value) { [DBNull]::Value } else { $value }
    }

    return $command.ExecuteNonQuery()
}

function Set-JsonProperty($json, [string]$name, [string]$value) {
    $property = $json.PSObject.Properties[$name]
    if ($property) {
        $property.Value = $value
    }
}

$companyPrefixes = @(
    "Alder", "Brightline", "Cedar Ridge", "Northstar", "Summit", "Blue Harbor", "Ironwood", "Redwood", "Clearwater", "Evergreen",
    "Silvergate", "Stonebridge", "Riverbend", "Pioneer", "Horizon", "Maple Street", "Juniper", "Keystone", "Lakeside", "Oakmont",
    "Prairie", "Beacon", "Harborview", "Mariner", "Crescent", "Ridgeline", "Westbridge", "Greenfield", "Meridian", "First Valley",
    "Copperline", "Elm & Vine", "Sagebrush", "Highland", "Parkside", "Granite", "Vantage", "Crosswind", "Fairmont", "Magnolia"
)
$companySuffixes = @(
    "Manufacturing", "Logistics", "Medical Group", "Foods", "Insurance", "Real Estate", "Energy", "Construction", "Systems", "Capital",
    "Hospitality", "Retail", "Pharma", "Transport", "Equipment", "Bank", "Advisory", "Holdings", "Software", "Partners"
)
$firstNames = @(
    "Ava", "Mason", "Sophia", "Ethan", "Mia", "Noah", "Grace", "Lucas", "Nora", "Liam",
    "Elena", "Owen", "Isla", "Caleb", "Maya", "Henry", "Zoe", "Miles", "Lena", "Julian",
    "Amara", "Theo", "Claire", "Nolan", "Ivy", "Elliot", "Riley", "Jonah", "Tessa", "Adrian"
)
$lastNames = @(
    "Chen", "Patel", "Morgan", "Reed", "Bennett", "Santos", "Walker", "Kim", "Lawson", "Brooks",
    "Diaz", "Nguyen", "Parker", "Ellis", "Rivera", "Hughes", "Coleman", "Foster", "Hayes", "Murphy",
    "Carter", "Bailey", "Price", "Wright", "Ross", "Sanders", "Mitchell", "Powell", "Cooper", "Bryant"
)
$matterTypes = @(
    "Contract Review", "Employment Counseling", "Lease Negotiation", "Asset Purchase", "Vendor Dispute",
    "Regulatory Response", "Trademark Review", "Insurance Coverage", "Board Advisory", "Collections Matter",
    "Data Privacy Review", "Construction Claim", "Shareholder Dispute", "Real Estate Closing", "Supply Agreement"
)
$practiceAreas = @("Corporate", "Litigation", "Employment", "Real Estate", "Insurance", "Intellectual Property", "Regulatory", "Finance")
$titles = @("Partner", "Associate", "Paralegal", "Conflicts Analyst", "Intake Coordinator", "Legal Assistant", "Records Specialist", "Practice Manager")
$cities = @("Austin", "Denver", "Chicago", "Raleigh", "Phoenix", "Columbus", "Nashville", "Portland", "Atlanta", "Milwaukee")
$states = @("TX", "CO", "IL", "NC", "AZ", "OH", "TN", "OR", "GA", "WI")

function Get-PersonName([int]$index) {
    $first = $firstNames[$index % $firstNames.Count]
    $last = $lastNames[[math]::Floor($index / $firstNames.Count) % $lastNames.Count]
    return [pscustomobject]@{ First = $first; Last = $last; Display = "$first $last" }
}

function Get-CompanyName([int]$index) {
    $prefix = $companyPrefixes[$index % $companyPrefixes.Count]
    $suffix = $companySuffixes[[math]::Floor($index / $companyPrefixes.Count) % $companySuffixes.Count]
    return "$prefix $suffix"
}

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
    if ($VerifyOnly) {
        $remaining = Invoke-Rows $connection @"
SELECT
    (SELECT COUNT(*) FROM Clients WHERE Name LIKE N'Volume Test Client %') AS VolumeClients,
    (SELECT COUNT(*) FROM Matters WHERE Name LIKE N'Volume Test Matter %') AS VolumeMatters,
    (SELECT COUNT(*) FROM Parties WHERE Name LIKE N'Volume Test Party %') AS VolumeParties,
    (SELECT COUNT(*) FROM Users WHERE DisplayName LIKE N'Volume%' OR Email LIKE N'%volume%' OR EntraUserPrincipalName LIKE N'%volume%') AS VolumeUsers,
    (SELECT COUNT(*) FROM FormSubmissions WHERE DataJson LIKE N'%volumeTest%') AS VolumeSubmissions
"@
        $samples = Invoke-Rows $connection @"
SELECT TOP 8 'Client' AS EntityType, CAST(ClientNumber AS nvarchar(20)) AS Number, Name
FROM Clients
WHERE Notes = N'Renamed generated volume-test client for realistic search testing.'
UNION ALL
SELECT TOP 8 'Matter' AS EntityType, CAST(MatterNumber AS nvarchar(20)) AS Number, Name
FROM Matters
WHERE Notes = N'Renamed generated volume-test matter for realistic search testing.'
UNION ALL
SELECT TOP 8 'Party' AS EntityType, CAST(PartyNumber AS nvarchar(20)) AS Number, Name
FROM Parties
WHERE Notes = N'Renamed generated volume-test party for realistic conflict-search testing.'
"@
        Write-Host "Remaining old-name counts:" -ForegroundColor Cyan
        $remaining | Format-List
        Write-Host "Sample renamed records:" -ForegroundColor Cyan
        $samples | Format-Table -AutoSize
        return
    }

    $clients = Invoke-Rows $connection "SELECT Id, ClientNumber, Name FROM Clients WHERE Name LIKE N'Volume Test Client %' OR Notes = N'Renamed generated volume-test client for realistic search testing.' ORDER BY ClientNumber"
    $matters = Invoke-Rows $connection "SELECT m.Id, m.MatterNumber, m.Name, m.ClientId, c.Name AS ClientName FROM Matters m LEFT JOIN Clients c ON c.Id = m.ClientId WHERE m.Name LIKE N'Volume Test Matter %' OR m.Notes = N'Renamed generated volume-test matter for realistic search testing.' ORDER BY m.MatterNumber"
    $parties = Invoke-Rows $connection "SELECT Id, PartyNumber, Name FROM Parties WHERE Name LIKE N'Volume Test Party %' OR Notes = N'Renamed generated volume-test party for realistic conflict-search testing.' ORDER BY PartyNumber"
    $users = Invoke-Rows $connection "SELECT Id, SystemId, FirstName, LastName, DisplayName, Email FROM Users WHERE DisplayName LIKE N'Volume%' OR Email LIKE N'%volume%' OR EntraUserPrincipalName LIKE N'%volume%' OR Email LIKE N'%@customer0.example' OR EntraUserPrincipalName LIKE N'%@customer0.example' ORDER BY SystemId"
    $submissions = Invoke-Rows $connection "SELECT Id, SubmissionNumber, SubmitterName, SubmitterUserId, ClientId, MatterId, DataJson FROM FormSubmissions WHERE DataJson LIKE N'%volumeTest%' ORDER BY SubmissionNumber"

    Write-Host "Matched $($clients.Count) clients, $($matters.Count) matters, $($parties.Count) parties, $($users.Count) users, and $($submissions.Count) submissions." -ForegroundColor Cyan
    if (-not $Apply) {
        Write-Host "Preview only. Rerun with -Apply to update customer0." -ForegroundColor Yellow
    }

    $clientMap = @{}
    $matterMap = @{}
    $userMap = @{}
    $updated = [ordered]@{
        Clients = 0
        Matters = 0
        Parties = 0
        PartyAliases = 0
        Users = 0
        Submissions = 0
    }

    for ($i = 0; $i -lt $clients.Count; $i++) {
        $client = $clients[$i]
        $contact = Get-PersonName ($i + 7)
        $isIndividual = ($i % 4) -eq 0
        $name = if ($isIndividual) { (Get-PersonName ($i + 37)).Display } else { Get-CompanyName $i }
        $domain = "$(New-Slug $name).example"
        $cityIndex = $i % $cities.Count
        $newClient = [pscustomobject]@{
            Name = $name
            PrimaryContact = $contact.Display
            Email = "intake@$domain"
            Phone = "555-01$("{0:D2}" -f ($i % 100))"
            AddressLine1 = "$(($i % 900) + 100) $($companyPrefixes[$i % $companyPrefixes.Count]) Ave"
            City = $cities[$cityIndex]
            State = $states[$cityIndex]
            PostalCode = "{0:D5}" -f (70000 + ($i % 9999))
        }
        $clientMap[$client.Id.ToString()] = $newClient

        if ($i -lt 5) {
            Write-Host "Client: $($client.Name) -> $($newClient.Name)"
        }

        if ($Apply) {
            $updated.Clients += Invoke-NonQuery $connection @"
UPDATE Clients
SET Name = @Name,
    PrimaryContact = @PrimaryContact,
    Email = @Email,
    Phone = @Phone,
    AddressLine1 = @AddressLine1,
    City = @City,
    State = @State,
    PostalCode = @PostalCode,
    Country = N'United States',
    Notes = N'Renamed generated volume-test client for realistic search testing.',
    UpdatedAt = SYSDATETIMEOFFSET()
WHERE Id = @Id
"@ @{
                Id = $client.Id
                Name = $newClient.Name
                PrimaryContact = $newClient.PrimaryContact
                Email = $newClient.Email
                Phone = $newClient.Phone
                AddressLine1 = $newClient.AddressLine1
                City = $newClient.City
                State = $newClient.State
                PostalCode = $newClient.PostalCode
            }
        }
    }

    for ($i = 0; $i -lt $matters.Count; $i++) {
        $matter = $matters[$i]
        $clientName = if ($matter.ClientId -and $clientMap.ContainsKey($matter.ClientId.ToString())) { $clientMap[$matter.ClientId.ToString()].Name } else { $matter.ClientName }
        $matterType = $matterTypes[$i % $matterTypes.Count]
        $name = "$clientName - $matterType"
        $matterMap[$matter.Id.ToString()] = [pscustomobject]@{
            Name = $name
            PracticeArea = $practiceAreas[$i % $practiceAreas.Count]
        }

        if ($i -lt 5) {
            Write-Host "Matter: $($matter.Name) -> $name"
        }

        if ($Apply) {
            $updated.Matters += Invoke-NonQuery $connection @"
UPDATE Matters
SET Name = @Name,
    PracticeArea = @PracticeArea,
    Notes = N'Renamed generated volume-test matter for realistic search testing.',
    UpdatedAt = SYSDATETIMEOFFSET()
WHERE Id = @Id
"@ @{
                Id = $matter.Id
                Name = $name
                PracticeArea = $matterMap[$matter.Id.ToString()].PracticeArea
            }
        }
    }

    for ($i = 0; $i -lt $parties.Count; $i++) {
        $party = $parties[$i]
        if (($i % 11) -eq 0) {
            $name = "City of $($cities[$i % $cities.Count])"
            $partyType = "Government"
        }
        elseif (($i % 5) -eq 0) {
            $name = (Get-PersonName ($i + 83)).Display
            $partyType = "Individual"
        }
        else {
            $name = Get-CompanyName ($i + 211)
            $partyType = "Organization"
        }

        $normalized = Normalize-Name $name
        if ($i -lt 5) {
            Write-Host "Party: $($party.Name) -> $name"
        }

        if ($Apply) {
            $updated.Parties += Invoke-NonQuery $connection @"
UPDATE Parties
SET Name = @Name,
    NormalizedName = @NormalizedName,
    PartyType = @PartyType,
    Notes = N'Renamed generated volume-test party for realistic conflict-search testing.',
    UpdatedAt = SYSDATETIMEOFFSET()
WHERE Id = @Id
"@ @{
                Id = $party.Id
                Name = $name
                NormalizedName = $normalized
                PartyType = $partyType
            }

            $alias = if ($partyType -eq "Organization") { "$($name.Split(" ")[0]) DBA" } else { $name }
            $updated.PartyAliases += Invoke-NonQuery $connection @"
UPDATE PartyAliases
SET Alias = @Alias,
    NormalizedAlias = @NormalizedAlias,
    Notes = N'Renamed generated volume-test alias.',
    CreatedAt = CreatedAt
WHERE PartyId = @PartyId
  AND Alias LIKE N'Volume Test Party %'
"@ @{
                PartyId = $party.Id
                Alias = $alias
                NormalizedAlias = Normalize-Name $alias
            }
        }
    }

    for ($i = 0; $i -lt $users.Count; $i++) {
        $user = $users[$i]
        $person = Get-PersonName ($i + 131)
        $email = "$(New-Slug $person.First).$(New-Slug $person.Last)@customer0.example"
        $newUser = [pscustomobject]@{
            First = $person.First
            Last = $person.Last
            Display = $person.Display
            Email = $email
            Title = $titles[$i % $titles.Count]
        }
        $userMap[$user.Id.ToString()] = $newUser

        Write-Host "User: $($user.DisplayName) -> $($newUser.Display)"

        if ($Apply) {
            $updated.Users += Invoke-NonQuery $connection @"
UPDATE Users
SET FirstName = @FirstName,
    LastName = @LastName,
    DisplayName = @DisplayName,
    Email = @Email,
    EntraUserPrincipalName = @Email,
    Title = @Title,
    UpdatedAt = SYSDATETIMEOFFSET()
WHERE Id = @Id
"@ @{
                Id = $user.Id
                FirstName = $newUser.First
                LastName = $newUser.Last
                DisplayName = $newUser.Display
                Email = $newUser.Email
                Title = $newUser.Title
            }
        }
    }

    for ($i = 0; $i -lt $submissions.Count; $i++) {
        $submission = $submissions[$i]
        $clientName = if ($submission.ClientId -and $clientMap.ContainsKey($submission.ClientId.ToString())) { $clientMap[$submission.ClientId.ToString()].Name } else { "Prospective Client $($i + 1)" }
        $matterName = if ($submission.MatterId -and $matterMap.ContainsKey($submission.MatterId.ToString())) { $matterMap[$submission.MatterId.ToString()].Name } else { "$clientName - Intake Review" }
        $submitterUserId = $null
        if ($users.Count -gt 0) {
            $submitterUserId = $users[$i % $users.Count].Id
        }

        $submitter = if ($submitterUserId -and $userMap.ContainsKey($submitterUserId.ToString())) { $userMap[$submitterUserId.ToString()].Display } else { (Get-PersonName ($i + 211)).Display }

        $dataJson = $submission.DataJson
        try {
            $json = $submission.DataJson | ConvertFrom-Json -Depth 40
            Set-JsonProperty $json "clientName" $clientName
            Set-JsonProperty $json "companyName" $clientName
            Set-JsonProperty $json "matterName" $matterName
            Set-JsonProperty $json "caseName" $matterName
            Set-JsonProperty $json "summary" "$matterName needs intake review, conflicts clearance, and approval routing."
            Set-JsonProperty $json "description" "Generated realistic volume-test submission for search, dashboard, and workflow testing."
            Set-JsonProperty $json "notes" "Generated realistic volume-test submission for search testing."
            $dataJson = $json | ConvertTo-Json -Depth 40 -Compress
        }
        catch {
            Write-Warning "Submission $($submission.SubmissionNumber) JSON could not be parsed; leaving DataJson unchanged."
        }

        if ($i -lt 5) {
            Write-Host "Submission $($submission.SubmissionNumber): $($submission.SubmitterName) -> $submitter"
        }

        if ($Apply) {
            $updated.Submissions += Invoke-NonQuery $connection @"
UPDATE FormSubmissions
SET SubmitterName = @SubmitterName,
    SubmitterUserId = @SubmitterUserId,
    DataJson = @DataJson
WHERE Id = @Id
"@ @{
                Id = $submission.Id
                SubmitterUserId = $submitterUserId
                SubmitterName = $submitter
                DataJson = $dataJson
            }
        }
    }

    if ($Apply) {
        Write-Host "Updated rows:" -ForegroundColor Green
        $updated.GetEnumerator() | ForEach-Object { Write-Host "  $($_.Key): $($_.Value)" }
    }
}
finally {
    $connection.Close()
}
