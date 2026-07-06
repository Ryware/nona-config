import { defineConfig, loadEnv } from 'vite'
import solid from 'vite-plugin-solid'
import { visualizer } from 'rollup-plugin-visualizer'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const proxyTarget = env.VITE_PROXY_TARGET
  const isAnalyze = env.ANALYZE === 'true'
  const proxy = proxyTarget
    ? Object.fromEntries(
        ['/auth', '/admin', '/api', '/public', '/openapi', '/scalar'].map((route) => [
          route,
          {
            target: proxyTarget,
            changeOrigin: true,
          },
        ]),
      )
    : undefined

  return {
    plugins: [
      solid({ hot: !process.env['VITEST'] }),
      ...(isAnalyze
        ? [visualizer({ open: true, filename: 'dist/stats.html', gzipSize: true })]
        : []),
    ],
    build: {
      rollupOptions: {
        output: {
          manualChunks(id) {
            if (id.includes('flatpickr')) return 'flatpickr';
            if (id.includes('@azure/msal-browser')) return 'msal';
          },
        },
      },
    },
    server: {
      host: '0.0.0.0',
      proxy,
    },
    preview: {
      proxy,
    },
    test: {
      environment: 'happy-dom',
      globals: true,
      exclude: ['**/node_modules/**', '**/dist/**'],
      setupFiles: ['./src/__tests__/setup.ts'],
      env: {
        VITE_API_BASE_URL: 'http://localhost:5027',
      },
      transformMode: { web: [/\.[jt]sx$/] },
      coverage: {
        provider: 'v8',
        reporter: ['text', 'lcov', 'html'],
        include: [
          'src/entities/**',
          'src/features/**',
          'src/shared/**',
          'src/pages/**',
          'src/widgets/**',
        ],
        exclude: [
          'src/shared/api/generated.ts',
          'src/**/*.test.{ts,tsx}',
          'src/__tests__/**',
        ],
        thresholds: {
          lines: 40,
          branches: 30,
        },
      },
    },
  }
})
