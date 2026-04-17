<script setup lang="ts">
import { BarChart3, Download, Calendar, Filter, RefreshCw, ChevronRight, TrendingUp, Wallet, Activity, Ticket, Clock, CheckCircle2, AlertCircle } from 'lucide-vue-next'
import type { RepairReportResponse } from '~/types'

definePageMeta({ middleware: ['staff-not-field-engineer'] })

const api = useApi()
const toast = useToast()

const activeTab = ref<'repairs' | 'tickets'>('repairs')

// ─── Repair report ───
const report = ref<RepairReportResponse | null>(null)
const loading = ref(false)
const selectedMonth = ref('')

const months = [
  { value: '2026-04', label: 'Апрель 2026' },
  { value: '2026-03', label: 'Март 2026' },
  { value: '2026-02', label: 'Февраль 2026' },
  { value: '2026-01', label: 'Январь 2026' },
]

async function loadReport() {
  loading.value = true
  try {
    report.value = await api.reports.getRepairs({
      month: selectedMonth.value || undefined,
    })
  } catch {
    toast.error('Не удалось загрузить отчёт')
  } finally {
    loading.value = false
  }
}

function exportCsv() {
  if (!report.value?.items?.length) {
    toast.warning('Нет данных для экспорта')
    return
  }
  const header = 'ID;Клиент;Оборудование;Серийный номер;Тип ремонта;Статус;Стоимость'
  const rows = report.value.items.map(i =>
    [i.ticketId, i.clientName, i.equipmentName || '', i.serialNumber || '', i.repairType || '', i.status, i.repairCost ?? ''].join(';')
  )
  const bom = '\uFEFF'
  const blob = new Blob([bom + header + '\n' + rows.join('\n')], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `report-repairs-${selectedMonth.value || 'all'}.csv`
  a.click()
  URL.revokeObjectURL(url)
  toast.success('Отчёт экспортирован')
}

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('ru-RU', {
    style: 'currency',
    currency: 'BYN',
    minimumFractionDigits: 2,
  }).format(amount)
}

// ─── Tickets report (all-time) ───
const allTickets = ref<any[]>([])
const ticketsLoading = ref(false)

async function loadTicketsReport() {
  ticketsLoading.value = true
  try {
    allTickets.value = await api.tickets.getAll()
  } catch {
    toast.error('Не удалось загрузить заявки')
  } finally {
    ticketsLoading.value = false
  }
}

const ticketStats = computed(() => {
  const t = allTickets.value
  const total = t.length
  const open = t.filter(x => x.status === 'Открыт' || x.status === 'В работе').length
  const closed = t.filter(x => x.status === 'Закрыт' || x.status === 'Решён').length
  const avgPerDay = (() => {
    if (!t.length) return 0
    const dates = t.map(x => new Date(x.createdAt).toDateString())
    const unique = new Set(dates)
    return Math.round(t.length / Math.max(unique.size, 1))
  })()

  const byStatus: Record<string, number> = {}
  const byPriority: Record<string, number> = {}
  const byDepartment: Record<string, number> = {}
  const byMonth: Record<string, number> = {}

  for (const tk of t) {
    byStatus[tk.status] = (byStatus[tk.status] || 0) + 1
    if (tk.priority) byPriority[tk.priority] = (byPriority[tk.priority] || 0) + 1
    if (tk.department) byDepartment[tk.department] = (byDepartment[tk.department] || 0) + 1
    const m = tk.createdAt ? tk.createdAt.substring(0, 7) : 'unknown'
    byMonth[m] = (byMonth[m] || 0) + 1
  }

  return { total, open, closed, avgPerDay, byStatus, byPriority, byDepartment, byMonth }
})

