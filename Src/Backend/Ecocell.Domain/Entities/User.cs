using Ecocell.Communication.Enums.User;

namespace Ecocell.Domain.Entities;

public class User
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid DocumentId { get; set; } 
    public Document Document { get; set; } = null!;
    public string Password { get; set; } = string.Empty;
    public Uri? AvatarUrl { get; set; } = null!;
    public UserType UserType { get; set; }
    public Role Role { get; set; } = Role.User;
    public Status Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }   
}
