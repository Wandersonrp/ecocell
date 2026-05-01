namespace Ecocell.Api.Shared;

public sealed record Error(string Code, string? Message = null, List<string>? Messages = null)
{
    private static readonly string NotFoundCode = ErrorCodes.NotFound;
    private static readonly string ConflictCode = ErrorCodes.Conflict;
    private static readonly string ErrorOnValidationCode = ErrorCodes.ErrorOnValidation;
    private static readonly string InvalidCredentialCode = ErrorCodes.InvalidCredential;
    private static readonly string UnauthorizedCode = ErrorCodes.Unauthorized;
    private static readonly string InternalServerErrorCode = ErrorCodes.InternalServerError;
    private static readonly string ForbiddenCode = ErrorCodes.ForbiddenCodeError;

    public static readonly Error None = new(string.Empty, string.Empty, new List<string>());

    public static Error NotFound(string message) => new Error(NotFoundCode, message);

    public static Error Conflict(string message) => new Error(ConflictCode, message);

    public static Error InvalidCredential() => new Error(InvalidCredentialCode, Message: "Invalid Credentials");

    public static Error ErrorOnValidation(List<string>? messages) => new Error(ErrorOnValidationCode, Messages: messages);

    public static Error Unauthorized() => new Error(UnauthorizedCode);

    public static Error InternalServerError() => new Error(InternalServerErrorCode, Message: "Unknown error");

    public static Error Forbidden() => new Error(ForbiddenCode, Message: "Forbidden");

    public static Error GeocodingNotFound(string address) =>
        new Error(ErrorCodes.GeocodingNotFound, $"Endereço não encontrado: {address}");

}
