using FluentValidation;
using Horafy.Application.Features.Bookings;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Payments;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Payments.Commands;

public sealed record CreatePaymentCommand(
    Guid BookingId,
    decimal Amount,
    PaymentMethod Method,
    string BackUrl) : IRequest<Result<CreatePaymentResult>>;

public sealed record CreatePaymentResult(Guid PaymentId, string PreferenceId, string? PaymentUrl);

public sealed class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.BackUrl).NotEmpty().MaximumLength(2000);
    }
}

internal sealed class CreatePaymentCommandHandler(
    IBookingRepository bookingRepository,
    ITenantRepository tenantRepository,
    IPaymentRepository paymentRepository,
    IPaymentGateway gateway,
    ICurrentTenantService currentTenant,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<CreatePaymentCommand, Result<CreatePaymentResult>>
{
    public async Task<Result<CreatePaymentResult>> Handle(
        CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null) return Result.Failure<CreatePaymentResult>(BookingErrors.NotFound);

        var depositAmount = 0m;
        if (currentTenant.TenantId.HasValue)
        {
            var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
            if (tenant?.PaymentSettings.RequiresPayment is true)
                depositAmount = tenant.PaymentSettings.CalculateDepositAmount(request.Amount);
        }

        var webhookUrl = $"{request.BackUrl.TrimEnd('/')}/webhooks/mercadopago";

        var prefResult = await gateway.CreatePreferenceAsync(
            new CreatePaymentPreferenceRequest(
                booking.Id, request.Amount, depositAmount,
                request.Method, booking.CustomerEmail,
                request.BackUrl, webhookUrl),
            cancellationToken);

        var payment = Payment.Create(
            booking.Id, prefResult.PreferenceId, request.Method,
            request.Amount, depositAmount,
            prefResult.PaymentUrl, prefResult.ExpiresAt);

        paymentRepository.Add(payment);
        booking.MarkPaymentPending();
        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreatePaymentResult(payment.Id, payment.PreferenceId, payment.PaymentUrl));
    }
}
