<script setup lang="ts">
import { Users, Search, Plus, Trash2, UserCog, ShieldAlert, SlidersHorizontal } from 'lucide-vue-next'
import type { Department, Employee } from '~/types'
import {
  STAFF_PERMISSION_SECTIONS,
  buildMergedPermState,
  defaultPermissionForRole,
} from '~/config/staffPermissionCatalog'

definePageMeta({ middleware: ['staff-not-field-engineer'] })

const api = useApi()
const auth = useAuthStore()
const staffPerm = useStaffPermissions()
const toast = useToast()

const employees = ref<Employee[]>([])
const loading = ref(true)
const searchQuery = ref('')
const departments = ref<Department[]>([])

async function loadEmployees() {
  loading.value = true
  try {
    employees.value = await api.employees.getAll()
  } catch (error) {
    console.error('Failed to load employees:', error)
  } finally {
    loading.value = false
  }
}

async function loadDepartments() {
  try {
    departments.value = await api.departments.getAll()
  } catch (error) {
    // non-blocking
    console.error('Failed to load departments:', error)
  }
}

const deptLabelByValue = computed(() => {
  const m = new Map<string, string>()
  for (const d of departments.value || []) {
    m.set(String(d.value || '').trim(), d.label)
  }
  return m
})

function resolveDepartmentLabel(v: string) {
  const raw = String(v || '').trim()
  if (!raw) return '—'
  return deptLabelByValue.value.get(raw) || raw
}

const ROLE_HIERARCHY: Record<string, number> = {
  'super_admin': 0,
  'director': 1,
  'head_support': 2,
  'head_engineers': 3,
  'head_dev': 4,
  'head_repair': 5,
  'coordinator': 6,
  'sysadmin': 7,
  'support_l2': 8,
  'support_l1': 9,
  'developer': 10,
  'field_engineer': 11,
  'accountant': 12,
  'procurement': 13,
  'agent': 14,
}

const ROLE_GROUP_LABELS: Record<string, string> = {
  'super_admin': 'Супер-админы',
  'director': 'Директора',
  'head_support': 'Начальники отделов',
  'head_engineers': 'Начальники отделов',
  'head_dev': 'Начальники отделов',
  'head_repair': 'Начальники отделов',
  'coordinator': 'Координаторы',
  'sysadmin': 'Системные администраторы',
  'support_l2': 'Поддержка 2 линия',
  'support_l1': 'Поддержка 1 линия',
  'developer': 'Разработчики',
  'field_engineer': 'Выездные инженеры',
  'accountant': 'Бухгалтерия',
  'procurement': 'Закупки',
  'agent': 'Агенты',
}

function roleOrder(role: string): number {
  return ROLE_HIERARCHY[role] ?? 99
}

function roleGroupLabel(role: string): string {
  return ROLE_GROUP_LABELS[role] || 'Прочие'
}

const filteredEmployees = computed(() => {
  let list = [...employees.value]
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    list = list.filter(e =>
      e.fullName.toLowerCase().includes(q) ||
      e.login.toLowerCase().includes(q) ||
      e.role.toLowerCase().includes(q) ||
      resolveDepartmentLabel(e.department || '').toLowerCase().includes(q)
    )
  }
  list.sort((a, b) => roleOrder(a.role) - roleOrder(b.role))
  return list
})

type EmployeeGroup = { label: string; items: Employee[] }

const groupedEmployees = computed<EmployeeGroup[]>(() => {
  const groups: EmployeeGroup[] = []
  let currentLabel = ''
  for (const e of filteredEmployees.value) {
    const label = roleGroupLabel(e.role)
    if (label !== currentLabel) {
      groups.push({ label, items: [] })
      currentLabel = label
    }
    groups[groups.length - 1].items.push(e)
  }
  return groups
})

const roleOptions = [
  { value: 'support_l1', label: 'Поддержка L1' },
  { value: 'support_l2', label: 'Поддержка L2' },
  { value: 'field_engineer', label: 'Выездной инженер' },
  { value: 'sysadmin', label: 'Сисадмин' },
  { value: 'developer', label: 'Разработчик' },
  { value: 'accountant', label: 'Бухгалтерия' },
  { value: 'coordinator', label: 'Координатор' },
  { value: 'director', label: 'Директор' },
  { value: 'super_admin', label: 'Супер-админ' },
  { value: 'head_engineers', label: 'Нач. инженеров' },
  { value: 'head_support', label: 'Нач. поддержки' },
  { value: 'head_dev', label: 'Нач. разработки' },
  { value: 'procurement', label: 'Закупки' },
  { value: 'head_repair', label: 'Нач. ремонта' },
  { value: 'agent', label: 'Агент' },
]

/** Подпись Employees.Role (RU) → slug; запасной вариант, если API отдало не slug. */
const employeeRoleTitleToSlug: Record<string, string> = {
  'Супер-админ': 'super_admin',
  'Сапорт 1 линия': 'support_l1',
  'Сапорт 2 линия': 'support_l2',
  'Разработчик': 'developer',
  'Выездной инженер': 'field_engineer',
  'Бухгалтерия': 'accountant',
  'Нач. отдела инженеров': 'head_engineers',
  'Нач. отдела сапорта': 'head_support',
  'Нач. отдела разработки': 'head_dev',
  'Системный администратор': 'sysadmin',
  'Координатор': 'coordinator',
  'Директор': 'director',
  'Закупки / Внеш.': 'procurement',
  'Нач. отдела ремонта': 'head_repair',
  'Агент': 'agent',
}

function normalizeRoleSlugForForm(raw: string): string {
  const t = String(raw || '').trim()
  if (!t) return ''
  if (roleOptions.some((r) => r.value === t)) return t
  return employeeRoleTitleToSlug[t] || t
}

