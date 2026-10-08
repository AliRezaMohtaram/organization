namespace OrgChart.Core.Model;

/// <summary>
/// An entity with a stable key. <see cref="Key"/> keeps the spelling it was created with;
/// <see cref="NormalizedKey"/> follows it and carries the unique index.
/// </summary>
public abstract class KeyedEntity : AuditableEntity
{
    private string _key = null!;

    public int Id { get; set; }

    /// <summary>Stable key; never changes after the entity is first saved. See <see cref="OrgKey"/>.</summary>
    public string Key
    {
        get => _key;
        set
        {
            _key = value;
            NormalizedKey = OrgKey.Normalize(value);
        }
    }

    public string NormalizedKey { get; private set; } = null!;

    public string Title { get; set; } = null!;
    public int SortOrder { get; set; }

    /// <summary>Soft delete: inactive rows stay because other modules may still reference the key.</summary>
    public bool IsActive { get; set; } = true;
}
