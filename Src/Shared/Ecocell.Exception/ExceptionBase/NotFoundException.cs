using System.Net;

namespace Ecocell.Exception.ExceptionBase;

public class NotFoundException : EcocellException
{
    public NotFoundException(string message) : base(message)
    {
    }

    public override HttpStatusCode GetStatusCode()
    {
        return HttpStatusCode.NotFound;
    }
}
