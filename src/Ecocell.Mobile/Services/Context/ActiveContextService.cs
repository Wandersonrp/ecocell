using Ecocell.Mobile.Services.Auth;

namespace Ecocell.Mobile.Services.Context;

/// <summary>
/// Guarda o contexto ativo em memória (não persiste entre sessões). Espelha o padrão do
/// <see cref="AuthStateService"/>. <see cref="Generation"/> incrementa a cada troca para que
/// view-models PC-scoped descartem respostas de requests iniciados em contexto anterior.
/// </summary>
public sealed class ActiveContextService
{
    public ActiveContext Current { get; private set; } = ActiveContext.Self();
    public int Generation { get; private set; }

    public event Action? ContextChanged;

    public ActiveContextService(AuthStateService authState)
    {
        // Logout sempre volta para Self (contexto não sobrevive à sessão).
        authState.AuthStateChanged += () =>
        {
            if (!authState.IsAuthenticated) ReturnToSelf();
        };
    }

    public void EnterCollectPoint(Guid collectPointId, string displayName)
    {
        Current = new ActiveContext(ActiveContextKind.CollectPoint, collectPointId, displayName);
        Generation++;
        ContextChanged?.Invoke();
    }

    public void ReturnToSelf()
    {
        if (Current.Kind == ActiveContextKind.Self) return;
        Current = ActiveContext.Self();
        Generation++;
        ContextChanged?.Invoke();
    }
}
