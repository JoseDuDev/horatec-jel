import type { CSSProperties } from 'react'
import type { Metadata } from 'next'
import Link from 'next/link'
import { getActiveBrand } from '@/lib/brand.server'
import type { Brand } from '@/lib/brand'
import { LeadForm } from '@/components/landing/lead-form'
import { PhoneMock } from '@/components/landing/phone-mock'

export async function generateMetadata(): Promise<Metadata> {
  const brand = await getActiveBrand()
  const copy = copyFor(brand)
  return {
    title: `${brand.name} — ${brand.tagline}`,
    description: copy.sub,
  }
}

/* Paletas por marca, ajustadas à mão (contraste AA verificado). */
const PALETTES: Record<string, Record<string, string>> = {
  agenda: {
    '--brand': 'oklch(0.585 0.203 277)',
    '--brand-ink': 'oklch(0.44 0.19 277)',
    '--hero-bg': 'oklch(0.225 0.06 281)',
    '--hero-glow': 'oklch(0.36 0.12 279)',
    '--hero-fg': 'oklch(0.975 0.008 280)',
    '--hero-muted': 'oklch(0.815 0.048 281)',
    '--tint': 'oklch(0.955 0.022 281)',
    '--tint-ink': 'oklch(0.36 0.14 279)',
  },
  alugue: {
    '--brand': 'oklch(0.51 0.088 183)',
    '--brand-ink': 'oklch(0.42 0.085 184)',
    '--hero-bg': 'oklch(0.235 0.038 193)',
    '--hero-glow': 'oklch(0.36 0.07 187)',
    '--hero-fg': 'oklch(0.975 0.006 190)',
    '--hero-muted': 'oklch(0.815 0.04 189)',
    '--tint': 'oklch(0.955 0.018 186)',
    '--tint-ink': 'oklch(0.36 0.07 185)',
  },
}

interface LandingCopy {
  h1: string
  sub: string
  steps: { title: string; text: string }[]
  features: { title: string; text: string }[]
  verticals: string
  formTitle: string
  formSub: string
  businessTypes: string[]
  mock: Parameters<typeof PhoneMock>[0]
}

