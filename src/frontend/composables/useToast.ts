type ToastType = 'success' | 'error' | 'warning' | 'info'

export interface ToastItem {
  id: number
  type: ToastType
  message: string
  headline?: string
  navigateTo?: string
}

export type ToastOptions = {
  headline?: string
  navigateTo?: string
  /** 0 — только закрытие вручную; по умолчанию 4000 мс */
  durationMs?: number
}

const toasts = ref<ToastItem[]>([])
let nextId = 0

const defaultDurations: Record<ToastType, number> = {
  success: 4000,
  error: 6000,
  warning: 5000,
  info: 4000,
}

function addToast(type: ToastType, message: string, options?: ToastOptions) {
  const id = ++nextId
  const hasNav = !!options?.navigateTo
  let durationMs = options?.durationMs
  if (durationMs === undefined) {
    durationMs = defaultDurations[type]
    if (hasNav) durationMs = Math.max(durationMs, 22_000)
  }
  toasts.value.push({
    id,
    type,
    message,
    headline: options?.headline,
    navigateTo: options?.navigateTo,
  })
  if (durationMs > 0) {
    setTimeout(() => removeToast(id), durationMs)
  }
}

function removeToast(id: number) {
  toasts.value = toasts.value.filter(t => t.id !== id)
}

export function useToast() {
  return {
    toasts: readonly(toasts),
    success: (msg: string, options?: ToastOptions) => addToast('success', msg, options),
    error: (msg: string, options?: ToastOptions) => addToast('error', msg, options),
    warning: (msg: string, options?: ToastOptions) => addToast('warning', msg, options),
    info: (msg: string, options?: ToastOptions) => addToast('info', msg, options),
    remove: removeToast,
  }
}
