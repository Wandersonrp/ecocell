namespace Ecocell.Api.Entities;

public class Person
{
    public string FullName { get; private set; } = string.Empty;
    public string? Email { get; private set; } = string.Empty;
    public string? Phone { get; private set; }

    public string GetFirstName()
    {
        if (!string.IsNullOrWhiteSpace(FullName))
            return FullName.Split(' ')[0];

        return string.Empty;
    }
}
