import { execSync } from 'child_process'
import path from 'path'

const ROOT = path.resolve(__dirname, '../..')

export default async function globalSetup() {
  console.log('[E2E] Subindo docker-compose.e2e.yml...')
  execSync('docker compose -f docker-compose.e2e.yml up -d --build --wait', {
    stdio: 'inherit',
    timeout: 1_200_000, // 20 min — primeira run: build Next.js (~6min) + .NET (~3min) + health checks
    cwd: ROOT,
  })
  console.log('[E2E] Stack pronto.')
}
