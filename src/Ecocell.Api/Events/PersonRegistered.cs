using Mediator;

namespace Ecocell.Api.Events;

/// <summary>
/// Evento publicado após o cadastro bem-sucedido de uma pessoa na plataforma.
/// Utilizado para disparar o envio do código OTP de confirmação de e-mail.
/// </summary>
/// <param name="PersonId">Identificador único da pessoa cadastrada.</param>
/// <param name="Email">Endereço de e-mail para o qual o código OTP será enviado.</param>
public sealed record PersonRegistered(Guid PersonId, string Email) : INotification;
