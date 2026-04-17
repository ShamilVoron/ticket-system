import { HubConnectionBuilder, HubConnectionState, LogLevel, HttpTransportType } from '@microsoft/signalr'
import type { HubConnection } from '@microsoft/signalr'
import { useAuthStore } from '~/stores/auth'
import {
  isLoopbackHost,
  resolveBrowserSignalRHubOrigin,
  resolvePublicApiBaseUrl,
} from '~/utils/resolvePublicApiBaseUrl'

export type ChatMessageDto = {
  id: string
  conversationId: string
  senderUserId: string
  senderFullName: string
  body: string
  createdAtUtc: string
  attachmentUrl?: string | null
  attachmentMimeType?: string | null
  attachmentFileName?: string | null
  reactions?: { emoji: string; userId: string; userName: string }[]
}

export type ChatConversationDetailDto = {
  id: string
  isGroup: boolean
  title: string | null
  members: { userId: string; fullName: string; avatarUrl: string | null }[]
  lastMessageAtUtc: string
}

export type ChatMessageDeletedPayload = {
  conversationId: string
  messageId: string
}

export type MessengerSidebarDto = {
  conversationId: string
  isGroup: boolean
  title: string | null
  peerUserId: string | null
  displayName: string
  avatarUrl: string | null
  lastMessagePreview: string | null
  lastMessageAtUtc: string
  /** Кто отправил последнее сообщение (для уведомлений) */
  lastMessageSenderUserId?: string | null
  /** posted | deleted | updated */
  sidebarEventKind?: string | null
}

function normalizeChatMessageDto(raw: ChatMessageDto | Record<string, unknown>): ChatMessageDto {
  const r = raw as Record<string, unknown>
  const str = (camel: string, pascal: string) => {
    const v = r[camel] ?? r[pascal]
    return v != null ? String(v) : ''
  }
  return {
    id: str('id', 'Id'),
    conversationId: str('conversationId', 'ConversationId'),
    senderUserId: str('senderUserId', 'SenderUserId'),
    senderFullName: str('senderFullName', 'SenderFullName'),
    body: str('body', 'Body'),
    createdAtUtc: str('createdAtUtc', 'CreatedAtUtc'),
    attachmentUrl: (r.attachmentUrl ?? r.AttachmentUrl ?? null) as string | null,
    attachmentMimeType: (r.attachmentMimeType ?? r.AttachmentMimeType ?? null) as string | null,
    attachmentFileName: (r.attachmentFileName ?? r.AttachmentFileName ?? null) as string | null,
    reactions: ((r.reactions ?? r.Reactions) as any[])?.map((x: any) => ({
      emoji: String(x.emoji ?? x.Emoji),
      userId: String(x.userId ?? x.UserId),
      userName: String(x.userName ?? x.UserName),
    })) ?? [],
  }
}

function normalizeSidebarDto(raw: MessengerSidebarDto | Record<string, unknown>): MessengerSidebarDto {
  const r = raw as Record<string, unknown>
  const str = (k: string, ...alts: string[]) => {
    for (const key of [k, ...alts]) {
      const v = r[key]
      if (v != null) return String(v)
    }
    return ''
  }
  const lastPreview = r.lastMessagePreview ?? r.LastMessagePreview
  return {
    conversationId: str('conversationId', 'ConversationId'),
    isGroup: Boolean(r.isGroup ?? r.IsGroup),
    title: (r.title ?? r.Title ?? null) as string | null,
    peerUserId: (r.peerUserId ?? r.PeerUserId ?? null) as string | null,
    displayName: str('displayName', 'DisplayName') || 'Чат',
    avatarUrl: (r.avatarUrl ?? r.AvatarUrl ?? null) as string | null,
    lastMessagePreview: lastPreview != null ? String(lastPreview) : null,
    lastMessageAtUtc: str('lastMessageAtUtc', 'LastMessageAtUtc'),
    lastMessageSenderUserId: (r.lastMessageSenderUserId ?? r.LastMessageSenderUserId ?? null) as string | null,
    sidebarEventKind: (r.sidebarEventKind ?? r.SidebarEventKind ?? null) as string | null,
  }
}

type ChatHandler = (msg: ChatMessageDto) => void
type SidebarHandler = (p: MessengerSidebarDto) => void
type ChatDeletedHandler = (p: ChatMessageDeletedPayload) => void
type ChatUpdatedHandler = (msg: ChatMessageDto) => void
type ConversationUpdatedHandler = (p: ChatConversationDetailDto) => void

let chatConnection: HubConnection | null = null
const chatHandlers = new Set<ChatHandler>()
const sidebarHandlers = new Set<SidebarHandler>()
const chatDeletedHandlers = new Set<ChatDeletedHandler>()
const chatUpdatedHandlers = new Set<ChatUpdatedHandler>()
const conversationUpdatedHandlers = new Set<ConversationUpdatedHandler>()
let reconnectTimer: ReturnType<typeof setTimeout> | null = null

