using Horafy.Shared;

namespace Horafy.Application.Features.Payments;

public static class PaymentErrors
{
    public static readonly Error NotFound =
        new("Payment.NotFound", "Pagamento não encontrado.", ErrorType.NotFound);

    public static readonly Error AlreadyApproved =
        new("Payment.AlreadyApproved", "Pagamento já foi aprovado.", ErrorType.Conflict);

    public static readonly Error RefundFailed =
        new("Payment.RefundFailed", "Falha ao processar estorno.", ErrorType.Failure);

    public static readonly Error NotApproved =
        new("Payment.NotApproved", "Apenas pagamentos aprovados podem ser estornados.", ErrorType.Validation);
}
