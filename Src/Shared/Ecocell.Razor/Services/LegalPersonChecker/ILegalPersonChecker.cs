namespace Ecocell.Razor.Services.LegalPersonChecker;

public interface ILegalPersonChecker<T>
{
    Task<T?> GetCompanyData(string cnpj);
}
