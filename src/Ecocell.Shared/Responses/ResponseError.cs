namespace Ecocell.Shared.Responses;

public sealed record ResponseError
{
    public List<string> Messages { get; private set; }

    public ResponseError(List<string> messages)
    {
        Messages = messages;
    }

    public ResponseError(string message)
    {
        Messages = [message];
    }
}