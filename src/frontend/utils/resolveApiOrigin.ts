/**
 * Базовый origin API для запросов из браузера.
 * 1) NUXT_PUBLIC_API_BASE_URL
 * 2) Иначе эвристика клона: фронт на :3011 / :3000 / :3001 → API на том же host :5000
 * 3) Иначе IP/localhost без порта в адресе → :5000 на том же host
 * 4) Иначе '' → относительные URL (прокси Nitro / один origin)
 */
export function resolveBrowserApiOrigin(configApiBaseUrl: string | undefined): string {
  const trimmed = String(configApiBaseUrl || '').trim().replace(/\/+$/, '')
  if (trimmed) return trimmed
  if (typeof window === 'undefined') return ''

  const loc = window.location
  const p = loc.port

  if (p === '3011' || p === '3000' || p === '3001') {
    return `${loc.protocol}//${loc.hostname}:5000`
  }

  const isLocalish =
    loc.hostname === 'localhost' ||
    loc.hostname === '127.0.0.1' ||
    /^(\d{1,3}\.){3}\d{1,3}$/.test(loc.hostname)

  if (!p && isLocalish) {
    return `${loc.protocol}//${loc.hostname}:5000`
  }

  return ''
}
