using System.Net;

namespace Ecocell.Exception.ExceptionBase;

public class NotFoundException : EcocellException
{
    public NotFoundException(string identifier) : base($"{ResourceErrorMessages.NOT_FOUND_ERROR} {identifier}")
    {
    }

    public override IList<string> GetErrorMessages()
    {
        return new List<string>() 
        { 
            Message
        };
    }

    public override HttpStatusCode GetStatusCode()
    {
        return HttpStatusCode.NotFound;
    }
}
