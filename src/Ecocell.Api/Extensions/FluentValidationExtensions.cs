using Ecocell.Api.Shared.Utils;
using FluentValidation;

namespace Ecocell.Api.Extensions;

public static class FluentValidationExtensions
{
    /// <summary>
    /// Valida se uma string contém um CPF válido (sem máscara).
    /// </summary>
    public static IRuleBuilderOptions<T, string> IsValidCpf<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder.Must(cpf =>
        {
            if (string.IsNullOrWhiteSpace(cpf)) return false;

            return DocumentValidator.IsCpf(cpf);
        }).WithMessage("CPF inválido.");
    }

    /// <summary>
    /// Valida se uma string contém um CNPJ válido (sem máscara).
    /// </summary>
    public static IRuleBuilderOptions<T, string> IsValidCnpj<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder.Must(cnpj =>
        {
            if (string.IsNullOrWhiteSpace(cnpj)) return false;

            return DocumentValidator.IsCnpj(cnpj);
        }).WithMessage("CNPJ inválido.");
    }

    /// <summary>
    /// Valida se uma string contém um e-mail válido com no máximo 255 caracteres.
    /// </summary>
    public static IRuleBuilderOptions<T, string> IsValidEmail<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .EmailAddress().WithMessage("E-mail inválido.")
            .MaximumLength(255).WithMessage("E-mail deve conter no máximo 255 caracteres.");
    }
}
