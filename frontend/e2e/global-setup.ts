import { execSync } from 'child_process'
import path from 'path'

const ROOT = path.resolve(__dirname, '../..')

export default async function globalSetup() {
  console.log('[E2E] Subindo docker-compose.e2e.yml...')
  execSync('docker compose -f docker-compose.e2e.yml up -d --build --wait', {
    stdio: 'inherit',
    timeout: 300_000,   // 5 min — next build pode ser lento
    cwd: ROOT,
  })
  console.log('[E2E] Stack pronto.')
}
