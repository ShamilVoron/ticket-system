<script setup lang="ts">
import { Search, RefreshCw, Ticket, ArrowUp, ArrowDown, Users, Building2, X, Eraser } from 'lucide-vue-next'
import type { Ticket as TicketType, SystemStatus } from '~/types'

const api = useApi()
const auth = useAuthStore()
const router = useRouter()
const toast = useToast()

const tickets = ref<TicketType[]>([])
const statuses = ref<SystemStatus[]>([])
const loading = ref(true)
const totalCount = ref(0)
const statsData = ref({ total: 0, open: 0, inProgress: 0, repair: 0 })
let searchDebounce: any = null

// Cookies for Filter & Sort
const searchQuery = useCookie('ticket_search', { default: () => '' })
const filterStatus = useCookie<string[]>('ticket_status', { default: () => [] })
const filterDepartments = useCookie<string[]>('ticket_depts', { default: () => [] })
const filterAssignees = useCookie<string[]>('ticket_assignees', { default: () => [] })
const filterClientNames = useCookie<string[]>('ticket_client_names', { default: () => [] })
const sortKey = useCookie<'id'|'date'|'title'|'client'|'status'|'priority'|'assignee'>('ticket_sort_key', { default: () => 'date' })
const sortOrder = useCookie<'asc'|'desc'>('ticket_sort_order', { default: () => 'desc' })

type EmployeeOption = { userId: string; fullName: string; role: string }
type ClientOption = { id?: number; label: string }

const departments = [
  'Координатор', '1 линия', '2 линия', 'Разработчики',
  'Выездные инженеры', 'Ремонт / сервис', 'Бухгалтерия',
  'Закупки', 'Системный администратор',
]

let pollInterval: any = null

// Extra filters: assignees (multi) and client (single)
const employees = ref<EmployeeOption[]>([])
const clients = ref<ClientOption[]>([])
const assigneeModalOpen = ref(false)
const assigneeSearch = ref('')
const clientModalOpen = ref(false)
const clientSearch = ref('')
const statusModalOpen = ref(false)
const statusSearch = ref('')
const deptModalOpen = ref(false)
const deptSearch = ref('')

const normalizedAssigneeSet = computed(() => new Set((filterAssignees.value || []).map((x) => (x || '').trim()).filter(Boolean)))
const normalizedStatusSet = computed(() => new Set((filterStatus.value || []).map((x) => (x || '').trim()).filter(Boolean)))
const normalizedDeptSet = computed(() => new Set((filterDepartments.value || []).map((x) => (x || '').trim()).filter(Boolean)))
const normalizedClientSet = computed(() => new Set((filterClientNames.value || []).map((x) => (x || '').trim()).filter(Boolean)))

const employeeNameById = computed(() => {
  const map = new Map<string, string>()
  for (const e of employees.value || []) {
    const uid = (e.userId || '').trim()
    const name = (e.fullName || '').trim()
    if (uid && name) map.set(uid, name)
  }
  return map
})

function resolveEmployeeLabel(idOrName: string): string {
  const raw = (idOrName || '').trim()
  if (!raw) return '—'
  return employeeNameById.value.get(raw) || raw
}

/** Исполнители: API отдаёт `assignees` (имена по id); в шаблоне нельзя кормить `resolveEmployeeLabel` всей строкой `assignee` с запятыми. */
function displayTicketAssignees(t: TicketType): string {
  const fromApi = (t.assignees || []).map(s => (s || '').trim()).filter(Boolean)
  if (fromApi.length > 0) return fromApi.join(', ')
  const raw = (t.assignee || '').trim()
  if (!raw) return '—'
  const parts = raw.split(',').map(s => s.trim()).filter(Boolean)
  if (parts.length === 0) return '—'
  return parts.map((id) => resolveEmployeeLabel(id)).join(', ')
}

const filteredEmployeesForModal = computed(() => {
  const q = assigneeSearch.value.trim().toLowerCase()
  const list = employees.value || []
  if (!q) return list
  return list.filter(e => (e.fullName || '').toLowerCase().includes(q) || (e.userId || '').toLowerCase().includes(q))
})
const filteredStatusesForModal = computed(() => {
  const q = statusSearch.value.trim().toLowerCase()
  const list = statuses.value || []
  if (!q) return list
  return list.filter(s => (s.name || '').toLowerCase().includes(q))
})
const filteredClientsForModal = computed(() => {
  const q = clientSearch.value.trim().toLowerCase()
  const list = clients.value || []
  if (!q) return list
  return list.filter(c => (c.label || '').toLowerCase().includes(q) || (c.id != null && String(c.id).includes(q)))
})
const filteredDepartmentsForModal = computed(() => {
  const q = deptSearch.value.trim().toLowerCase()
  const list = departments
  if (!q) return list
  return list.filter(d => d.toLowerCase().includes(q))
})

