<script setup lang="ts">
import { RefreshCw, ChevronLeft, ChevronRight, Ticket, Clock, CheckCircle2, Hourglass, BarChart3, Activity } from 'lucide-vue-next'
import type { Ticket as TicketType } from '~/types'

const api = useApi()
const auth = useAuthStore()

const tickets = ref<TicketType[]>([])
const loading = ref(true)
const PER_PAGE = 10

async function loadTickets() {
  loading.value = true
  try {
    const data = await api.tickets.getAll()
    tickets.value = data.filter((t: any) =>
      t.assigneeIds?.includes(auth.userId) ||
      t.assignees?.includes(auth.userId) ||
      t.assignees?.includes(auth.fullName) ||
      t.assignee === auth.userId ||
      t.assignee === auth.fullName
    )
  } catch {
    useToast().error('Не удалось загрузить заявки')
  } finally {
    loading.value = false
  }
}

const inWork = computed(() => tickets.value.filter(t => t.status === 'В работе' || t.status === 'Открыт'))
const WAITING_STATUSES = ['Ожидание', 'Ожидание клиента', 'Ожидание запчастей', 'Отложен', 'На согласовании']
const waiting = computed(() => tickets.value.filter(t => WAITING_STATUSES.includes(t.status)))
const done = computed(() => tickets.value.filter(t => t.status === 'Закрыт' || t.status === 'Решён' || t.status === 'Решено'))

const pageInWork = ref(1)
const pageWaiting = ref(1)
const pageDone = ref(1)

watch(inWork, () => { pageInWork.value = 1 })
watch(waiting, () => { pageWaiting.value = 1 })
watch(done, () => { pageDone.value = 1 })

function paginate(items: TicketType[], page: number) {
  const start = (page - 1) * PER_PAGE
  return items.slice(start, start + PER_PAGE)
}
function totalPages(items: TicketType[]) {
  return Math.max(1, Math.ceil(items.length / PER_PAGE))
}

const pagedInWork = computed(() => paginate(inWork.value, pageInWork.value))
const pagedWaiting = computed(() => paginate(waiting.value, pageWaiting.value))
const pagedDone = computed(() => paginate(done.value, pageDone.value))

const stats = computed(() => ({
  total: tickets.value.length,
  inWork: inWork.value.length,
  waiting: waiting.value.length,
  done: done.value.length,
}))

const byPriority = computed(() => {
  const m: Record<string, number> = {}
  for (const t of tickets.value) {
    const p = t.priority || 'Без приоритета'
    m[p] = (m[p] || 0) + 1
  }
  return Object.entries(m).sort((a, b) => b[1] - a[1])
})

const byWeek = computed(() => {
  const weeks: Record<string, number> = {}
  const now = new Date()
  for (let i = 6; i >= 0; i--) {
    const d = new Date(now)
    d.setDate(d.getDate() - i)
    const key = d.toLocaleDateString('ru-RU', { day: '2-digit', month: '2-digit' })
    weeks[key] = 0
  }
  for (const t of tickets.value) {
    if (!t.closedAt) continue
    const d = new Date(t.closedAt)
    const key = d.toLocaleDateString('ru-RU', { day: '2-digit', month: '2-digit' })
    if (weeks[key] !== undefined) weeks[key]++
  }
  return Object.entries(weeks)
})

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('ru-RU', {
    day: '2-digit', month: '2-digit', year: 'numeric',
  })
}

function priorityColor(p: string): string {
  if (p === 'Критический') return 'bg-red-500'
  if (p === 'Высокий') return 'bg-orange-500'
  if (p === 'Низкий') return 'bg-blue-400'
  return 'bg-gray-400'
}

function statusBadge(status: string): string {
  if (status === 'Открыт') return 'bg-green-50 text-green-700 border-green-200'
  if (status === 'В работе') return 'bg-yellow-50 text-yellow-700 border-yellow-200'
  if (WAITING_STATUSES.includes(status)) return 'bg-orange-50 text-orange-700 border-orange-200'
  return 'bg-gray-50 text-gray-600 border-gray-200'
}

useTicketSignalR(() => { loadTickets() })

onMounted(() => { loadTickets() })
</script>

