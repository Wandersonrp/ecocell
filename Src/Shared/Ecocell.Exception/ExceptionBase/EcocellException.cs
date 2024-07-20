using System.Net;

namespace Ecocell.Exception.ExceptionBase;

public abstract class EcocellException : SystemException
{
    public EcocellException(string message) : base(message)
    {        
    }

    public abstract HttpStatusCode GetStatusCode();
}
