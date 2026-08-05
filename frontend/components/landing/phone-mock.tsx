interface PhoneMockProps {
  tenantName: string
  tenantInitials: string
  portalHost: string
  itemTitle: string
  itemDetail: string
  itemPrice: string
  slotLabel: string
  slots: string[]
  selectedSlot: string
  days: { dow: string; day: string }[]
  selectedDay: string
  cta: string
  confirmation: string
}

/**
 * Ilustração do portal do tenant em moldura de celular — leve (HTML/CSS puro),
 * cores herdam as variáveis de marca definidas na página (--brand, --tint…).
 */
export function PhoneMock(props: PhoneMockProps) {
  return (
    <div
      aria-hidden="true"
      className="w-[290px] select-none rounded-[2.4rem] bg-zinc-900/90 p-2.5 shadow-2xl shadow-black/40 sm:w-[310px]"
    >
      <div className="overflow-hidden rounded-[1.9rem] bg-white">
        {/* Barra do portal */}
        <div className="flex items-center gap-2.5 px-4 pb-3 pt-4">
          <div className="flex size-9 items-center justify-center rounded-full bg-(--brand) text-xs font-bold text-white">
            {props.tenantInitials}
          </div>
          <div>
            <p className="text-[13px] font-semibold leading-tight text-zinc-900">{props.tenantName}</p>
            <p className="text-[11px] leading-tight text-zinc-500">{props.portalHost}</p>
          </div>
        </div>

        {/* Serviço/item escolhido */}
        <div className="mx-3 rounded-xl bg-zinc-100 px-3.5 py-3">
          <p className="text-[13px] font-semibold text-zinc-900">{props.itemTitle}</p>
          <p className="mt-0.5 text-[12px] text-zinc-600">
            {props.itemDetail} · <span className="font-semibold text-zinc-900">{props.itemPrice}</span>
          </p>
        </div>

        {/* Dias */}
        <div className="mt-3 flex gap-1.5 px-3">
          {props.days.map(d => {
            const active = d.day === props.selectedDay
            return (
              <div
                key={d.day}
                className={
                  active
                    ? 'flex-1 rounded-lg bg-(--brand) py-1.5 text-center text-white'
                    : 'flex-1 rounded-lg bg-zinc-100 py-1.5 text-center text-zinc-600'
                }
              >
                <p className="text-[10px] uppercase leading-tight opacity-80">{d.dow}</p>
                <p className="text-[13px] font-bold leading-tight">{d.day}</p>
              </div>
            )
          })}
        </div>

        {/* Horários / datas */}
        <p className="mt-3 px-4 text-[11px] font-medium uppercase tracking-wide text-zinc-500">
          {props.slotLabel}
        </p>
        <div className="mt-1.5 grid grid-cols-3 gap-1.5 px-3">
          {props.slots.map(s => {
            const active = s === props.selectedSlot
            return (
              <div
                key={s}
                className={
                  active
                    ? 'rounded-lg border border-(--brand) bg-(--tint) py-1.5 text-center text-[12px] font-semibold text-(--tint-ink)'
                    : 'rounded-lg border border-zinc-200 py-1.5 text-center text-[12px] text-zinc-600'
                }
              >
                {s}
              </div>
            )
          })}
        </div>

        {/* CTA + confirmação */}
        <div className="p-3 pt-3.5">
          <div className="rounded-xl bg-(--brand) py-2.5 text-center text-[13px] font-semibold text-white">
            {props.cta}
          </div>
          <div className="mt-2 flex items-center gap-1.5 rounded-xl bg-emerald-50 px-3 py-2">
            <svg className="size-3.5 shrink-0 text-emerald-600" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="m2.5 8.5 3.5 3.5 7-8" />
            </svg>
            <p className="text-[11px] leading-tight text-emerald-800">{props.confirmation}</p>
          </div>
        </div>
      </div>
    </div>
  )
}
