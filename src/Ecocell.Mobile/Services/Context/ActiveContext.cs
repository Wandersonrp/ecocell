namespace Ecocell.Mobile.Services.Context;

public enum ActiveContextKind { Self, CollectPoint }

/// <summary>Contexto ativo do app: o próprio usuário (Self) ou um Ponto de Coleta que ele gerencia.</summary>
public sealed record ActiveContext(ActiveContextKind Kind, Guid? CollectPointId, string? DisplayName)
{
    public static ActiveContext Self() => new(ActiveContextKind.Self, null, null);

    public bool IsCollectPoint => Kind == ActiveContextKind.CollectPoint;
}
