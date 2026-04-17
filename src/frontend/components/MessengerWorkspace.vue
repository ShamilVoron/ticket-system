<script setup lang="ts">
import { MessageSquare, Send, Users, X, Paperclip, Trash2, Settings } from 'lucide-vue-next'
import type {
  ChatMessageDto,
  ChatConversationDetailDto,
  MessengerSidebarDto,
} from '~/composables/useMessengerSignalR'
import MessageReactions from '~/components/MessageReactions.vue'
import { resolvePublicApiBaseUrl } from '~/utils/resolvePublicApiBaseUrl'

type ConvItem = {
  id: string
  isGroup: boolean
  title: string | null
  peerUserId: string | null
  displayName: string
  avatarUrl: string | null
  lastMessagePreview: string | null
  lastMessageAtUtc: string
  unreadCount: number
}

type StaffRow = {
  userId: string
  fullName: string
  role: string
  department: string
  avatarUrl: string
}

type PendingAttachment = { url: string; mimeType: string; fileName: string }

const auth = useAuthStore()
const { can } = useStaffPermissions()
const api = useApi()
const route = useRoute()
const config = useRuntimeConfig()
const toast = useToast()
const {
  onChatMessage,
  onSidebar,
  onChatMessageDeleted,
  onChatMessageUpdated,
  onChatConversationUpdated,
  joinConversation,
  leaveConversation,
} = useMessengerSignalR()

const conversations = ref<ConvItem[]>([])
const staff = ref<StaffRow[]>([])
const selectedId = ref<string | null>(null)
const detail = ref<{ title: string | null; isGroup: boolean; members: { userId: string; fullName: string }[] } | null>(
  null,
)
const messages = ref<ChatMessageDto[]>([])
const draft = ref('')
const loadingStaff = ref(true)
const loadingChats = ref(true)
const messengerApiMissing = ref(false)
const loadingMsgs = ref(false)
const showGroupModal = ref(false)
const groupTitle = ref('')
const groupSelected = ref<Record<string, boolean>>({})
const showEditGroupModal = ref(false)
const editGroupTitle = ref('')
const editGroupSelected = ref<Record<string, boolean>>({})
const editGroupInitialMemberIds = ref<string[]>([])
const pendingAttachment = ref<PendingAttachment | null>(null)
const fileInputRef = ref<HTMLInputElement | null>(null)
const uploadingFile = ref(false)
const messengerUnreadTotal = useState('messengerUnread', () => 0)

let prevJoined: string | null = null

