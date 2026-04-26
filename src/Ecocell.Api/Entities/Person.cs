using Ecocell.Api.Enums;

namespace Ecocell.Api.Entities;

public abstract class Person : BaseEntity
{
    public Role Role { get; protected set; } = Role.User;
    public string Email { get; protected set; } = string.Empty;
    public Journey Journey { get; protected set; }
    public PersonType PersonType { get; protected set; }
    public PersonStatus PersonStatus { get; protected set; } = PersonStatus.Active;
}
