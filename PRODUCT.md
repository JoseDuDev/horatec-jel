# Product

## Register

product

> A superfície primária é o app (admin do tenant, portal público, painel da plataforma).
> Superfícies de marketing — como a landing da raiz (`frontend/app/page.tsx`) — usam o
> register **brand** por tarefa.

## Users

- **Donos de pequenos negócios brasileiros** (barbearias, clínicas, quadras esportivas,
  salões de festas, locação de brinquedos): criam e administram seu portal de
  agendamentos/locações. Pouco tempo, pouca paciência com jargão de software; usam
  majoritariamente o celular.
- **Clientes finais desses negócios**: agendam serviços ou alugam itens pelo portal
  público do tenant (`/[slug]`), muitas vezes vindos de um link no WhatsApp/Instagram.
- **Operadores da plataforma** (equipe interna): administram tenants e planos em
  `/platform`.

## Product Purpose

Horafy é um SaaS multi-tenant de agendamento e locação. Cada tenant ganha portal
próprio, dados isolados (schema por tenant), pagamentos (Mercado Pago), notificações
por WhatsApp/e-mail e fidelidade. O mesmo backend é publicado sob marcas distintas
resolvidas por domínio: **Agenda** (agendamentos, `agenda.mjml.com.br`), **Alugue**
(locações, `alugue.mjml.com.br`) e **MJML** (marca-mãe/fallback). Sucesso = negócio
pequeno operando a agenda inteira pela plataforma sem precisar de suporte.

A aquisição hoje é assistida (sem cadastro self-service): a landing captura interesse
via formulário e o onboarding é feito pela equipe.

## Brand Personality

**Acolhedor, simples, confiável.** Fala com o dono do negócio como gente, não como
software: linguagem cotidiana em pt-BR, frases curtas, zero jargão técnico
("agenda cheia", "sem furo de horário" — nunca "otimize seu fluxo de bookings").
Calor humano vem do texto e da tipografia; o visual permanece limpo e sem exageros,
porque o produto lida com o ganha-pão das pessoas.

## Anti-references

- Landing genérica de SaaS B2B (hero-métrica, grids de cards idênticos, gradientes
  roxos, badges "AI-powered") — nada disso conversa com uma barbearia.
- Tom corporativo/enterprise ("soluções escaláveis para gestão de recursos").
- Excesso de animação ou efeito que pese no celular de entrada — o público acessa
  por aparelhos modestos e redes lentas.
- Template padrão do create-next-app (foi exatamente o que a raiz mostrava).

## Design Principles

1. **Fale a língua do balcão** — todo texto deve poder ser lido em voz alta por um
   dono de barbearia sem soar estranho.
2. **Uma página, várias marcas** — superfícies públicas usam `getActiveBrand()`;
   vocabulário e exemplos seguem o módulo da marca (agendar vs alugar), nunca
   hard-code de uma marca só.
3. **Mobile primeiro de verdade** — o dono e o cliente final estão no celular;
   desktop é o caso secundário.
4. **Mostre o produto, não promessas** — exemplos concretos de negócios reais
   (barbearia, clínica, quadra) valem mais que abstrações.
5. **Reuse o sistema existente** — shadcn/ui, tokens do globals.css, Geist;
   a landing pode ampliar a paleta da marca, mas não inventa um design system novo.

## Accessibility & Inclusion

- Alvo WCAG 2.1 AA: contraste de corpo ≥ 4.5:1, foco visível, formulários com
  labels reais e mensagens de erro claras.
- `prefers-reduced-motion` respeitado em toda animação.
- Interface inteira em pt-BR.
- Funciona bem em aparelhos Android de entrada e conexões 3G/4G (peso de página
  contido, sem bibliotecas pesadas para efeito decorativo).
