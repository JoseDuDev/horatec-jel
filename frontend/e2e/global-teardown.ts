import { execSync } from 'child_process'
import path from 'path'

const ROOT = path.resolve(__dirname, '../..')

export default async function globalTeardown() {
  console.log('[E2E] Derrubando docker-compose.e2e.yml...')
  execSync('docker compose -f docker-compose.e2e.yml down -v', {
    stdio: 'inherit',
    cwd: ROOT,
  })
  console.log('[E2E] Stack removido.')
}