function copyFor(brand: Brand): LandingCopy {
  const rentals = brand.primaryModule === 'Rentals'

  if (rentals) {
    return {
      h1: 'Seus itens alugados, sem dor de cabeça.',
      sub: 'Quem quer alugar escolhe a data no seu portal e você confirma pelo celular. Sem planilha, sem mensagem perdida.',
      steps: [
        {
          title: 'Conte pra gente do seu negócio',
          text: 'Preencha o formulário aqui embaixo. Leva dois minutos.',
        },
        {
          title: 'A gente monta seu portal com você',
          text: 'Seus itens, seus preços, suas regras de retirada e devolução. Você não configura nada sozinho.',
        },
        {
          title: 'Seus clientes reservam sozinhos',
          text: 'Você manda o link no WhatsApp ou põe no Instagram. Cada reserva cai direto no seu painel.',
        },
      ],
      features: [
        {
          title: 'Aviso no WhatsApp',
          text: 'Confirmação de reserva e lembrete de retirada e devolução, sem você digitar nada.',
        },
        {
          title: 'Pagamento na reserva',
          text: 'Sinal ou valor cheio, por Pix ou cartão, direto no Mercado Pago. Quem paga, leva.',
        },
        {
          title: 'Cliente que volta, ganha',
          text: 'Pontos, carteira digital e vale-presente para transformar cliente novo em cliente de sempre.',
        },
        {
          title: 'Seu estoque no bolso',
          text: 'O que está alugado, o que volta hoje e quanto entrou. Tudo na tela do celular.',
        },
      ],
      verticals:
        'Locação de brinquedos, artigos de festa, equipamentos, trajes, utensílios — e o seu negócio também.',
      formTitle: 'Bora alugar mais?',
      formSub:
        'Deixa seu contato que a gente te chama no WhatsApp, entende seu negócio e monta seu portal com você.',
      businessTypes: [
        'Locação de brinquedos',
        'Artigos de festa',
        'Equipamentos e ferramentas',
        'Trajes e vestidos',
      ],
      mock: {
        tenantName: 'Festa & Cia da Ana',
        tenantInitials: 'FC',
        portalHost: 'alugue.mjml.com.br',
        itemTitle: 'Pula-pula grande',
        itemDetail: 'diária',
        itemPrice: 'R$ 250',
        slotLabel: 'Retirada',
        slots: ['08:00', '09:00', '10:00', '14:00', '15:00', '16:00'],
        selectedSlot: '09:00',
        days: [
          { dow: 'sex', day: '11' },
          { dow: 'sáb', day: '12' },
          { dow: 'dom', day: '13' },
          { dow: 'seg', day: '14' },
        ],
        selectedDay: '12',
        cta: 'Reservar esta data',
        confirmation: 'Reserva confirmada! Lembrete de devolução no seu WhatsApp.',
      },
    }
  }

  return {
    h1: 'A agenda do seu negócio, cheia e sem furo.',
    sub: 'Seus clientes marcam sozinhos pelo celular e você recebe o aviso na hora, no WhatsApp. Sem caderno, sem vaivém de mensagem.',
    steps: [
      {
        title: 'Conte pra gente do seu negócio',
        text: 'Preencha o formulário aqui embaixo. Leva dois minutos.',
      },
      {
        title: 'A gente monta seu portal com você',
        text: 'Seus serviços, seus horários, suas cores. Você não configura nada sozinho.',
      },
      {
        title: 'Seus clientes marcam sozinhos',
        text: 'Você manda o link no WhatsApp ou põe no Instagram. Cada horário marcado cai direto na sua agenda.',
      },
    ],
    features: [
      {
        title: 'Lembrete no WhatsApp',
        text: 'O cliente recebe aviso antes do horário. Menos esquecimento, menos cadeira vazia.',
      },
      {
        title: 'Pagamento na hora de marcar',
        text: 'Sinal ou valor cheio, por Pix ou cartão, direto no Mercado Pago. Quem paga, aparece.',
      },
      {
        title: 'Cliente que volta, ganha',
        text: 'Pontos, carteira digital e vale-presente para transformar cliente novo em cliente de sempre.',
      },
      {
        title: 'Sua agenda no bolso',
        text: 'O dia inteiro na tela do celular: horários, caixa e relatórios. Nada para instalar.',
      },
    ],
    verticals:
      'Barbearias, salões de beleza, clínicas, estúdios, quadras esportivas, salões de festa — e o seu negócio também.',
    formTitle: 'Bora encher a agenda?',
    formSub:
      'Deixa seu contato que a gente te chama no WhatsApp, entende seu negócio e monta seu portal com você.',
    businessTypes: [
      'Barbearia',
      'Salão de beleza',
      'Clínica ou consultório',
      'Estúdio (tatuagem, foto…)',
      'Quadra esportiva',
      'Salão de festas',
    ],
    mock: {
      tenantName: 'Barbearia do Léo',
      tenantInitials: 'BL',
      portalHost: 'agenda.mjml.com.br',
      itemTitle: 'Corte + barba',
      itemDetail: '45 min',
      itemPrice: 'R$ 70',
      slotLabel: 'Horários de sábado',
      slots: ['09:00', '09:45', '10:30', '14:00', '14:45', '15:30'],
      selectedSlot: '09:45',
      days: [
        { dow: 'qui', day: '10' },
        { dow: 'sex', day: '11' },
        { dow: 'sáb', day: '12' },
        { dow: 'dom', day: '13' },
      ],
      selectedDay: '12',
      cta: 'Confirmar horário',
      confirmation: 'Horário confirmado! Lembrete automático no seu WhatsApp.',
    },
  }
}

