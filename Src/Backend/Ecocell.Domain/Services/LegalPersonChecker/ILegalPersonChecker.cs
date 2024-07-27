namespace Ecocell.Domain.Services.LegalPersonChecker;

public interface ILegalPersonChecker<T>
{
    Task<T?> GetCompanyData(string cnpj);
}
