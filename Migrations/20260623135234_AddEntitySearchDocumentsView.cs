using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CMIForge.Migrations
{
    /// <inheritdoc />
    public partial class AddEntitySearchDocumentsView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER VIEW [dbo].[EntitySearchDocuments]
                AS
                    SELECT
                        [Id] = CONVERT(nvarchar(100), CONCAT(N'Client-', CONVERT(nvarchar(36), [c].[Id]))),
                        [SourceType] = CONVERT(nvarchar(40), N'Client'),
                        [SourceId] = CONVERT(nvarchar(36), [c].[Id]),
                        [ParentSourceType] = CONVERT(nvarchar(40), N''),
                        [ParentSourceId] = CONVERT(nvarchar(36), N''),
                        [ClientId] = CONVERT(nvarchar(36), [c].[Id]),
                        [MatterId] = CONVERT(nvarchar(36), N''),
                        [PartyId] = CONVERT(nvarchar(36), N''),
                        [ContactId] = CONVERT(nvarchar(36), N''),
                        [DisplayName] = CONVERT(nvarchar(300), [c].[Name]),
                        [SecondaryText] = CONVERT(nvarchar(500), CONCAT(N'Client ', FORMAT([c].[ClientNumber], '00000000'), N' · ', [c].[Status])),
                        [Status] = CONVERT(nvarchar(80), [c].[Status]),
                        [EntityNumber] = CONVERT(int, [c].[ClientNumber]),
                        [IsArchived] = CONVERT(bit, [c].[IsArchived]),
                        [SearchableText] = CONVERT(nvarchar(max), CONCAT_WS(N' ', [c].[Name], [c].[PrimaryContact], [c].[Email], [c].[Phone], [c].[City], [c].[State], [c].[Country], [c].[Notes])),
                        [UpdatedAt] = CONVERT(datetimeoffset, [c].[UpdatedAt])
                    FROM [dbo].[Clients] AS [c]

                    UNION ALL

                    SELECT
                        [Id] = CONVERT(nvarchar(100), CONCAT(N'ClientAlias-', CONVERT(nvarchar(36), [a].[Id]))),
                        [SourceType] = CONVERT(nvarchar(40), N'ClientAlias'),
                        [SourceId] = CONVERT(nvarchar(36), [a].[Id]),
                        [ParentSourceType] = CONVERT(nvarchar(40), N'Client'),
                        [ParentSourceId] = CONVERT(nvarchar(36), [c].[Id]),
                        [ClientId] = CONVERT(nvarchar(36), [c].[Id]),
                        [MatterId] = CONVERT(nvarchar(36), N''),
                        [PartyId] = CONVERT(nvarchar(36), N''),
                        [ContactId] = CONVERT(nvarchar(36), N''),
                        [DisplayName] = CONVERT(nvarchar(300), [a].[Alias]),
                        [SecondaryText] = CONVERT(nvarchar(500), CONCAT(N'Alias for client ', FORMAT([c].[ClientNumber], '00000000'), N' · ', [c].[Name])),
                        [Status] = CONVERT(nvarchar(80), [c].[Status]),
                        [EntityNumber] = CONVERT(int, [c].[ClientNumber]),
                        [IsArchived] = CONVERT(bit, [c].[IsArchived]),
                        [SearchableText] = CONVERT(nvarchar(max), CONCAT_WS(N' ', [a].[Alias], [a].[NormalizedAlias], [a].[Notes], [c].[Name], [c].[PrimaryContact], [c].[Email], [c].[Phone], [c].[Notes])),
                        [UpdatedAt] = CONVERT(datetimeoffset, [c].[UpdatedAt])
                    FROM [dbo].[ClientAliases] AS [a]
                    INNER JOIN [dbo].[Clients] AS [c] ON [c].[Id] = [a].[ClientId]

                    UNION ALL

                    SELECT
                        [Id] = CONVERT(nvarchar(100), CONCAT(N'Matter-', CONVERT(nvarchar(36), [m].[Id]))),
                        [SourceType] = CONVERT(nvarchar(40), N'Matter'),
                        [SourceId] = CONVERT(nvarchar(36), [m].[Id]),
                        [ParentSourceType] = CONVERT(nvarchar(40), N'Client'),
                        [ParentSourceId] = CONVERT(nvarchar(36), [m].[ClientId]),
                        [ClientId] = CONVERT(nvarchar(36), [m].[ClientId]),
                        [MatterId] = CONVERT(nvarchar(36), [m].[Id]),
                        [PartyId] = CONVERT(nvarchar(36), N''),
                        [ContactId] = CONVERT(nvarchar(36), N''),
                        [DisplayName] = CONVERT(nvarchar(300), [m].[Name]),
                        [SecondaryText] = CONVERT(nvarchar(500), CONCAT(N'Matter ', FORMAT([m].[MatterNumber], '00000000'), N' · ', [c].[Name], N' · ', [m].[Status])),
                        [Status] = CONVERT(nvarchar(80), [m].[Status]),
                        [EntityNumber] = CONVERT(int, [m].[MatterNumber]),
                        [IsArchived] = CONVERT(bit, CASE WHEN [m].[IsArchived] = 1 OR [c].[IsArchived] = 1 THEN 1 ELSE 0 END),
                        [SearchableText] = CONVERT(nvarchar(max), CONCAT_WS(N' ', [m].[Name], [m].[PracticeArea], [m].[Status], [m].[Notes], [c].[Name], [c].[PrimaryContact], [c].[Email])),
                        [UpdatedAt] = CONVERT(datetimeoffset, [m].[UpdatedAt])
                    FROM [dbo].[Matters] AS [m]
                    INNER JOIN [dbo].[Clients] AS [c] ON [c].[Id] = [m].[ClientId]

                    UNION ALL

                    SELECT
                        [Id] = CONVERT(nvarchar(100), CONCAT(N'Party-', CONVERT(nvarchar(36), [p].[Id]))),
                        [SourceType] = CONVERT(nvarchar(40), N'Party'),
                        [SourceId] = CONVERT(nvarchar(36), [p].[Id]),
                        [ParentSourceType] = CONVERT(nvarchar(40), N''),
                        [ParentSourceId] = CONVERT(nvarchar(36), N''),
                        [ClientId] = CONVERT(nvarchar(36), N''),
                        [MatterId] = CONVERT(nvarchar(36), N''),
                        [PartyId] = CONVERT(nvarchar(36), [p].[Id]),
                        [ContactId] = CONVERT(nvarchar(36), N''),
                        [DisplayName] = CONVERT(nvarchar(300), [p].[Name]),
                        [SecondaryText] = CONVERT(nvarchar(500), CONCAT(N'Party ', FORMAT([p].[PartyNumber], '00000000'), N' · ', [p].[PartyType], N' · ', [p].[Status])),
                        [Status] = CONVERT(nvarchar(80), [p].[Status]),
                        [EntityNumber] = CONVERT(int, [p].[PartyNumber]),
                        [IsArchived] = CONVERT(bit, [p].[IsArchived]),
                        [SearchableText] = CONVERT(nvarchar(max), CONCAT_WS(N' ', [p].[Name], [p].[NormalizedName], [p].[PartyType], [p].[Status], [p].[Notes])),
                        [UpdatedAt] = CONVERT(datetimeoffset, [p].[UpdatedAt])
                    FROM [dbo].[Parties] AS [p]

                    UNION ALL

                    SELECT
                        [Id] = CONVERT(nvarchar(100), CONCAT(N'PartyAlias-', CONVERT(nvarchar(36), [a].[Id]))),
                        [SourceType] = CONVERT(nvarchar(40), N'PartyAlias'),
                        [SourceId] = CONVERT(nvarchar(36), [a].[Id]),
                        [ParentSourceType] = CONVERT(nvarchar(40), N'Party'),
                        [ParentSourceId] = CONVERT(nvarchar(36), [p].[Id]),
                        [ClientId] = CONVERT(nvarchar(36), N''),
                        [MatterId] = CONVERT(nvarchar(36), N''),
                        [PartyId] = CONVERT(nvarchar(36), [p].[Id]),
                        [ContactId] = CONVERT(nvarchar(36), N''),
                        [DisplayName] = CONVERT(nvarchar(300), [a].[Alias]),
                        [SecondaryText] = CONVERT(nvarchar(500), CONCAT(N'Alias for party ', FORMAT([p].[PartyNumber], '00000000'), N' · ', [p].[Name])),
                        [Status] = CONVERT(nvarchar(80), [p].[Status]),
                        [EntityNumber] = CONVERT(int, [p].[PartyNumber]),
                        [IsArchived] = CONVERT(bit, [p].[IsArchived]),
                        [SearchableText] = CONVERT(nvarchar(max), CONCAT_WS(N' ', [a].[Alias], [a].[NormalizedAlias], [a].[Notes], [p].[Name], [p].[NormalizedName], [p].[PartyType], [p].[Notes])),
                        [UpdatedAt] = CONVERT(datetimeoffset, [p].[UpdatedAt])
                    FROM [dbo].[PartyAliases] AS [a]
                    INNER JOIN [dbo].[Parties] AS [p] ON [p].[Id] = [a].[PartyId]

                    UNION ALL

                    SELECT
                        [Id] = CONVERT(nvarchar(100), CONCAT(N'Contact-', CONVERT(nvarchar(36), [ct].[Id]))),
                        [SourceType] = CONVERT(nvarchar(40), N'Contact'),
                        [SourceId] = CONVERT(nvarchar(36), [ct].[Id]),
                        [ParentSourceType] = CONVERT(nvarchar(40), N''),
                        [ParentSourceId] = CONVERT(nvarchar(36), N''),
                        [ClientId] = CONVERT(nvarchar(36), N''),
                        [MatterId] = CONVERT(nvarchar(36), N''),
                        [PartyId] = CONVERT(nvarchar(36), N''),
                        [ContactId] = CONVERT(nvarchar(36), [ct].[Id]),
                        [DisplayName] = CONVERT(nvarchar(300), CASE WHEN NULLIF([ct].[DisplayName], N'') IS NULL THEN CONCAT_WS(N' ', [ct].[FirstName], [ct].[MiddleName], [ct].[LastName]) ELSE [ct].[DisplayName] END),
                        [SecondaryText] = CONVERT(nvarchar(500), CONCAT(N'Contact ', FORMAT([ct].[ContactNumber], '00000000'), N' · ', [ct].[Organization], CASE WHEN NULLIF([ct].[Title], N'') IS NULL THEN N'' ELSE CONCAT(N' · ', [ct].[Title]) END)),
                        [Status] = CONVERT(nvarchar(80), CASE WHEN [ct].[IsArchived] = 1 THEN N'Archived' ELSE N'Active' END),
                        [EntityNumber] = CONVERT(int, [ct].[ContactNumber]),
                        [IsArchived] = CONVERT(bit, [ct].[IsArchived]),
                        [SearchableText] = CONVERT(nvarchar(max), CONCAT_WS(N' ', [ct].[DisplayName], [ct].[FirstName], [ct].[MiddleName], [ct].[LastName], [ct].[Organization], [ct].[Title], [ct].[Email], [ct].[Phone], [ct].[MobilePhone], [ct].[City], [ct].[State], [ct].[Country], [ct].[Notes])),
                        [UpdatedAt] = CONVERT(datetimeoffset, [ct].[UpdatedAt])
                    FROM [dbo].[Contacts] AS [ct];
                """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[EntitySearchDocuments]', N'V') IS NOT NULL
                BEGIN
                    DROP VIEW [dbo].[EntitySearchDocuments];
                END;
                """, suppressTransaction: true);
        }
    }
}
