namespace Ecocell.Api.Enums;

/// <summary>Jornada de descarte selecionada pelo usuário na plataforma EcoCell (uso interno da API).</summary>
public enum Journey
{
    /// <summary>Nenhuma jornada selecionada.</summary>
    None = 0,
    /// <summary>Depositante — descarta resíduos nos pontos de coleta.</summary>
    Depositor = 1,
    /// <summary>Ponto de coleta — recebe e armazena os resíduos.</summary>
    CollectPoint = 2,
    /// <summary>Coletor — transporta os resíduos do ponto de coleta.</summary>
    Collector = 3,
}
