export default defineNuxtConfig({
  // В `nuxt dev` оставляем панель; в production-сборке (`npm run build` + `node .output/...`) не подключаем devtools
  devtools: { enabled: process.env.NODE_ENV !== 'production' },
  modules: ['@nuxtjs/tailwindcss', '@pinia/nuxt'],
  ssr: false,

  app: {
    head: {
      title: 'Ticket System',
      htmlAttrs: { lang: 'ru' },
      meta: [
        { charset: 'utf-8' },
        { name: 'viewport', content: 'width=device-width, initial-scale=1, viewport-fit=cover' },
        { name: 'description', content: 'Ticket Management System' },
        { name: 'theme-color', content: '#0A0A0B' },
        { name: 'apple-mobile-web-app-capable', content: 'yes' },
        { name: 'mobile-web-app-capable', content: 'yes' },
        { name: 'apple-mobile-web-app-status-bar-style', content: 'black-translucent' },
      ],
      link: [
        { rel: 'icon', type: 'image/jpeg', href: '/favicon.svg' },
        { rel: 'apple-touch-icon', href: '/apple-touch-icon.png' },
        { rel: 'manifest', href: '/manifest.json' },
      ],
    },
  },

  css: [
    '@fontsource-variable/inter/index.css',
    '@fontsource-variable/jetbrains-mono/index.css',
    '~/assets/css/main.css',
  ],

  runtimeConfig: {
    public: {
      apiBaseUrl: '',
      /**
       * Если задан (как NUXT_DEV_BACKEND_URL при старте dev), браузер ходит на хабы по этому origin (negotiate+WS).
       * Иначе — только относительные `/hubs/*` через прокси (без жёсткого :5000).
       */
      devBackendUrl: process.env.NUXT_DEV_BACKEND_URL || '',
    },
  },


  devServer: {
    host: '0.0.0.0',
    port: 3000,
  },

  nitro: (() => {
    const raw =
      (process.env.NUXT_DEV_BACKEND_URL || '').trim() || 'http://127.0.0.1:5000'
    const base = raw.replace(/\/$/, '')
    return {
      routeRules: {
        '/api/**': { proxy: `${base}/api/**` },
        '/hubs/**': { proxy: `${base}/hubs/**` },
        '/uploads/**': { proxy: `${base}/uploads/**` },
      },
    }
  })(),

  vite: {
    server: {
      proxy: {
        '/api': {
          target: process.env.NUXT_DEV_BACKEND_URL || 'http://127.0.0.1:5000',
          changeOrigin: true,
        },
        '/hubs': {
          target: process.env.NUXT_DEV_BACKEND_URL || 'http://127.0.0.1:5000',
          changeOrigin: true,
          ws: true,
        },
        '/uploads': {
          target: process.env.NUXT_DEV_BACKEND_URL || 'http://127.0.0.1:5000',
          changeOrigin: true,
        },
      },
    },
  },

  tailwindcss: {
    configPath: '~/tailwind.config.ts',
    cssPath: '~/assets/css/main.css',
  },

  compatibilityDate: '2025-01-01',
})