function chatMediaUrl(path: string | null | undefined): string {
  const raw = (path || '').trim()
  if (!raw) return ''
  if (/^https?:\/\//i.test(raw)) return raw
  const base = resolvePublicApiBaseUrl(config.public.apiBaseUrl as string | undefined)
  if (raw.startsWith('/') && !base) return raw
  if (raw.startsWith('/')) return `${base}${raw}`
  return raw
}

function isImageMime(m: string | null | undefined): boolean {
  return !!(m && m.toLowerCase().startsWith('image/'))
}

function mergeConvFromSidebar(p: MessengerSidebarDto) {
  const idx = conversations.value.findIndex((c) => c.id === p.conversationId)
  const existing = conversations.value[idx]
  const row: ConvItem = {
    id: p.conversationId,
    isGroup: p.isGroup,
    title: p.title,
    peerUserId: p.peerUserId,
    displayName: p.displayName,
    avatarUrl: p.avatarUrl,
    lastMessagePreview: p.lastMessagePreview,
    lastMessageAtUtc: p.lastMessageAtUtc,
    unreadCount: selectedId.value === p.conversationId
      ? 0
      : (existing?.unreadCount || 0) + 1,
  }
  const next = [...conversations.value]
  if (idx >= 0) next.splice(idx, 1)
  next.unshift(row)
  next.sort((a, b) => new Date(b.lastMessageAtUtc).getTime() - new Date(a.lastMessageAtUtc).getTime())
  conversations.value = next
  messengerUnreadTotal.value = next.reduce((sum, c) => sum + c.unreadCount, 0)
}

async function loadLists() {
  messengerApiMissing.value = false
  loadingStaff.value = true
  loadingChats.value = true

  try {
    const st = await api.employees.getAll()
    staff.value = st.map((x: Record<string, unknown>) => ({
      userId: String(x.userId ?? ''),
      fullName: String(x.fullName ?? ''),
      role: String(x.role ?? ''),
      department: String(x.department ?? ''),
      avatarUrl: typeof x.avatarUrl === 'string' ? x.avatarUrl : '',
    }))
  } catch (e: unknown) {
    const err = e as { message?: string }
    toast.error(err?.message || 'Не удалось загрузить список сотрудников')
    staff.value = []
  } finally {
    loadingStaff.value = false
  }

  try {
    const list = await api.messenger.listConversations()
    conversations.value = list.map((c: any) => ({ ...c, unreadCount: c.unreadCount || 0 }))
  } catch (e: unknown) {
    conversations.value = []
    const err = e as { statusCode?: number; status?: number; response?: { status?: number }; message?: string }
    const code = err?.statusCode ?? err?.status ?? err?.response?.status
    if (code === 404) {
      messengerApiMissing.value = true
    } else if (code && code !== 401) {
      toast.error(err?.message || `Ошибка загрузки бесед (${code})`)
    }
  } finally {
    loadingChats.value = false
  }
}

async function openDirect(otherUserId: string) {
  if (otherUserId === auth.userId) return
  try {
    const { id } = await api.messenger.ensureDirect(otherUserId)
    await loadLists()
    await selectConversation(id)
  } catch (e: any) {
    toast.error(e?.data?.message || e?.message || 'Не удалось открыть диалог')
  }
}

function openGroupModal() {
  if (!can('sectionMessengerCreateGroups')) return
  groupTitle.value = ''
  groupSelected.value = {}
  showGroupModal.value = true
}

function openEditGroupModal() {
  if (!can('sectionMessengerCreateGroups')) return
  if (!detail.value?.isGroup || !selectedId.value) return
  editGroupTitle.value = detail.value.title || ''
  const sel: Record<string, boolean> = {}
  for (const m of detail.value.members) sel[m.userId] = true
  editGroupSelected.value = sel
  editGroupInitialMemberIds.value = detail.value.members.map((m) => m.userId)
  showEditGroupModal.value = true
}

function toggleGroupMember(uid: string) {
  if (uid === auth.userId) return
  groupSelected.value = { ...groupSelected.value, [uid]: !groupSelected.value[uid] }
}

function toggleEditGroupMember(uid: string) {
  if (uid === auth.userId) return
  editGroupSelected.value = { ...editGroupSelected.value, [uid]: !editGroupSelected.value[uid] }
}

async function submitGroup() {
  if (!can('sectionMessengerCreateGroups')) return
  const title = groupTitle.value.trim()
  const ids = Object.entries(groupSelected.value)
    .filter(([, v]) => v)
    .map(([k]) => k)
  if (title.length < 1) {
    toast.warning('Укажите название группы')
    return
  }
  if (ids.length < 1) {
    toast.warning('Добавьте хотя бы одного участника')
    return
  }
  try {
    const { id } = await api.messenger.createGroup(title, ids)
    showGroupModal.value = false
    await loadLists()
    await selectConversation(id)
  } catch (e: any) {
    toast.error(e?.data?.message || e?.message || 'Не удалось создать группу')
  }
}

async function submitEditGroup() {
  if (!can('sectionMessengerCreateGroups')) return
  const cid = selectedId.value
  if (!cid) return
  const title = editGroupTitle.value.trim()
  if (title.length < 1) {
    toast.warning('Укажите название группы')
    return
  }
  const initial = new Set(editGroupInitialMemberIds.value)
  const selectedIds = Object.entries(editGroupSelected.value)
    .filter(([, v]) => v)
    .map(([k]) => k)
  const add = selectedIds.filter((id) => !initial.has(id))
  const remove = [...initial].filter((id) => !selectedIds.includes(id))
  if (selectedIds.length < 2) {
    toast.warning('В группе должно быть минимум два участника')
    return
  }
  try {
    await api.messenger.updateChatGroup(cid, {
      title,
      addMemberUserIds: add.length ? add : null,
      removeMemberUserIds: remove.length ? remove : null,
    })
    showEditGroupModal.value = false
    await loadLists()
    const d = await api.messenger.getConversation(cid)
    detail.value = d
      ? {
          title: d.title,
          isGroup: d.isGroup,
          members: d.members.map((m) => ({ userId: m.userId, fullName: m.fullName })),
        }
      : detail.value
  } catch (e: any) {
    toast.error(e?.data?.message || e?.message || 'Не удалось сохранить группу')
  }
}

async function selectConversation(id: string) {
  if (prevJoined) await leaveConversation(prevJoined)
  prevJoined = id
  selectedId.value = id
  pendingAttachment.value = null
  loadingMsgs.value = true

  // Clear unread locally immediately
  const conv = conversations.value.find((c) => c.id === id)
  if (conv) conv.unreadCount = 0
  messengerUnreadTotal.value = conversations.value.reduce((sum, c) => sum + c.unreadCount, 0)

  try {
    const d = await api.messenger.getConversation(id)
    detail.value = d
      ? {
          title: d.title,
          isGroup: d.isGroup,
          members: d.members.map((m) => ({ userId: m.userId, fullName: m.fullName })),
        }
      : null
    const msgs = await api.messenger.getMessages(id)
    messages.value = msgs
    await joinConversation(id)
    await nextTick()
    scrollThreadToEnd()

    // Mark as read on server
    try {
      await api.messenger.markAsRead(id)
    } catch { /* ignore */ }
  } catch (e: any) {
    toast.error(e?.data?.message || e?.message || 'Не удалось открыть чат')
  } finally {
    loadingMsgs.value = false
  }
}

const threadEl = ref<HTMLElement | null>(null)
function scrollThreadToEnd() {
  const el = threadEl.value
  if (!el) return
  el.scrollTop = el.scrollHeight
}

function triggerFilePick() {
  fileInputRef.value?.click()
}

async function uploadFileAsPending(file: File) {
  const id = selectedId.value
  if (!id || uploadingFile.value) return
  uploadingFile.value = true
  try {
    const r = await api.messenger.uploadChatAttachment(id, file)
    pendingAttachment.value = { url: r.url, mimeType: r.mimeType, fileName: r.fileName }
  } catch (e: unknown) {
    const err = e as { data?: { message?: string }; message?: string }
    toast.error(err?.data?.message || err?.message || 'Не удалось загрузить файл')
  } finally {
    uploadingFile.value = false
  }
}

function onFileInputChange(ev: Event) {
  const input = ev.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (file) void uploadFileAsPending(file)
}

function onDraftPaste(e: ClipboardEvent) {
  const id = selectedId.value
  if (!id) return
  const items = e.clipboardData?.items
  if (!items?.length) return
  for (const it of items) {
    if (it.kind === 'file') {
      const f = it.getAsFile()
      if (f) {
        e.preventDefault()
        void uploadFileAsPending(f)
        return
      }
    }
  }
}

function clearPendingAttachment() {
  pendingAttachment.value = null
}

async function sendMessage() {
  const id = selectedId.value
  const text = draft.value.trim()
  const att = pendingAttachment.value
  if (!id || (!text && !att)) return
  draft.value = ''
  pendingAttachment.value = null
  try {
    const msg = await api.messenger.postMessage(
      id,
      text,
      att ? { url: att.url, mimeType: att.mimeType, fileName: att.fileName } : undefined,
    )
    if (msg && !messages.value.some((m) => m.id === msg.id)) {
      messages.value = [...messages.value, msg]
      nextTick(() => scrollThreadToEnd())
    }
  } catch (e: unknown) {
    draft.value = text
    pendingAttachment.value = att
    const err = e as { data?: { message?: string }; message?: string }
    toast.error(err?.data?.message || err?.message || 'Не отправлено')
  }
}

async function deleteMessage(messageId: string) {
  const id = selectedId.value
  if (!id) return
  try {
    await api.messenger.deleteChatMessage(id, messageId)
    messages.value = messages.value.filter((m) => m.id !== messageId)
  } catch (e: unknown) {
    const err = e as { data?: { message?: string }; message?: string }
    toast.error(err?.data?.message || err?.message || 'Не удалось удалить')
  }
}

async function toggleMessageReaction(messageId: string, emoji: string) {
  const id = selectedId.value
  if (!id) return
  if (!can('canReactToMessengerMessages')) return
  try {
    const updated = await api.messenger.toggleReaction(id, messageId, emoji)
    const idx = messages.value.findIndex((m) => m.id === messageId)
    if (idx !== -1) {
      messages.value[idx] = updated as ChatMessageDto
    }
  } catch {
    toast.error('Не удалось изменить реакцию')
  }
}

function formatTime(iso: string) {
  const d = new Date(iso)
  return d.toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })
}