function getRoleLabel(role: string): string {
  const map: Record<string, string> = {
    'super_admin': 'Супер-админ',
    'support_l1': 'Поддержка L1',
    'support_l2': 'Поддержка L2',
    'developer': 'Разработчик',
    'field_engineer': 'Выездной инженер',
    'sysadmin': 'Сисадмин',
    'coordinator': 'Координатор',
    'director': 'Директор',
    'accountant': 'Бухгалтерия',
    'head_engineers': 'Нач. инженеров',
    'head_support': 'Нач. поддержки',
    'head_dev': 'Нач. разработки',
    'procurement': 'Закупки',
    'head_repair': 'Нач. ремонта',
    'agent': 'Агент',
  }
  return map[role] || role
}

function getRoleBadgeStyle(role: string): string {
  const map: Record<string, string> = {
    'super_admin': 'bg-purple-50 text-purple-700 border-purple-100',
    'coordinator': 'bg-pink-50 text-pink-700 border-pink-100',
    'director': 'bg-blue-50 text-blue-700 border-blue-100',
    'support_l2': 'bg-green-50 text-green-700 border-green-100',
    'support_l1': 'bg-yellow-50 text-yellow-700 border-yellow-100',
    'developer': 'bg-indigo-50 text-indigo-700 border-indigo-100',
    'field_engineer': 'bg-emerald-50 text-emerald-700 border-emerald-100',
    'accountant': 'bg-emerald-50 text-emerald-700 border-emerald-100',
  }
  return map[role] || 'bg-gray-50 text-gray-600 border-gray-200'
}

// Pagination (50 per page)
const perPage = 50
const page = ref(1)
watch([searchQuery], () => { page.value = 1 })
const totalPages = computed(() => Math.max(1, Math.ceil(filteredEmployees.value.length / perPage)))
const paginatedEmployees = computed(() => {
  const p = Math.min(Math.max(1, page.value), totalPages.value)
  const start = (p - 1) * perPage
  return filteredEmployees.value.slice(start, start + perPage)
})

// Super-admin editor
type EmployeePermissions = Record<string, boolean>
type EmployeeDetails = {
  id: number
  userId: string
  fullName: string
  role: string
  department: string
  login: string
  email: string
  permissionsJson: string
  telegramChatId: string
  okdeskId: number | null
}

const editOpen = ref(false)
const saving = ref(false)
const editError = ref('')
const editing = ref<EmployeeDetails | null>(null)
const isCreateMode = ref(false)
const deptPickerOpen = ref(false)
const deptQuery = ref('')

const permissionSections = STAFF_PERMISSION_SECTIONS

/** Состояние чекбоксов: JSON + дефолты по роли (код + SystemSettings). */
function buildPermStateFromJson(json: string, roleSlug: string): EmployeePermissions {
  const r = normalizeRoleSlugForForm(roleSlug)
  const fromServer = staffPerm.roleDefaultsMap.value[r]
  return buildMergedPermState(json || '{}', r, fromServer) as EmployeePermissions
}

const permState = ref<EmployeePermissions>({})
const newPermKey = ref('')

const form = reactive({
  id: 0,
  userId: '',
  fullName: '',
  role: '',
  department: '',
  login: '',
  password: '',
  telegramChatId: '',
  okdeskId: null as number | null,
  permissionsRaw: '',
})

function openEditModal() {
  if (!auth.isSuperAdmin) return
  // Create mode
  isCreateMode.value = true
  editing.value = null
  editError.value = ''
  saving.value = false
  form.id = 0
  form.userId = ''
  form.fullName = ''
  form.role = ''
  form.department = ''
  form.login = ''
  form.password = ''
  form.telegramChatId = ''
  form.okdeskId = null
  permState.value = buildPermStateFromJson('{}', '')
  form.permissionsRaw = JSON.stringify(permState.value, null, 2)
  editOpen.value = true
}

function openDeptPicker() {
  if (!auth.isSuperAdmin) return
  deptQuery.value = ''
  deptPickerOpen.value = true
}

const filteredDepartments = computed(() => {
  const list = departments.value || []
  const q = deptQuery.value.trim().toLowerCase()
  if (!q) return list
  return list.filter(d =>
    d.label.toLowerCase().includes(q) ||
    d.value.toLowerCase().includes(q) ||
    (d.desc || '').toLowerCase().includes(q)
  )
})

function selectDepartment(d: Department) {
  form.department = d.value
  deptPickerOpen.value = false
}

async function openEditEmployee(userId: string) {
  if (!auth.isSuperAdmin) return
  editError.value = ''
  saving.value = false
  isCreateMode.value = false
  editOpen.value = true
  editing.value = null
  try {
    const d = await api.employees.getById(userId) as any
    const details: EmployeeDetails = {
      id: Number(d.id || 0),
      userId: String(d.userId || userId),
      fullName: String(d.fullName || ''),
      role: String(d.role || ''),
      department: String(d.department || ''),
      login: String(d.login || ''),
      email: String(d.email || ''),
      permissionsJson: String(d.permissionsJson || ''),
      telegramChatId: String(d.telegramChatId || ''),
      okdeskId: d.okdeskId != null ? Number(d.okdeskId) : null,
    }
    editing.value = details
    form.id = details.id
    form.userId = details.userId
    form.fullName = details.fullName
    form.role = normalizeRoleSlugForForm(details.role)
    form.department = details.department
    form.login = details.login
    form.password = ''
    form.telegramChatId = details.telegramChatId
    form.okdeskId = details.okdeskId
    form.permissionsRaw = details.permissionsJson || '{}'

    permState.value = buildPermStateFromJson(details.permissionsJson || '{}', form.role)
    form.permissionsRaw = JSON.stringify(permState.value, null, 2)
  } catch (e: any) {
    editError.value = e?.data?.error || e?.message || 'Не удалось загрузить сотрудника'
  }
}

