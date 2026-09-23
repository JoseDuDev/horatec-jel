/**
 * Liveness do container do frontend.
 *
 * De propósito não toca na API nem no banco: se este endpoint dependesse da API,
 * uma queda dela reprovaria o healthcheck do frontend e o Traefik tiraria o
 * frontend do roteamento junto — uma falha viraria duas. Aqui a pergunta é só
 * "o processo Node está de pé e servindo?".
 */

// Sem isto o Next pode responder uma versão estática gerada no build, o que
// provaria menos: queremos que o servidor execute algo a cada chamada.
export const dynamic = 'force-dynamic'

export async function GET() {
  return Response.json({ status: 'ok' })
}
