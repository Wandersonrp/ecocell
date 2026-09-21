namespace Ecocell.Api.Services.VerificationCodes;

/// <summary>
/// Dados do código OTP armazenado no Redis para um determinado identificador.
/// </summary>
/// <param name="CodeHash">Hash SHA-256 do código gerado.</param>
/// <param name="Attempts">Número de tentativas inválidas realizadas contra este código.</param>
/// <param name="IssuedAt">Momento em que o código foi emitido.</param>
public sealed record VerificationCodeRecord(string CodeHash, int Attempts, DateTimeOffset IssuedAt);
