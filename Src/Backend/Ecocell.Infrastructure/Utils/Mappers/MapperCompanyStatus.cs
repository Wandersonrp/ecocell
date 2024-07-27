using Ecocell.Communication.Enums.User;

namespace Ecocell.Infrastructure.Utils.Mappers;

public class MapperCompanyStatus
{
    public static CompanyStatus ToCompanyStatus(string companyStatusDescription)
    {
        return companyStatusDescription switch
        {
            "Ativa" => CompanyStatus.Active,
            "Suspensa" => CompanyStatus.Supended,
            "Inapta" => CompanyStatus.Unfit,
            "Baixada" => CompanyStatus.Finished,
            "Extinção por Encerramento de Falência" => CompanyStatus.Failed,
            _ => throw new ArgumentException("Invalid company status description")
        };
    }
}
