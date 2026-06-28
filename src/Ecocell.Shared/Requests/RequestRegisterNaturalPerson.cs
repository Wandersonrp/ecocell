namespace Ecocell.Shared.Requests;

public record RequestRegisterNaturalPerson
{
    public string Cpf { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }
}