watch(permState, () => {
  // keep raw json in sync with toggles
  try {
    form.permissionsRaw = JSON.stringify(permState.value, null, 2)
  } catch {
    // ignore
  }
}, { deep: true })

/** В режиме создания: при выборе роли подставляем дефолты видимости меню (как у существующих ролей). */
watch(
  () => form.role,
  (r) => {
    if (!editOpen.value || !isCreateMode.value) return
    permState.value = buildPermStateFromJson('{}', r)
    form.permissionsRaw = JSON.stringify(permState.value, null, 2)
  },
)

/** Вкладка «Сотрудники» | «Настройки ролей» */
const employeesTab = ref<'list' | 'roleDefaults'>('list')
const selectedRoleSlug = ref('support_l1')
const rolePermEditState = ref<Record<string, boolean>>({})
const savingRoleDefaults = ref(false)

function syncRolePermEditFromDefaults() {
  const r = normalizeRoleSlugForForm(selectedRoleSlug.value)
  const srv = staffPerm.roleDefaultsMap.value[r]
  rolePermEditState.value = { ...buildMergedPermState('{}', r, srv) }
}

watch(selectedRoleSlug, () => {
  if (employeesTab.value === 'roleDefaults') syncRolePermEditFromDefaults()
})

watch(employeesTab, (t) => {
  if (t === 'roleDefaults') syncRolePermEditFromDefaults()
})

async function saveRoleDefaults() {
  if (!auth.isSuperAdmin) return
  savingRoleDefaults.value = true
  try {
    const r = normalizeRoleSlugForForm(selectedRoleSlug.value)
    const next: Record<string, Record<string, boolean>> = {
      ...staffPerm.roleDefaultsMap.value,
      [r]: { ...rolePermEditState.value },
    }
    await api.systemSettings.saveRolePermissionDefaults(next)
    staffPerm.roleDefaultsMap.value = next
    await staffPerm.refresh()
    toast.success('Дефолты для роли сохранены')
  } catch (e: any) {
    toast.error(e?.data?.error || e?.message || 'Не удалось сохранить')
  } finally {
    savingRoleDefaults.value = false
  }
}

async function saveEmployee() {
  if (!auth.isSuperAdmin) return
  saving.value = true
  editError.value = ''
  let savedForUserId = ''
  try {
    // prefer toggles json; if raw edited manually, respect it
    let permissionsJson = form.permissionsRaw
    try {
      const parsed = JSON.parse(form.permissionsRaw || '{}')
      permissionsJson = JSON.stringify(parsed)
    } catch {
      // if invalid json, fall back to toggles
      permissionsJson = JSON.stringify(permState.value || {})
    }

    if (isCreateMode.value) {
      const fullName = String(form.fullName || '').trim()
      const role = String(form.role || '').trim()
      const login = String(form.login || '').trim()
      const password = String(form.password || '').trim()

      if (!fullName) throw new Error('ФИО обязательно')
      if (!role) throw new Error('Роль обязательна')
      if (!login) throw new Error('Логин обязателен')
      if (!password) throw new Error('Пароль обязателен')

      const created = await api.employees.createAccount({
        fullName,
        role,
        department: String(form.department || '').trim(),
        login,
        password,
      }) as any

      const newUserId = String(created?.userId || '')
      if (!newUserId) throw new Error('Не удалось получить userId созданного сотрудника')

      // Apply permissions / telegram (create-account doesn't include these)
      await api.employees.updateProfile(newUserId, {
        permissionsJson,
        telegramChatId: String(form.telegramChatId || '').trim(),
        okdeskId: form.okdeskId != null ? Number(form.okdeskId) : null,
      })
      savedForUserId = newUserId
    } else {
      await api.employees.updateProfile(form.userId, {
        fullName: String(form.fullName || '').trim() || null,
        role: String(form.role || '').trim() || null,
        department: String(form.department || '').trim(),
        login: String(form.login || '').trim() || null,
        password: String(form.password || '').trim() || null,
        permissionsJson,
        telegramChatId: String(form.telegramChatId || '').trim(),
        okdeskId: form.okdeskId != null ? Number(form.okdeskId) : null,
      })
      savedForUserId = String(form.userId || '').trim()
    }

    editOpen.value = false
    await loadEmployees()
    if (
      import.meta.client &&
      savedForUserId &&
      auth.userId &&
      savedForUserId.toLowerCase() === auth.userId.toLowerCase()
    ) {
      await staffPerm.refresh()
    }
  } catch (e: any) {
    editError.value = e?.data?.error || e?.message || 'Не удалось сохранить сотрудника'
  } finally {
    saving.value = false
  }
}

function addPermissionKey() {
  const k = newPermKey.value.trim()
  if (!k) return
  if (permState.value[k] === undefined) permState.value[k] = false
  newPermKey.value = ''
}

async function deleteEmployee(userId: string) {
  if (!auth.isSuperAdmin) return
  if (!confirm('Удалить сотрудника?')) return
  try {
    await api.employees.delete(userId)
    await loadEmployees()
  } catch (e: any) {
    alert(e?.data?.error || e?.message || 'Не удалось удалить сотрудника')
  }
}

onMounted(() => {
  loadDepartments()
  loadEmployees()
  if (import.meta.client && auth.isStaff) void staffPerm.refresh()
})
</script>

