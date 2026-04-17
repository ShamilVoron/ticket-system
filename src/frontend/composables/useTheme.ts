const STORAGE_KEY = 'ticket-system-theme'

type Theme = 'light' | 'dark'

const _theme = ref<Theme>('light')

function applyTheme(t: Theme) {
  if (import.meta.client) {
    document.documentElement.classList.toggle('dark', t === 'dark')
    localStorage.setItem(STORAGE_KEY, t)
  }
}

export function useTheme() {
  function init() {
    if (!import.meta.client) return
    const stored = localStorage.getItem(STORAGE_KEY) as Theme | null
    const preferred = stored || (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')
    _theme.value = preferred
    applyTheme(preferred)
  }

  function toggle() {
    const next: Theme = _theme.value === 'dark' ? 'light' : 'dark'
    _theme.value = next
    applyTheme(next)
  }

  const isDark = computed(() => _theme.value === 'dark')

  return { theme: readonly(_theme), isDark, toggle, init }
}
