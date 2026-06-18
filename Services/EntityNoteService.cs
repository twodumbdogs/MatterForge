using MatterForge.Data;
using MatterForge.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MatterForge.Services;

public class EntityNoteService(MatterForgeDbContext db)
{
    public const string ClientEntityType = "Client";
    public const string MatterEntityType = "Matter";
    public const string PartyEntityType = "Party";

    public async Task<List<EntityNote>> ListAsync(string entityType, Guid entityId)
    {
        if (!await EnsureTableAsync())
        {
            return [];
        }

        return await db.EntityNotes
            .Include(x => x.CreatedByUser)
            .Where(x => x.EntityType == entityType && x.EntityId == entityId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(string entityType, Guid entityId, string body, Guid? createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        if (!await EnsureTableAsync())
        {
            return;
        }

        db.EntityNotes.Add(new EntityNote
        {
            EntityType = entityType,
            EntityId = entityId,
            Body = body.Trim(),
            CreatedByUserId = createdByUserId
        });

        await db.SaveChangesAsync();
    }

    private async Task<bool> EnsureTableAsync()
    {
        if (!db.Database.IsRelational())
        {
            return true;
        }

        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[EntityNotes]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [EntityNotes](
                        [Id] uniqueidentifier NOT NULL,
                        [EntityType] nvarchar(80) NOT NULL,
                        [EntityId] uniqueidentifier NOT NULL,
                        [Body] nvarchar(2000) NOT NULL,
                        [CreatedByUserId] uniqueidentifier NULL,
                        [CreatedAt] datetimeoffset NOT NULL,
                        CONSTRAINT [PK_EntityNotes] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_EntityNotes_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
                    );

                    CREATE INDEX [IX_EntityNotes_CreatedByUserId] ON [EntityNotes] ([CreatedByUserId]);
                    CREATE INDEX [IX_EntityNotes_EntityType_EntityId_CreatedAt] ON [EntityNotes] ([EntityType], [EntityId], [CreatedAt]);
                END
                """);

            return true;
        }
        catch (SqlException ex) when (ex.Number is 262 or 2760)
        {
            return false;
        }
    }
}
