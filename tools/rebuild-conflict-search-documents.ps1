param(
    [string]$ResourceGroup = "gw-rg",
    [string]$ServerName = "gwmatterforge",
    [string]$Server = "gwmatterforge.database.windows.net",
    [string]$Database = "cmiforge-customer0",
    [switch]$SkipHistory,
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

$catalogName = "CMIForgeConflictSearchCatalog"
$tempFirewallRuleName = "cmiforge-conflict-search-document-warmup"
$originalPublicNetworkAccess = $null
$temporaryAccessEnabled = $false

function Require-Command {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' is not installed or not on PATH."
    }
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

function Invoke-NonQuery($connection, [string]$sql, [int]$timeoutSeconds = 900) {
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.CommandTimeout = $timeoutSeconds
    return $command.ExecuteNonQuery()
}

function Invoke-Rows($connection, [string]$sql, [int]$timeoutSeconds = 900) {
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.CommandTimeout = $timeoutSeconds
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

function Invoke-Scalar($connection, [string]$sql, [int]$timeoutSeconds = 900) {
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.CommandTimeout = $timeoutSeconds
    return $command.ExecuteScalar()
}

function Write-Section {
    param([string]$Message)
    Write-Host ""
    Write-Host $Message -ForegroundColor Cyan
}

$ensureFullTextSql = @"
IF OBJECT_ID(N'[dbo].[ConflictSearchDocuments]', N'U') IS NULL
BEGIN
    THROW 51000, 'ConflictSearchDocuments does not exist. Run EF migrations before warming conflict search documents.', 1;
END;

IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE [name] = N'$catalogName')
    BEGIN
        CREATE FULLTEXT CATALOG [$catalogName] WITH ACCENT_SENSITIVITY = OFF;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[ConflictSearchDocuments]'))
    BEGIN
        CREATE FULLTEXT INDEX ON [dbo].[ConflictSearchDocuments]
        (
            [SearchableText] LANGUAGE 1033,
            [NormalizedSearchableText] LANGUAGE 1033
        )
        KEY INDEX [PK_ConflictSearchDocuments]
        ON [$catalogName]
        WITH CHANGE_TRACKING AUTO;
    END;
END;
"@

$liveSourceTypesSql = "N'Party', N'PartyAlias', N'Client', N'ClientAlias', N'Matter'"
$allSourceTypesSql = "$liveSourceTypesSql, N'PriorSearch', N'PriorResult', N'ArchivedResult'"
$sourceTypesSql = if ($SkipHistory) { $liveSourceTypesSql } else { $allSourceTypesSql }

$rebuildSql = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now datetimeoffset = SYSUTCDATETIME();

BEGIN TRANSACTION;

DELETE FROM [dbo].[ConflictSearchDocuments]
WHERE [SourceType] IN ($sourceTypesSql);

;WITH [PartyAliasText] AS
(
    SELECT
        [PartyId],
        [Aliases] = STRING_AGG(CONVERT(nvarchar(max), [Alias]), N' ')
    FROM [dbo].[PartyAliases]
    GROUP BY [PartyId]
)
INSERT INTO [dbo].[ConflictSearchDocuments]
(
    [Id], [SourceType], [SourceId], [PartyId], [MatterId], [ClientId],
    [MatchedName], [MatchedOn], [MatchType], [PartyRole],
    [SearchableText], [NormalizedSearchableText], [SortNumber], [CreatedAt], [UpdatedAt]
)
SELECT
    NEWID(),
    N'Party',
    [p].[Id],
    [p].[Id],
    NULL,
    NULL,
    LEFT([p].[Name], 240),
    N'Party name',
    N'Name match',
    N'Global party',
    CONCAT_WS(N' ', [p].[Name], [p].[NormalizedName], [p].[PartyType], [p].[Status], [p].[Notes], [pa].[Aliases]),
    LOWER(CONCAT_WS(N' ', [p].[Name], [p].[NormalizedName], [p].[Notes], [pa].[Aliases])),
    [p].[PartyNumber],
    @Now,
    @Now
FROM [dbo].[Parties] AS [p]
LEFT JOIN [PartyAliasText] AS [pa] ON [pa].[PartyId] = [p].[Id];

INSERT INTO [dbo].[ConflictSearchDocuments]
(
    [Id], [SourceType], [SourceId], [PartyId], [MatterId], [ClientId],
    [MatchedName], [MatchedOn], [MatchType], [PartyRole],
    [SearchableText], [NormalizedSearchableText], [SortNumber], [CreatedAt], [UpdatedAt]
)
SELECT
    NEWID(),
    N'PartyAlias',
    [a].[Id],
    [a].[PartyId],
    NULL,
    NULL,
    LEFT([a].[Alias], 240),
    N'Alias',
    N'Alias match',
    N'Global party',
    CONCAT_WS(N' ', [a].[Alias], [a].[NormalizedAlias], [a].[Notes], [p].[Name], [p].[PartyType]),
    LOWER(CONCAT_WS(N' ', [a].[Alias], [a].[NormalizedAlias], [a].[Notes], [p].[Name])),
    [p].[PartyNumber],
    @Now,
    @Now
FROM [dbo].[PartyAliases] AS [a]
INNER JOIN [dbo].[Parties] AS [p] ON [p].[Id] = [a].[PartyId];

;WITH [ClientAliasText] AS
(
    SELECT
        [ClientId],
        [Aliases] = STRING_AGG(CONVERT(nvarchar(max), [Alias]), N' ')
    FROM [dbo].[ClientAliases]
    GROUP BY [ClientId]
)
INSERT INTO [dbo].[ConflictSearchDocuments]
(
    [Id], [SourceType], [SourceId], [PartyId], [MatterId], [ClientId],
    [MatchedName], [MatchedOn], [MatchType], [PartyRole],
    [SearchableText], [NormalizedSearchableText], [SortNumber], [CreatedAt], [UpdatedAt]
)
SELECT
    NEWID(),
    N'Client',
    [c].[Id],
    NULL,
    NULL,
    [c].[Id],
    LEFT([c].[Name], 240),
    N'Client name',
    N'Name match',
    N'Client',
    CONCAT_WS(N' ', [c].[Name], [c].[PrimaryContact], [c].[Email], [c].[Notes], [c].[Status], [ca].[Aliases]),
    LOWER(CONCAT_WS(N' ', [c].[Name], [c].[PrimaryContact], [c].[Notes], [ca].[Aliases])),
    [c].[ClientNumber],
    @Now,
    @Now
FROM [dbo].[Clients] AS [c]
LEFT JOIN [ClientAliasText] AS [ca] ON [ca].[ClientId] = [c].[Id];

INSERT INTO [dbo].[ConflictSearchDocuments]
(
    [Id], [SourceType], [SourceId], [PartyId], [MatterId], [ClientId],
    [MatchedName], [MatchedOn], [MatchType], [PartyRole],
    [SearchableText], [NormalizedSearchableText], [SortNumber], [CreatedAt], [UpdatedAt]
)
SELECT
    NEWID(),
    N'ClientAlias',
    [a].[Id],
    NULL,
    NULL,
    [a].[ClientId],
    LEFT([a].[Alias], 240),
    N'Client alias',
    N'Alias match',
    N'Client',
    CONCAT_WS(N' ', [a].[Alias], [a].[NormalizedAlias], [a].[Notes], [c].[Name]),
    LOWER(CONCAT_WS(N' ', [a].[Alias], [a].[NormalizedAlias], [a].[Notes], [c].[Name])),
    [c].[ClientNumber],
    @Now,
    @Now
FROM [dbo].[ClientAliases] AS [a]
INNER JOIN [dbo].[Clients] AS [c] ON [c].[Id] = [a].[ClientId];

INSERT INTO [dbo].[ConflictSearchDocuments]
(
    [Id], [SourceType], [SourceId], [PartyId], [MatterId], [ClientId],
    [MatchedName], [MatchedOn], [MatchType], [PartyRole],
    [SearchableText], [NormalizedSearchableText], [SortNumber], [CreatedAt], [UpdatedAt]
)
SELECT
    NEWID(),
    N'Matter',
    [m].[Id],
    NULL,
    [m].[Id],
    [m].[ClientId],
    LEFT([m].[Name], 240),
    N'Matter name',
    N'Name match',
    N'Matter',
    CONCAT_WS(N' ', [m].[Name], [m].[PracticeArea], [m].[Status], [m].[Notes], [c].[Name]),
    LOWER(CONCAT_WS(N' ', [m].[Name], [m].[PracticeArea], [m].[Notes], [c].[Name])),
    [m].[MatterNumber],
    @Now,
    @Now
FROM [dbo].[Matters] AS [m]
LEFT JOIN [dbo].[Clients] AS [c] ON [c].[Id] = [m].[ClientId];
"@

if (-not $SkipHistory) {
    $rebuildSql += @"

INSERT INTO [dbo].[ConflictSearchDocuments]
(
    [Id], [SourceType], [SourceId], [PartyId], [MatterId], [ClientId],
    [MatchedName], [MatchedOn], [MatchType], [PartyRole],
    [SearchableText], [NormalizedSearchableText], [SortNumber], [CreatedAt], [UpdatedAt]
)
SELECT
    NEWID(),
    N'PriorSearch',
    [cs].[Id],
    NULL,
    [cs].[MatterId],
    [m].[ClientId],
    LEFT(CONCAT(N'Search C-', RIGHT(CONCAT(N'00000000', CONVERT(nvarchar(20), [cs].[SearchNumber])), 8), N': ', [cs].[SearchName]), 240),
    N'Prior conflict search',
    N'Prior search text match',
    N'Prior search history',
    CONCAT_WS(N' ', [cs].[SearchName], [cs].[SearchTerms], [cs].[ReviewNotes], [cs].[AiSummary], [cs].[Status], [cs].[ReviewerDecision], [m].[Name], [c].[Name]),
    LOWER(CONCAT_WS(N' ', [cs].[SearchName], [cs].[SearchTerms], [cs].[ReviewNotes], [cs].[AiSummary], [cs].[Status], [cs].[ReviewerDecision], [m].[Name], [c].[Name])),
    [cs].[SearchNumber],
    @Now,
    @Now
FROM [dbo].[Conflicts] AS [cs]
LEFT JOIN [dbo].[Matters] AS [m] ON [m].[Id] = [cs].[MatterId]
LEFT JOIN [dbo].[Clients] AS [c] ON [c].[Id] = [m].[ClientId];

INSERT INTO [dbo].[ConflictSearchDocuments]
(
    [Id], [SourceType], [SourceId], [PartyId], [MatterId], [ClientId],
    [MatchedName], [MatchedOn], [MatchType], [PartyRole],
    [SearchableText], [NormalizedSearchableText], [SortNumber], [CreatedAt], [UpdatedAt]
)
SELECT
    NEWID(),
    N'PriorResult',
    [r].[Id],
    [r].[PartyId],
    [r].[MatterId],
    [r].[ClientId],
    LEFT(CONCAT(N'Search C-', RIGHT(CONCAT(N'00000000', CONVERT(nvarchar(20), ISNULL([cs].[SearchNumber], 0))), 8), N' result: ', [r].[MatchedName]), 240),
    N'Prior result clearance notes',
    N'Prior result notes match',
    N'Prior result history',
    CONCAT_WS(N' ', [r].[ClearanceNotes], [r].[ClearanceStatus], [r].[SearchTerm], [r].[MatchedName], [r].[MatchedOn], [r].[MatchType], [r].[PartyRole], [r].[RiskLevel], [r].[Explanation], [r].[AiAssessment], [p].[Name], [m].[Name], [c].[Name]),
    LOWER(CONCAT_WS(N' ', [r].[ClearanceNotes], [r].[ClearanceStatus], [r].[SearchTerm], [r].[MatchedName], [r].[MatchedOn], [r].[MatchType], [r].[PartyRole], [r].[RiskLevel], [r].[Explanation], [r].[AiAssessment], [p].[Name], [m].[Name], [c].[Name])),
    ISNULL([cs].[SearchNumber], 0),
    @Now,
    @Now
FROM [dbo].[ConflictsResults] AS [r]
LEFT JOIN [dbo].[Conflicts] AS [cs] ON [cs].[Id] = [r].[ConflictSearchId]
LEFT JOIN [dbo].[Parties] AS [p] ON [p].[Id] = [r].[PartyId]
LEFT JOIN [dbo].[Matters] AS [m] ON [m].[Id] = [r].[MatterId]
LEFT JOIN [dbo].[Clients] AS [c] ON [c].[Id] = [r].[ClientId]
WHERE [r].[ClearanceNotes] <> N'';

INSERT INTO [dbo].[ConflictSearchDocuments]
(
    [Id], [SourceType], [SourceId], [PartyId], [MatterId], [ClientId],
    [MatchedName], [MatchedOn], [MatchType], [PartyRole],
    [SearchableText], [NormalizedSearchableText], [SortNumber], [CreatedAt], [UpdatedAt]
)
SELECT
    NEWID(),
    N'ArchivedResult',
    [h].[Id],
    [h].[PartyId],
    [h].[MatterId],
    [h].[ClientId],
    LEFT(CONCAT(N'Archived search C-', RIGHT(CONCAT(N'00000000', CONVERT(nvarchar(20), [h].[SearchNumber])), 8), N' result: ', [h].[MatchedName]), 240),
    N'Archived result clearance notes',
    N'Archived result notes match',
    N'Prior result history',
    CONCAT_WS(N' ', [h].[ClearanceNotes], [h].[ClearanceStatus], [h].[SearchTerm], [h].[MatchedName], [h].[MatchedOn], [h].[MatchType], [h].[PartyRole], [h].[RiskLevel], [h].[Explanation], [h].[AiAssessment], [h].[PartyName], [h].[MatterName], [h].[ClientName], [h].[SearchableText]),
    LOWER(CONCAT_WS(N' ', [h].[ClearanceNotes], [h].[ClearanceStatus], [h].[SearchTerm], [h].[MatchedName], [h].[MatchedOn], [h].[MatchType], [h].[PartyRole], [h].[RiskLevel], [h].[Explanation], [h].[AiAssessment], [h].[PartyName], [h].[MatterName], [h].[ClientName], [h].[NormalizedSearchableText])),
    [h].[SearchNumber],
    @Now,
    @Now
FROM [dbo].[ConflictsHitArchives] AS [h]
WHERE [h].[ClearanceNotes] <> N'';
"@
}

$rebuildSql += @"

COMMIT TRANSACTION;
"@

$startPopulationSql = @"
IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[ConflictSearchDocuments]'))
BEGIN
    ALTER FULLTEXT INDEX ON [dbo].[ConflictSearchDocuments] START FULL POPULATION;
END;
"@

$statusSql = @"
SELECT
    [SourceType],
    [DocumentCount] = COUNT_BIG(*)
FROM [dbo].[ConflictSearchDocuments]
GROUP BY [SourceType]
ORDER BY [SourceType];
"@

$fullTextStatusSql = @"
SELECT
    [FullTextInstalled] = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'),
    [FullTextIndexExists] = CASE WHEN EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[ConflictSearchDocuments]')) THEN 1 ELSE 0 END,
    [TableFullTextPopulateStatus] = FULLTEXTCATALOGPROPERTY(N'$catalogName', 'PopulateStatus'),
    [TotalDocuments] = (SELECT COUNT_BIG(*) FROM [dbo].[ConflictSearchDocuments]);
"@

$sampleSql = @"
IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[ConflictSearchDocuments]'))
BEGIN
    SELECT TOP (10)
        [d].[SourceType],
        [d].[MatchedName],
        [ft].[RANK]
    FROM CONTAINSTABLE([dbo].[ConflictSearchDocuments], ([SearchableText], [NormalizedSearchableText]), N'"Alder*"', 10) AS [ft]
    INNER JOIN [dbo].[ConflictSearchDocuments] AS [d] ON [d].[Id] = [ft].[KEY]
    ORDER BY [ft].[RANK] DESC, [d].[SortNumber];
END;
"@

Require-Command "az"

try {
    Enable-TemporarySqlAccess

    Write-Section "Getting Azure SQL access token..."
    $token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Azure CLI did not return a database token. Run az login and try again."
    }

    $connectionString = "Server=tcp:$Server,1433;Initial Catalog=$Database;Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;"
    $connection = [Microsoft.Data.SqlClient.SqlConnection]::new($connectionString)
    $connection.AccessToken = $token
    $connection.Open()

    try {
        Write-Section "Ensuring full-text catalog and index..."
        [void](Invoke-NonQuery $connection $ensureFullTextSql 300)

        Write-Section "Rebuilding conflict search documents in $Database..."
        $elapsed = [System.Diagnostics.Stopwatch]::StartNew()
        [void](Invoke-NonQuery $connection $rebuildSql 1200)
        $elapsed.Stop()
        Write-Host "Rebuilt document table in $([Math]::Round($elapsed.Elapsed.TotalSeconds, 1)) seconds." -ForegroundColor Green

        Write-Section "Starting full-text population..."
        try {
            [void](Invoke-NonQuery $connection $startPopulationSql 300)
            Write-Host "Full-text population requested." -ForegroundColor Green
        }
        catch {
            if ($_.Exception.Message -like "*population is already in progress*") {
                Write-Host "Full-text population is already in progress." -ForegroundColor Yellow
            }
            else {
                throw
            }
        }

        Write-Section "Document counts and full-text status..."
        Invoke-Rows $connection $statusSql 300 | Format-Table -AutoSize
        Invoke-Rows $connection $fullTextStatusSql 300 | Format-Table -AutoSize

        Write-Section "Sample full-text hits for Alder..."
        $sampleRows = Invoke-Rows $connection $sampleSql 300
        if ($sampleRows.Count -eq 0) {
            Write-Host "No sample rows returned yet. Full-text population may still be catching up." -ForegroundColor Yellow
        }
        else {
            $sampleRows | Format-Table -AutoSize
        }
    }
    finally {
        $connection.Close()
    }
}
finally {
    Restore-TemporarySqlAccess
}
