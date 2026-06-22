using CMIForge.Data;
using CMIForge.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CMIForge.Services;

public class EntityNoteService(CMIForgeDbContext db)
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
            .Include(x => x.ImpersonatedUser)
            .Where(x => x.EntityType == entityType && x.EntityId == entityId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(string entityType, Guid entityId, string body, Guid? createdByUserId, Guid? impersonatedUserId = null)
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
            CreatedByUserId = createdByUserId,
            ImpersonatedUserId = impersonatedUserId
        });

        await db.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(string entityType, Guid entityId, Guid noteId, Guid? currentUserId, bool canManageNotes = false)
    {
        if (!await EnsureTableAsync())
        {
            return false;
        }

        var note = await db.EntityNotes.FirstOrDefaultAsync(x =>
            x.Id == noteId &&
            x.EntityType == entityType &&
            x.EntityId == entityId);
        if (note is null)
        {
            return false;
        }

        if (!canManageNotes && (!currentUserId.HasValue || note.CreatedByUserId != currentUserId.Value))
        {
            return false;
        }

        db.EntityNotes.Remove(note);
        await db.SaveChangesAsync();
        return true;
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
                        [ImpersonatedUserId] uniqueidentifier NULL,
                        [CreatedAt] datetimeoffset NOT NULL,
                        CONSTRAINT [PK_EntityNotes] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_EntityNotes_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_EntityNotes_Users_ImpersonatedUserId] FOREIGN KEY ([ImpersonatedUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
                    );

                    CREATE INDEX [IX_EntityNotes_CreatedByUserId] ON [EntityNotes] ([CreatedByUserId]);
                    CREATE INDEX [IX_EntityNotes_ImpersonatedUserId] ON [EntityNotes] ([ImpersonatedUserId]);
                    CREATE INDEX [IX_EntityNotes_EntityType_EntityId_CreatedAt] ON [EntityNotes] ([EntityType], [EntityId], [CreatedAt]);
                END

                IF OBJECT_ID(N'[EntityNotes]', N'U') IS NOT NULL AND COL_LENGTH(N'[EntityNotes]', N'ImpersonatedUserId') IS NULL
                BEGIN
                    ALTER TABLE [EntityNotes] ADD [ImpersonatedUserId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[EntityNotes]', N'U') IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_EntityNotes_Users_ImpersonatedUserId')
                BEGIN
                    ALTER TABLE [EntityNotes] WITH CHECK ADD CONSTRAINT [FK_EntityNotes_Users_ImpersonatedUserId]
                        FOREIGN KEY ([ImpersonatedUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
                END

                IF OBJECT_ID(N'[EntityNotes]', N'U') IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM sys.indexes WHERE [name] = N'IX_EntityNotes_ImpersonatedUserId' AND [object_id] = OBJECT_ID(N'[EntityNotes]'))
                BEGIN
                    CREATE INDEX [IX_EntityNotes_ImpersonatedUserId] ON [EntityNotes] ([ImpersonatedUserId]);
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