/** Относительный `/hubs/chat`, либо прямой origin из `devBackendUrl`, если задан NUXT_DEV_BACKEND_URL. */
function getChatHubUrl(): string {
  const config = useRuntimeConfig()
  const direct = resolveBrowserSignalRHubOrigin(config.public.devBackendUrl as string | undefined)
  if (direct) return `${direct}/hubs/chat`
  const base = resolvePublicApiBaseUrl(config.public.apiBaseUrl as string | undefined)
  let url = base ? `${base.replace(/\/$/, '')}/hubs/chat` : '/hubs/chat'
  if (typeof window !== 'undefined' && url.startsWith('http')) {
    try {
      const u = new URL(url)
      if (u.origin !== window.location.origin && isLoopbackHost(u.hostname)) {
        url = '/hubs/chat'
      }
    } catch {
      /* keep */
    }
  }
  return url
}

function scheduleReconnect() {
  if (reconnectTimer) return
  reconnectTimer = setTimeout(() => {
    reconnectTimer = null
    startChatConnection()
  }, 4000)
}

async function startChatConnection() {
  if (!chatConnection || chatConnection.state !== HubConnectionState.Disconnected) return
  try {
    await chatConnection.start()
  } catch {
    scheduleReconnect()
  }
}

function ensureChatConnection() {
  if (chatConnection && chatConnection.state !== HubConnectionState.Disconnected) return

  const auth = useAuthStore()
  chatConnection = new HubConnectionBuilder()
    .withUrl(getChatHubUrl(), {
      accessTokenFactory: () => {
        if (import.meta.client) auth.hydrate()
        return auth.token || ''
      },
      transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
    })
    .withAutomaticReconnect([0, 500, 1000, 2000, 5000, 10000])
    .configureLogging(LogLevel.Warning)
    .build()

  chatConnection.on('ChatMessage', (dto: ChatMessageDto) => {
    const normalized = normalizeChatMessageDto(dto as ChatMessageDto & Record<string, unknown>)
    chatHandlers.forEach((cb) => {
      try {
        cb(normalized)
      } catch {}
    })
  })

  chatConnection.on('MessengerSidebar', (dto: MessengerSidebarDto) => {
    const normalized = normalizeSidebarDto(dto as MessengerSidebarDto & Record<string, unknown>)
    sidebarHandlers.forEach((cb) => {
      try {
        cb(normalized)
      } catch {}
    })
  })

  chatConnection.on('ChatMessageDeleted', (dto: ChatMessageDeletedPayload) => {
    chatDeletedHandlers.forEach((cb) => {
      try {
        cb(dto)
      } catch {}
    })
  })

  chatConnection.on('ChatMessageUpdated', (dto: ChatMessageDto) => {
    const normalized = normalizeChatMessageDto(dto as ChatMessageDto & Record<string, unknown>)
    chatUpdatedHandlers.forEach((cb) => {
      try {
        cb(normalized)
      } catch {}
    })
  })

  chatConnection.on('ChatConversationUpdated', (dto: ChatConversationDetailDto) => {
    conversationUpdatedHandlers.forEach((cb) => {
      try {
        cb(dto)
      } catch {}
    })
  })

  chatConnection.onclose(() => scheduleReconnect())

  startChatConnection()
}

export function useMessengerSignalR() {
  function onChatMessage(cb: ChatHandler) {
    chatHandlers.add(cb)
    onUnmounted(() => {
      chatHandlers.delete(cb)
    })
  }

  function onSidebar(cb: SidebarHandler) {
    sidebarHandlers.add(cb)
    onUnmounted(() => {
      sidebarHandlers.delete(cb)
    })
  }

  function onChatMessageDeleted(cb: ChatDeletedHandler) {
    chatDeletedHandlers.add(cb)
    onUnmounted(() => {
      chatDeletedHandlers.delete(cb)
    })
  }

  function onChatMessageUpdated(cb: ChatUpdatedHandler) {
    chatUpdatedHandlers.add(cb)
    onUnmounted(() => {
      chatUpdatedHandlers.delete(cb)
    })
  }

  function onChatConversationUpdated(cb: ConversationUpdatedHandler) {
    conversationUpdatedHandlers.add(cb)
    onUnmounted(() => {
      conversationUpdatedHandlers.delete(cb)
    })
  }

  async function joinConversation(conversationId: string) {
    ensureChatConnection()
    const c = chatConnection
    if (!c) return
    if (c.state === HubConnectionState.Disconnected) {
      try {
        await c.start()
      } catch {
        return
      }
    }
    if (c.state !== HubConnectionState.Connected) return
    try {
      await c.invoke('JoinConversation', conversationId)
    } catch {}
  }

  async function leaveConversation(conversationId: string) {
    const c = chatConnection
    if (!c || c.state !== HubConnectionState.Connected) return
    try {
      await c.invoke('LeaveConversation', conversationId)
    } catch {}
  }

  if (import.meta.client) {
    ensureChatConnection()
  }

  onMounted(() => {
    ensureChatConnection()
  })

  return {
    onChatMessage,
    onSidebar,
    onChatMessageDeleted,
    onChatMessageUpdated,
    onChatConversationUpdated,
    joinConversation,
    leaveConversation,
  }
}
