using Ecocell.Shared.Utils;
using Shouldly;

namespace Ecocell.UnitTests.Shared.Utils;

public class DocumentValidatorTests
{
    // ── IsCpf ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    [InlineData("111.444.777-35")]
    public void IsCpf_ShouldReturnTrue_WhenCpfIsValid(string cpf)
    {
        DocumentValidator.IsCpf(cpf).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsCpf_ShouldReturnFalse_WhenCpfIsNullOrWhiteSpace(string? cpf)
    {
        DocumentValidator.IsCpf(cpf!).ShouldBeFalse();
    }

    [Theory]
    [InlineData("000.000.000-00")]
    [InlineData("111.111.111-11")]
    [InlineData("99999999999")]
    public void IsCpf_ShouldReturnFalse_WhenAllDigitsAreEqual(string cpf)
    {
        DocumentValidator.IsCpf(cpf).ShouldBeFalse();
    }

    [Theory]
    [InlineData("123.456.789")]
    [InlineData("1234567890123")]
    [InlineData("abc")]
    public void IsCpf_ShouldReturnFalse_WhenDigitCountIsWrong(string cpf)
    {
        DocumentValidator.IsCpf(cpf).ShouldBeFalse();
    }

    [Theory]
    [InlineData("529.982.247-26")]
    [InlineData("111.444.777-36")]
    public void IsCpf_ShouldReturnFalse_WhenCheckDigitIsWrong(string cpf)
    {
        DocumentValidator.IsCpf(cpf).ShouldBeFalse();
    }

    // ── IsCnpj ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("11.222.333/0001-81")]
    [InlineData("11222333000181")]
    [InlineData("27.165.554/0001-03")]
    public void IsCnpj_ShouldReturnTrue_WhenCnpjIsValid(string cnpj)
    {
        DocumentValidator.IsCnpj(cnpj).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsCnpj_ShouldReturnFalse_WhenCnpjIsNullOrWhiteSpace(string? cnpj)
    {
        DocumentValidator.IsCnpj(cnpj!).ShouldBeFalse();
    }

    [Theory]
    [InlineData("00.000.000/0000-00")]
    [InlineData("11111111111111")]
    public void IsCnpj_ShouldReturnFalse_WhenAllDigitsAreEqual(string cnpj)
    {
        DocumentValidator.IsCnpj(cnpj).ShouldBeFalse();
    }

    [Theory]
    [InlineData("11.111.111/0001-00")]
    [InlineData("11111111000100")]
    public void IsCnpj_ShouldReturnFalse_WhenBaseDigitsAreAllEqual(string cnpj)
    {
        DocumentValidator.IsCnpj(cnpj).ShouldBeFalse();
    }

    [Theory]
    [InlineData("123.456.789/0001")]
    [InlineData("123456789000")]
    [InlineData("abc")]
    public void IsCnpj_ShouldReturnFalse_WhenDigitCountIsWrong(string cnpj)
    {
        DocumentValidator.IsCnpj(cnpj).ShouldBeFalse();
    }

    [Theory]
    [InlineData("11.222.333/0001-82")]
    [InlineData("27.165.554/0001-04")]
    public void IsCnpj_ShouldReturnFalse_WhenCheckDigitIsWrong(string cnpj)
    {
        DocumentValidator.IsCnpj(cnpj).ShouldBeFalse();
    }
}
