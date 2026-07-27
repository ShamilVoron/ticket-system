<script setup lang="ts">
import { RefreshCw, MapPin, ChevronRight, AlertCircle } from 'lucide-vue-next'
import type { Ticket as TicketType } from '~/types'

definePageMeta({
  layout: 'field',
  middleware: 'field',
})

const api = useApi()
const auth = useAuthStore()
const pageHeader = usePageHeader()
const toast = useToast()

const tickets = ref<TicketType[]>([])
const loading = ref(true)

function isMine(t: any): boolean {
  return (
    t.assigneeIds?.includes(auth.userId) ||
    t.assignees?.includes(auth.userId) ||
    t.assignees?.includes(auth.fullName) ||
    t.assignee === auth.userId ||
    t.assignee === auth.fullName
  )
}

async function loadTickets() {
  loading.value = true
  try {
    if (auth.fullName) {
      try {
        const paged = await api.tickets.getPaged({
          page: 1,
          pageSize: 100,
          assignees: [auth.fullName],
          sortKey: 'createdAt',
          sortOrder: 'desc',
        })
        const items = (paged?.items ?? []).filter(isMine)
        if (items.length > 0) {
          tickets.value = items
          return
        }
      } catch {
        /* fall through to getAll */
      }
    }
    const all = await api.tickets.getAll()
    tickets.value = all.filter(isMine)
  } catch {
    toast.error('Не удалось загрузить заявки')
    tickets.value = []
  } finally {
    loading.value = false
  }
}

function priorityBadge(p: string): string {
  if (p === 'Критический') return 'brutal-badge-red'
  if (p === 'Высокий') return 'bg-orange-100 text-orange-800 border-orange-200 brutal-badge'
  if (p === 'Низкий') return 'brutal-badge-cyan'
  return 'brutal-badge bg-gray-100 text-gray-700 border-gray-200'
}

function objectLine(t: TicketType): string {
  return t.objectName || t.clientName || '—'
}

useTicketSignalR(() => {
  void loadTickets()
})

onMounted(() => {
  pageHeader.set('Мои заявки', false)
  void loadTickets()
})

onBeforeUnmount(() => {
  pageHeader.clear()
})
</script>

<template>
  <div class="max-w-lg mx-auto space-y-3">
    <div class="flex items-center justify-between gap-2">
      <p class="text-sm text-gray-500 dark:text-gray-400">
        {{ loading ? 'Загрузка…' : `${tickets.length} назначено` }}
      </p>
      <button
        type="button"
        class="p-2.5 rounded-lg text-gray-500 hover:bg-gray-100 dark:hover:bg-zinc-800 min-h-[44px] min-w-[44px] inline-flex items-center justify-center"
        :disabled="loading"
        title="Обновить"
        @click="loadTickets"
      >
        <RefreshCw :size="18" :class="{ 'animate-spin': loading }" />
      </button>
    </div>

    <div v-if="loading" class="flex items-center justify-center py-20">
      <RefreshCw :size="28" class="animate-spin text-indigo-600" />
    </div>

    <div
      v-else-if="tickets.length === 0"
      class="brutal-card overflow-hidden"
    >
      <EmptyState
        title="Нет назначенных заявок"
        description="Когда вас назначат — они появятся здесь"
      >
        <template #icon>
          <AlertCircle :size="28" class="text-gray-400" />
        </template>
      </EmptyState>
    </div>

    <NuxtLink
      v-for="t in tickets"
      :key="t.id"
      :to="`/field/tickets/${t.id}`"
      class="brutal-card block p-4 active:scale-[0.99] transition-transform"
    >
      <div class="flex items-start gap-3">
        <div class="min-w-0 flex-1 space-y-2">
          <div class="flex items-start justify-between gap-2">
            <h2 class="font-semibold text-gray-900 dark:text-gray-100 text-[15px] leading-snug">
              <span class="text-gray-400 font-medium">#{{ t.id }}</span>
              {{ t.title || 'Без названия' }}
            </h2>
            <ChevronRight :size="18" class="shrink-0 text-gray-400 mt-0.5" />
          </div>

          <div class="flex flex-wrap items-center gap-1.5">
            <TicketStatusBadge :status="t.status || '—'" />
            <span v-if="t.priority" :class="priorityBadge(t.priority)">{{ t.priority }}</span>
          </div>

          <div class="flex items-center gap-1.5 text-sm text-gray-500 dark:text-gray-400">
            <MapPin :size="14" class="shrink-0" />
            <span class="truncate">{{ objectLine(t) }}</span>
          </div>
        </div>
      </div>
    </NuxtLink>
  </div>
</template>
