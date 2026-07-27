import { HubConnectionBuilder, HubConnectionState, LogLevel, HttpTransportType } from '@microsoft/signalr'
import type { HubConnection } from '@microsoft/signalr'
import { useAuthStore } from '~/stores/auth'
import {
  isLoopbackHost,
  resolveBrowserSignalRHubOrigin,
  resolvePublicApiBaseUrl,
} from '~/utils/resolvePublicApiBaseUrl'

/** Пейлоад события TicketSync (сервер: TicketSyncPayload). */
export type TicketSyncPayload = {
  ticketId?: number | null
  kind?: string
  actorUserId?: string | null
  message?: string | null
  recipientUserIds?: string[] | null
}

export type TicketSignalRConnectionState =
  | 'Disconnected'
  | 'Connecting'
  | 'Connected'
  | 'Reconnecting'

type SyncCallback = (payload: TicketSyncPayload) => void

let connection: HubConnection | null = null
let callbacks: Set<SyncCallback> = new Set()
let reconnectTimer: ReturnType<typeof setTimeout> | null = null

/** Shared reactive connection state for layout banner / diagnostics. */
const connectionState = ref<TicketSignalRConnectionState>('Disconnected')

function syncConnectionState() {
  if (!connection) {
    connectionState.value = 'Disconnected'
    return
  }
  switch (connection.state) {
    case HubConnectionState.Connected:
      connectionState.value = 'Connected'
      break
    case HubConnectionState.Connecting:
      connectionState.value = 'Connecting'
      break
    case HubConnectionState.Reconnecting:
      connectionState.value = 'Reconnecting'
      break
    default:
      connectionState.value = 'Disconnected'
  }
}

function getHubUrl(): string {
  const config = useRuntimeConfig()
  const direct = resolveBrowserSignalRHubOrigin(config.public.devBackendUrl as string | undefined)
  if (direct) return `${direct}/hubs/notifications`
  const base = resolvePublicApiBaseUrl(config.public.apiBaseUrl as string | undefined)
  let url = base ? `${base.replace(/\/$/, '')}/hubs/notifications` : '/hubs/notifications'
  if (typeof window !== 'undefined' && url.startsWith('http')) {
    try {
      const u = new URL(url)
      if (u.origin !== window.location.origin && isLoopbackHost(u.hostname)) {
        url = '/hubs/notifications'
      }
    } catch {
      /* keep */
    }
  }
  return url
}

function normalizePayload(raw: any): TicketSyncPayload {
  if (raw && typeof raw === 'object' && ('ticketId' in raw || 'kind' in raw || 'message' in raw)) {
    return {
      ticketId: raw.ticketId ?? null,
      kind: raw.kind ?? 'generic',
      actorUserId: raw.actorUserId ?? null,
      message: raw.message ?? null,
      recipientUserIds: Array.isArray(raw.recipientUserIds) ? raw.recipientUserIds : null,
    }
  }
  const legacyId = typeof raw === 'number' ? raw : raw?.ticketId ?? null
  return { ticketId: legacyId, kind: 'generic', message: null, actorUserId: null, recipientUserIds: null }
}

function ensureConnection() {
  if (connection && connection.state !== HubConnectionState.Disconnected) return

  const auth = useAuthStore()

  connection = new HubConnectionBuilder()
    .withUrl(getHubUrl(), {
      accessTokenFactory: () => {
        if (import.meta.client) auth.hydrate()
        return auth.token || ''
      },
      transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
    })
    .withAutomaticReconnect([0, 500, 1000, 2000, 5000, 10000])
    .configureLogging(LogLevel.Warning)
    .build()

  connection.on('TicketSync', (payload: any) => {
    const normalized = normalizePayload(payload)
    callbacks.forEach((cb) => {
      try {
        cb(normalized)
      } catch {
        /* ignore */
      }
    })
  })

  connection.onreconnecting(() => {
    connectionState.value = 'Reconnecting'
  })
  connection.onreconnected(() => {
    connectionState.value = 'Connected'
  })
  connection.onclose(() => {
    connectionState.value = 'Disconnected'
    scheduleReconnect()
  })

  connectionState.value = 'Connecting'
  startConnection()
}

async function startConnection() {
  if (!connection || connection.state !== HubConnectionState.Disconnected) return
  connectionState.value = 'Connecting'
  try {
    await connection.start()
    syncConnectionState()
  } catch {
    connectionState.value = 'Disconnected'
    scheduleReconnect()
  }
}

function scheduleReconnect() {
  if (reconnectTimer) return
  reconnectTimer = setTimeout(() => {
    reconnectTimer = null
    startConnection()
  }, 5000)
}

/** True when the shared TicketSync hub is connected (or connecting). */
export function isTicketSignalRConnected(): boolean {
  if (!connection) return false
  const s = connection.state
  return s === HubConnectionState.Connected || s === HubConnectionState.Connecting || s === HubConnectionState.Reconnecting
}

/**
 * Подписка на TicketSync. Возвращает реактивный connectionState для баннера офлайна.
 * callback опционален — layout может вызывать только ради состояния.
 */
export function useTicketSignalR(callback?: SyncCallback) {
  ensureConnection()
  if (callback) {
    callbacks.add(callback)
    onUnmounted(() => {
      callbacks.delete(callback)
      if (callbacks.size === 0 && connection) {
        connection.stop()
        connection = null
        connectionState.value = 'Disconnected'
      }
    })
  }

  return {
    connectionState: readonly(connectionState),
    isConnected: computed(() => connectionState.value === 'Connected'),
  }
}

/** Только id заявки (удобно для обновления списков). */
export function ticketIdFromSync(payload: TicketSyncPayload): number | null {
  return payload.ticketId ?? null
}
