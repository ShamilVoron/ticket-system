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

type SyncCallback = (payload: TicketSyncPayload) => void

let connection: HubConnection | null = null
let callbacks: Set<SyncCallback> = new Set()
let reconnectTimer: ReturnType<typeof setTimeout> | null = null

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

  connection.onclose(() => {
    scheduleReconnect()
  })

  startConnection()
}

async function startConnection() {
  if (!connection || connection.state !== HubConnectionState.Disconnected) return
  try {
    await connection.start()
  } catch {
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

/** Колбэк при любом TicketSync; в payload — ticketId и детали для уведомлений. */
export function useTicketSignalR(callback: SyncCallback) {
  ensureConnection()
  callbacks.add(callback)

  onUnmounted(() => {
    callbacks.delete(callback)
    if (callbacks.size === 0 && connection) {
      connection.stop()
      connection = null
    }
  })
}

/** Только id заявки (удобно для обновления списков). */
export function ticketIdFromSync(payload: TicketSyncPayload): number | null {
  return payload.ticketId ?? null
}
