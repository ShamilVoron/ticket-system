/**
 * База для запросов из браузера.
 *
 * Нельзя отдавать в клиент `http://localhost:5000` / `127.0.0.1` — это ВСЕГДА машина пользователя:
 * LAN, SSH-туннель только на :3000, обычный dev с прокси Nitro → запросы должны идти на тот же origin
 * (`/api/...`, `/hubs/...`), а Nitro уже ходит на Kestrel.
 *
 * Разрешён только не-loopback URL из env (например отдельный стенд по IP/домену при настроенном CORS).
 */
export function isLoopbackHost(hostname: string): boolean {
  const h = hostname.toLowerCase()
  return h === 'localhost' || h === '127.0.0.1' || h === '[::1]'
}

export function resolvePublicApiBaseUrl(apiBaseUrl: string | undefined): string {
  if (typeof window === 'undefined') {
    return ''
  }

  const raw = (apiBaseUrl ?? '').trim()
  if (!raw) {
    return ''
  }

  try {
    const u = new URL(raw)
    if (isLoopbackHost(u.hostname)) {
      return ''
    }
    return u.toString().replace(/\/$/, '')
  } catch {
    return raw.replace(/\/$/, '')
  }
}

/**
 * Прямой URL хабов в браузере — только если задан `NUXT_DEV_BACKEND_URL` (runtime public `devBackendUrl`).
 * Иначе тот же origin + прокси Nitro/Vite (порт API с машины пользователя часто не :5000 / недоступен — будет ERR_CONNECTION_REFUSED).
 */
export function resolveBrowserSignalRHubOrigin(devBackendUrlFromConfig: string | undefined): string {
  if (typeof window === 'undefined') return ''
  if (!import.meta.dev) return ''
  const raw = (devBackendUrlFromConfig ?? '').trim()
  if (!raw) return ''
  try {
    return new URL(raw).origin
  } catch {
    return ''
  }
}
