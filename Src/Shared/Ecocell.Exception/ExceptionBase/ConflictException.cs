using System.Net;

namespace Ecocell.Exception.ExceptionBase;

public class ConflictException : EcocellException
{
    public ConflictException(string message) : base(message)
    {
    }

    public override HttpStatusCode GetStatusCode()
    {
        return HttpStatusCode.Conflict;
    }
}
