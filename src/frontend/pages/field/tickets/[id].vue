<script setup lang="ts">
import {
  RefreshCw,
  MapPin,
  Building2,
  Camera,
  FileText,
  Paperclip,
  MessageSquare,
  CheckCircle2,
} from 'lucide-vue-next'
import { resolvePublicApiBaseUrl } from '~/utils/resolvePublicApiBaseUrl'
import type { Ticket, Comment, Attachment, SystemStatus } from '~/types'

definePageMeta({
  layout: 'field',
  middleware: 'field',
})

const route = useRoute()
const api = useApi()
const auth = useAuthStore()
const pageHeader = usePageHeader()
const toast = useToast()

const ticketId = computed(() => Number(route.params.id))

const ticket = ref<Ticket | null>(null)
const comments = ref<Comment[]>([])
const attachments = ref<Attachment[]>([])
const statuses = ref<SystemStatus[]>([])
const loading = ref(true)
const statusBusy = ref(false)
const uploading = ref(false)
const fileInputRef = ref<HTMLInputElement | null>(null)

const apiBase = computed(() => {
  const cfg = useRuntimeConfig()
  return resolvePublicApiBaseUrl(cfg.public.apiBaseUrl as string | undefined)
})

/** Часто используемые статусы для выезда (до 3 кнопок). */
const quickStatuses = computed(() => {
  const preferred = ['В работе', 'Ожидание', 'Решён', 'Решено', 'Закрыт']
  const fromApi = statuses.value
    .filter(s => s.isActive !== false)
    .map(s => s.name)
    .filter(Boolean)

  const pool = fromApi.length ? fromApi : preferred
  const picked: string[] = []
  for (const name of preferred) {
    if (pool.includes(name) && !picked.includes(name) && name !== ticket.value?.status) {
      picked.push(name)
    }
    if (picked.length >= 3) break
  }
  // If still short, fill from API list
  for (const name of pool) {
    if (picked.length >= 3) break
    if (name !== ticket.value?.status && !picked.includes(name)) picked.push(name)
  }
  return picked.slice(0, 3)
})

const publicComments = computed(() =>
  comments.value.filter(c => !c.isInternal).slice().reverse(),
)

