<script setup lang="ts">
import { RefreshCw, Save, ChevronLeft, ChevronRight, User2, Calendar, X } from 'lucide-vue-next'

const api = useApi()
const auth = useAuthStore()
const router = useRouter()
const { can, refresh } = useStaffPermissions()
const toast = useToast()

const employees = ref<any[]>([])
const loading = ref(true)
const saving = ref(false)
const hasChanges = ref(false)

const MONTHS_RU = ['Январь','Февраль','Март','Апрель','Май','Июнь','Июль','Август','Сентябрь','Октябрь','Ноябрь','Декабрь']
const DAY_NAMES = ['Вс','Пн','Вт','Ср','Чт','Пт','Сб']

const SHIFT_CODES = [
  { code: 'Д', label: 'Дневная', color: 'bg-gray-200 text-gray-700 dark:bg-zinc-600 dark:text-gray-200' },
  { code: 'ДО', label: 'Дневная офис', color: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/40 dark:text-yellow-300' },
  { code: 'Н', label: 'Ночная', color: 'bg-blue-900 text-blue-100 dark:bg-blue-950 dark:text-blue-200' },
  { code: 'ДС', label: 'Допсмена', color: 'bg-green-200 text-green-800 dark:bg-green-800/40 dark:text-green-300' },
  { code: 'В', label: 'Выходной', color: 'bg-emerald-700 text-emerald-100 dark:bg-emerald-800 dark:text-emerald-200' },
  { code: 'О', label: 'Отпуск', color: 'bg-violet-200 text-violet-800 dark:bg-violet-900/40 dark:text-violet-300' },
  { code: 'К', label: 'Командировка', color: 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-300' },
]

function shiftStyle(code: string): string {
  return SHIFT_CODES.find(s => s.code === code)?.color || 'bg-gray-100 text-gray-500'
}

const now = new Date()
const selectedYear = ref(now.getFullYear())
const selectedMonth = ref(now.getMonth())

const monthDates = computed(() => {
  const y = selectedYear.value
  const m = selectedMonth.value
  const daysInMonth = new Date(y, m + 1, 0).getDate()
  return Array.from({ length: daysInMonth }, (_, i) => new Date(y, m, i + 1))
})

function prevMonth() {
  if (selectedMonth.value === 0) { selectedMonth.value = 11; selectedYear.value-- }
  else selectedMonth.value--
}
function nextMonth() {
  if (selectedMonth.value === 11) { selectedMonth.value = 0; selectedYear.value++ }
  else selectedMonth.value++
}
function goToday() {
  selectedYear.value = now.getFullYear()
  selectedMonth.value = now.getMonth()
}

function isToday(d: Date): boolean {
  return d.getDate() === now.getDate() && d.getMonth() === now.getMonth() && d.getFullYear() === now.getFullYear()
}
function isWeekend(d: Date): boolean {
  return d.getDay() === 0 || d.getDay() === 6
}
function dayName(d: Date): string {
  return DAY_NAMES[d.getDay()]
}

// Schedule data stored as JSON object: { "YYYY-MM-DD": { code: "Д", city?: "Минск" } }
// Stored in workScheduleGridJson
type DayEntry = { code: string; city?: string }
type ScheduleMap = Record<string, DayEntry>

function parseScheduleMap(json: string): ScheduleMap {
  try {
    const parsed = JSON.parse(json)
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) return parsed
  } catch {}
  return {}
}

const scheduleEdits = ref<Record<string, DayEntry | null>>({})

function cellKey(emp: any, date: Date): string {
  return `${emp.userId}:${date.toISOString().slice(0, 10)}`
}
function dateKey(date: Date): string {
  return date.toISOString().slice(0, 10)
}

function getCellValue(emp: any, date: Date): DayEntry | null {
  const ck = cellKey(emp, date)
  if (scheduleEdits.value[ck] !== undefined) return scheduleEdits.value[ck]
  const map = parseScheduleMap(emp.workScheduleGridJson || '')
  return map[dateKey(date)] || null
}

function setCellValue(emp: any, date: Date, entry: DayEntry | null) {
  scheduleEdits.value[cellKey(emp, date)] = entry
  hasChanges.value = true
}

// Picker state
const pickerOpen = ref(false)
const pickerEmp = ref<any>(null)
const pickerDate = ref<Date | null>(null)
const pickerCity = ref('')
const pickerSelectedCode = ref('')

function openPicker(emp: any, date: Date) {
  if (!auth.isSuperAdmin || !can('sectionScheduleEdit')) return
  const current = getCellValue(emp, date)
  pickerEmp.value = emp
  pickerDate.value = date
  pickerSelectedCode.value = current?.code || ''
  pickerCity.value = current?.city || ''
  pickerOpen.value = true
}

function selectCode(code: string) {
  if (pickerSelectedCode.value === code) {
    pickerSelectedCode.value = ''
  } else {
    pickerSelectedCode.value = code
    if (code !== 'К') pickerCity.value = ''
  }
}

function applyPicker() {
  if (!pickerEmp.value || !pickerDate.value) return
  if (!pickerSelectedCode.value) {
    setCellValue(pickerEmp.value, pickerDate.value, null)
  } else {
    const entry: DayEntry = { code: pickerSelectedCode.value }
    if (pickerSelectedCode.value === 'К' && pickerCity.value.trim()) {
      entry.city = pickerCity.value.trim()
    }
    setCellValue(pickerEmp.value, pickerDate.value, entry)
  }
  pickerOpen.value = false
}

function clearPicker() {
  if (!pickerEmp.value || !pickerDate.value) return
  setCellValue(pickerEmp.value, pickerDate.value, null)
  pickerOpen.value = false
}

function buildScheduleMap(emp: any): ScheduleMap {
  const map = parseScheduleMap(emp.workScheduleGridJson || '')
  for (const date of monthDates.value) {
    const ck = cellKey(emp, date)
    const dk = dateKey(date)
    if (scheduleEdits.value[ck] !== undefined) {
      const val = scheduleEdits.value[ck]
      if (val) map[dk] = val
      else delete map[dk]
    }
  }
  return map
}

async function saveAll() {
  if (!auth.isSuperAdmin || !can('sectionScheduleEdit')) return
  if (!hasChanges.value) return
  saving.value = true
  const changedUserIds = new Set<string>()
  for (const key of Object.keys(scheduleEdits.value)) {
    changedUserIds.add(key.split(':')[0])
  }
  try {
    for (const userId of changedUserIds) {
      const emp = employees.value.find(e => e.userId === userId)
      if (!emp) continue
      const newMap = buildScheduleMap(emp)
      await api.employees.changeSchedule(userId, emp.workSchedule || null, JSON.stringify(newMap))
      emp.workScheduleGridJson = JSON.stringify(newMap)
    }
    scheduleEdits.value = {}
    hasChanges.value = false
    toast.success('Графики сохранены')
  } catch {
    toast.error('Не удалось сохранить')
  } finally {
    saving.value = false
  }
}

const DEPT_ORDER = [
  'Координатор', '1 линия', '2 линия', 'Разработчики',
  'Выездные инженеры', 'Ремонт / сервис', 'Бухгалтерия',
  'Закупки', 'Системный администратор',
]
const DEPT_COLORS: Record<string, string> = {
  'Координатор': 'bg-rose-50 text-rose-700 dark:bg-rose-900/20 dark:text-rose-300 border-rose-200 dark:border-rose-800',
  '1 линия': 'bg-sky-50 text-sky-700 dark:bg-sky-900/20 dark:text-sky-300 border-sky-200 dark:border-sky-800',
  '2 линия': 'bg-blue-50 text-blue-700 dark:bg-blue-900/20 dark:text-blue-300 border-blue-200 dark:border-blue-800',
  'Разработчики': 'bg-violet-50 text-violet-700 dark:bg-violet-900/20 dark:text-violet-300 border-violet-200 dark:border-violet-800',
  'Выездные инженеры': 'bg-amber-50 text-amber-700 dark:bg-amber-900/20 dark:text-amber-300 border-amber-200 dark:border-amber-800',
  'Ремонт / сервис': 'bg-orange-50 text-orange-700 dark:bg-orange-900/20 dark:text-orange-300 border-orange-200 dark:border-orange-800',
  'Бухгалтерия': 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/20 dark:text-emerald-300 border-emerald-200 dark:border-emerald-800',
  'Закупки': 'bg-teal-50 text-teal-700 dark:bg-teal-900/20 dark:text-teal-300 border-teal-200 dark:border-teal-800',
  'Системный администратор': 'bg-gray-50 text-gray-700 dark:bg-zinc-800 dark:text-gray-300 border-gray-200 dark:border-zinc-600',
}

type DeptGroup = { name: string; emps: any[] }

const sortedEmployees = computed(() => {
  const all = [...employees.value]
  all.sort((a, b) => {
    const ai = DEPT_ORDER.indexOf(a.department || '')
    const bi = DEPT_ORDER.indexOf(b.department || '')
    return (ai === -1 ? 999 : ai) - (bi === -1 ? 999 : bi)
  })
  return all
})

const deptGroups = computed<DeptGroup[]>(() => {
  const groups: DeptGroup[] = []
  let current = ''
  for (const emp of sortedEmployees.value) {
    const dept = emp.department || 'Без отдела'
    if (dept !== current) {
      groups.push({ name: dept, emps: [] })
      current = dept
    }
    groups[groups.length - 1].emps.push(emp)
  }
  return groups
})

function deptColor(name: string): string {
  return DEPT_COLORS[name] || 'bg-gray-50 text-gray-600 dark:bg-zinc-800 dark:text-gray-400 border-gray-200 dark:border-zinc-600'
}

async function loadEmployees() {
  loading.value = true
  try {
    employees.value = await api.employees.getAll()
  } catch {
    toast.error('Не удалось загрузить сотрудников')
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  await refresh()
  if (auth.isStaff && !can('sectionScheduleView')) {
    toast.warning('Нет доступа к графику работы')
    await router.replace('/')
    return
  }
  loadEmployees()
})
</script>

<template>
  <div class="w-full space-y-3">
    <!-- Legend -->
    <div class="flex flex-wrap items-center gap-2 text-[11px]">
      <span v-for="s in SHIFT_CODES" :key="s.code" :class="['inline-flex items-center gap-1 px-2 py-1 rounded-md font-bold', s.color]">
        {{ s.code }} <span class="font-medium opacity-70">{{ s.label }}</span>
      </span>
    </div>

    <!-- Header: month selector + save -->
    <div class="flex items-center justify-between flex-wrap gap-3">
      <div class="flex items-center gap-2">
        <button @click="prevMonth" class="p-2 rounded-lg hover:bg-gray-200 dark:hover:bg-zinc-700 transition-colors">
          <ChevronLeft :size="18" class="text-gray-500" />
        </button>
        <div class="flex items-center gap-2 min-w-[180px] justify-center">
          <Calendar :size="16" class="text-gray-400" />
          <span class="text-sm font-bold text-gray-900 dark:text-gray-100">{{ MONTHS_RU[selectedMonth] }} {{ selectedYear }}</span>
        </div>
        <button @click="nextMonth" class="p-2 rounded-lg hover:bg-gray-200 dark:hover:bg-zinc-700 transition-colors">
          <ChevronRight :size="18" class="text-gray-500" />
        </button>
        <button @click="goToday" class="text-xs text-indigo-600 font-bold ml-1 hover:underline">Сегодня</button>
      </div>
      <button
        v-if="auth.isSuperAdmin && can('sectionScheduleEdit') && hasChanges"
        @click="saveAll"
        :disabled="saving"
        class="inline-flex items-center gap-2 px-5 py-2.5 text-xs font-bold text-white bg-indigo-600 hover:bg-indigo-700 rounded-xl disabled:opacity-50 transition-all shadow-lg shadow-indigo-100 uppercase tracking-widest"
      >
        <Save :size="14" />
        {{ saving ? 'Сохранение...' : 'Сохранить' }}
      </button>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex items-center justify-center py-24">
      <RefreshCw :size="32" class="animate-spin text-indigo-600" />
    </div>

    <!-- Schedule Grid -->
    <div v-else class="bg-white dark:bg-[#1a1a1d] rounded-xl border border-gray-200 dark:border-zinc-700 shadow-sm overflow-auto max-h-[calc(100vh-12rem)]">
      <table class="w-full border-collapse text-xs">
        <thead class="sticky top-0 z-20">
          <tr class="bg-gray-50 dark:bg-[#1a1a1d] border-b border-gray-100 dark:border-zinc-800">
            <th rowspan="2" class="px-3 py-2.5 text-left text-[10px] font-bold text-gray-400 uppercase tracking-widest sticky left-0 z-30 bg-gray-50 dark:bg-[#1a1a1d] min-w-[80px] border-r border-gray-200 dark:border-zinc-700">Дата</th>
            <th
              v-for="g in deptGroups" :key="g.name"
              :colspan="g.emps.length"
              :class="['px-2 py-1.5 text-center text-[10px] font-bold uppercase tracking-wider border-r-2 border-l', deptColor(g.name)]"
            >{{ g.name }}</th>
          </tr>
          <tr class="bg-gray-50 dark:bg-[#1a1a1d] border-b border-gray-200 dark:border-zinc-700">
            <template v-for="(g, gi) in deptGroups" :key="g.name">
              <th
                v-for="(emp, ei) in g.emps" :key="emp.userId"
                :class="[
                  'px-1 py-2 text-center min-w-[70px] border-r border-gray-100 dark:border-zinc-700/50',
                  ei === g.emps.length - 1 && gi < deptGroups.length - 1 ? 'border-r-2 border-r-gray-300 dark:border-r-zinc-600' : ''
                ]"
              >
                <div class="flex flex-col items-center gap-0.5">
                  <div class="w-6 h-6 rounded-full bg-indigo-100 dark:bg-indigo-900/40 flex items-center justify-center shrink-0 overflow-hidden mx-auto">
                    <img v-if="emp.avatarUrl && !emp._avatarBroken" :src="emp.avatarUrl" class="w-full h-full object-cover" @error="emp._avatarBroken = true" />
                    <User2 v-else :size="10" class="text-indigo-600 dark:text-indigo-400" />
                  </div>
                  <span class="text-[9px] font-bold text-gray-900 dark:text-gray-100 leading-tight truncate max-w-[65px]">{{ emp.fullName?.split(' ')[0] }}</span>
                </div>
              </th>
            </template>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="date in monthDates" :key="date.toISOString()"
            :class="[
              'border-b border-gray-50 dark:border-zinc-800',
              isToday(date) ? 'bg-indigo-50/70 dark:bg-indigo-900/15' : isWeekend(date) ? 'bg-gray-50/50 dark:bg-zinc-800/30' : ''
            ]"
          >
            <td
              class="px-2 py-1.5 sticky left-0 z-10 border-r border-gray-200 dark:border-zinc-700"
              :class="isToday(date) ? 'bg-indigo-50 dark:bg-indigo-900/20' : isWeekend(date) ? 'bg-gray-50 dark:bg-zinc-800/50' : 'bg-white dark:bg-[#1a1a1d]'"
            >
              <div class="flex items-center gap-1">
                <span class="text-xs font-bold min-w-[16px]" :class="isToday(date) ? 'text-indigo-700 dark:text-indigo-300' : isWeekend(date) ? 'text-red-400' : 'text-gray-900 dark:text-gray-100'">{{ date.getDate() }}</span>
                <span class="text-[9px] font-medium" :class="isToday(date) ? 'text-indigo-500' : isWeekend(date) ? 'text-red-300' : 'text-gray-400'">{{ dayName(date) }}</span>
              </div>
            </td>
            <template v-for="(g, gi) in deptGroups" :key="g.name">
              <td
                v-for="(emp, ei) in g.emps" :key="emp.userId"
                :class="[
                  'px-0.5 py-1 text-center border-r border-gray-50 dark:border-zinc-800 cursor-pointer transition-colors',
                  getCellValue(emp, date) ? shiftStyle(getCellValue(emp, date)!.code) : 'hover:bg-indigo-50/50 dark:hover:bg-indigo-900/10',
                  ei === g.emps.length - 1 && gi < deptGroups.length - 1 ? '!border-r-2 !border-r-gray-300 dark:!border-r-zinc-600' : ''
                ]"
                @click="openPicker(emp, date)"
              >
                <template v-if="getCellValue(emp, date)">
                  <div class="flex flex-col items-center gap-0.5">
                    <span class="text-[10px] font-bold">
                      {{ getCellValue(emp, date)!.code }}
                    </span>
                    <span v-if="getCellValue(emp, date)!.city" class="text-[8px] opacity-70 leading-none truncate max-w-[60px]">{{ getCellValue(emp, date)!.city }}</span>
                  </div>
                </template>
                <span v-else class="text-gray-200 dark:text-gray-700">·</span>
              </td>
            </template>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Picker Modal -->
    <Teleport to="body">
      <div v-if="pickerOpen" class="fixed inset-0 z-[100] flex items-center justify-center bg-gray-900/50 backdrop-blur-sm p-4" @click.self="pickerOpen = false">
        <div class="bg-white dark:bg-[#1e1e21] rounded-2xl shadow-2xl w-full max-w-xs overflow-hidden ring-1 ring-black/5">
          <div class="px-5 py-3 border-b border-gray-100 dark:border-zinc-700 flex items-center justify-between">
            <div>
              <div class="text-sm font-bold text-gray-900 dark:text-gray-100">{{ pickerEmp?.fullName }}</div>
              <div class="text-[11px] text-gray-400">{{ pickerDate?.toLocaleDateString('ru-RU', { day: '2-digit', month: 'long', year: 'numeric' }) }}</div>
            </div>
            <button @click="pickerOpen = false" class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"><X :size="18" /></button>
          </div>
          <div class="p-4 space-y-3">
            <div class="grid grid-cols-4 gap-1.5">
              <button
                v-for="s in SHIFT_CODES" :key="s.code"
                @click="selectCode(s.code)"
                :class="[
                  'px-2 py-2 rounded-lg text-xs font-bold transition-all border-2',
                  pickerSelectedCode === s.code
                    ? 'border-indigo-500 ring-2 ring-indigo-200 dark:ring-indigo-800 ' + s.color
                    : 'border-transparent ' + s.color + ' opacity-60 hover:opacity-100'
                ]"
              >
                <div>{{ s.code }}</div>
                <div class="text-[8px] font-medium opacity-70 leading-tight mt-0.5">{{ s.label }}</div>
              </button>
            </div>

            <Transition name="fade">
              <div v-if="pickerSelectedCode === 'К'" class="pt-1">
                <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-1">Город командировки</label>
                <input
                  v-model="pickerCity"
                  placeholder="Введите город..."
                  class="w-full border border-gray-200 dark:border-zinc-600 rounded-lg px-3 py-2 text-sm bg-white dark:bg-zinc-800 text-gray-900 dark:text-gray-100 focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500"
                />
              </div>
            </Transition>
          </div>
          <div class="px-4 py-3 bg-gray-50 dark:bg-zinc-800/50 border-t border-gray-100 dark:border-zinc-700 flex justify-between gap-2">
            <button @click="clearPicker" class="px-3 py-2 text-[11px] font-bold text-red-500 hover:text-red-700 uppercase tracking-widest">Очистить</button>
            <button @click="applyPicker" class="px-5 py-2 text-[11px] font-bold text-white bg-indigo-600 hover:bg-indigo-700 rounded-lg transition-all uppercase tracking-widest">ОК</button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: all 0.2s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; transform: translateY(-4px); }
</style>
