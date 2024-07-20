using System.Net;

namespace Ecocell.Exception.ExceptionBase;

public class ErrorOnValidationException : EcocellException
{
    public ErrorOnValidationException(string message) : base(message)
    {
    }

    public override HttpStatusCode GetStatusCode()
    {
        return HttpStatusCode.BadRequest;
    }
}