const sortedByStatus = computed(() =>
  Object.entries(ticketStats.value.byStatus)
    .sort((a, b) => b[1] - a[1])
)
const sortedByPriority = computed(() =>
  Object.entries(ticketStats.value.byPriority)
    .sort((a, b) => b[1] - a[1])
)
const sortedByDepartment = computed(() =>
  Object.entries(ticketStats.value.byDepartment)
    .sort((a, b) => b[1] - a[1])
    .slice(0, 10)
)
const sortedByMonth = computed(() =>
  Object.entries(ticketStats.value.byMonth)
    .sort((a, b) => b[0].localeCompare(a[0]))
    .slice(0, 12)
)

function exportTicketsCsv() {
  if (!allTickets.value.length) {
    toast.warning('Нет данных для экспорта')
    return
  }
  const header = 'ID;Дата;Название;Статус;Приоритет;Отдел;Клиент;Объект;Тип обращения'
  const rows = allTickets.value.map(t =>
    [t.id, t.createdAt, `"${(t.title || '').replace(/"/g, '""')}"`, t.status, t.priority, t.department || '', t.clientName || '', t.objectName || '', t.requestType || ''].join(';')
  )
  const bom = '\uFEFF'
  const blob = new Blob([bom + header + '\n' + rows.join('\n')], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = 'report-tickets-all.csv'
  a.click()
  URL.revokeObjectURL(url)
  toast.success('Отчёт экспортирован')
}

function monthLabel(m: string): string {
  const MONTHS_RU: Record<string, string> = {
    '01': 'Янв', '02': 'Фев', '03': 'Мар', '04': 'Апр',
    '05': 'Май', '06': 'Июн', '07': 'Июл', '08': 'Авг',
    '09': 'Сен', '10': 'Окт', '11': 'Ноя', '12': 'Дек',
  }
  const [y, mo] = m.split('-')
  return `${MONTHS_RU[mo] || mo} ${y}`
}

function switchTab(tab: 'repairs' | 'tickets') {
  activeTab.value = tab
  if (tab === 'tickets' && !allTickets.value.length && !ticketsLoading.value) {
    loadTicketsReport()
  }
}

onMounted(() => {
  selectedMonth.value = '2026-04'
  loadReport()
})
</script>

<template>
  <div class="space-y-6 w-full">
    <!-- Header -->
    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <p class="text-sm text-gray-500">Детальный обзор работ и статистики</p>
      <div class="flex items-center gap-2">
        <template v-if="activeTab === 'repairs'">
          <div class="relative">
            <Calendar class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" :size="16" />
            <select v-model="selectedMonth" class="pl-9 pr-8 py-2 bg-white border border-gray-200 rounded-lg text-sm font-medium text-gray-700 focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500 appearance-none cursor-pointer" @change="loadReport">
              <option v-for="m in months" :key="m.value" :value="m.value">{{ m.label }}</option>
            </select>
          </div>
          <button @click="exportCsv" class="inline-flex items-center gap-2 bg-white border border-gray-200 text-gray-700 hover:bg-gray-50 px-4 py-2 rounded-lg text-sm font-medium transition-colors shadow-sm">
            <Download :size="16" /> Экспорт
          </button>
        </template>
        <template v-else>
          <button @click="exportTicketsCsv" class="inline-flex items-center gap-2 bg-white border border-gray-200 text-gray-700 hover:bg-gray-50 px-4 py-2 rounded-lg text-sm font-medium transition-colors shadow-sm">
            <Download :size="16" /> Экспорт CSV
          </button>
        </template>
      </div>
    </div>

    <!-- Tabs -->
    <div class="flex items-center gap-1 bg-gray-100 p-1 rounded-lg w-fit">
      <button
        :class="['px-4 py-2 rounded-md text-sm font-semibold transition-all', activeTab === 'repairs' ? 'bg-white shadow-sm text-gray-900' : 'text-gray-500 hover:text-gray-700']"
        @click="switchTab('repairs')"
      >Ремонтные работы</button>
      <button
        :class="['px-4 py-2 rounded-md text-sm font-semibold transition-all', activeTab === 'tickets' ? 'bg-white shadow-sm text-gray-900' : 'text-gray-500 hover:text-gray-700']"
        @click="switchTab('tickets')"
      >Заявки (все время)</button>
    </div>

    <!-- ═══════ TAB 1: Repairs ═══════ -->
    <template v-if="activeTab === 'repairs'">
      <div v-if="loading" class="flex items-center justify-center py-24">
        <RefreshCw :size="32" class="animate-spin text-indigo-600" />
      </div>

      <template v-else-if="report">
        <!-- Stats Cards -->
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div class="bg-white p-5 rounded-xl border border-gray-200 shadow-sm relative overflow-hidden group">
            <div class="absolute right-0 top-0 p-4 opacity-[0.03] group-hover:opacity-[0.08] transition-opacity">
              <Activity :size="80" class="text-indigo-600" />
            </div>
            <div class="flex items-center gap-4 relative z-10">
              <div class="w-12 h-12 rounded-xl bg-indigo-50 flex items-center justify-center text-indigo-600">
                <BarChart3 :size="24" />
              </div>
              <div>
                <div class="text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-1">Всего заявок</div>
                <div class="text-2xl font-bold text-gray-900">{{ report.summary.totalCount }}</div>
              </div>
            </div>
          </div>
          
          <div class="bg-white p-5 rounded-xl border border-gray-200 shadow-sm relative overflow-hidden group">
            <div class="absolute right-0 top-0 p-4 opacity-[0.03] group-hover:opacity-[0.08] transition-opacity">
              <Wallet :size="80" class="text-green-600" />
            </div>
            <div class="flex items-center gap-4 relative z-10">
              <div class="w-12 h-12 rounded-xl bg-green-50 flex items-center justify-center text-green-600">
                <TrendingUp :size="24" />
              </div>
              <div>
                <div class="text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-1">Общая стоимость</div>
                <div class="text-2xl font-bold text-gray-900">{{ formatCurrency(report.summary.totalCost) }}</div>
              </div>
            </div>
          </div>
          
          <div class="bg-white p-5 rounded-xl border border-gray-200 shadow-sm relative overflow-hidden group">
            <div class="absolute right-0 top-0 p-4 opacity-[0.03] group-hover:opacity-[0.08] transition-opacity">
              <Activity :size="80" class="text-orange-600" />
            </div>
            <div class="flex items-center gap-4 relative z-10">
              <div class="w-12 h-12 rounded-xl bg-orange-50 flex items-center justify-center text-orange-600">
                <Calendar :size="24" />
              </div>
              <div>
                <div class="text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-1">Средний чек</div>
                <div class="text-2xl font-bold text-gray-900">
                  {{ formatCurrency(report.summary.totalCount ? report.summary.totalCost / report.summary.totalCount : 0) }}
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Analysis Section -->
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <div class="bg-white rounded-xl border border-gray-200 shadow-sm flex flex-col min-h-[220px]">
            <div class="px-5 py-4 border-b border-gray-50 flex items-center justify-between">
              <h3 class="text-sm font-bold text-gray-900">Топ-5 по клиентам</h3>
              <span class="text-[10px] bg-gray-100 text-gray-500 px-2 py-0.5 rounded-full font-bold uppercase tracking-tighter">BYN</span>
            </div>
            <div class="p-5 space-y-4 flex-1">
              <div v-for="item in report.summary.byClient.slice(0, 5)" :key="item.key" class="flex items-center justify-between group">
                <div class="flex flex-col">
                  <span class="text-sm font-semibold text-gray-700 group-hover:text-indigo-600 transition-colors">{{ item.key }}</span>
                  <span class="text-[10px] text-gray-400 uppercase font-bold tracking-tight">{{ item.count }} шт.</span>
                </div>
                <span class="text-sm font-bold text-gray-900 bg-gray-50 px-3 py-1 rounded-lg border border-gray-100">{{ formatCurrency(item.sum) }}</span>
              </div>
              <div v-if="!report.summary.byClient.length" class="flex items-center justify-center flex-1 text-sm text-gray-400 py-8">Нет данных</div>
            </div>
          </div>

          <div class="bg-white rounded-xl border border-gray-200 shadow-sm flex flex-col min-h-[220px]">
            <div class="px-5 py-4 border-b border-gray-50 flex items-center justify-between">
              <h3 class="text-sm font-bold text-gray-900">По типу оборудования</h3>
              <span class="text-[10px] bg-gray-100 text-gray-500 px-2 py-0.5 rounded-full font-bold uppercase tracking-tighter">COUNT</span>
            </div>
            <div class="p-5 space-y-4 flex-1">
              <div v-for="item in report.summary.byEquipmentType.slice(0, 5)" :key="item.key" class="flex items-center justify-between group">
                <div class="flex flex-col">
                  <span class="text-sm font-semibold text-gray-700 group-hover:text-indigo-600 transition-colors">{{ item.key }}</span>
                  <span class="text-[10px] text-gray-400 uppercase font-bold tracking-tight">{{ formatCurrency(item.sum) }}</span>
                </div>
                <span class="text-sm font-bold text-indigo-600 bg-indigo-50 px-3 py-1 rounded-lg border border-indigo-100">{{ item.count }} шт.</span>
              </div>
              <div v-if="!report.summary.byEquipmentType.length" class="flex items-center justify-center flex-1 text-sm text-gray-400 py-8">Нет данных</div>
            </div>
          </div>
        </div>

        <!-- Detailed Table -->
        <div class="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden min-h-[200px]">
          <div class="px-5 py-4 border-b border-gray-100 bg-gray-50/30 flex items-center justify-between">
            <h3 class="text-sm font-bold text-gray-900">Детализация ремонтных работ</h3>
            <Filter :size="14" class="text-gray-400" />
          </div>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-sm">
              <thead>
                <tr class="bg-gray-50/50 border-b border-gray-100">
                  <th class="px-5 py-3 text-[11px] font-bold text-gray-400 uppercase tracking-widest">ID</th>
                  <th class="px-5 py-3 text-[11px] font-bold text-gray-400 uppercase tracking-widest">Клиент</th>
                  <th class="px-5 py-3 text-[11px] font-bold text-gray-400 uppercase tracking-widest">Оборудование</th>
                  <th class="px-5 py-3 text-[11px] font-bold text-gray-400 uppercase tracking-widest text-center">Тип</th>
                  <th class="px-5 py-3 text-[11px] font-bold text-gray-400 uppercase tracking-widest text-center">Статус</th>
                  <th class="px-5 py-3 text-[11px] font-bold text-gray-400 uppercase tracking-widest text-right">Стоимость</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-50">
                <tr v-for="item in report.items" :key="item.ticketId" class="hover:bg-gray-50/50 transition-colors group">
                  <td class="px-5 py-4"><span class="font-mono text-gray-400 text-xs">#{{ item.ticketId }}</span></td>
                  <td class="px-5 py-4"><span class="font-semibold text-gray-900">{{ item.clientName }}</span></td>
                  <td class="px-5 py-4">
                    <div class="flex flex-col">
                      <span class="text-gray-900 font-medium line-clamp-1">{{ item.equipmentName || '—' }}</span>
                      <span class="text-[10px] text-gray-400 font-mono tracking-tighter uppercase">{{ item.serialNumber || 'SN: —' }}</span>
                    </div>
                  </td>
                  <td class="px-5 py-4 text-center">
                    <span class="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider bg-gray-100 text-gray-500 border border-gray-200">{{ item.repairType || 'Standard' }}</span>
                  </td>
                  <td class="px-5 py-4 text-center">
                    <span class="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider bg-indigo-50 text-indigo-700 border border-indigo-100">{{ item.status }}</span>
                  </td>
                  <td class="px-5 py-4 text-right">
                    <span class="font-bold text-gray-900">{{ item.repairCost ? formatCurrency(item.repairCost) : '—' }}</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <div class="px-5 py-3 bg-gray-50/50 border-t border-gray-50 flex justify-end">
            <div class="flex items-center gap-4 text-xs">
              <span class="text-gray-400 uppercase font-bold tracking-widest">Итого:</span>
              <span class="font-extrabold text-gray-900">{{ formatCurrency(report.summary.totalCost) }}</span>
            </div>
          </div>
        </div>
      </template>
      
      <div v-else-if="!loading && !report" class="text-center py-24 bg-white rounded-xl border border-gray-200">
        <div class="w-16 h-16 bg-gray-50 rounded-full flex items-center justify-center mx-auto mb-4">
          <BarChart3 :size="32" class="text-gray-300" />
        </div>
        <h3 class="text-lg font-semibold text-gray-900 mb-1">Нет данных для отчёта</h3>
        <p class="text-sm text-gray-500">За указанный период ремонтные работы не проводились.</p>
      </div>
    </template>

    <!-- ═══════ TAB 2: All Tickets ═══════ -->
    <template v-if="activeTab === 'tickets'">
      <div v-if="ticketsLoading" class="flex items-center justify-center py-24">
        <RefreshCw :size="32" class="animate-spin text-indigo-600" />
      </div>

      <template v-else-if="allTickets.length">
        <!-- Stats Cards -->
        <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div class="bg-white p-5 rounded-xl border border-gray-200 shadow-sm">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-lg bg-indigo-50 flex items-center justify-center text-indigo-600"><Ticket :size="20" /></div>
              <div>
                <div class="text-[10px] font-bold text-gray-400 uppercase tracking-widest">Всего</div>
                <div class="text-xl font-bold text-gray-900">{{ ticketStats.total }}</div>
              </div>
            </div>
          </div>
          <div class="bg-white p-5 rounded-xl border border-gray-200 shadow-sm">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-lg bg-amber-50 flex items-center justify-center text-amber-600"><Clock :size="20" /></div>
              <div>
                <div class="text-[10px] font-bold text-gray-400 uppercase tracking-widest">Открытые</div>
                <div class="text-xl font-bold text-gray-900">{{ ticketStats.open }}</div>
              </div>
            </div>
          </div>
          <div class="bg-white p-5 rounded-xl border border-gray-200 shadow-sm">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-lg bg-green-50 flex items-center justify-center text-green-600"><CheckCircle2 :size="20" /></div>
              <div>
                <div class="text-[10px] font-bold text-gray-400 uppercase tracking-widest">Закрытые</div>
                <div class="text-xl font-bold text-gray-900">{{ ticketStats.closed }}</div>
              </div>
            </div>
          </div>
          <div class="bg-white p-5 rounded-xl border border-gray-200 shadow-sm">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-lg bg-purple-50 flex items-center justify-center text-purple-600"><Activity :size="20" /></div>
              <div>
                <div class="text-[10px] font-bold text-gray-400 uppercase tracking-widest">Сред./день</div>
                <div class="text-xl font-bold text-gray-900">{{ ticketStats.avgPerDay }}</div>
              </div>
            </div>
          </div>
        </div>

        <!-- Breakdown -->
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          <!-- By Status -->
          <div class="bg-white rounded-xl border border-gray-200 shadow-sm flex flex-col min-h-[220px]">
            <div class="px-5 py-4 border-b border-gray-50">
              <h3 class="text-sm font-bold text-gray-900">По статусу</h3>
            </div>
            <div class="p-5 space-y-3 flex-1">
              <div v-for="[status, count] in sortedByStatus" :key="status" class="flex items-center justify-between">
                <span class="text-sm text-gray-700 font-medium">{{ status }}</span>
                <div class="flex items-center gap-2">
                  <div class="w-24 h-2 bg-gray-100 rounded-full overflow-hidden">
                    <div class="h-full bg-indigo-500 rounded-full" :style="{ width: `${(count / ticketStats.total) * 100}%` }"></div>
                  </div>
                  <span class="text-xs font-bold text-gray-900 w-8 text-right">{{ count }}</span>
                </div>
              </div>
            </div>
          </div>

          <!-- By Priority -->
          <div class="bg-white rounded-xl border border-gray-200 shadow-sm flex flex-col min-h-[220px]">
            <div class="px-5 py-4 border-b border-gray-50">
              <h3 class="text-sm font-bold text-gray-900">По приоритету</h3>
            </div>
            <div class="p-5 space-y-3 flex-1">
              <div v-for="[priority, count] in sortedByPriority" :key="priority" class="flex items-center justify-between">
                <span class="text-sm text-gray-700 font-medium">{{ priority }}</span>
                <div class="flex items-center gap-2">
                  <div class="w-24 h-2 bg-gray-100 rounded-full overflow-hidden">
                    <div class="h-full rounded-full" :class="priority === 'Критический' ? 'bg-red-500' : priority === 'Высокий' ? 'bg-orange-500' : priority === 'Низкий' ? 'bg-blue-400' : 'bg-gray-400'" :style="{ width: `${(count / ticketStats.total) * 100}%` }"></div>
                  </div>
                  <span class="text-xs font-bold text-gray-900 w-8 text-right">{{ count }}</span>
                </div>
              </div>
            </div>
          </div>

          <!-- By Department -->
          <div class="bg-white rounded-xl border border-gray-200 shadow-sm flex flex-col min-h-[220px]">
            <div class="px-5 py-4 border-b border-gray-50">
              <h3 class="text-sm font-bold text-gray-900">По отделу (топ-10)</h3>
            </div>
            <div class="p-5 space-y-3 flex-1">
              <div v-for="[dept, count] in sortedByDepartment" :key="dept" class="flex items-center justify-between">
                <span class="text-sm text-gray-700 font-medium truncate max-w-[140px]">{{ dept }}</span>
                <div class="flex items-center gap-2">
                  <div class="w-24 h-2 bg-gray-100 rounded-full overflow-hidden">
                    <div class="h-full bg-emerald-500 rounded-full" :style="{ width: `${(count / ticketStats.total) * 100}%` }"></div>
                  </div>
                  <span class="text-xs font-bold text-gray-900 w-8 text-right">{{ count }}</span>
                </div>
              </div>
              <div v-if="!sortedByDepartment.length" class="text-sm text-gray-400 text-center py-4">Нет данных</div>
            </div>
          </div>
        </div>

        <!-- Monthly trend -->
        <div class="bg-white rounded-xl border border-gray-200 shadow-sm">
          <div class="px-5 py-4 border-b border-gray-50">
            <h3 class="text-sm font-bold text-gray-900">Динамика по месяцам</h3>
          </div>
          <div class="p-5">
            <div class="flex items-end gap-3 h-56">
              <div
                v-for="[month, count] in [...sortedByMonth].reverse()"
                :key="month"
                class="flex-1 flex flex-col items-center gap-1 min-w-0"
              >
                <span class="text-[10px] font-bold text-gray-900">{{ count }}</span>
                <div
                  class="w-full bg-indigo-500 rounded-t-md min-h-[4px] transition-all"
                  :style="{ height: `${Math.max(4, (count / Math.max(...sortedByMonth.map(e => e[1]), 1)) * 120)}px` }"
                ></div>
                <span class="text-[9px] text-gray-400 font-bold truncate w-full text-center">{{ monthLabel(month) }}</span>
              </div>
            </div>
          </div>
        </div>
      </template>

      <div v-else-if="!ticketsLoading" class="text-center py-24 bg-white rounded-xl border border-gray-200">
        <div class="w-16 h-16 bg-gray-50 rounded-full flex items-center justify-center mx-auto mb-4">
          <AlertCircle :size="32" class="text-gray-300" />
        </div>
        <h3 class="text-lg font-semibold text-gray-900 mb-1">Нет заявок</h3>
        <p class="text-sm text-gray-500">Данные по заявкам отсутствуют.</p>
      </div>
    </template>
  </div>
</template>
