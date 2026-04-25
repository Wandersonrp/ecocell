namespace Ecocell.Api.Entities;

public abstract class BaseEntity
{
    public long Id { get; set; }
    public Guid Identifier { get; private set; } = Guid.CreateVersion7();
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }

    protected void MarkAsUpdated() => UpdatedAt = DateTime.UtcNow;
}