function attachmentHref(a: Attachment): string {
  const url = (a.url || '').trim()
  if (!url) return '#'
  if (/^https?:\/\//i.test(url)) return url
  if (url.startsWith('/')) return `${apiBase.value}${url}`
  return url
}

function isImage(a: Attachment): boolean {
  const ct = (a.contentType || '').toLowerCase()
  const name = (a.fileName || '').toLowerCase()
  return ct.startsWith('image/') || /\.(jpe?g|png|gif|webp|heic)$/i.test(name)
}

async function loadData() {
  if (!ticketId.value || Number.isNaN(ticketId.value)) return
  loading.value = true
  try {
    const [ticketData, commentsData, attaches, statusList] = await Promise.all([
      api.tickets.getById(ticketId.value),
      api.tickets.getComments(ticketId.value),
      api.tickets.getAttachments(ticketId.value),
      api.systemSettings.getStatuses().catch(() => []),
    ])
    ticket.value = ticketData
    comments.value = commentsData || []
    attachments.value = attaches || []
    statuses.value = statusList || []
    pageHeader.set(`#${ticketId.value}`, true)
    try {
      await api.tickets.markAsRead(ticketId.value)
    } catch {
      /* ignore */
    }
  } catch {
    toast.error('Не удалось загрузить заявку')
    ticket.value = null
  } finally {
    loading.value = false
  }
}

async function setStatus(status: string) {
  if (!ticket.value || statusBusy.value) return
  statusBusy.value = true
  try {
    await api.tickets.updateStatus(ticketId.value, status)
    ticket.value.status = status
    toast.success('Статус обновлён')
  } catch {
    toast.error('Не удалось сменить статус')
  } finally {
    statusBusy.value = false
  }
}

async function onPhotoPicked(e: Event) {
  const input = e.target as HTMLInputElement
  if (!input.files?.length) return
  uploading.value = true
  try {
    for (let i = 0; i < input.files.length; i++) {
      const formData = new FormData()
      formData.append('file', input.files[i])
      formData.append('uploadedBy', auth.fullName)
      await api.tickets.uploadAttachment(ticketId.value, formData)
    }
    toast.success('Фото загружено')
    const attaches = await api.tickets.getAttachments(ticketId.value)
    attachments.value = attaches || []
  } catch {
    toast.error('Ошибка загрузки')
  } finally {
    uploading.value = false
    if (fileInputRef.value) fileInputRef.value.value = ''
  }
}

useTicketSignalR((payload) => {
  if (payload.ticketId === ticketId.value) void loadData()
})

onMounted(() => {
  void loadData()
})

onBeforeUnmount(() => {
  pageHeader.clear()
})

watch(ticketId, () => {
  void loadData()
})
</script>

<template>
  <div class="max-w-lg mx-auto space-y-4">
    <div v-if="loading" class="flex items-center justify-center py-20">
      <RefreshCw :size="28" class="animate-spin text-indigo-600" />
    </div>

    <template v-else-if="ticket">
      <!-- Summary -->
      <div class="brutal-card p-4 space-y-3">
        <div class="flex flex-wrap items-center gap-2">
          <TicketStatusBadge :status="ticket.status" />
          <span
            v-if="ticket.priority"
            class="brutal-badge bg-gray-100 text-gray-700 border-gray-200"
          >
            {{ ticket.priority }}
          </span>
        </div>

        <h2 class="text-lg font-bold text-gray-900 dark:text-gray-100 leading-snug">
          {{ ticket.title || 'Без названия' }}
        </h2>

        <div class="space-y-1.5 text-sm text-gray-600 dark:text-gray-400">
          <div v-if="ticket.clientName" class="flex items-center gap-2">
            <Building2 :size="15" class="shrink-0" />
            <span class="truncate">{{ ticket.clientName }}</span>
          </div>
          <div v-if="ticket.objectName" class="flex items-center gap-2">
            <MapPin :size="15" class="shrink-0" />
            <span class="truncate">{{ ticket.objectName }}</span>
          </div>
        </div>

        <p
          v-if="ticket.problem"
          class="text-sm text-gray-700 dark:text-gray-300 whitespace-pre-wrap leading-relaxed border-t border-gray-100 dark:border-zinc-700 pt-3"
        >
          {{ ticket.problem }}
        </p>
      </div>

      <!-- Primary actions (max 3) -->
      <div class="space-y-2">
        <p class="text-[11px] font-bold text-gray-400 uppercase tracking-wider px-0.5">
          Действия
        </p>

        <!-- 1. Status -->
        <div class="brutal-card p-3 space-y-2">
          <div class="text-xs font-semibold text-gray-500 flex items-center gap-1.5">
            <CheckCircle2 :size="14" />
            Сменить статус
          </div>
          <div class="grid grid-cols-1 gap-2">
            <button
              v-for="s in quickStatuses"
              :key="s"
              type="button"
              class="brutal-btn-secondary min-h-[48px] text-[14px] font-semibold w-full"
              :disabled="statusBusy"
              @click="setStatus(s)"
            >
              {{ s }}
            </button>
          </div>
        </div>

        <!-- 2. Photo -->
        <button
          type="button"
          class="brutal-btn-primary w-full min-h-[52px] text-[15px]"
          :disabled="uploading"
          @click="fileInputRef?.click()"
        >
          <Camera :size="20" />
          {{ uploading ? 'Загрузка…' : 'Загрузить фото' }}
        </button>
        <input
          ref="fileInputRef"
          type="file"
          accept="image/*"
          capture="environment"
          class="hidden"
          @change="onPhotoPicked"
        />

        <!-- 3. Report -->
        <NuxtLink
          :to="`/field/report/${ticketId}`"
          class="brutal-btn-success w-full min-h-[52px] text-[15px]"
        >
          <FileText :size="20" />
          Акт выезда
        </NuxtLink>
      </div>

      <!-- Attachments -->
      <div v-if="attachments.length" class="brutal-card p-4 space-y-3">
        <div class="text-xs font-bold text-gray-400 uppercase tracking-wider flex items-center gap-1.5">
          <Paperclip :size="14" />
          Вложения ({{ attachments.length }})
        </div>
        <div class="grid grid-cols-3 gap-2">
          <a
            v-for="a in attachments"
            :key="a.id"
            :href="attachmentHref(a)"
            target="_blank"
            rel="noopener"
            class="block rounded-lg overflow-hidden border border-gray-200 dark:border-zinc-700 bg-gray-50 dark:bg-zinc-800 aspect-square"
          >
            <img
              v-if="isImage(a)"
              :src="attachmentHref(a)"
              :alt="a.fileName"
              class="w-full h-full object-cover"
            />
            <div
              v-else
              class="w-full h-full flex items-center justify-center p-2 text-[10px] text-center text-gray-500 break-all"
            >
              {{ a.fileName }}
            </div>
          </a>
        </div>
      </div>

      <!-- Comments (public) -->
      <div class="brutal-card p-4 space-y-3">
        <div class="text-xs font-bold text-gray-400 uppercase tracking-wider flex items-center gap-1.5">
          <MessageSquare :size="14" />
          Комментарии
        </div>
        <div v-if="!publicComments.length" class="text-sm text-gray-500 py-2">
          Пока нет публичных комментариев
        </div>
        <div
          v-for="c in publicComments"
          :key="c.id"
          class="border-t border-gray-100 dark:border-zinc-700 pt-3 first:border-0 first:pt-0"
        >
          <div class="flex items-baseline justify-between gap-2 mb-1">
            <span class="text-sm font-semibold text-gray-800 dark:text-gray-200">
              {{ c.authorName }}
            </span>
            <span class="text-[11px] text-gray-400 shrink-0">
              {{ new Date(c.createdAt).toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }) }}
            </span>
          </div>
          <p class="text-sm text-gray-700 dark:text-gray-300 whitespace-pre-wrap">{{ c.text }}</p>
        </div>
      </div>
    </template>

    <div v-else class="brutal-card p-8 text-center text-gray-500">
      Заявка не найдена
    </div>
  </div>
</template>