<template>
  <div class="space-y-6 w-full">
    <!-- Header + tabs -->
    <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <p class="text-sm text-gray-500">
        <template v-if="employeesTab === 'list'">
          Всего <span class="font-semibold text-gray-900">{{ filteredEmployees.length }}</span> активных пользователей системы
        </template>
        <template v-else>
          Дефолтные права для ролей подставляются, если у сотрудника в
          <span class="font-mono text-[11px]">PermissionsJson</span> нет ключа; личные галочки сотрудника важнее.
        </template>
      </p>
      <div class="flex flex-wrap items-center gap-2">
        <div
          v-if="auth.isSuperAdmin"
          class="inline-flex rounded-lg border border-gray-200 dark:border-zinc-600 p-0.5 bg-gray-50 dark:bg-zinc-800/80"
        >
          <button
            type="button"
            class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md text-sm font-medium transition-colors"
            :class="
              employeesTab === 'list'
                ? 'bg-white dark:bg-zinc-700 text-gray-900 dark:text-gray-100 shadow-sm'
                : 'text-gray-500 dark:text-zinc-400 hover:text-gray-800 dark:hover:text-zinc-200'
            "
            @click="employeesTab = 'list'"
          >
            <Users :size="16" />
            Сотрудники
          </button>
          <button
            type="button"
            class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md text-sm font-medium transition-colors"
            :class="
              employeesTab === 'roleDefaults'
                ? 'bg-white dark:bg-zinc-700 text-gray-900 dark:text-gray-100 shadow-sm'
                : 'text-gray-500 dark:text-zinc-400 hover:text-gray-800 dark:hover:text-zinc-200'
            "
            @click="employeesTab = 'roleDefaults'"
          >
            <SlidersHorizontal :size="16" />
            Настройки ролей
          </button>
        </div>
        <button
          v-if="auth.isSuperAdmin && employeesTab === 'list'"
          class="inline-flex items-center gap-2 bg-indigo-600 hover:bg-indigo-700 text-white px-4 py-2 rounded-lg text-sm font-medium transition-colors shadow-sm shrink-0"
          @click="openEditModal"
        >
          <Plus :size="18" />
          Новый сотрудник
        </button>
      </div>
    </div>

    <!-- Настройки дефолтов по ролям -->
    <div v-if="employeesTab === 'roleDefaults' && auth.isSuperAdmin" class="space-y-4">
      <div class="flex flex-col sm:flex-row sm:flex-wrap sm:items-center gap-3 rounded-xl border border-gray-200 dark:border-zinc-600 bg-white dark:bg-zinc-900/40 p-4">
        <label class="flex items-center gap-2 text-sm font-medium text-gray-700 dark:text-zinc-300 shrink-0">
          Роль
          <select
            v-model="selectedRoleSlug"
            class="min-w-[12rem] px-3 py-2 text-sm bg-gray-50 dark:bg-zinc-800 border border-gray-200 dark:border-zinc-600 rounded-lg text-gray-900 dark:text-gray-100"
          >
            <option v-for="ro in roleOptions" :key="ro.value" :value="ro.value">{{ ro.label }}</option>
          </select>
        </label>
        <button
          type="button"
          class="inline-flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-semibold bg-indigo-600 text-white hover:bg-indigo-700 disabled:opacity-50 shrink-0"
          :disabled="savingRoleDefaults"
          @click="saveRoleDefaults"
        >
          {{ savingRoleDefaults ? 'Сохранение…' : 'Сохранить для этой роли' }}
        </button>
      </div>

      <div class="max-h-[min(70vh,720px)] overflow-y-auto pr-1 space-y-4">
        <div
          v-for="section in permissionSections"
          :key="'rd-' + section.id"
          class="rounded-xl border border-gray-200 dark:border-zinc-600 bg-gray-50/80 dark:bg-zinc-800/60 overflow-hidden"
        >
          <div class="px-3 py-2.5 border-b border-gray-200/80 dark:border-zinc-600 bg-white/90 dark:bg-zinc-800/90">
            <div class="text-[13px] font-semibold text-gray-900 dark:text-gray-100">{{ section.title }}</div>
            <div v-if="section.description" class="text-[11px] text-gray-600 dark:text-zinc-400 mt-0.5 leading-snug">
              {{ section.description }}
            </div>
          </div>
          <div class="p-2 space-y-1">
            <label
              v-for="item in section.items"
              :key="'rd-' + item.key"
              class="flex items-start gap-3 p-2.5 rounded-lg border border-transparent hover:border-gray-200 dark:hover:border-zinc-600 hover:bg-white dark:hover:bg-zinc-800 cursor-pointer"
            >
              <input
                v-model="rolePermEditState[item.key]"
                type="checkbox"
                class="mt-0.5 h-4 w-4 shrink-0 accent-indigo-600 dark:accent-indigo-500"
              />
              <div class="min-w-0 flex-1">
                <div class="text-sm font-medium text-gray-900 dark:text-gray-100 leading-snug">{{ item.label }}</div>
                <div v-if="item.hint" class="text-[11px] text-gray-600 dark:text-zinc-400 mt-0.5 leading-snug">{{ item.hint }}</div>
                <div class="text-[10px] text-gray-500 dark:text-zinc-500 font-mono mt-1 select-all">{{ item.key }}</div>
              </div>
            </label>
          </div>
        </div>
      </div>
    </div>

    <!-- Search Tool -->
    <div v-if="employeesTab === 'list'" class="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
      <div class="relative">
        <Search class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" :size="18" />
        <input
          v-model="searchQuery"
          type="text"
          class="w-full pl-10 pr-4 py-2.5 bg-gray-50 border-none rounded-lg text-sm focus:ring-2 focus:ring-indigo-500/20 focus:bg-white transition-all text-gray-900"
          placeholder="Поиск по имени, логину, роли или подразделению..."
        />
      </div>
    </div>

    <!-- Loading -->
    <div v-if="employeesTab === 'list' && loading" class="flex items-center justify-center py-24">
      <div class="text-sm text-gray-500">Загрузка…</div>
    </div>

    <div v-else-if="employeesTab === 'list' && filteredEmployees.length === 0" class="text-center py-24 bg-white rounded-xl border border-gray-200 shadow-sm">
      <div class="w-16 h-16 bg-gray-50 rounded-full flex items-center justify-center mx-auto mb-4">
        <Users :size="32" class="text-gray-300" />
      </div>
      <h3 class="text-lg font-semibold text-gray-900 mb-1">Сотрудники не найдены</h3>
      <p class="text-sm text-gray-500">Попробуйте использовать другой запрос или проверьте настройки фильтрации</p>
    </div>

    <!-- List/Table -->
    <div v-else-if="employeesTab === 'list'" class="space-y-0">
      <!-- Mobile Cards -->
      <div class="md:hidden space-y-3">
        <template v-for="group in groupedEmployees" :key="group.label">
          <div class="flex items-center gap-2 pt-2">
            <div class="text-[11px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest">{{ group.label }}</div>
            <span class="text-[10px] text-gray-300 dark:text-gray-600 font-mono">({{ group.items.length }})</span>
            <div class="flex-1 border-t border-gray-200 dark:border-zinc-700"></div>
          </div>
          <div
            v-for="e in group.items"
            :key="'m'+e.userId"
            class="bg-white rounded-xl border border-gray-200 p-3.5"
            :class="auth.isSuperAdmin ? 'active:bg-gray-50 cursor-pointer' : ''"
            @click="auth.isSuperAdmin && openEditEmployee(e.userId)"
          >
            <div class="flex items-start justify-between gap-2 mb-1">
              <div class="min-w-0">
                <div class="font-bold text-gray-900 text-sm leading-snug truncate">{{ e.fullName }}</div>
                <div v-if="auth.isSuperAdmin" class="text-[10px] text-gray-400 font-mono mt-0.5">{{ e.userId }}</div>
              </div>
              <span :class="['shrink-0 px-2 py-0.5 rounded text-[10px] font-bold border', getRoleBadgeStyle(e.role)]">
                {{ getRoleLabel(e.role) }}
              </span>
            </div>
            <div class="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-gray-500 mt-1">
              <span v-if="e.department" class="bg-gray-50 px-1.5 py-0.5 rounded border border-gray-100 font-medium">{{ resolveDepartmentLabel(e.department) }}</span>
              <span v-if="e.login" class="font-mono">{{ e.login }}</span>
              <span v-if="e.authEmail" class="truncate max-w-[180px]">{{ e.authEmail }}</span>
            </div>
            <div v-if="auth.isSuperAdmin" class="flex items-center gap-2 mt-2 pt-2 border-t border-gray-100">
              <button
                class="text-xs text-indigo-600 font-semibold"
                @click.stop="openEditEmployee(e.userId)"
              >Редактировать</button>
              <button
                class="text-xs text-red-500 font-semibold"
                @click.stop="deleteEmployee(e.userId)"
              >Удалить</button>
            </div>
          </div>
        </template>
      </div>

      <!-- Desktop Table -->
      <div class="hidden md:block bg-white border border-gray-200 rounded-xl shadow-sm overflow-hidden">
      <div class="overflow-x-auto">
        <table class="min-w-[1050px] w-full text-left border-collapse">
          <thead class="bg-gray-50 border-b border-gray-200">
            <tr class="text-[11px] font-bold text-gray-500 uppercase tracking-wider">
              <th class="px-5 py-3 w-[280px]">ФИО</th>
              <th class="px-5 py-3 w-[160px]">Роль</th>
              <th class="px-5 py-3 w-[220px]">Подотдел</th>
              <th class="px-5 py-3 w-[160px]">Логин</th>
              <th class="px-5 py-3 w-[260px]">Email</th>
              <th v-if="auth.isSuperAdmin" class="px-5 py-3 w-[180px]">User ID</th>
              <th v-if="auth.isSuperAdmin" class="px-5 py-3 w-[160px] text-right">Действия</th>
            </tr>
          </thead>
          <tbody class="text-sm">
            <template v-for="group in groupedEmployees" :key="group.label">
              <tr class="bg-gray-50/80 dark:bg-zinc-800/40">
                <td :colspan="auth.isSuperAdmin ? 7 : 5" class="px-5 py-2">
                  <div class="flex items-center gap-2">
                    <span class="text-[11px] font-bold text-gray-500 dark:text-gray-400 uppercase tracking-widest">{{ group.label }}</span>
                    <span class="text-[10px] text-gray-300 dark:text-gray-600 font-mono">({{ group.items.length }})</span>
                    <div class="flex-1 border-t border-gray-200 dark:border-zinc-700"></div>
                  </div>
                </td>
              </tr>
              <tr v-for="e in group.items" :key="e.userId" class="hover:bg-gray-50/60 transition-colors border-b border-gray-100 dark:border-zinc-800">
                <td class="px-5 py-3.5">
                  <div class="font-semibold text-gray-900 truncate">{{ e.fullName }}</div>
                </td>
                <td class="px-5 py-3.5">
                  <span :class="['inline-flex items-center px-2 py-1 rounded-md border text-[11px] font-semibold', getRoleBadgeStyle(e.role)]">
                    {{ getRoleLabel(e.role) }}
                  </span>
                </td>
                <td class="px-5 py-3.5 text-gray-700">
                  <span class="truncate inline-block max-w-[18rem]">{{ resolveDepartmentLabel(e.department || '') }}</span>
                </td>
                <td class="px-5 py-3.5 text-gray-700 font-mono">
                  {{ e.login || '—' }}
                </td>
                <td class="px-5 py-3.5 text-gray-700">
                  <span class="truncate inline-block max-w-[18rem]">{{ e.authEmail || '—' }}</span>
                </td>
                <td v-if="auth.isSuperAdmin" class="px-5 py-3.5 text-gray-500 font-mono text-xs">
                  <span class="truncate inline-block max-w-[10rem]">{{ e.userId }}</span>
                </td>
                <td v-if="auth.isSuperAdmin" class="px-5 py-3.5">
                  <div class="flex items-center justify-end gap-2">
                    <button
                      class="inline-flex items-center gap-2 px-3 py-2 bg-gray-50 hover:bg-indigo-50 hover:text-indigo-700 text-gray-600 text-xs font-semibold rounded-lg transition-colors border border-gray-100"
                      @click="openEditEmployee(e.userId)"
                      title="Редактировать"
                    >
                      <UserCog :size="14" />
                      Редактировать
                    </button>
                    <button
                      class="p-2 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors"
                      title="Удалить"
                      @click="deleteEmployee(e.userId)"
                    >
                      <Trash2 :size="16" />
                    </button>
                  </div>
                </td>
              </tr>
            </template>
          </tbody>
        </table>
      </div>
    </div>
    </div>

    <!-- Pagination -->
    <div v-if="employeesTab === 'list' && filteredEmployees.length > perPage" class="flex items-center justify-between px-4 py-3 border border-gray-200 bg-white rounded-lg shadow-sm">
      <div class="text-xs text-gray-500">
        Страница <span class="font-semibold text-gray-900">{{ page }}</span> из <span class="font-semibold text-gray-900">{{ totalPages }}</span>
      </div>
      <div class="flex items-center gap-1.5">
        <button type="button" class="px-3 py-1.5 text-xs font-medium border border-gray-200 rounded hover:bg-gray-50 disabled:opacity-50" :disabled="page <= 1" @click="page = 1">«</button>
        <button type="button" class="px-3 py-1.5 text-xs font-medium border border-gray-200 rounded hover:bg-gray-50 disabled:opacity-50" :disabled="page <= 1" @click="page = Math.max(1, page - 1)">Назад</button>
        <button type="button" class="px-3 py-1.5 text-xs font-medium border border-gray-200 rounded hover:bg-gray-50 disabled:opacity-50" :disabled="page >= totalPages" @click="page = Math.min(totalPages, page + 1)">Вперёд</button>
        <button type="button" class="px-3 py-1.5 text-xs font-medium border border-gray-200 rounded hover:bg-gray-50 disabled:opacity-50" :disabled="page >= totalPages" @click="page = totalPages">»</button>
      </div>
    </div>

    <!-- Edit modal (super admin) — светлая/тёмная тема: явные цвета, без наследования body text -->
    <Teleport to="body">
      <div v-if="editOpen" class="fixed inset-0 z-50 bg-black/40 dark:bg-black/60 backdrop-blur-sm flex items-center justify-center p-4" @click.self="editOpen = false">
        <div class="bg-white dark:bg-[#1e1e21] w-full max-w-5xl rounded-xl shadow-modal border border-gray-200 dark:border-zinc-700 overflow-hidden text-gray-900 dark:text-gray-100">
          <div class="px-5 py-4 border-b border-gray-100 dark:border-zinc-700 flex items-center justify-between bg-white dark:bg-[#1e1e21]">
            <div class="font-semibold text-gray-900 dark:text-gray-100 text-sm truncate">
              {{ isCreateMode ? 'Создание сотрудника' : 'Редактирование сотрудника' }}
            </div>
            <button type="button" class="p-2 text-gray-400 hover:text-gray-700 dark:text-zinc-500 dark:hover:text-zinc-200" @click="editOpen = false">✕</button>
          </div>

          <div class="p-5 space-y-4 bg-white dark:bg-[#1a1a1d]">
            <div v-if="editError" class="text-sm text-red-800 dark:text-red-200 bg-red-50 dark:bg-red-950/50 border border-red-200 dark:border-red-900/60 rounded-lg px-3 py-2 flex items-center gap-2">
              <ShieldAlert :size="16" />
              {{ editError }}
            </div>

            <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
              <div class="space-y-4">
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label class="block text-xs font-semibold text-gray-500 dark:text-zinc-400 mb-1">ID (в БД)</label>
                    <input :value="isCreateMode ? '—' : (form.id || '—')" disabled class="w-full px-3 py-2 text-sm bg-gray-100 dark:bg-zinc-800 border border-gray-200 dark:border-zinc-600 rounded-lg text-gray-700 dark:text-zinc-200" />
                  </div>
                  <div>
                    <label class="block text-xs font-semibold text-gray-500 dark:text-zinc-400 mb-1">UserId</label>
                    <input :value="isCreateMode ? '—' : (form.userId || '—')" disabled class="w-full px-3 py-2 text-sm bg-gray-100 dark:bg-zinc-800 border border-gray-200 dark:border-zinc-600 rounded-lg text-gray-700 dark:text-zinc-200 font-mono" />
                  </div>
                </div>

                <div>
                  <label class="block text-xs font-semibold text-gray-500 dark:text-zinc-400 mb-1">ФИО</label>
                  <input v-model="form.fullName" class="w-full px-3 py-2 text-sm bg-gray-50 dark:bg-zinc-800 border border-gray-200 dark:border-zinc-600 rounded-lg text-gray-900 dark:text-gray-100 placeholder:text-gray-400 dark:placeholder:text-zinc-500 focus:bg-white dark:focus:bg-zinc-800 focus:ring-2 focus:ring-indigo-500/20 outline-none" />
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label class="block text-xs font-semibold text-gray-500 dark:text-zinc-400 mb-1">Роль</label>
                    <select v-model="form.role" class="w-full px-3 py-2 text-sm bg-gray-50 dark:bg-zinc-800 border border-gray-200 dark:border-zinc-600 rounded-lg text-gray-900 dark:text-gray-100 focus:bg-white dark:focus:bg-zinc-800 focus:ring-2 focus:ring-indigo-500/20 outline-none [&>option]:dark:bg-zinc-800">
                      <option value="" disabled>Выберите роль…</option>
                      <option v-for="r in roleOptions" :key="r.value" :value="r.value">{{ r.label }} ({{ r.value }})</option>
                    </select>
                  </div>
                  <div>
                    <label class="block text-xs font-semibold text-gray-500 dark:text-zinc-400 mb-1">Подотдел</label>
                    <button
                      type="button"
                      class="w-full px-3 py-2 text-sm bg-gray-50 dark:bg-zinc-800 border border-gray-200 dark:border-zinc-600 rounded-lg hover:bg-white dark:hover:bg-zinc-700/80 transition-colors text-left"
                      @click="openDeptPicker"
                    >
                      <span class="text-gray-900 dark:text-gray-100 font-medium">{{ resolveDepartmentLabel(form.department) }}</span>
                      <span class="ml-2 text-[11px] text-gray-400 dark:text-zinc-500 font-mono">{{ form.department || '' }}</span>
                    </button>
                  </div>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label class="block text-xs font-semibold text-gray-500 dark:text-zinc-400 mb-1">Логин</label>
                    <input v-model="form.login" class="w-full px-3 py-2 text-sm bg-gray-50 dark:bg-zinc-800 border border-gray-200 dark:border-zinc-600 rounded-lg text-gray-900 dark:text-gray-100 focus:bg-white dark:focus:bg-zinc-800 focus:ring-2 focus:ring-indigo-500/20 outline-none" />
                  </div>
                  <div>
                    <label class="block text-xs font-semibold text-gray-500 dark:text-zinc-400 mb-1">{{ isCreateMode ? 'Пароль' : 'Новый пароль' }}</label>
                    <input v-model="form.password" type="password" class="w-full px-3 py-2 text-sm bg-gray-50 dark:bg-zinc-800 border border-gray-200 dark:border-zinc-600 rounded-lg text-gray-900 dark:text-gray-100 focus:bg-white dark:focus:bg-zinc-800 focus:ring-2 focus:ring-indigo-500/20 outline-none" :placeholder="isCreateMode ? 'обязательно' : 'оставь пустым, если не менять'" />
                  </div>
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label class="block text-xs font-semibold text-gray-500 dark:text-zinc-400 mb-1">Telegram chatId</label>
                    <input v-model="form.telegramChatId" class="w-full px-3 py-2 text-sm bg-gray-50 dark:bg-zinc-800 border border-gray-200 dark:border-zinc-600 rounded-lg text-gray-900 dark:text-gray-100 focus:bg-white dark:focus:bg-zinc-800 focus:ring-2 focus:ring-indigo-500/20 outline-none font-mono" />
                  </div>
                  <div>
                    <label class="block text-xs font-semibold text-gray-500 dark:text-zinc-400 mb-1">Okdesk ID</label>
                    <input v-model.number="form.okdeskId" type="number" class="w-full px-3 py-2 text-sm bg-gray-50 dark:bg-zinc-800 border border-gray-200 dark:border-zinc-600 rounded-lg text-gray-900 dark:text-gray-100 focus:bg-white dark:focus:bg-zinc-800 focus:ring-2 focus:ring-indigo-500/20 outline-none font-mono" placeholder="например, 9" />
                  </div>
                </div>
              </div>

              <div class="space-y-4 max-h-[min(70vh,720px)] overflow-y-auto pr-1">
                <div>
                  <div class="text-xs font-bold text-gray-500 dark:text-zinc-400 uppercase tracking-wider">Права доступа</div>
                  <p class="mt-1 text-[12px] text-gray-600 dark:text-zinc-400 leading-snug">
                    Индивидуальные настройки UI и функций → в
                    <span class="font-mono text-[11px] text-gray-800 dark:text-zinc-300">PermissionsJson</span>.
                    Права по заявкам (статусы, чужие задачи, удаление и т.д.) идут от
                    <span class="font-medium text-gray-700 dark:text-zinc-300">роли</span>
                    по матрице по умолчанию; сюда их не дублируем. Галочки подкручивают боковое меню, карточку заявки, мессенджер, оборудование, таблички и график на клиенте.
                  </p>
                  <p
                    v-if="form.role === 'super_admin'"
                    class="mt-2 text-[12px] text-indigo-700 dark:text-indigo-300 bg-indigo-50 dark:bg-indigo-950/40 border border-indigo-200/80 dark:border-indigo-900/60 rounded-lg px-2.5 py-2 leading-snug"
                  >
                    Для супер-админа, если в JSON нет ключа, подставляется «всё разрешено». Снятые галочки и явные false в JSON скрывают разделы и действия так же, как у других ролей.
                  </p>
                </div>

                <div
                  v-for="section in permissionSections"
                  :key="section.id"
                  class="rounded-xl border border-gray-200 dark:border-zinc-600 bg-gray-50/80 dark:bg-zinc-800/60 overflow-hidden"
                >
                  <div class="px-3 py-2.5 border-b border-gray-200/80 dark:border-zinc-600 bg-white/90 dark:bg-zinc-800/90">
                    <div class="text-[13px] font-semibold text-gray-900 dark:text-gray-100">{{ section.title }}</div>
                    <div v-if="section.description" class="text-[11px] text-gray-600 dark:text-zinc-400 mt-0.5 leading-snug">
                      {{ section.description }}
                    </div>
                  </div>
                  <div class="p-2 space-y-1">
                    <label
                      v-for="item in section.items"
                      :key="item.key"
                      class="flex items-start gap-3 p-2.5 rounded-lg border border-transparent hover:border-gray-200 dark:hover:border-zinc-600 hover:bg-white dark:hover:bg-zinc-800 cursor-pointer"
                    >
                      <input type="checkbox" class="mt-0.5 h-4 w-4 shrink-0 accent-indigo-600 dark:accent-indigo-500" v-model="permState[item.key]" />
                      <div class="min-w-0 flex-1">
                        <div class="text-sm font-medium text-gray-900 dark:text-gray-100 leading-snug">{{ item.label }}</div>
                        <div v-if="item.hint" class="text-[11px] text-gray-600 dark:text-zinc-400 mt-0.5 leading-snug">{{ item.hint }}</div>
                        <div class="text-[10px] text-gray-500 dark:text-zinc-500 font-mono mt-1 select-all">{{ item.key }}</div>
                      </div>
                    </label>
                  </div>
                </div>

                <div class="flex items-center gap-2">
                  <input
                    v-model="newPermKey"
                    class="flex-1 px-3 py-2 text-sm bg-gray-50 dark:bg-zinc-800 border border-gray-200 dark:border-zinc-600 rounded-lg text-gray-900 dark:text-gray-100 focus:bg-white dark:focus:bg-zinc-800 focus:ring-2 focus:ring-indigo-500/20 outline-none font-mono"
                    placeholder="Свой ключ, напр. canExportTickets"
                    @keydown.enter.prevent="addPermissionKey"
                  />
                  <button
                    type="button"
                    class="px-3 py-2 text-sm font-semibold border border-gray-200 dark:border-zinc-600 rounded-lg bg-white dark:bg-zinc-800 text-gray-900 dark:text-gray-100 hover:bg-gray-50 dark:hover:bg-zinc-700 shrink-0"
                    @click="addPermissionKey"
                  >
                    Добавить ключ
                  </button>
                </div>

                <div>
                  <label class="block text-xs font-semibold text-gray-500 dark:text-zinc-400 mb-1">JSON прав (для опытных)</label>
                  <textarea v-model="form.permissionsRaw" rows="6" class="w-full px-3 py-2 text-sm bg-gray-50 dark:bg-zinc-900 border border-gray-200 dark:border-zinc-600 rounded-lg text-gray-900 dark:text-zinc-200 focus:bg-white dark:focus:bg-zinc-900 focus:ring-2 focus:ring-indigo-500/20 outline-none font-mono resize-y min-h-[120px]"></textarea>
                  <div class="mt-1 text-[11px] text-gray-500 dark:text-zinc-500">
                    Редактирование вручную перезапишет состояние галочек при следующем открытии, если JSON невалиден — сохранится вариант из чекбоксов.
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div class="px-5 py-4 border-t border-gray-100 dark:border-zinc-700 bg-gray-50 dark:bg-zinc-900/80 flex items-center justify-end gap-2">
            <button type="button" class="px-4 py-2 text-sm font-semibold border border-gray-200 dark:border-zinc-600 rounded-lg bg-white dark:bg-zinc-800 text-gray-900 dark:text-gray-100 hover:bg-gray-50 dark:hover:bg-zinc-700" @click="editOpen = false" :disabled="saving">Отмена</button>
            <button type="button" class="px-4 py-2 text-sm font-semibold rounded-lg bg-indigo-600 text-white hover:bg-indigo-700 disabled:opacity-50" @click="saveEmployee" :disabled="saving || (isCreateMode ? false : !String(form.userId || '').trim())">
              {{ saving ? 'Сохранение…' : 'Сохранить' }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Department picker -->
    <Teleport to="body">
      <div v-if="deptPickerOpen" class="fixed inset-0 z-[60] bg-black/40 dark:bg-black/60 backdrop-blur-sm flex items-center justify-center p-4" @click.self="deptPickerOpen = false">
        <div class="bg-white dark:bg-[#1e1e21] w-full max-w-xl rounded-xl shadow-modal border border-gray-200 dark:border-zinc-700 overflow-hidden text-gray-900 dark:text-gray-100">
          <div class="px-5 py-4 border-b border-gray-100 dark:border-zinc-700 flex items-center justify-between">
            <div class="font-semibold text-gray-900 dark:text-gray-100 text-sm truncate">Выбор подотдела</div>
            <button type="button" class="p-2 text-gray-400 hover:text-gray-700 dark:text-zinc-500 dark:hover:text-zinc-200" @click="deptPickerOpen = false">✕</button>
          </div>
          <div class="p-5 space-y-3 bg-white dark:bg-[#1a1a1d]">
            <input
              v-model="deptQuery"
              class="w-full px-3 py-2 text-sm bg-gray-50 dark:bg-zinc-800 border border-gray-200 dark:border-zinc-600 rounded-lg text-gray-900 dark:text-gray-100 focus:bg-white dark:focus:bg-zinc-800 focus:ring-2 focus:ring-indigo-500/20 outline-none"
              placeholder="Поиск по названию…"
            />
            <div class="max-h-[420px] overflow-auto border border-gray-200 dark:border-zinc-600 rounded-lg bg-white dark:bg-zinc-900/50">
              <button
                v-for="d in filteredDepartments"
                :key="d.value"
                type="button"
                class="w-full text-left px-4 py-3 hover:bg-gray-50 dark:hover:bg-zinc-800 border-b border-gray-100 dark:border-zinc-700 last:border-b-0"
                @click="selectDepartment(d)"
              >
                <div class="font-semibold text-gray-900 dark:text-gray-100">{{ d.label }}</div>
                <div class="text-[11px] text-gray-500 dark:text-zinc-500 font-mono">{{ d.value }}</div>
                <div v-if="d.desc" class="text-[12px] text-gray-600 dark:text-zinc-400 mt-0.5">{{ d.desc }}</div>
              </button>
              <div v-if="filteredDepartments.length === 0" class="px-4 py-6 text-sm text-gray-500 dark:text-zinc-400 text-center">Ничего не найдено</div>
            </div>
          </div>
          <div class="px-5 py-4 border-t border-gray-100 dark:border-zinc-700 bg-gray-50 dark:bg-zinc-900/80 flex items-center justify-end gap-2">
            <button type="button" class="px-4 py-2 text-sm font-semibold border border-gray-200 dark:border-zinc-600 rounded-lg bg-white dark:bg-zinc-800 text-gray-900 dark:text-gray-100 hover:bg-gray-50 dark:hover:bg-zinc-700" @click="deptPickerOpen = false">Закрыть</button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
