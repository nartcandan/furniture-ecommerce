/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  // The API only allows this origin (Frontend:BaseUrl), so the port must not move.
  server: { port: 5173, strictPort: true },
  // Unit tests cover rules that need no browser, so they run in Node.
  test: { environment: 'node', include: ['src/**/*.test.ts'] },
})
