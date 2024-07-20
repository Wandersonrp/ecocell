using System.Net;

namespace Ecocell.Exception.ExceptionBase;

public class ConflictException : EcocellException
{
    public ConflictException(string identifier) : base($"{ResourceErrorMessages.CONFLICT_ERROR} {identifier}")
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
        return HttpStatusCode.Conflict;
    }
}
