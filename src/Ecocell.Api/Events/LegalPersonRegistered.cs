using Mediator;

namespace Ecocell.Api.Events;

/// <summary>
/// Evento publicado após o cadastro bem-sucedido de uma pessoa jurídica.
/// </summary>
/// <param name="LegalPersonId">Identificador da pessoa jurídica cadastrada.</param>
/// <param name="LegalPersonEmail">E-mail da pessoa jurídica para notificação.</param>
/// <param name="ResponsiblePersonEmail">E-mail do gestor PF responsável pelo cadastro.</param>
public sealed record LegalPersonRegistered(
    Guid LegalPersonId,
    string LegalPersonEmail,
    string ResponsiblePersonEmail) : INotification;