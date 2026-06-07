using Horafy.Shared;

namespace Horafy.Domain.Entities.Vouchers;

public static class VoucherErrors
{
    public static readonly Error NotFound             = new("Voucher.NotFound",           "Voucher não encontrado.",                      ErrorType.NotFound);
    public static readonly Error CodeAlreadyExists    = new("Voucher.CodeAlreadyExists",  "Este código já está em uso.",                  ErrorType.Conflict);
    public static readonly Error Inactive             = new("Voucher.Inactive",           "Este voucher está inativo.",                   ErrorType.Validation);
    public static readonly Error Expired              = new("Voucher.Expired",            "Este voucher está expirado.",                  ErrorType.Validation);
    public static readonly Error MaxUsesReached       = new("Voucher.MaxUsesReached",     "Este voucher atingiu o limite de usos.",        ErrorType.Validation);
    public static readonly Error InvalidDiscountValue = new("Voucher.InvalidDiscount",    "O valor do desconto deve ser maior que zero.", ErrorType.Validation);
    public static readonly Error InvalidPercentage    = new("Voucher.InvalidPercentage",  "A porcentagem não pode exceder 100.",          ErrorType.Validation);
}
