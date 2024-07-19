namespace Ecocell.Domain.Services.Cryptography;

public interface IHashGenerator
{
    string Hash(string value);
}
