using FluentAssertions;
using Horafy.Shared;
using Xunit;

namespace Horafy.Domain.Tests.Shared;

public sealed class ResultTests
{
    [Fact]
    public void Success_DeveRetornarResultadoComSucesso()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_DeveRetornarResultadoComErro()
    {
        var error = Error.Validation("Campo.Obrigatorio", "Campo é obrigatório.");
        var result = Result.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Campo.Obrigatorio");
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void SuccessT_DeveConterValor()
    {
        var result = Result.Success("Horafy");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Horafy");
    }

    [Fact]
    public void FailureT_AcessarValue_DeveLancarExcecao()
    {
        var result = Result.Failure<string>(Error.NotFound);

        var acao = () => result.Value;

        acao.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ImplicitConversion_DeveConverterValorParaResultSuccess()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void CriarSuccess_ComErroNaoNone_DeveLancarExcecao()
    {
        var acao = () => new ResultTestHelper(true, Error.NotFound);

        acao.Should().Throw<InvalidOperationException>();
    }

    // Helper para testar construtor protegido
    private sealed class ResultTestHelper : Result
    {
        public ResultTestHelper(bool isSuccess, Error error) : base(isSuccess, error) { }
    }
}
