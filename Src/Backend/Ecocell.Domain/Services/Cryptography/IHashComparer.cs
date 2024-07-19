namespace Ecocell.Domain.Services.Cryptography;

public interface IHashComparer
{
    bool Compare(string value, string hash);
}
