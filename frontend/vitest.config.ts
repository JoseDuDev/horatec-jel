import { defineConfig, configDefaults } from 'vitest/config'
import react from '@vitejs/plugin-react'
import { resolve } from 'path'

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./vitest.setup.ts'],
    // Os specs em e2e/ são do Playwright (test:e2e), não do vitest — excluí-los evita
    // que o vitest tente coletá-los e falhe ao importar @playwright/test.
    exclude: [...configDefaults.exclude, 'e2e/**'],
  },
  resolve: {
    alias: { '@': resolve(__dirname, '.') },
  },
})