async function loadData() {
  loading.value = true
  try {
    const [ticketsRes, statusesRes, statsRes] = await Promise.allSettled([
      api.tickets.getPaged({
        page: page.value,
        pageSize: perPage,
        search: searchQuery.value || undefined,
        sortKey: sortKey.value,
        sortOrder: sortOrder.value,
        statuses: filterStatus.value?.length ? filterStatus.value : undefined,
        departments: filterDepartments.value?.length ? filterDepartments.value : undefined,
        assignees: filterAssignees.value?.length ? filterAssignees.value : undefined,
        clientNames: filterClientNames.value?.length ? filterClientNames.value : undefined,
      }),
      api.systemSettings.getStatuses(),
      api.tickets.getStats(),
    ])
    if (ticketsRes.status === 'fulfilled') {
      tickets.value = ticketsRes.value.items
      totalCount.value = ticketsRes.value.totalCount
    } else {
      console.error('Failed to load tickets:', ticketsRes.reason)
    }
    if (statusesRes.status === 'fulfilled') statuses.value = statusesRes.value
    else console.error('Failed to load statuses:', statusesRes.reason)
    if (statsRes.status === 'fulfilled') {
      statsData.value = {
        total: statsRes.value.totalToday,
        open: statsRes.value.openToday,
        inProgress: statsRes.value.inProgressToday,
        repair: statsRes.value.repairToday,
      }
    } else {
      console.error('Failed to load stats:', statsRes.reason)
    }
  } catch (error) {
    console.error('Failed to load tickets:', error)
  } finally {
    loading.value = false
  }
}

async function weakRefresh() {
  try {
    const ticketsRes = await api.tickets.getPaged({
      page: page.value,
      pageSize: perPage,
      search: searchQuery.value || undefined,
      sortKey: sortKey.value,
      sortOrder: sortOrder.value,
      statuses: filterStatus.value?.length ? filterStatus.value : undefined,
      departments: filterDepartments.value?.length ? filterDepartments.value : undefined,
      assignees: filterAssignees.value?.length ? filterAssignees.value : undefined,
      clientNames: filterClientNames.value?.length ? filterClientNames.value : undefined,
    })
    tickets.value = ticketsRes.items
    totalCount.value = ticketsRes.totalCount
  } catch(e) {}
}

async function loadFilterRefs() {
  // Load employees + clients for filter modals (only once)
  try {
    const [emps] = await Promise.all([
      api.employees.getAll(),
    ])
    employees.value = Array.isArray(emps) ? emps : []

    // Clients list strategy:
    // 1) Prefer Companies (jur. entities) if API role allows.
    // 2) Fallback to distinct ticket.clientName (always consistent with filtering).
    // 3) As a last resort, fallback to /api/Clients (contact full names).
    const opts: ClientOption[] = []
    const seen = new Set<string>()
    const add = (labelRaw: string, id?: number) => {
      const label = (labelRaw || '').trim()
      if (!label) return
      const key = label.toLowerCase()
      if (seen.has(key)) return
      seen.add(key)
      opts.push({ id, label })
    }

    try {
      const companies = await api.companies.getAll(true)
      if (Array.isArray(companies)) {
        for (const c of companies as any[]) add(String(c?.name ?? c?.Name ?? ''), Number(c?.id ?? c?.Id))
      }
    } catch {}

    if (opts.length === 0) {
      for (const t of tickets.value || []) add(String((t as any)?.clientName ?? ''))
    }

    if (opts.length === 0) {
      try {
        const cls = await api.clients.getAll()
        if (Array.isArray(cls)) {
          for (const c of cls as any[]) add(String(c?.fullName ?? c?.FullName ?? ''), Number(c?.id ?? c?.Id))
        }
      } catch {}
    }

    clients.value = opts.sort((a, b) => a.label.localeCompare(b.label, 'ru'))
  } catch {
    employees.value = []
    clients.value = []
    toast.error('Не удалось загрузить справочники')
  }
}

function toggleAssignee(name: string) {
  const n = (name || '').trim()
  if (!n) return
  const set = new Set(filterAssignees.value || [])
  if (set.has(n)) set.delete(n)
  else set.add(n)
  filterAssignees.value = Array.from(set)
}
function clearAssignees() {
  filterAssignees.value = []
}

function toggleStatus(name: string) {
  const n = (name || '').trim()
  if (!n) return
  const set = new Set(filterStatus.value || [])
  if (set.has(n)) set.delete(n)
  else set.add(n)
  filterStatus.value = Array.from(set)
}
function clearStatuses() {
  filterStatus.value = []
}

function toggleClient(name: string) {
  const n = (name || '').trim()
  if (!n) return
  const set = new Set(filterClientNames.value || [])
  if (set.has(n)) set.delete(n)
  else set.add(n)
  filterClientNames.value = Array.from(set)
}
function clearClients() {
  filterClientNames.value = []
}

function toggleDepartment(name: string) {
  const n = (name || '').trim()
  if (!n) return
  const set = new Set(filterDepartments.value || [])
  if (set.has(n)) set.delete(n)
  else set.add(n)
  filterDepartments.value = Array.from(set)
}
function clearDepartments() {
  filterDepartments.value = []
}

function clearAllFilters() {
  searchQuery.value = ''
  filterStatus.value = []
  filterDepartments.value = []
  filterAssignees.value = []
  filterClientNames.value = []
}

// Server-side filtering / sorting — local computed is just a pass-through
const filteredTickets = computed(() => tickets.value)

