using Ecocell.Api.Enums;

namespace Ecocell.Api.Services.CurrentUser;

/// <summary>
/// Projeção do usuário autenticado retornada por <see cref="ICurrentUserService"/>.
/// Contém apenas os campos necessários para verificação de identidade e autorização.
/// </summary>
public sealed record CurrentUserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public Role Role { get; init; }
    public PersonStatus PersonStatus { get; init; }
    public PersonType PersonType { get; init; }
    public Journey Journey { get; init; }
}