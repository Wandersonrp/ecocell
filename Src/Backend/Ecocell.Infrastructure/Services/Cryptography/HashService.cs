using Ecocell.Domain.Services.Cryptography;

namespace Ecocell.Infrastructure.Services.Cryptography;

public class HashService : IHashGenerator, IHashComparer
{
    public bool Compare(string value, string hash)
    {
        return BC.Verify(value, hash);
    }

    public string Hash(string value)
    {
        var salt = BC.GenerateSalt(10);

        return BC.HashPassword(value, salt);
    }
}