// Pagination (25 per page)
const perPage = 25
const page = ref(1)
watch(searchQuery, () => {
  page.value = 1
  if (searchDebounce) clearTimeout(searchDebounce)
  searchDebounce = setTimeout(() => loadData(), 300)
})
watch([filterStatus, filterDepartments, filterAssignees, filterClientNames, sortKey, sortOrder], () => {
  page.value = 1
  loadData()
})
const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / perPage)))
const paginatedTickets = computed(() => tickets.value)

function toggleSort(key: any) {
  if (sortKey.value === key) {
    sortOrder.value = sortOrder.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortKey.value = key
    sortOrder.value = 'asc'
  }
}

const stats = computed(() => statsData.value)

// Returns Tailwind background colors for the row depending on priority
function getPriorityRowClass(priority: string): string {
  if (priority === 'Критический') return 'bg-red-50 hover:bg-red-100'
  if (priority === 'Высокий') return 'bg-yellow-50/50 hover:bg-yellow-100/50'
  return 'hover:bg-gray-50'
}

function getPriorityBadgeClass(priority: string): string {
  const map: Record<string, string> = {
    'Низкий': 'bg-gray-100 text-gray-700 border-gray-300',
    'Средний': 'bg-gray-100 text-gray-700 border-gray-300',
    'Высокий': 'bg-yellow-100 text-yellow-800 border-yellow-300',
    'Критический': 'bg-red-100 text-red-800 border-red-300',
  }
  return map[priority] || map['Низкий']
}

