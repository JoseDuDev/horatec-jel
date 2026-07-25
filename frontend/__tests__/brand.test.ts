import { resolveBrandFromHost, DEFAULT_BRAND } from '@/lib/brand'

describe('resolveBrandFromHost', () => {
  it('alugue.mjml.com.br resolve a marca de locação', () => {
    const b = resolveBrandFromHost('alugue.mjml.com.br')
    expect(b.id).toBe('alugue')
    expect(b.primaryModule).toBe('Rentals')
    expect(b.defaultCapabilities).toBe('Rentals')
  })

  it('agenda.mjml.com.br resolve a marca de agendamento', () => {
    const b = resolveBrandFromHost('agenda.mjml.com.br')
    expect(b.id).toBe('agenda')
    expect(b.primaryModule).toBe('Appointments')
  })

  it('subdomínio de tenant herda a marca do domínio pai', () => {
    expect(resolveBrandFromHost('barbearia-do-joao.agenda.mjml.com.br').id).toBe('agenda')
    expect(resolveBrandFromHost('festas-da-ana.alugue.mjml.com.br').id).toBe('alugue')
  })

  it('slug de tenant contendo o nome da outra marca não confunde a resolução', () => {
    // Regressão: com match por substring, "agenda-da-maria" casava com a regra
    // "agenda" e o tenant de locação recebia a marca errada.
    expect(resolveBrandFromHost('agenda-da-maria.alugue.mjml.com.br').id).toBe('alugue')
    expect(resolveBrandFromHost('alugue-tudo.agenda.mjml.com.br').id).toBe('agenda')
  })

  it('host desconhecido usa a marca-mãe (fallback)', () => {
    expect(resolveBrandFromHost('qualquer-negocio.com').id).toBe(DEFAULT_BRAND.id)
    expect(resolveBrandFromHost(null).id).toBe(DEFAULT_BRAND.id)
    expect(resolveBrandFromHost(undefined).id).toBe(DEFAULT_BRAND.id)
  })

  it('domínio da plataforma sem subdomínio de marca cai na marca-mãe', () => {
    expect(resolveBrandFromHost('mjml.com.br').id).toBe(DEFAULT_BRAND.id)
  })

  it('BRAND_CONFIG mapeia host -> id de marca embutida', () => {
    const cfg = JSON.stringify({ 'meudominio.com': 'alugue' })
    expect(resolveBrandFromHost('www.meudominio.com', cfg).id).toBe('alugue')
  })

  it('BRAND_CONFIG aceita marca inline (override sobre a marca-mãe)', () => {
    const cfg = JSON.stringify({
      'x.com': { id: 'x', name: 'X Rentals', primaryModule: 'Rentals', defaultCapabilities: 'Rentals' },
    })
    const b = resolveBrandFromHost('x.com', cfg)
    expect(b.name).toBe('X Rentals')
    expect(b.primaryModule).toBe('Rentals')
  })

  it('BRAND_CONFIG inválido é ignorado (cai no fallback)', () => {
    expect(resolveBrandFromHost('qualquer.com', '{invalid json').id).toBe(DEFAULT_BRAND.id)
  })
})
