/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import { fileURLToPath, URL } from 'node:url'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    // Aspire's AddViteApp injects PORT; fall back to Vite's default for standalone runs.
    port: process.env.PORT ? Number(process.env.PORT) : 5173,
    host: true,
    // Accept requests proxied through the gateway (their Host header differs).
    allowedHosts: true,
    // When hitting the Vite server directly in dev, forward /api to the API so the
    // relative-/api app works with full HMR. (Through the gateway, YARP routes /api instead.)
    proxy: {
      '/api': {
        target:
          process.env['services__webapi__https__0'] ??
          process.env['services__webapi__http__0'] ??
          'https://localhost:7042',
        changeOrigin: true,
        secure: false,
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/test/setup.ts',
  },
})
