using System.Text.Encodings.Web;
using System.Text.Json;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Model;

namespace OrgChart.EFCore.Admin;

/// <summary>Adds <see cref="AuditLog"/> rows to the context; they are saved with the change they describe.</summary>
internal sealed class AuditWriter(OrgChartDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions s_json = new()
    {
        // Keep Persian text readable in the stored JSON.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public void Write(string operation, string entityType, object? entityId, object change) =>
        db.AuditLogs.Add(new AuditLog
        {
            At = timeProvider.GetUtcNow().UtcDateTime,
            ActorUserId = currentUser.UserId,
            Operation = operation,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            ChangeJson = JsonSerializer.Serialize(change, s_json),
        });
}
