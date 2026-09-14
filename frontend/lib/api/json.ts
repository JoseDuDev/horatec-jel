/**
 * Lê o corpo de uma resposta como JSON tolerando corpo vazio.
 *
 * Vários endpoints do backend respondem 200 sem corpo (`Ok()` sobre um
 * `Result` sem valor — forgot-password, reset-password etc.). Chamar
 * `res.json()` direto nesses casos estoura
 * "Failed to execute 'json' on 'Response': Unexpected end of JSON input".
 */
export async function parseJsonBody<T>(res: Response): Promise<T> {
  if (res.status === 204 || res.status === 205) return undefined as T

  const text = await res.text()
  if (text.trim() === '') return undefined as T

  return JSON.parse(text) as T
}
