namespace Ecocell.Domain.Entities;

public class NaturalPerson : User
{
    public bool IsDiscarding { get; set; }
    public DateOnly? BirthDate { get; set; }
}
