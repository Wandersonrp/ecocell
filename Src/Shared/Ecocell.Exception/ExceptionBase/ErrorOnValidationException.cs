using System.Net;

namespace Ecocell.Exception.ExceptionBase;

public class ErrorOnValidationException : EcocellException
{
    private readonly IList<string> _errorMessages;

    public ErrorOnValidationException(IList<string> errorMessages) : base(string.Empty)
    {
        _errorMessages = errorMessages;
    }

    public override IList<string> GetErrorMessages()
    {
        return _errorMessages;
    }

    public override HttpStatusCode GetStatusCode()
    {
        return HttpStatusCode.BadRequest;
    }
}
