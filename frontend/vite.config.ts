import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  server: {
    host: '0.0.0.0',
    port: 443,
    strictPort: true,
    allowedHosts: ['koot.ai.ba.gl', 'localhost', '127.0.0.1'],
    proxy: {
      '/api': {
        target: 'http://localhost:5024',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5024',
        ws: true,
        changeOrigin: true,
      },
      '/uploads': {
        target: 'http://localhost:5024',
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/__tests__/setup.ts'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'lcov'],
      thresholds: {
        lines: 50,
        functions: 50,
        branches: 50,
        statements: 50,
      },
    },
  },
})
