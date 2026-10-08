using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Model;

namespace OrgChart.EFCore.Services;

/// <summary>
/// Fills <see cref="AuditableEntity"/> fields from <see cref="ICurrentUser"/> on save. Created* is never changed by an update.
/// </summary>
internal sealed class AuditableEntityInterceptor(ICurrentUser currentUser, TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        string? userId = currentUser.UserId;

        foreach (EntityEntry<AuditableEntity> entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = utcNow;
                    entry.Entity.CreatedBy = userId;
                    break;
                case EntityState.Modified:
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Property(e => e.CreatedBy).IsModified = false;
                    entry.Entity.UpdatedAt = utcNow;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }
    }
}
