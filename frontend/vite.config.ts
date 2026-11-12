import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  // The API only allows this origin (Frontend:BaseUrl), so the port must not move.
  server: { port: 5173, strictPort: true },
})