export default async function LandingPage() {
  const brand = await getActiveBrand()
  const copy = copyFor(brand)
  const palette = PALETTES[brand.id] ?? PALETTES.agenda

  return (
    <div style={palette as CSSProperties} className="flex-1 bg-background">
      {/* ── Hero ─────────────────────────────────────────────── */}
      <div className="relative overflow-hidden bg-(--hero-bg) text-(--hero-fg)">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -right-40 -top-48 size-[34rem] rounded-full bg-(--hero-glow) opacity-50 blur-3xl"
        />
        <header className="relative mx-auto flex w-full max-w-6xl items-center justify-between px-5 py-5 sm:px-8">
          <p className="text-xl font-bold tracking-tight">{brand.name}</p>
          <Link
            href="/login"
            className="rounded-lg border border-white/25 px-4 py-2 text-sm font-medium transition-colors hover:bg-white/10 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
          >
            Entrar
          </Link>
        </header>

        <section className="relative mx-auto grid w-full max-w-6xl items-center gap-12 px-5 pb-16 pt-10 sm:px-8 sm:pb-24 sm:pt-16 lg:grid-cols-[1.1fr_auto] lg:gap-8">
          <div className="max-w-xl">
            <h1 className="landing-rise text-balance text-4xl font-bold leading-[1.08] tracking-tight sm:text-5xl lg:text-6xl">
              {copy.h1}
            </h1>
            <p className="landing-rise-2 mt-6 max-w-[52ch] text-lg leading-8 text-(--hero-muted) sm:text-xl sm:leading-9">
              {copy.sub}
            </p>
            <div className="landing-rise-3 mt-9 flex flex-wrap items-center gap-4">
              <a
                href="#interesse"
                className="rounded-xl bg-white px-6 py-3.5 text-base font-semibold text-(--brand-ink) shadow-lg transition-transform hover:scale-[1.02] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
              >
                Quero conhecer
              </a>
              <p className="text-sm text-(--hero-muted)">Grátis pra começar a conversa</p>
            </div>
          </div>
          <div className="landing-rise-4 justify-self-center lg:justify-self-end">
            <PhoneMock {...copy.mock} />
          </div>
        </section>
      </div>

      {/* ── Como funciona (sequência real, por isso numerada) ── */}
      <section className="mx-auto w-full max-w-6xl px-5 py-16 sm:px-8 sm:py-24">
        <h2 className="landing-reveal text-balance text-3xl font-bold tracking-tight text-foreground sm:text-4xl">
          Como funciona
        </h2>
        <ol className="mt-10 grid gap-10 sm:grid-cols-3 sm:gap-8">
          {copy.steps.map((step, i) => (
            <li key={step.title} className="landing-reveal flex gap-4 sm:block">
              <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-(--tint) text-base font-bold text-(--tint-ink) sm:mb-4">
                {i + 1}
              </span>
              <div>
                <h3 className="text-lg font-semibold text-foreground">{step.title}</h3>
                <p className="mt-2 max-w-[38ch] text-base leading-7 text-foreground/70">
                  {step.text}
                </p>
              </div>
            </li>
          ))}
        </ol>
      </section>

      {/* ── O que já vem pronto ───────────────────────────────── */}
      <section className="border-y border-border bg-muted/40">
        <div className="mx-auto w-full max-w-6xl px-5 py-16 sm:px-8 sm:py-24">
          <h2 className="landing-reveal max-w-2xl text-balance text-3xl font-bold tracking-tight text-foreground sm:text-4xl">
            Tudo que o balcão precisa já vem pronto
          </h2>
          <div className="mt-10 grid gap-x-12 gap-y-10 sm:grid-cols-2">
            {copy.features.map(f => (
              <div key={f.title} className="landing-reveal flex gap-4">
                <svg
                  aria-hidden="true"
                  className="mt-1 size-6 shrink-0 text-(--brand-ink)"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2.5"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  <path d="m4.5 12.5 5 5 10-11" />
                </svg>
                <div>
                  <h3 className="text-lg font-semibold text-foreground">{f.title}</h3>
                  <p className="mt-1.5 max-w-[44ch] text-base leading-7 text-foreground/70">
                    {f.text}
                  </p>
                </div>
              </div>
            ))}
          </div>
          <p className="landing-reveal mt-12 max-w-2xl text-base leading-7 text-foreground/60">
            {copy.verticals}
          </p>
        </div>
      </section>

      {/* ── Formulário de interesse ───────────────────────────── */}
      <section id="interesse" className="bg-(--tint) scroll-mt-8">
        <div className="mx-auto w-full max-w-xl px-5 py-16 sm:px-8 sm:py-24">
          <h2 className="landing-reveal text-balance text-center text-3xl font-bold tracking-tight text-foreground sm:text-4xl">
            {copy.formTitle}
          </h2>
          <p className="landing-reveal mx-auto mt-4 max-w-md text-center text-base leading-7 text-foreground/70">
            {copy.formSub}
          </p>
          <div className="landing-reveal mt-10">
            <LeadForm brandId={brand.id} businessTypes={copy.businessTypes} />
          </div>
        </div>
      </section>

      {/* ── Rodapé ────────────────────────────────────────────── */}
      <footer className="mx-auto flex w-full max-w-6xl flex-col items-center gap-3 px-5 py-10 text-center sm:flex-row sm:justify-between sm:text-left">
        <p className="text-sm text-muted-foreground">
          <span className="font-semibold text-foreground">{brand.name}</span> — {brand.tagline}
        </p>
        <p className="text-sm text-muted-foreground">
          Um produto{' '}
          <a
            href="https://mjml.com.br"
            className="font-medium text-(--brand-ink) underline-offset-4 hover:underline"
          >
            MJML
          </a>
        </p>
        <p className="text-sm text-muted-foreground">
          Já sou cliente:{' '}
          <Link href="/login" className="font-medium text-(--brand-ink) underline-offset-4 hover:underline">
            entrar no painel
          </Link>
        </p>
      </footer>
    </div>
  )
}