function getStatusColor(status: string): string {
  const statusObj = statuses.value.find(s => s.name === status)
  if (!statusObj) return 'bg-gray-100 text-gray-700 border-gray-200'
  // standard maps already returned from db, they are standard tailwind
  return statusObj.colorClass || 'bg-gray-100 text-gray-700 border-gray-200'
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('ru-RU', { 
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

function openTicket(id: number) {
  router.push(`/tickets/${id}`)
}

function pluralizeSubtask(count: number): string {
  const mod10 = count % 10
  const mod100 = count % 100
  if (mod10 === 1 && mod100 !== 11) return 'подзадача'
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return 'подзадачи'
  return 'подзадач'
}

useTicketSignalR(() => {
  weakRefresh()
})

onMounted(() => {
  loadData()
  void loadFilterRefs()
  pollInterval = setInterval(weakRefresh, 30000)
})

onUnmounted(() => {
  if (pollInterval) clearInterval(pollInterval)
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <p class="text-sm text-gray-500">
        Показано <span class="font-semibold text-gray-900">{{ tickets.length }}</span> из 
        <span class="font-semibold text-gray-900">{{ totalCount }}</span>
      </p>
    </div>

    <!-- Filters -->
    <div class="bg-white p-3 sm:p-4 rounded-lg shadow-sm border border-gray-200 space-y-3 sm:space-y-0 sm:flex sm:flex-wrap sm:gap-3 lg:flex-nowrap">
      <div class="flex-1 relative min-w-0">
        <Search class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" :size="16" />
        <input
          v-model="searchQuery"
          type="text"
          class="w-full pl-9 pr-3 py-2.5 sm:py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500"
          placeholder="Поиск по теме, клиенту, #ID…"
        />
      </div>
      <div class="flex gap-2 overflow-x-auto -mx-3 px-3 sm:mx-0 sm:px-0 sm:overflow-visible pb-1 sm:pb-0">
        <!-- Department filter -->
        <div v-if="auth.isStaff" class="flex items-stretch gap-1.5 shrink-0">
          <button
            type="button"
            class="inline-flex items-center gap-1.5 border border-gray-300 rounded-lg text-sm px-3 py-2.5 sm:py-2 bg-white hover:bg-gray-50 active:bg-gray-100 transition-colors"
            @click="deptModalOpen = true"
          >
            <span class="whitespace-nowrap hidden sm:inline">Отделы</span>
            <span class="whitespace-nowrap sm:hidden">Отд.</span>
            <span v-if="(filterDepartments?.length || 0) > 0" class="text-xs font-semibold text-indigo-700 bg-indigo-50 border border-indigo-100 rounded px-1.5 py-0.5">
              {{ filterDepartments.length }}
            </span>
          </button>
          <button
            v-if="(filterDepartments?.length || 0) > 0"
            type="button"
            class="p-2.5 sm:p-2 border border-gray-300 rounded-lg text-gray-400 hover:text-gray-700 hover:bg-gray-50 active:bg-gray-100 transition-colors"
            title="Сбросить отделы"
            @click="clearDepartments"
          >
            <X :size="16" />
          </button>
        </div>

        <!-- Status filter -->
        <div class="flex items-stretch gap-1.5 shrink-0">
          <button
            type="button"
            class="inline-flex items-center gap-1.5 border border-gray-300 rounded-lg text-sm px-3 py-2.5 sm:py-2 bg-white hover:bg-gray-50 active:bg-gray-100 transition-colors"
            @click="statusModalOpen = true"
          >
            <span class="whitespace-nowrap hidden sm:inline">Статусы</span>
            <span class="whitespace-nowrap sm:hidden">Стат.</span>
            <span v-if="(filterStatus?.length || 0) > 0" class="text-xs font-semibold text-indigo-700 bg-indigo-50 border border-indigo-100 rounded px-1.5 py-0.5">
              {{ filterStatus.length }}
            </span>
          </button>
          <button
            v-if="(filterStatus?.length || 0) > 0"
            type="button"
            class="p-2.5 sm:p-2 border border-gray-300 rounded-lg text-gray-400 hover:text-gray-700 hover:bg-gray-50 active:bg-gray-100 transition-colors"
            title="Сбросить статусы"
            @click="clearStatuses"
          >
            <X :size="16" />
          </button>
        </div>

        <!-- Assignee filter -->
        <div v-if="auth.isStaff" class="flex items-stretch gap-1.5 shrink-0">
          <button
            type="button"
            class="inline-flex items-center gap-1.5 border border-gray-300 rounded-lg text-sm px-3 py-2.5 sm:py-2 bg-white hover:bg-gray-50 active:bg-gray-100 transition-colors"
            @click="assigneeModalOpen = true"
          >
            <Users :size="16" class="text-gray-400" />
            <span class="whitespace-nowrap hidden sm:inline">Ответственный</span>
            <span class="whitespace-nowrap sm:hidden">Исп.</span>
            <span v-if="(filterAssignees?.length || 0) > 0" class="text-xs font-semibold text-indigo-700 bg-indigo-50 border border-indigo-100 rounded px-1.5 py-0.5">
              {{ filterAssignees.length }}
            </span>
          </button>
          <button
            v-if="(filterAssignees?.length || 0) > 0"
            type="button"
            class="p-2.5 sm:p-2 border border-gray-300 rounded-lg text-gray-400 hover:text-gray-700 hover:bg-gray-50 active:bg-gray-100 transition-colors"
            title="Сбросить ответственных"
            @click="clearAssignees"
          >
            <X :size="16" />
          </button>
        </div>

        <!-- Client filter -->
        <div v-if="auth.isStaff" class="flex items-stretch gap-1.5 shrink-0">
          <button
            type="button"
            class="inline-flex items-center gap-1.5 border border-gray-300 rounded-lg text-sm px-3 py-2.5 sm:py-2 bg-white hover:bg-gray-50 active:bg-gray-100 transition-colors"
            @click="clientModalOpen = true"
          >
            <Building2 :size="16" class="text-gray-400" />
            <span class="whitespace-nowrap hidden sm:inline">Клиент</span>
            <span v-if="(filterClientNames?.length || 0) > 0" class="text-xs font-semibold text-gray-700 bg-gray-50 border border-gray-200 rounded px-1.5 py-0.5 max-w-[6rem] sm:max-w-[10rem] truncate">
              {{ filterClientNames.length }}
            </span>
          </button>
          <button
            v-if="(filterClientNames?.length || 0) > 0"
            type="button"
            class="p-2.5 sm:p-2 border border-gray-300 rounded-lg text-gray-400 hover:text-gray-700 hover:bg-gray-50 active:bg-gray-100 transition-colors"
            title="Сбросить клиентов"
            @click="clearClients"
          >
            <X :size="16" />
          </button>
        </div>

        <!-- Clear all filters -->
        <button
          v-if="searchQuery || (filterStatus?.length || 0) > 0 || (filterDepartments?.length || 0) > 0 || (filterAssignees?.length || 0) > 0 || (filterClientNames?.length || 0) > 0"
          type="button"
          class="inline-flex items-center gap-1.5 border border-gray-300 rounded-lg text-sm px-3 py-2.5 sm:py-2 bg-white hover:bg-gray-50 active:bg-gray-100 transition-colors shrink-0 text-gray-600"
          title="Очистить все фильтры"
          @click="clearAllFilters"
        >
          <Eraser :size="16" />
          <span class="whitespace-nowrap hidden sm:inline">Сбросить</span>
        </button>
      </div>
    </div>

    <!-- Stats Cards (today) -->
    <div class="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4">
      <div class="bg-white p-3 sm:p-4 rounded-lg shadow-sm border border-gray-200">
        <div class="text-xs sm:text-sm text-gray-500 mb-0.5">Заявок за сегодня</div>
        <div class="text-xl sm:text-2xl font-bold text-gray-900">{{ stats.total }}</div>
      </div>
      <div class="bg-white p-3 sm:p-4 rounded-lg shadow-sm border border-green-200">
        <div class="text-xs sm:text-sm text-gray-500 mb-0.5">Открыты сегодня</div>
        <div class="text-xl sm:text-2xl font-bold text-green-600">{{ stats.open }}</div>
      </div>
      <div class="bg-white p-3 sm:p-4 rounded-lg shadow-sm border border-yellow-200">
        <div class="text-xs sm:text-sm text-gray-500 mb-0.5">В работе сегодня</div>
        <div class="text-xl sm:text-2xl font-bold text-yellow-600">{{ stats.inProgress }}</div>
      </div>
      <div class="bg-white p-3 sm:p-4 rounded-lg shadow-sm border border-indigo-200">
        <div class="text-xs sm:text-sm text-gray-500 mb-0.5">Ремонты сегодня</div>
        <div class="text-xl sm:text-2xl font-bold text-indigo-600">{{ stats.repair }}</div>
      </div>
    </div>

    <!-- Skeleton Loading -->
    <div v-if="loading && tickets.length === 0" class="space-y-3">
      <div v-for="i in 6" :key="i" class="bg-white rounded-lg border border-gray-200 p-4 animate-pulse">
        <div class="flex items-start gap-3">
          <div class="w-12 h-4 bg-gray-200 rounded"></div>
          <div class="flex-1 space-y-2">
            <div class="h-4 bg-gray-200 rounded w-3/4"></div>
            <div class="h-3 bg-gray-100 rounded w-1/2"></div>
          </div>
          <div class="w-16 h-5 bg-gray-200 rounded-full"></div>
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <div v-else-if="filteredTickets.length === 0" class="bg-white rounded-lg shadow-sm border border-gray-200 text-center py-16">
      <Ticket :size="48" class="mx-auto mb-3 text-gray-300" />
      <p class="text-gray-900 font-medium">Заявок не найдено</p>
      <p class="text-sm text-gray-500 mt-1">Попробуйте изменить параметры поиска или фильтры</p>
    </div>

    <template v-else>
      <!-- Mobile Card List (< md) -->
      <div class="md:hidden space-y-2">
        <!-- Mobile sort -->
        <div class="flex items-center gap-2 px-1 pb-1 overflow-x-auto -mx-1">
          <span class="text-[10px] text-gray-400 uppercase font-bold tracking-wider shrink-0">Сорт:</span>
          <button
            v-for="sk in [
              { key: 'date', label: 'Дата' },
              { key: 'priority', label: 'Приоритет' },
              { key: 'status', label: 'Статус' },
              { key: 'id', label: 'ID' },
            ]"
            :key="sk.key"
            type="button"
            class="text-[11px] font-semibold px-2.5 py-1.5 rounded-full border shrink-0 transition-colors"
            :class="sortKey === sk.key ? 'bg-indigo-50 text-indigo-700 border-indigo-200' : 'bg-white text-gray-500 border-gray-200 active:bg-gray-50'"
            @click="toggleSort(sk.key as any)"
          >
            {{ sk.label }}
            <template v-if="sortKey === sk.key">{{ sortOrder === 'asc' ? '↑' : '↓' }}</template>
          </button>
        </div>

        <div
          v-for="t in paginatedTickets"
          :key="t.id"
          @click="openTicket(t.id)"
          :class="['bg-white rounded-xl border p-3.5 active:bg-gray-50 transition-colors cursor-pointer', t.priority === 'Критический' ? 'border-red-200 bg-red-50/30' : t.priority === 'Высокий' ? 'border-yellow-200/80' : 'border-gray-200']"
        >
          <div class="flex items-start justify-between gap-2 mb-2">
            <div class="flex items-center gap-2 min-w-0">
              <span class="text-xs font-mono text-gray-400 shrink-0">#{{ t.id }}</span>
              <span v-if="t.isRepair" class="text-indigo-500 shrink-0" title="Ремонт">🔧</span>
              <span :class="['inline-flex items-center px-2 py-0.5 rounded text-[10px] font-semibold border shrink-0', getStatusColor(t.status)]">
                {{ t.status }}
              </span>
            </div>
            <span :class="['inline-flex items-center px-2 py-0.5 rounded text-[10px] font-semibold border shrink-0', getPriorityBadgeClass(t.priority)]">
              {{ t.priority }}
            </span>
          </div>

          <div class="flex items-center gap-2 mb-1.5">
            <span v-if="t.hasUnread" class="w-2 h-2 rounded-full bg-blue-500 shrink-0" title="Непрочитанное" />
            <div :class="['text-sm font-semibold leading-snug', t.priority === 'Критический' ? 'text-red-700' : 'text-gray-900']">
              {{ t.alternativeTitle || t.title }}
            </div>
            <span
              v-if="t.okdeskId"
              class="shrink-0 inline-flex items-center rounded bg-blue-50 px-1.5 py-0.5 text-[10px] font-medium text-blue-700 ring-1 ring-inset ring-blue-700/10"
            >
              Okdesk
            </span>
          </div>

          <div class="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-gray-500">
            <span v-if="auth.isStaff && t.clientName" class="flex items-center gap-1">
              <Building2 :size="12" class="text-gray-400" /> {{ t.clientName }}
            </span>
            <span v-if="auth.isStaff && displayTicketAssignees(t) !== '—'" class="truncate max-w-[140px]">
              {{ displayTicketAssignees(t) }}
            </span>
            <span class="font-mono text-gray-400">{{ formatDate(t.createdAt).split(',')[0] }}</span>
          </div>

          <div v-if="t.requestType || t.subtaskCount > 0" class="flex items-center gap-2 mt-2">
            <span v-if="t.requestType" class="text-[9px] uppercase font-bold tracking-tight text-gray-400 bg-gray-50 px-1 border border-gray-100 rounded">{{ t.requestType }}</span>
            <span v-if="t.subtaskCount > 0" class="text-[9px] inline-flex items-center gap-0.5 text-indigo-600 bg-indigo-50 px-1.5 py-0.5 rounded font-bold">
              {{ t.subtaskCount }} {{ pluralizeSubtask(t.subtaskCount) }}
            </span>
          </div>
        </div>
      </div>

      <!-- Desktop Table (>= md) -->
      <div class="hidden md:block bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
        <div class="overflow-x-auto">
          <table class="min-w-full divide-y divide-gray-200">
            <thead class="bg-gray-50">
              <tr>
                <th @click="toggleSort('id')" class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100 select-none w-20">
                  <div class="flex items-center gap-1">ID <component :is="sortOrder==='asc'?ArrowUp:ArrowDown" v-if="sortKey==='id'" :size="12" /></div>
                </th>
                <th @click="toggleSort('title')" class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100 select-none">
                  <div class="flex items-center gap-1">Тема <component :is="sortOrder==='asc'?ArrowUp:ArrowDown" v-if="sortKey==='title'" :size="12" /></div>
                </th>
                <th v-if="auth.isStaff" @click="toggleSort('client')" class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100 select-none">
                  <div class="flex items-center gap-1">Клиент <component :is="sortOrder==='asc'?ArrowUp:ArrowDown" v-if="sortKey==='client'" :size="12" /></div>
                </th>
                <th @click="toggleSort('status')" class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100 select-none">
                  <div class="flex items-center gap-1">Статус <component :is="sortOrder==='asc'?ArrowUp:ArrowDown" v-if="sortKey==='status'" :size="12" /></div>
                </th>
                <th @click="toggleSort('priority')" class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100 select-none">
                  <div class="flex items-center gap-1">Приоритет <component :is="sortOrder==='asc'?ArrowUp:ArrowDown" v-if="sortKey==='priority'" :size="12" /></div>
                </th>
                <th v-if="auth.isStaff" class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Отдел</th>
                <th v-if="auth.isStaff" @click="toggleSort('assignee')" class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100 select-none">
                  <div class="flex items-center gap-1">Исполнитель <component :is="sortOrder==='asc'?ArrowUp:ArrowDown" v-if="sortKey==='assignee'" :size="12" /></div>
                </th>
                <th @click="toggleSort('date')" class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100 select-none w-36">
                  <div class="flex items-center gap-1">Дата <component :is="sortOrder==='asc'?ArrowUp:ArrowDown" v-if="sortKey==='date'" :size="12" /></div>
                </th>
              </tr>
            </thead>
            <tbody class="bg-white divide-y divide-gray-200">
              <tr
                v-for="t in paginatedTickets"
                :key="t.id"
                @click="openTicket(t.id)"
                :class="['cursor-pointer transition-colors', getPriorityRowClass(t.priority)]"
              >
                <td class="px-4 py-3 whitespace-nowrap text-sm text-gray-500 font-mono">
                  #{{ t.id }}
                  <span v-if="t.isRepair" class="ml-1 text-indigo-500" title="Ремонт">🔧</span>
                </td>
                <td class="px-4 py-3 text-sm">
                  <div class="flex items-center gap-2">
                    <span v-if="t.hasUnread" class="w-2 h-2 rounded-full bg-blue-500 shrink-0" title="Непрочитанное" />
                    <div :class="['font-medium break-words', t.priority === 'Критический' ? 'text-red-700' : 'text-gray-900']">
                      {{ t.title }}
                    </div>
                    <span
                      v-if="t.okdeskId"
                      class="shrink-0 inline-flex items-center rounded bg-blue-50 px-1.5 py-0.5 text-[10px] font-medium text-blue-700 ring-1 ring-inset ring-blue-700/10"
                    >
                      Okdesk
                    </span>
                  </div>
                  <div v-if="t.alternativeTitle" class="mt-1 text-[12px] text-gray-500 break-words">
                    <span class="font-semibold">Альт:</span> {{ t.alternativeTitle }}
                  </div>
                  <div class="flex items-center gap-2 mt-1">
                    <span v-if="t.requestType" class="text-[10px] uppercase font-bold tracking-tight text-gray-400 bg-gray-50 px-1 border border-gray-100 rounded">{{ t.requestType }}</span>
                    <span v-if="t.subtaskCount > 0" class="text-[10px] inline-flex items-center gap-1 text-indigo-600 bg-indigo-50 px-1.5 py-0.5 rounded font-bold uppercase tracking-tighter">
                      {{ t.subtaskCount }} {{ pluralizeSubtask(t.subtaskCount) }}
                    </span>
                  </div>
                </td>
                <td v-if="auth.isStaff" class="px-4 py-3 text-sm text-gray-500">
                  {{ t.clientName || '—' }}
                </td>
                <td class="px-4 py-3 whitespace-nowrap">
                  <span :class="['inline-flex items-center px-2 py-0.5 rounded text-xs font-medium border', getStatusColor(t.status)]">
                    {{ t.status }}
                  </span>
                </td>
                <td class="px-4 py-3 whitespace-nowrap">
                  <span :class="['inline-flex items-center px-2 py-0.5 rounded text-xs font-medium border', getPriorityBadgeClass(t.priority)]">
                    {{ t.priority }}
                  </span>
                </td>
                <td v-if="auth.isStaff" class="px-4 py-3 whitespace-nowrap text-sm text-gray-500">
                  {{ t.department || '—' }}
                </td>
                <td v-if="auth.isStaff" class="px-4 py-3 text-sm text-gray-500 max-w-[12rem] truncate">
                  {{ displayTicketAssignees(t) }}
                </td>
                <td class="px-4 py-3 whitespace-nowrap text-sm text-gray-500 font-mono">
                  {{ formatDate(t.createdAt) }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Pagination -->
      <div v-if="totalPages > 1" class="flex items-center justify-between px-3 sm:px-4 py-3 bg-white rounded-lg border border-gray-200 shadow-sm">
        <div class="text-xs text-gray-500">
          <span class="hidden sm:inline">Страница </span><span class="font-semibold text-gray-900">{{ page }}</span> из <span class="font-semibold text-gray-900">{{ totalPages }}</span>
          <span class="hidden sm:inline text-gray-400 ml-1">({{ totalCount }} заявок)</span>
        </div>
        <div class="flex items-center gap-1.5">
          <button
            type="button"
            class="px-3 py-2 text-xs font-semibold border border-gray-200 rounded-lg hover:bg-gray-50 active:bg-gray-100 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            :disabled="page <= 1"
            @click="page = 1"
          >«</button>
          <button
            type="button"
            class="px-4 py-2 text-xs font-semibold border border-gray-200 rounded-lg hover:bg-gray-50 active:bg-gray-100 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            :disabled="page <= 1"
            @click="page = Math.max(1, page - 1)"
          >← Назад</button>
          <span class="px-3 py-2 text-xs font-bold text-indigo-700 bg-indigo-50 border border-indigo-200 rounded-lg">{{ page }}</span>
          <button
            type="button"
            class="px-4 py-2 text-xs font-semibold border border-gray-200 rounded-lg hover:bg-gray-50 active:bg-gray-100 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            :disabled="page >= totalPages"
            @click="page = Math.min(totalPages, page + 1)"
          >Далее →</button>
          <button
            type="button"
            class="px-3 py-2 text-xs font-semibold border border-gray-200 rounded-lg hover:bg-gray-50 active:bg-gray-100 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            :disabled="page >= totalPages"
            @click="page = totalPages"
          >»</button>
        </div>
      </div>
    </template>

    <!-- Status Modal -->
    <Teleport to="body">
      <div v-if="statusModalOpen" class="fixed inset-0 z-50 bg-black/40 backdrop-blur-sm flex items-center justify-center p-4" @click.self="statusModalOpen = false">
        <div class="bg-white w-full max-w-lg rounded-xl shadow-modal border border-gray-200 overflow-hidden">
          <div class="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
            <div class="font-semibold text-gray-900 text-sm">Статусы</div>
            <button class="p-2 text-gray-400 hover:text-gray-700" @click="statusModalOpen = false"><X :size="18" /></button>
          </div>
          <div class="p-4 space-y-3">
            <div class="relative">
              <Search class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" :size="16" />
              <input v-model="statusSearch" class="w-full pl-9 pr-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" placeholder="Поиск по статусам..." />
            </div>
            <div class="max-h-72 overflow-y-auto divide-y divide-gray-100 border border-gray-100 rounded-lg">
              <label v-for="s in filteredStatusesForModal" :key="s.name" class="flex items-center gap-3 px-3 py-2.5 hover:bg-gray-50 cursor-pointer">
                <input type="checkbox" class="h-4 w-4" :checked="normalizedStatusSet.has((s.name||'').trim())" @change="toggleStatus(s.name)" />
                <div class="min-w-0 flex-1">
                  <div class="text-sm font-medium text-gray-900 truncate">{{ s.name }}</div>
                </div>
              </label>
              <div v-if="filteredStatusesForModal.length === 0" class="px-4 py-6 text-center text-sm text-gray-400">Ничего не найдено</div>
            </div>
          </div>
          <div class="px-4 py-3 border-t border-gray-100 bg-gray-50 flex items-center justify-between">
            <button class="text-xs font-medium text-gray-500 hover:text-gray-900" @click="clearStatuses">Сбросить</button>
            <button class="px-4 py-2 text-xs font-semibold bg-indigo-600 text-white rounded-md hover:bg-indigo-700" @click="statusModalOpen = false">Готово</button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Assignee Modal -->
    <Teleport to="body">
      <div v-if="assigneeModalOpen" class="fixed inset-0 z-50 bg-black/40 backdrop-blur-sm flex items-center justify-center p-4" @click.self="assigneeModalOpen = false">
        <div class="bg-white w-full max-w-lg rounded-xl shadow-modal border border-gray-200 overflow-hidden">
          <div class="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
            <div class="font-semibold text-gray-900 text-sm">Ответственные</div>
            <button class="p-2 text-gray-400 hover:text-gray-700" @click="assigneeModalOpen = false"><X :size="18" /></button>
          </div>
          <div class="p-4 space-y-3">
            <div class="relative">
              <Search class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" :size="16" />
              <input v-model="assigneeSearch" class="w-full pl-9 pr-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" placeholder="Поиск по сотрудникам..." />
            </div>
            <div class="max-h-72 overflow-y-auto divide-y divide-gray-100 border border-gray-100 rounded-lg">
              <label v-for="e in filteredEmployeesForModal" :key="e.userId" class="flex items-center gap-3 px-3 py-2.5 hover:bg-gray-50 cursor-pointer">
                <input type="checkbox" class="h-4 w-4" :checked="normalizedAssigneeSet.has((e.fullName||'').trim())" @change="toggleAssignee(e.fullName)" />
                <div class="min-w-0 flex-1">
                  <div class="text-sm font-medium text-gray-900 truncate">{{ e.fullName }}</div>
                  <div class="text-xs text-gray-400 truncate">{{ e.role }} · {{ e.userId }}</div>
                </div>
              </label>
              <div v-if="filteredEmployeesForModal.length === 0" class="px-4 py-6 text-center text-sm text-gray-400">Ничего не найдено</div>
            </div>
          </div>
          <div class="px-4 py-3 border-t border-gray-100 bg-gray-50 flex items-center justify-between">
            <button class="text-xs font-medium text-gray-500 hover:text-gray-900" @click="clearAssignees">Сбросить</button>
            <button class="px-4 py-2 text-xs font-semibold bg-indigo-600 text-white rounded-md hover:bg-indigo-700" @click="assigneeModalOpen = false">Готово</button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Client Modal -->
    <Teleport to="body">
      <div v-if="clientModalOpen" class="fixed inset-0 z-50 bg-black/40 backdrop-blur-sm flex items-center justify-center p-4" @click.self="clientModalOpen = false">
        <div class="bg-white w-full max-w-lg rounded-xl shadow-modal border border-gray-200 overflow-hidden">
          <div class="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
            <div class="font-semibold text-gray-900 text-sm">Клиенты</div>
            <button class="p-2 text-gray-400 hover:text-gray-700" @click="clientModalOpen = false"><X :size="18" /></button>
          </div>
          <div class="p-4 space-y-3">
            <div class="relative">
              <Search class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" :size="16" />
              <input v-model="clientSearch" class="w-full pl-9 pr-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" placeholder="Поиск по клиентам..." />
            </div>
            <div class="max-h-72 overflow-y-auto divide-y divide-gray-100 border border-gray-100 rounded-lg">
              <label v-for="c in filteredClientsForModal" :key="c.id ?? c.label" class="flex items-center gap-3 px-3 py-2.5 hover:bg-gray-50 cursor-pointer">
                <input type="checkbox" class="h-4 w-4" :checked="normalizedClientSet.has((c.label||'').trim())" @change="toggleClient(c.label)" />
                <div class="min-w-0 flex-1">
                  <div class="text-sm font-medium text-gray-900 truncate">{{ c.label }}</div>
                  <div v-if="c.id != null" class="text-xs text-gray-400">ID: {{ c.id }}</div>
                </div>
              </label>
              <div v-if="filteredClientsForModal.length === 0" class="px-4 py-6 text-center text-sm text-gray-400">Ничего не найдено</div>
            </div>
          </div>
          <div class="px-4 py-3 border-t border-gray-100 bg-gray-50 flex items-center justify-between">
            <button class="text-xs font-medium text-gray-500 hover:text-gray-900" @click="clearClients">Сбросить</button>
            <button class="px-4 py-2 text-xs font-semibold bg-indigo-600 text-white rounded-md hover:bg-indigo-700" @click="clientModalOpen = false">Готово</button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Department Modal -->
    <Teleport to="body">
      <div v-if="deptModalOpen" class="fixed inset-0 z-50 bg-black/40 backdrop-blur-sm flex items-center justify-center p-4" @click.self="deptModalOpen = false">
        <div class="bg-white w-full max-w-lg rounded-xl shadow-modal border border-gray-200 overflow-hidden">
          <div class="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
            <div class="font-semibold text-gray-900 text-sm">Отделы</div>
            <button class="p-2 text-gray-400 hover:text-gray-700" @click="deptModalOpen = false"><X :size="18" /></button>
          </div>
          <div class="p-4 space-y-3">
            <div class="relative">
              <Search class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" :size="16" />
              <input v-model="deptSearch" class="w-full pl-9 pr-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" placeholder="Поиск по отделам..." />
            </div>
            <div class="max-h-72 overflow-y-auto divide-y divide-gray-100 border border-gray-100 rounded-lg">
              <label v-for="d in filteredDepartmentsForModal" :key="d" class="flex items-center gap-3 px-3 py-2.5 hover:bg-gray-50 cursor-pointer">
                <input type="checkbox" class="h-4 w-4" :checked="normalizedDeptSet.has(d)" @change="toggleDepartment(d)" />
                <div class="min-w-0 flex-1">
                  <div class="text-sm font-medium text-gray-900 truncate">{{ d }}</div>
                </div>
              </label>
              <div v-if="filteredDepartmentsForModal.length === 0" class="px-4 py-6 text-center text-sm text-gray-400">Ничего не найдено</div>
            </div>
          </div>
          <div class="px-4 py-3 border-t border-gray-100 bg-gray-50 flex items-center justify-between">
            <button class="text-xs font-medium text-gray-500 hover:text-gray-900" @click="clearDepartments">Сбросить</button>
            <button class="px-4 py-2 text-xs font-semibold bg-indigo-600 text-white rounded-md hover:bg-indigo-700" @click="deptModalOpen = false">Готово</button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
