namespace Ecocell.Api.Shared;

public abstract class ErrorCodes
{
    public const string NotFound = "NotFound";
    public const string Conflict = "Conflict";
    public const string ErrorOnValidation = "ErrorOnValidation";
    public const string InvalidCredential = "InvalidCredential";
    public const string Unauthorized = "Unauthorized";
    public const string InternalServerError = "InternalServerError";
    public const string RefreshTokenExpiredError = "RefreshTokenExpiredError";
    public const string ForbiddenCodeError = "ForbiddenCodeError";
    public const string GeocodingNotFound = "GEOCODING_NOT_FOUND";
    public const string GeocodingUnavailable = "GEOCODING_UNAVAILABLE";
}
