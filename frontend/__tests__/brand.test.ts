import { resolveBrandFromHost, DEFAULT_BRAND } from '@/lib/brand'

describe('resolveBrandFromHost', () => {
  it('host com "alugafy" resolve a marca de locação', () => {
    const b = resolveBrandFromHost('alugafy.com.br')
    expect(b.id).toBe('alugafy')
    expect(b.primaryModule).toBe('Rentals')
    expect(b.defaultCapabilities).toBe('Rentals')
  })

  it('host com "agendafy" resolve a marca de agendamento', () => {
    const b = resolveBrandFromHost('app.agendafy.com')
    expect(b.id).toBe('agendafy')
    expect(b.primaryModule).toBe('Appointments')
  })

  it('host desconhecido usa a marca-mãe (fallback)', () => {
    expect(resolveBrandFromHost('qualquer-negocio.com').id).toBe(DEFAULT_BRAND.id)
    expect(resolveBrandFromHost(null).id).toBe(DEFAULT_BRAND.id)
    expect(resolveBrandFromHost(undefined).id).toBe(DEFAULT_BRAND.id)
  })

  it('BRAND_CONFIG mapeia host -> id de marca embutida', () => {
    const cfg = JSON.stringify({ 'meudominio.com': 'alugafy' })
    expect(resolveBrandFromHost('www.meudominio.com', cfg).id).toBe('alugafy')
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