const colleagues = computed(() =>
  staff.value.filter((s) => s.userId !== auth.userId).sort((a, b) => a.fullName.localeCompare(b.fullName, 'ru')),
)

const staffSortedForEdit = computed(() =>
  [...staff.value].sort((a, b) => a.fullName.localeCompare(b.fullName, 'ru')),
)

watch(
  () => route.query.c,
  (c) => {
    const id = typeof c === 'string' ? c.trim() : ''
    if (!id) return
    if (selectedId.value === id) return
    void selectConversation(id)
  },
  { immediate: true },
)

onMounted(() => {
  loadLists()
})

onChatMessage((msg) => {
  if (msg.conversationId !== selectedId.value) return
  if (messages.value.some((m) => m.id === msg.id)) return
  messages.value = [...messages.value, msg]
  nextTick(() => scrollThreadToEnd())
})

onSidebar(mergeConvFromSidebar)

onChatMessageDeleted((p) => {
  if (p.conversationId !== selectedId.value) return
  messages.value = messages.value.filter((m) => m.id !== p.messageId)
})

onChatMessageUpdated((msg) => {
  if (msg.conversationId !== selectedId.value) return
  const idx = messages.value.findIndex((m) => m.id === msg.id)
  if (idx !== -1) {
    messages.value[idx] = msg
  }
})

