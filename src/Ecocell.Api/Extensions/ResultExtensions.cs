using Ecocell.Api.Shared;

namespace Ecocell.Api.Extensions;

public static class ResultExtensions
{
    public static IResult ToProcessResult(this Result result, int successStatusCode = StatusCodes.Status200OK)
    {
        return result.IsSuccess
            ? Results.StatusCode(successStatusCode)
            : result.Error.Handle();
    }

    public static IResult ToProcessResult<T>(this ResultT<T> result, int successStatusCode = StatusCodes.Status200OK)
    {
        return result.IsSuccess
            ? Results.Json(result.Value, statusCode: successStatusCode)
            : result.Error.Handle();
    }

    private static IResult Handle(this Error error)
    {        
        var extensions = new Dictionary<string, object?>
        {
            { "code", error.Code }
        };         

        if(error.Messages is { Count: > 0})
        {
            extensions.Add("errors", error.Messages);
        }

        return Results.Problem(
            statusCode: GetStatusCode(error.Code),
            title: GetTitle(error.Code),
            detail: error.Message,            
            extensions: extensions);
    }

    private static string GetTitle(string errorCode) =>
        errorCode switch
        {
            ErrorCodes.ErrorOnValidation => "Bad Request",
            ErrorCodes.NotFound => "Not Found",
            ErrorCodes.Conflict => "Conflict",
            ErrorCodes.InvalidCredential => "Unauthorized",
            ErrorCodes.ForbiddenCodeError => "Forbidden",
            _ => "Internal Server Error"
        };

    private static int GetStatusCode(string error) =>
        error switch
        {
            ErrorCodes.ErrorOnValidation => StatusCodes.Status400BadRequest,
            ErrorCodes.NotFound => StatusCodes.Status404NotFound,
            ErrorCodes.Conflict => StatusCodes.Status409Conflict,
            ErrorCodes.InvalidCredential => StatusCodes.Status401Unauthorized,
            ErrorCodes.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorCodes.ForbiddenCodeError => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };
}
