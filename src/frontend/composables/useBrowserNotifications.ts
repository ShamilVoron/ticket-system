import { ref, readonly } from 'vue'

const STORAGE_KEY = 'ticket-system-notifications'

const enabled = ref(false)
const inited = ref(false)
/** Синхронизируется с Notification.permission (обновляется в init / toggle) */
const permission = ref<NotificationPermission | 'unsupported'>('default')

export type BrowserNotificationsToggleResult =
  | 'enabled'
  | 'disabled'
  | 'denied'
  | 'unsupported'
  /** Страница по HTTP (не localhost) — API уведомлений недоступен */
  | 'insecure'

const secureContext = ref(true)

function hasNotificationApi(): boolean {
  return typeof window !== 'undefined' && typeof Notification !== 'undefined'
}

function refreshSecureContext() {
  secureContext.value = typeof window !== 'undefined' && window.isSecureContext
}

function refreshPermission() {
  if (!hasNotificationApi()) {
    permission.value = 'unsupported'
    return
  }
  permission.value = Notification.permission
}

function syncEnabledWithPermission() {
  refreshPermission()
  if (!hasNotificationApi()) {
    enabled.value = false
    localStorage.setItem(STORAGE_KEY, '0')
    return
  }
  if (localStorage.getItem(STORAGE_KEY) === '1' && Notification.permission !== 'granted') {
    enabled.value = false
    localStorage.setItem(STORAGE_KEY, '0')
  }
}

function init() {
  if (inited.value) return
  if (typeof window === 'undefined') return
  inited.value = true
  refreshSecureContext()
  enabled.value = localStorage.getItem(STORAGE_KEY) === '1'
  syncEnabledWithPermission()
}

function shouldSuppressForFocus(): boolean {
  if (typeof document === 'undefined') return true
  return document.visibilityState === 'visible' && document.hasFocus()
}

async function toggle(): Promise<BrowserNotificationsToggleResult> {
  init()
  if (typeof window === 'undefined') return 'unsupported'

  if (enabled.value) {
    enabled.value = false
    localStorage.setItem(STORAGE_KEY, '0')
    refreshPermission()
    refreshSecureContext()
    return 'disabled'
  }

  refreshSecureContext()
  if (!secureContext.value) {
    return 'insecure'
  }

  if (!hasNotificationApi()) {
    refreshPermission()
    return 'unsupported'
  }

  let perm = Notification.permission
  if (perm === 'default') {
    perm = await Notification.requestPermission()
  }

  if (perm !== 'granted') {
    enabled.value = false
    localStorage.setItem(STORAGE_KEY, '0')
    refreshPermission()
    return perm === 'denied' ? 'denied' : 'unsupported'
  }

  refreshPermission()
  enabled.value = true
  localStorage.setItem(STORAGE_KEY, '1')

  try {
    new Notification('Ticket System', {
      body: 'Браузерные уведомления включены',
      icon: notificationIconAbsUrl(),
      tag: 'ticket-system-setup',
    })
  } catch {
    /* ignore */
  }

  return 'enabled'
}

export type BrowserNotifyOptions = {
  tag?: string
  /** Показать даже когда вкладка активна (по умолчанию только в фоне) */
  force?: boolean
}

/** PNG для ОС (SVG в уведомлениях Windows часто отображается некорректно). */
function notificationIconAbsUrl(): string {
  if (typeof window === 'undefined') return '/notification-icon.png'
  return `${window.location.origin}/notification-icon.png`
}

function notify(title: string, body?: string, options?: BrowserNotifyOptions) {
  init()
  if (!enabled.value) return
  if (!secureContext.value) return
  if (!hasNotificationApi() || Notification.permission !== 'granted') return
  if (!options?.force && shouldSuppressForFocus()) return
  try {
    new Notification(title, {
      body,
      icon: notificationIconAbsUrl(),
      tag: options?.tag ?? 'ticket-system',
    })
  } catch {
    /* ignore */
  }
}

export function useBrowserNotifications() {
  init()
  return {
    enabled: readonly(enabled),
    permission: readonly(permission),
    secureContext: readonly(secureContext),
    toggle,
    notify,
  }
}
