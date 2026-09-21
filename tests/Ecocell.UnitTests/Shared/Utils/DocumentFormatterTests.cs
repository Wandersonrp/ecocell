using Ecocell.Shared.Utils;
using Shouldly;

namespace Ecocell.UnitTests.Shared.Utils;

public class DocumentFormatterTests
{
    [Theory]
    [InlineData("11222333000181", "11.222.333/0001-81")]
    [InlineData("11.222.333/0001-81", "11.222.333/0001-81")]
    public void FormatCnpj_ShouldReturnFormatted_WhenDigitCountIs14(string cnpj, string expected)
    {
        DocumentFormatter.FormatCnpj(cnpj).ShouldBe(expected);
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("")]
    public void FormatCnpj_ShouldReturnOriginal_WhenDigitCountIsNot14(string cnpj)
    {
        DocumentFormatter.FormatCnpj(cnpj).ShouldBe(cnpj);
    }
}
