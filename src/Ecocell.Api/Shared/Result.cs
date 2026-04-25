namespace Ecocell.Api.Shared;

public class Result
{    
    private Result()
    {        
        IsSuccess = true;
        Error = Error.None;
    }

    public Result(Error error)
    {
        if (error == Error.None)
        {
            throw new ArgumentException("Invalid error", nameof(error));
        }

        IsSuccess = false;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;    

    public Error Error { get; }

    public static Result Success() => new Result();

    public static Result Failure(Error error) => new Result(error);
}