<template>
  <div class="space-y-4 w-full">
    <!-- Loading -->
    <div v-if="loading" class="flex items-center justify-center py-24">
      <RefreshCw :size="32" class="animate-spin text-indigo-600" />
    </div>

    <!-- Main layout: sidebar left + kanban right -->
    <div v-else class="flex flex-col lg:flex-row gap-4 lg:h-[calc(100vh-8rem)]">
      <!-- Left sidebar: Stats + Chart -->
      <div class="lg:w-[320px] xl:w-[360px] shrink-0 flex flex-col gap-4 lg:overflow-y-auto">
        <!-- Quick Stats -->
        <div class="bg-white rounded-xl border border-gray-200 shadow-sm p-5 flex-1 flex flex-col">
          <div class="text-xs font-bold text-gray-400 uppercase tracking-widest mb-4">Статистика</div>
          <div class="grid grid-cols-2 gap-4">
            <div class="flex items-center gap-3">
              <div class="w-11 h-11 rounded-lg bg-indigo-50 flex items-center justify-center text-indigo-600"><Ticket :size="20" /></div>
              <div>
                <div class="text-[10px] text-gray-400 font-bold uppercase">Всего</div>
                <div class="text-2xl font-bold text-gray-900">{{ stats.total }}</div>
              </div>
            </div>
            <div class="flex items-center gap-3">
              <div class="w-11 h-11 rounded-lg bg-yellow-50 flex items-center justify-center text-yellow-600"><Clock :size="20" /></div>
              <div>
                <div class="text-[10px] text-gray-400 font-bold uppercase">В работе</div>
                <div class="text-2xl font-bold text-gray-900">{{ stats.inWork }}</div>
              </div>
            </div>
            <div class="flex items-center gap-3">
              <div class="w-11 h-11 rounded-lg bg-orange-50 flex items-center justify-center text-orange-600"><Hourglass :size="20" /></div>
              <div>
                <div class="text-[10px] text-gray-400 font-bold uppercase">Ожидание</div>
                <div class="text-2xl font-bold text-gray-900">{{ stats.waiting }}</div>
              </div>
            </div>
            <div class="flex items-center gap-3">
              <div class="w-11 h-11 rounded-lg bg-green-50 flex items-center justify-center text-green-600"><CheckCircle2 :size="20" /></div>
              <div>
                <div class="text-[10px] text-gray-400 font-bold uppercase">Закрыты</div>
                <div class="text-2xl font-bold text-gray-900">{{ stats.done }}</div>
              </div>
            </div>
          </div>
          <div v-if="byPriority.length" class="mt-5 pt-4 border-t border-gray-100 flex-1">
            <div class="text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-3">По приоритету</div>
            <div class="space-y-2.5">
              <div v-for="[priority, count] in byPriority" :key="priority" class="flex items-center gap-2">
                <span class="text-[11px] text-gray-600 font-medium w-28 truncate">{{ priority }}</span>
                <div class="flex-1 h-2 bg-gray-100 rounded-full overflow-hidden">
                  <div :class="[priorityColor(priority), 'h-full rounded-full']" :style="{ width: `${(count / Math.max(stats.total, 1)) * 100}%` }"></div>
                </div>
                <span class="text-[11px] font-bold text-gray-900 w-5 text-right">{{ count }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Weekly Chart -->
        <div class="bg-white rounded-xl border border-gray-200 shadow-sm p-5 flex-1 flex flex-col">
          <div class="text-xs font-bold text-gray-400 uppercase tracking-widest mb-4">Решено за 7 дней</div>
          <div class="flex items-end gap-2 flex-1 min-h-[140px]">
            <div v-for="[day, count] in byWeek" :key="day" class="flex-1 flex flex-col items-center gap-1.5">
              <span class="text-[10px] font-bold text-gray-900">{{ count || '' }}</span>
              <div class="w-full bg-indigo-500 rounded-t min-h-[3px] transition-all" :style="{ height: `${Math.max(3, (count / Math.max(...byWeek.map(e => e[1]), 1)) * 100)}px` }"></div>
              <span class="text-[9px] text-gray-400 font-bold">{{ day }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Right: Kanban Board (always visible) -->
      <div class="flex-1 min-w-0 grid grid-cols-1 md:grid-cols-3 gap-4 lg:min-h-0">
        <!-- Column: В работе -->
        <div class="flex flex-col min-h-0">
          <div class="flex items-center gap-2 mb-3 px-1">
            <div class="w-2.5 h-2.5 rounded-full bg-yellow-500"></div>
            <span class="text-sm font-bold text-gray-900">В работе</span>
            <span class="text-[10px] font-bold text-gray-400 bg-gray-100 px-2 py-0.5 rounded-full">{{ inWork.length }}</span>
          </div>
          <div class="flex-1 space-y-2 bg-gray-50 rounded-xl border border-gray-200 p-2.5 overflow-y-auto min-h-[300px]">
            <div
              v-for="t in pagedInWork" :key="t.id"
              class="bg-white rounded-lg border border-gray-200 p-3 hover:border-indigo-200 hover:shadow-md transition-all cursor-pointer group"
              @click="navigateTo(`/tickets/${t.id}`)"
            >
              <div class="flex items-start justify-between gap-2 mb-1.5">
                <span class="font-mono text-[10px] text-gray-400 bg-gray-50 px-1.5 py-0.5 rounded">#{{ t.id }}</span>
                <span :class="['px-1.5 py-0.5 rounded text-[9px] font-bold uppercase border', statusBadge(t.status)]">{{ t.status }}</span>
              </div>
              <h4 class="text-[13px] font-semibold text-gray-900 group-hover:text-indigo-600 transition-colors leading-snug mb-1.5 line-clamp-2">{{ t.title }}</h4>
              <div class="flex items-center justify-between text-[10px] text-gray-400">
                <span class="truncate max-w-[55%]">{{ t.clientName }}</span>
                <span class="font-mono">{{ formatDate(t.createdAt) }}</span>
              </div>
              <div v-if="t.priority" class="mt-1.5">
                <span :class="['inline-block px-1.5 py-0.5 rounded text-[9px] font-bold border',
                  t.priority === 'Критический' ? 'bg-red-50 text-red-700 border-red-200' :
                  t.priority === 'Высокий' ? 'bg-orange-50 text-orange-700 border-orange-200' :
                  t.priority === 'Низкий' ? 'bg-blue-50 text-blue-600 border-blue-200' :
                  'bg-gray-50 text-gray-500 border-gray-200'
                ]">{{ t.priority }}</span>
              </div>
            </div>
            <div v-if="inWork.length === 0" class="flex flex-col items-center justify-center py-10 text-gray-400">
              <Clock :size="20" class="mb-1.5 opacity-30" />
              <span class="text-[11px]">Пусто</span>
            </div>
          </div>
          <div v-if="totalPages(inWork) > 1" class="flex items-center justify-between mt-2 px-1">
            <span class="text-[10px] text-gray-400">{{ pageInWork }} / {{ totalPages(inWork) }}</span>
            <div class="flex gap-1">
              <button @click="pageInWork--" :disabled="pageInWork <= 1" class="p-1 rounded hover:bg-gray-200 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"><ChevronLeft :size="14" class="text-gray-500" /></button>
              <button @click="pageInWork++" :disabled="pageInWork >= totalPages(inWork)" class="p-1 rounded hover:bg-gray-200 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"><ChevronRight :size="14" class="text-gray-500" /></button>
            </div>
          </div>
        </div>

        <!-- Column: Ожидание -->
        <div class="flex flex-col min-h-0">
          <div class="flex items-center gap-2 mb-3 px-1">
            <div class="w-2.5 h-2.5 rounded-full bg-orange-500"></div>
            <span class="text-sm font-bold text-gray-900">Ожидание</span>
            <span class="text-[10px] font-bold text-gray-400 bg-gray-100 px-2 py-0.5 rounded-full">{{ waiting.length }}</span>
          </div>
          <div class="flex-1 space-y-2 bg-gray-50 rounded-xl border border-gray-200 p-2.5 overflow-y-auto min-h-[300px]">
            <div
              v-for="t in pagedWaiting" :key="t.id"
              class="bg-white rounded-lg border border-gray-200 p-3 hover:border-indigo-200 hover:shadow-md transition-all cursor-pointer group"
              @click="navigateTo(`/tickets/${t.id}`)"
            >
              <div class="flex items-start justify-between gap-2 mb-1.5">
                <span class="font-mono text-[10px] text-gray-400 bg-gray-50 px-1.5 py-0.5 rounded">#{{ t.id }}</span>
                <span :class="['px-1.5 py-0.5 rounded text-[9px] font-bold uppercase border', statusBadge(t.status)]">{{ t.status }}</span>
              </div>
              <h4 class="text-[13px] font-semibold text-gray-900 group-hover:text-indigo-600 transition-colors leading-snug mb-1.5 line-clamp-2">{{ t.title }}</h4>
              <div class="flex items-center justify-between text-[10px] text-gray-400">
                <span class="truncate max-w-[55%]">{{ t.clientName }}</span>
                <span class="font-mono">{{ formatDate(t.createdAt) }}</span>
              </div>
              <div v-if="t.priority" class="mt-1.5">
                <span :class="['inline-block px-1.5 py-0.5 rounded text-[9px] font-bold border',
                  t.priority === 'Критический' ? 'bg-red-50 text-red-700 border-red-200' :
                  t.priority === 'Высокий' ? 'bg-orange-50 text-orange-700 border-orange-200' :
                  t.priority === 'Низкий' ? 'bg-blue-50 text-blue-600 border-blue-200' :
                  'bg-gray-50 text-gray-500 border-gray-200'
                ]">{{ t.priority }}</span>
              </div>
            </div>
            <div v-if="waiting.length === 0" class="flex flex-col items-center justify-center py-10 text-gray-400">
              <Hourglass :size="20" class="mb-1.5 opacity-30" />
              <span class="text-[11px]">Пусто</span>
            </div>
          </div>
          <div v-if="totalPages(waiting) > 1" class="flex items-center justify-between mt-2 px-1">
            <span class="text-[10px] text-gray-400">{{ pageWaiting }} / {{ totalPages(waiting) }}</span>
            <div class="flex gap-1">
              <button @click="pageWaiting--" :disabled="pageWaiting <= 1" class="p-1 rounded hover:bg-gray-200 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"><ChevronLeft :size="14" class="text-gray-500" /></button>
              <button @click="pageWaiting++" :disabled="pageWaiting >= totalPages(waiting)" class="p-1 rounded hover:bg-gray-200 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"><ChevronRight :size="14" class="text-gray-500" /></button>
            </div>
          </div>
        </div>

        <!-- Column: Закрыты / Решено -->
        <div class="flex flex-col min-h-0">
          <div class="flex items-center gap-2 mb-3 px-1">
            <div class="w-2.5 h-2.5 rounded-full bg-green-500"></div>
            <span class="text-sm font-bold text-gray-900">Закрыты / Решено</span>
            <span class="text-[10px] font-bold text-gray-400 bg-gray-100 px-2 py-0.5 rounded-full">{{ done.length }}</span>
          </div>
          <div class="flex-1 space-y-2 bg-gray-50 rounded-xl border border-gray-200 p-2.5 overflow-y-auto min-h-[300px]">
            <div
              v-for="t in pagedDone" :key="t.id"
              class="bg-white rounded-lg border border-gray-200 p-3 hover:border-indigo-200 hover:shadow-md transition-all cursor-pointer group opacity-80"
              @click="navigateTo(`/tickets/${t.id}`)"
            >
              <div class="flex items-start justify-between gap-2 mb-1.5">
                <span class="font-mono text-[10px] text-gray-400 bg-gray-50 px-1.5 py-0.5 rounded">#{{ t.id }}</span>
                <span class="px-1.5 py-0.5 rounded text-[9px] font-bold uppercase border bg-green-50 text-green-700 border-green-200">{{ t.status }}</span>
              </div>
              <h4 class="text-[13px] font-semibold text-gray-900 group-hover:text-indigo-600 transition-colors leading-snug mb-1.5 line-clamp-2">{{ t.title }}</h4>
              <div class="flex items-center justify-between text-[10px] text-gray-400">
                <span class="truncate max-w-[55%]">{{ t.clientName }}</span>
                <span class="font-mono">{{ formatDate(t.createdAt) }}</span>
              </div>
            </div>
            <div v-if="done.length === 0" class="flex flex-col items-center justify-center py-10 text-gray-400">
              <CheckCircle2 :size="20" class="mb-1.5 opacity-30" />
              <span class="text-[11px]">Пусто</span>
            </div>
          </div>
          <div v-if="totalPages(done) > 1" class="flex items-center justify-between mt-2 px-1">
            <span class="text-[10px] text-gray-400">{{ pageDone }} / {{ totalPages(done) }}</span>
            <div class="flex gap-1">
              <button @click="pageDone--" :disabled="pageDone <= 1" class="p-1 rounded hover:bg-gray-200 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"><ChevronLeft :size="14" class="text-gray-500" /></button>
              <button @click="pageDone++" :disabled="pageDone >= totalPages(done)" class="p-1 rounded hover:bg-gray-200 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"><ChevronRight :size="14" class="text-gray-500" /></button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
