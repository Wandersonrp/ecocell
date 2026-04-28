namespace Ecocell.Api.Enums;

/// <summary>Status de cadastro de uma pessoa na plataforma EcoCell (uso interno da API).</summary>
public enum PersonStatus
{
    /// <summary>Conta inativa.</summary>
    Inactive = 0,
    /// <summary>Conta ativa e apta a operar na plataforma.</summary>
    Active = 1,
    /// <summary>Conta suspensa por violação de regras.</summary>
    Suspended = 2,
    /// <summary>Aguardando confirmação do e-mail pelo usuário.</summary>
    AwaitingConfirmation = 3,
    /// <summary>Aguardando aprovação manual pela equipe EcoCell.</summary>
    PendingApproval = 4,
    /// <summary>Cadastro recusado pela equipe EcoCell.</summary>
    Refused = 5,
}
