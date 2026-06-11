import { execSync } from 'child_process'
import path from 'path'

const ROOT = path.resolve(__dirname, '../..')

export default async function globalSetup() {
  console.log('[E2E] Subindo docker-compose.e2e.yml...')
  execSync('docker compose -f docker-compose.e2e.yml up -d --build --wait', {
    stdio: 'inherit',
    timeout: 600_000,   // 10 min — primeira run faz download das layers do Docker
    cwd: ROOT,
  })
  console.log('[E2E] Stack pronto.')
}
