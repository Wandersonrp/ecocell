namespace Ecocell.Api.Shared;

public class ResultT<T>
{
    private readonly T? _value;

    private ResultT(T value)
    {
        Value = value;
        IsSuccess = true;
        Error = Error.None;
    }

    public ResultT(Error error)
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

    public T Value
    {
        get
        {
            if (IsFailure)
            {
                throw new InvalidOperationException("There is no value for failure");
            }

            return _value!;
        }

        private init => _value = value;
    }

    public Error Error { get; }

    public static ResultT<T> Success(T value) => new ResultT<T>(value);

    public static ResultT<T> Failure(Error error) => new ResultT<T>(error);
}
