namespace Horafy.Application.Features.Notifications;

public static class DefaultTemplates
{
    public static class WhatsApp
    {
        public const string BookingCreated =
            "Olá, {{customer_name}}! 👋 Seu agendamento de *{{service_name}}* " +
            "com *{{resource_name}}* foi recebido para *{{scheduled_at}}*. " +
            "Em breve você receberá a confirmação. — {{tenant_name}}";

        public const string BookingConfirmed =
            "✅ Confirmado! Olá, {{customer_name}}. Seu horário de *{{service_name}}* " +
            "com *{{resource_name}}* está marcado para *{{scheduled_at}}*. " +
            "Te esperamos! — {{tenant_name}}";

        public const string BookingCancelled =
            "❌ Agendamento cancelado. Olá, {{customer_name}}. " +
            "{{cancellation_reason}}Entre em contato para reagendar. — {{tenant_name}}";

        public const string BookingReminderOneDay =
            "⏰ Lembrete! Olá, {{customer_name}}. Amanhã você tem *{{service_name}}* " +
            "com *{{resource_name}}* às *{{scheduled_time}}*. Te esperamos! — {{tenant_name}}";

        public const string BookingReminderTwoHours =
            "⏰ Daqui a 2 horas! Olá, {{customer_name}}. Seu agendamento de " +
            "*{{service_name}}* com *{{resource_name}}* é às *{{scheduled_time}}*. " +
            "— {{tenant_name}}";

        public const string PaymentPending =
            "💳 Link de pagamento. Olá, {{customer_name}}. Para confirmar seu agendamento, " +
            "pague *R$ {{amount}}* pelo link: {{payment_url}} — {{tenant_name}}";

        public const string PaymentConfirmed =
            "✅ Pagamento confirmado! Olá, {{customer_name}}. Recebemos seu pagamento " +
            "de *R$ {{amount}}*. Agendamento confirmado! — {{tenant_name}}";

        public const string RentalReturnReminder =
            "⏰ Lembrete de devolução! Olá, {{customer_name}}. A devolução de " +
            "*{{item_name}}* é amanhã, *{{due_at}}*. — {{tenant_name}}";

        public const string RentalOverdue =
            "⚠️ Locação em atraso. Olá, {{customer_name}}. A devolução de *{{item_name}}* " +
            "venceu em *{{due_at}}* ({{days_overdue}} dia(s) de atraso). " +
            "Por favor, devolva o quanto antes para evitar multas. — {{tenant_name}}";
    }

    public static class EmailSubject
    {
        public const string BookingCreated   = "Agendamento recebido — {{service_name}}";
        public const string BookingConfirmed = "Agendamento confirmado — {{service_name}}";
        public const string BookingCancelled = "Agendamento cancelado";
        public const string BookingReminder  = "Lembrete: {{service_name}} em {{scheduled_at}}";
        public const string PaymentPending   = "Link de pagamento — R$ {{amount}}";
        public const string PaymentConfirmed = "Pagamento confirmado — R$ {{amount}}";
        public const string RentalReturnReminder = "Lembrete de devolução — {{item_name}}";
        public const string RentalOverdue        = "Locação em atraso — {{item_name}}";
    }

    public static class EmailBody
    {
        public const string BookingCreated =
            "<h2>Olá, {{customer_name}}!</h2>" +
            "<p>Seu agendamento de <strong>{{service_name}}</strong> com " +
            "<strong>{{resource_name}}</strong> foi recebido para " +
            "<strong>{{scheduled_at}}</strong>.</p><p>— {{tenant_name}}</p>";

        public const string BookingConfirmed =
            "<h2>✅ Agendamento confirmado!</h2>" +
            "<p>Olá, {{customer_name}}. Seu horário de <strong>{{service_name}}</strong> " +
            "com <strong>{{resource_name}}</strong> está confirmado para " +
            "<strong>{{scheduled_at}}</strong>.</p><p>— {{tenant_name}}</p>";

        public const string BookingCancelled =
            "<h2>❌ Agendamento cancelado</h2>" +
            "<p>Olá, {{customer_name}}. {{cancellation_reason}}" +
            "Entre em contato para reagendar.</p><p>— {{tenant_name}}</p>";

        public const string BookingReminder =
            "<h2>⏰ Lembrete!</h2><p>Olá, {{customer_name}}. Não esqueça do seu " +
            "agendamento de <strong>{{service_name}}</strong> com " +
            "<strong>{{resource_name}}</strong> em <strong>{{scheduled_at}}</strong>.</p>" +
            "<p>— {{tenant_name}}</p>";

        public const string PaymentPending =
            "<h2>💳 Pagamento pendente</h2><p>Olá, {{customer_name}}. Para confirmar " +
            "seu agendamento, efetue o pagamento de <strong>R$ {{amount}}</strong>:</p>" +
            "<p><a href=\"{{payment_url}}\">Pagar agora</a></p><p>— {{tenant_name}}</p>";

        public const string PaymentConfirmed =
            "<h2>✅ Pagamento confirmado!</h2><p>Olá, {{customer_name}}. Recebemos seu " +
            "pagamento de <strong>R$ {{amount}}</strong>. Agendamento confirmado!</p>" +
            "<p>— {{tenant_name}}</p>";

        public const string RentalReturnReminder =
            "<h2>⏰ Lembrete de devolução</h2><p>Olá, {{customer_name}}. A devolução de " +
            "<strong>{{item_name}}</strong> está marcada para <strong>{{due_at}}</strong>.</p>" +
            "<p>— {{tenant_name}}</p>";

        public const string RentalOverdue =
            "<h2>⚠️ Locação em atraso</h2><p>Olá, {{customer_name}}. A devolução de " +
            "<strong>{{item_name}}</strong> venceu em <strong>{{due_at}}</strong> " +
            "({{days_overdue}} dia(s) de atraso). Por favor, devolva o quanto antes " +
            "para evitar multas.</p><p>— {{tenant_name}}</p>";
    }
}