onChatConversationUpdated((d: ChatConversationDetailDto) => {
  if (d.id !== selectedId.value) return
  detail.value = {
    title: d.title,
    isGroup: d.isGroup,
    members: d.members.map((m) => ({ userId: m.userId, fullName: m.fullName })),
  }
})

onUnmounted(async () => {
  if (prevJoined) await leaveConversation(prevJoined)
})
</script>

<template>
  <div
    class="flex rounded-xl border border-zinc-200/80 dark:border-zinc-800 bg-white dark:bg-[#141415] overflow-hidden shadow-sm"
    style="min-height: calc(100dvh - 8.5rem); max-height: calc(100dvh - 8.5rem)"
  >
    <input
      ref="fileInputRef"
      type="file"
      class="hidden"
      accept=".jpg,.jpeg,.png,.gif,.webp,.pdf,.docx,.xlsx,.zip,.txt,.csv"
      @change="onFileInputChange"
    />

    <!-- Sidebar -->
    <aside
      class="w-full sm:w-[300px] lg:w-[320px] shrink-0 flex flex-col border-r border-zinc-200/80 dark:border-zinc-800 bg-zinc-50/80 dark:bg-[#0f0f10]"
    >
      <div v-if="can('sectionMessengerCreateGroups')" class="p-3 border-b border-zinc-200/70 dark:border-zinc-800 flex items-center gap-2">
        <button
          type="button"
          class="flex-1 inline-flex items-center justify-center gap-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-[13px] font-medium py-2 px-3 transition-colors"
          @click="openGroupModal"
        >
          <Users class="w-4 h-4" />
          Новая группа
        </button>
      </div>

      <div class="flex-1 overflow-y-auto">
        <p class="px-3 pt-3 pb-1 text-[11px] font-semibold text-zinc-500 uppercase tracking-wider">Беседы</p>
        <div v-if="loadingChats" class="px-3 py-3 text-sm text-zinc-500">Загрузка бесед…</div>
        <div
          v-else-if="messengerApiMissing"
          class="mx-2 mb-2 rounded-lg border border-amber-500/40 bg-amber-500/10 px-3 py-2.5 text-[12px] leading-snug text-amber-800 dark:text-amber-200"
        >
          На API нет модуля чата (404). Нужно выкатить бэкенд из репозитория
          <code class="rounded bg-black/10 px-1 text-[11px]">ticket-system</code> с контроллером Messenger и миграцией БД. Список коллег ниже
          берётся из
          <code class="rounded bg-black/10 px-1 text-[11px]">/api/Employees</code> и уже должен отображаться.
        </div>
        <div v-else-if="!conversations.length" class="px-3 py-4 text-sm text-zinc-500">Нет бесед. Выберите коллегу.</div>
        <ul v-else class="px-2 pb-2 space-y-0.5">
          <li v-for="c in conversations" :key="c.id">
            <button
              type="button"
              class="w-full text-left rounded-lg px-2.5 py-2 flex gap-2.5 transition-colors"
              :class="
                selectedId === c.id
                  ? 'bg-indigo-500/15 text-zinc-900 dark:text-white'
                  : 'hover:bg-zinc-200/60 dark:hover:bg-zinc-800/80 text-zinc-700 dark:text-zinc-300'
              "
              @click="selectConversation(c.id)"
            >
              <div
                class="w-10 h-10 rounded-full bg-zinc-200 dark:bg-zinc-700 shrink-0 overflow-hidden flex items-center justify-center text-xs font-medium"
              >
                <img v-if="c.avatarUrl" :src="c.avatarUrl" alt="" class="w-full h-full object-cover" />
                <span v-else>{{ c.displayName.slice(0, 1).toUpperCase() }}</span>
              </div>
              <div class="min-w-0 flex-1">
                <div class="flex items-center gap-2">
                  <div class="text-[13px] font-medium truncate">{{ c.displayName }}</div>
                  <span
                    v-if="c.unreadCount > 0"
                    class="shrink-0 inline-flex items-center justify-center min-w-[1.25rem] h-5 px-1 rounded-full bg-blue-500 text-white text-[10px] font-bold"
                  >
                    {{ c.unreadCount > 99 ? '99+' : c.unreadCount }}
                  </span>
                </div>
                <div class="text-[11px] text-zinc-500 dark:text-zinc-400 truncate">
                  {{ c.lastMessagePreview || (c.isGroup ? 'Группа' : 'Нет сообщений') }}
                </div>
              </div>
            </button>
          </li>
        </ul>

        <p class="px-3 pt-4 pb-1 text-[11px] font-semibold text-zinc-500 uppercase tracking-wider">Сотрудники</p>
        <div v-if="loadingStaff" class="px-3 py-3 text-sm text-zinc-500">Загрузка сотрудников…</div>
        <ul v-else class="px-2 pb-3 space-y-0.5">
          <li v-for="s in colleagues" :key="s.userId">
            <button
              type="button"
              class="w-full text-left rounded-lg px-2.5 py-2 flex gap-2.5 hover:bg-zinc-200/60 dark:hover:bg-zinc-800/80 transition-colors"
              @click="openDirect(s.userId)"
            >
              <div
                class="w-9 h-9 rounded-full bg-zinc-200 dark:bg-zinc-700 shrink-0 overflow-hidden flex items-center justify-center text-[11px] font-medium"
              >
                <img v-if="s.avatarUrl" :src="s.avatarUrl" alt="" class="w-full h-full object-cover" />
                <span v-else>{{ s.fullName.slice(0, 1).toUpperCase() }}</span>
              </div>
              <div class="min-w-0 flex-1">
                <div class="text-[13px] font-medium text-zinc-800 dark:text-zinc-200 truncate">{{ s.fullName }}</div>
                <div class="text-[11px] text-zinc-500 truncate">{{ s.department || s.role }}</div>
              </div>
            </button>
          </li>
        </ul>
      </div>
    </aside>

    <!-- Main thread -->
    <section class="flex-1 flex flex-col min-w-0 bg-white dark:bg-[#141415]">
      <div
        v-if="!selectedId"
        class="flex-1 flex flex-col items-center justify-center text-zinc-500 dark:text-zinc-400 px-6 text-center"
      >
        <MessageSquare class="w-14 h-14 mb-3 opacity-40" stroke-width="1.25" />
        <p class="text-sm">Выберите беседу или сотрудника слева</p>
      </div>

      <template v-else>
        <header class="h-12 px-4 border-b border-zinc-200/80 dark:border-zinc-800 flex items-center gap-3 shrink-0">
          <div class="min-w-0 flex-1">
            <div class="text-[14px] font-semibold text-zinc-900 dark:text-zinc-100 truncate">
              {{ detail?.isGroup ? detail?.title || 'Группа' : detail?.members.find((m) => m.userId !== auth.userId)?.fullName || 'Чат' }}
            </div>
            <div v-if="detail?.isGroup && detail.members.length" class="text-[11px] text-zinc-500 truncate">
              {{ detail.members.map((m) => m.fullName).join(', ') }}
            </div>
          </div>
          <button
            v-if="detail?.isGroup && can('sectionMessengerCreateGroups')"
            type="button"
            class="p-2 rounded-lg text-zinc-500 hover:bg-zinc-200/80 dark:hover:bg-zinc-800 shrink-0"
            title="Настройки группы"
            @click="openEditGroupModal"
          >
            <Settings class="w-5 h-5" />
          </button>
        </header>

        <div ref="threadEl" class="flex-1 overflow-y-auto p-4 space-y-3">
          <div v-if="loadingMsgs" class="text-sm text-zinc-500">Загрузка сообщений…</div>
          <template v-else>
            <div
              v-for="m in messages"
              :key="m.id"
              class="flex group/msg"
              :class="m.senderUserId === auth.userId ? 'justify-end' : 'justify-start'"
            >
              <div
                class="max-w-[85%] rounded-2xl px-3 py-2 text-[13px] leading-snug relative"
                :class="
                  m.senderUserId === auth.userId
                    ? 'bg-indigo-600 text-white rounded-br-md'
                    : 'bg-zinc-200/90 dark:bg-zinc-800 text-zinc-900 dark:text-zinc-100 rounded-bl-md'
                "
              >
                <button
                  v-if="m.senderUserId === auth.userId"
                  type="button"
                  class="absolute -top-1 -right-1 p-1 rounded-md opacity-0 group-hover/msg:opacity-100 transition-opacity bg-black/25 hover:bg-black/40 text-white"
                  title="Удалить"
                  @click="deleteMessage(m.id)"
                >
                  <Trash2 class="w-3.5 h-3.5" />
                </button>
                <div v-if="m.senderUserId !== auth.userId" class="text-[11px] font-medium opacity-80 mb-0.5">
                  {{ m.senderFullName }}
                </div>
                <a
                  v-if="m.attachmentUrl && !isImageMime(m.attachmentMimeType)"
                  :href="chatMediaUrl(m.attachmentUrl)"
                  target="_blank"
                  rel="noopener noreferrer"
                  class="block break-all underline font-medium mb-1"
                  :class="m.senderUserId === auth.userId ? 'text-indigo-100' : 'text-indigo-600 dark:text-indigo-400'"
                >
                  📎 {{ m.attachmentFileName || 'Файл' }}
                </a>
                <div v-if="m.attachmentUrl && isImageMime(m.attachmentMimeType)" class="mb-1 rounded-lg overflow-hidden max-w-[280px]">
                  <a :href="chatMediaUrl(m.attachmentUrl)" target="_blank" rel="noopener noreferrer">
                    <img :src="chatMediaUrl(m.attachmentUrl)" alt="" class="max-h-64 w-full object-cover" />
                  </a>
                </div>
                <div v-if="m.body" class="whitespace-pre-wrap break-words">{{ m.body }}</div>
                <div
                  class="text-[10px] mt-1 opacity-70"
                  :class="m.senderUserId === auth.userId ? 'text-indigo-100' : 'text-zinc-600 dark:text-zinc-400'"
                >
                  {{ formatTime(m.createdAtUtc) }}
                </div>
              </div>
              <MessageReactions
                :reactions="(m.reactions || []) as any"
                :current-user-id="auth.userId"
                :can-add="can('canReactToMessengerMessages')"
                @toggle="(emoji: string) => toggleMessageReaction(m.id, emoji)"
              />
            </div>
          </template>
        </div>

        <div class="p-3 border-t border-zinc-200/80 dark:border-zinc-800 shrink-0 space-y-2">
          <div
            v-if="pendingAttachment"
            class="flex items-center gap-2 text-[12px] rounded-lg border border-zinc-200 dark:border-zinc-700 bg-zinc-50 dark:bg-zinc-900 px-2 py-1.5"
          >
            <span class="truncate text-zinc-700 dark:text-zinc-200 flex-1">📎 {{ pendingAttachment.fileName }}</span>
            <button type="button" class="text-zinc-500 hover:text-zinc-800 dark:hover:text-zinc-100 p-1" @click="clearPendingAttachment">
              <X class="w-4 h-4" />
            </button>
          </div>
          <div class="flex gap-2">
            <textarea
              v-model="draft"
              rows="2"
              placeholder="Сообщение… (Ctrl+V — вставить фото или файл)"
              class="flex-1 resize-none rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-[#1a1a1d] text-zinc-900 dark:text-zinc-100 text-[13px] px-3 py-2 focus:ring-2 focus:ring-indigo-500/30 focus:border-indigo-500 outline-none"
              @keydown.enter.exact.prevent="sendMessage"
              @paste="onDraftPaste"
            />
            <div class="flex flex-col gap-1 self-end shrink-0">
              <button
                type="button"
                class="p-2.5 rounded-lg border border-zinc-200 dark:border-zinc-600 text-zinc-600 dark:text-zinc-300 hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors disabled:opacity-50"
                title="Прикрепить файл"
                :disabled="uploadingFile"
                @click="triggerFilePick"
              >
                <Paperclip class="w-5 h-5" />
              </button>
              <button
                type="button"
                class="p-2.5 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white transition-colors"
                title="Отправить"
                @click="sendMessage"
              >
                <Send class="w-5 h-5" />
              </button>
            </div>
          </div>
        </div>
      </template>
    </section>

    <!-- Group modal (create) -->
    <Teleport to="body">
      <div
        v-if="showGroupModal"
        class="fixed inset-0 z-[200] flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm"
        @click.self="showGroupModal = false"
      >
        <div
          class="w-full max-w-md rounded-xl bg-white dark:bg-[#1a1a1d] border border-zinc-200 dark:border-zinc-700 shadow-xl max-h-[90vh] flex flex-col"
        >
          <div class="flex items-center justify-between px-4 py-3 border-b border-zinc-200 dark:border-zinc-700">
            <span class="font-semibold text-zinc-900 dark:text-zinc-100">Новая группа</span>
            <button type="button" class="p-1 rounded-lg hover:bg-zinc-100 dark:hover:bg-zinc-800" @click="showGroupModal = false">
              <X class="w-5 h-5 text-zinc-500" />
            </button>
          </div>
          <div class="p-4 overflow-y-auto flex-1 space-y-3">
            <label class="block text-sm text-zinc-600 dark:text-zinc-400">
              Название
              <input
                v-model="groupTitle"
                type="text"
                class="mt-1 w-full rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-[#141415] px-3 py-2 text-sm text-zinc-900 dark:text-zinc-100"
                maxlength="120"
              />
            </label>
            <p class="text-[11px] font-semibold text-zinc-500 uppercase tracking-wider">Участники</p>
            <ul class="space-y-1 max-h-52 overflow-y-auto">
              <li v-for="s in colleagues" :key="s.userId">
                <label class="flex items-center gap-2 text-sm cursor-pointer py-1">
                  <input type="checkbox" :checked="!!groupSelected[s.userId]" class="rounded border-zinc-300" @change="toggleGroupMember(s.userId)" />
                  <span class="text-zinc-800 dark:text-zinc-200">{{ s.fullName }}</span>
                </label>
              </li>
            </ul>
          </div>
          <div class="p-4 border-t border-zinc-200 dark:border-zinc-700 flex justify-end gap-2">
            <button
              type="button"
              class="px-4 py-2 rounded-lg text-sm text-zinc-600 dark:text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800"
              @click="showGroupModal = false"
            >
              Отмена
            </button>
            <button
              type="button"
              class="px-4 py-2 rounded-lg text-sm font-medium bg-indigo-600 text-white hover:bg-indigo-500"
              @click="submitGroup"
            >
              Создать
            </button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Group modal (edit) -->
    <Teleport to="body">
      <div
        v-if="showEditGroupModal"
        class="fixed inset-0 z-[200] flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm"
        @click.self="showEditGroupModal = false"
      >
        <div
          class="w-full max-w-md rounded-xl bg-white dark:bg-[#1a1a1d] border border-zinc-200 dark:border-zinc-700 shadow-xl max-h-[90vh] flex flex-col"
        >
          <div class="flex items-center justify-between px-4 py-3 border-b border-zinc-200 dark:border-zinc-700">
            <span class="font-semibold text-zinc-900 dark:text-zinc-100">Группа</span>
            <button type="button" class="p-1 rounded-lg hover:bg-zinc-100 dark:hover:bg-zinc-800" @click="showEditGroupModal = false">
              <X class="w-5 h-5 text-zinc-500" />
            </button>
          </div>
          <div class="p-4 overflow-y-auto flex-1 space-y-3">
            <label class="block text-sm text-zinc-600 dark:text-zinc-400">
              Название
              <input
                v-model="editGroupTitle"
                type="text"
                class="mt-1 w-full rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-[#141415] px-3 py-2 text-sm text-zinc-900 dark:text-zinc-100"
                maxlength="120"
              />
            </label>
            <p class="text-[11px] font-semibold text-zinc-500 uppercase tracking-wider">Участники</p>
            <ul class="space-y-1 max-h-52 overflow-y-auto">
              <li v-for="s in staffSortedForEdit" :key="s.userId">
                <label
                  class="flex items-center gap-2 text-sm py-1"
                  :class="s.userId === auth.userId ? 'cursor-default opacity-80' : 'cursor-pointer'"
                >
                  <input
                    type="checkbox"
                    :disabled="s.userId === auth.userId"
                    :checked="!!editGroupSelected[s.userId]"
                    class="rounded border-zinc-300"
                    @change="toggleEditGroupMember(s.userId)"
                  />
                  <span class="text-zinc-800 dark:text-zinc-200">
                    {{ s.fullName }}<span v-if="s.userId === auth.userId" class="text-zinc-500 text-[11px]"> (вы)</span>
                  </span>
                </label>
              </li>
            </ul>
          </div>
          <div class="p-4 border-t border-zinc-200 dark:border-zinc-700 flex justify-end gap-2">
            <button
              type="button"
              class="px-4 py-2 rounded-lg text-sm text-zinc-600 dark:text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800"
              @click="showEditGroupModal = false"
            >
              Отмена
            </button>
            <button
              type="button"
              class="px-4 py-2 rounded-lg text-sm font-medium bg-indigo-600 text-white hover:bg-indigo-500"
              @click="submitEditGroup"
            >
              Сохранить
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
