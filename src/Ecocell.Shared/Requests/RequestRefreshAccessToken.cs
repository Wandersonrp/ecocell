namespace Ecocell.Shared.Requests;

/// <summary>Requisição para renovar o par de tokens usando o refresh token.</summary>
public record RequestRefreshAccessToken
{
    /// <summary>Token opaco de renovação emitido no login ou no último refresh.</summary>
    public string RefreshToken { get; set; } = string.Empty;
}