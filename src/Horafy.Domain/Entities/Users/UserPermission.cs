namespace Horafy.Domain.Entities.Users;

/// <summary>
/// Permissões granulares que podem ser atribuídas a qualquer usuário,
/// independentemente de seu role. Permite fine-grained access control
/// além da hierarquia de roles.
/// </summary>
public enum UserPermission
{
    // ── Gestão do tenant ────────────────────────────────────────────
    ManageTenant = 10,
    ViewTenant   = 11,

    // ── Equipe ──────────────────────────────────────────────────────
    ManageStaff = 20,
    ViewStaff   = 21,

    // ── Serviços/recursos ───────────────────────────────────────────
    ManageServices = 30,
    ViewServices   = 31,

    // ── Agendamentos ────────────────────────────────────────────────
    ManageBookings = 40,
    ViewBookings   = 41,
    CreateBooking  = 42,
    CancelBooking  = 43,

    // ── Relatórios ──────────────────────────────────────────────────
    ViewReports   = 50,
    ExportReports = 51,

    // ── Financeiro ──────────────────────────────────────────────────
    ManageBilling = 60,
    ViewBilling   = 61
}
