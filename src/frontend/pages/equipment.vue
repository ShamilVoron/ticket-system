<script setup lang="ts">
import { Wrench, Search, Plus, Package, Truck, Ruler, ShieldAlert, X, RefreshCw } from 'lucide-vue-next'
import type { Equipment } from '~/types'

const api = useApi()
const toast = useToast()
const { can } = useStaffPermissions()
const canEditEquipmentRow = computed(() => can('sectionEquipmentEdit'))

const EQUIP_TYPE_RU: Record<string, string> = {
  minipc: 'Мини-ПК',
  monoblok: 'Моноблок',
  notebook: 'Ноутбук',
  printer: 'Принтер',
  psu: 'Блок питания',
  ssd_belfood: 'SSD (Belfood)',
  printer_belfood: 'Принтер (Belfood)',
  tool: 'Инструмент',
  supply: 'Расходник',
}
const STATUS_RU: Record<string, string> = {
  active: 'Активно',
  pj: 'В проекте',
  decommission: 'Списано',
}

function trType(raw: string): string {
  const k = (raw || '').trim()
  return EQUIP_TYPE_RU[k.toLowerCase()] || EQUIP_TYPE_RU[k] || k
}
function trStatus(raw: string): string {
  const k = (raw || '').trim()
  return STATUS_RU[k.toLowerCase()] || STATUS_RU[k] || k
}

const equipment = ref<Equipment[]>([])
const loading = ref(true)
const searchQuery = ref('')
const activeTab = ref('replacement_fund')

const typeFilter = ref<string>('__all__')
const statusFilter = ref<string>('__all__')

// Reference data from DB
type ObjOption = { id: number; name: string; address: string; legalEntity: string }
type CompanyOption = { id: number; name: string }
const serviceObjects = ref<ObjOption[]>([])
const companies = ref<CompanyOption[]>([])

// Searchable picker state
const locationPickerOpen = ref(false)
const locationPickerSearch = ref('')
const clientPickerOpen = ref(false)
const clientPickerSearch = ref('')

const filteredObjectsForPicker = computed(() => {
  const q = locationPickerSearch.value.trim().toLowerCase()
  if (!q) return serviceObjects.value
  return serviceObjects.value.filter(o =>
    o.name.toLowerCase().includes(q) || o.address.toLowerCase().includes(q)
  )
})

const filteredCompaniesForPicker = computed(() => {
  const q = clientPickerSearch.value.trim().toLowerCase()
  if (!q) return companies.value
  return companies.value.filter(c => c.name.toLowerCase().includes(q))
})

function pickLocation(obj: ObjOption) {
  form.location = obj.name
  if (obj.legalEntity) form.clientName = obj.legalEntity
  locationPickerOpen.value = false
}

function pickClient(comp: CompanyOption) {
  form.clientName = comp.name
  clientPickerOpen.value = false
}

const isTools = computed(() => activeTab.value === 'tools_supplies')

const editOpen = ref(false)
const saving = ref(false)
const editError = ref('')
const editing = ref<Equipment | null>(null)

const form = reactive<Partial<Equipment>>({
  tab: '',
  equipmentType: '',
  fundStatus: '',
  name: '',
  serialNumber: '',
  location: '',
  status: '',
  clientName: '',
  notes: '',
  defect: '',
  processor: '',
  ram: '',
  diskInfo: '',
  osInfo: '',
  interfaces: '',
  completeness: '',
  faults: '',
  installPosition: '',
  powerSpecs: '',
  issuedTo: '',
  purchaseDate: null,
  issueDate: null,
})

const tabs = [
  { id: 'replacement_fund', label: 'Фонд замен', icon: Truck },
  { id: 'client_equipment', label: 'Оборудование клиентов', icon: Package },
  { id: 'tools_supplies', label: 'Инструменты и расходники', icon: Ruler },
]

async function loadEquipment() {
  loading.value = true
  try {
    equipment.value = await api.equipment.getAll(activeTab.value)
  } catch (error: any) {
    console.error('Failed to load equipment:', error)
    const s = error?.statusCode ?? error?.status ?? error?.response?.status
    toast.error(
      s
        ? `Не удалось загрузить оборудование (код ${s}). Выйдите и войдите снова.`
        : 'Не удалось загрузить оборудование.'
    )
  } finally {
    loading.value = false
  }
}

function normalizeKey(v: string | null | undefined) {
  return String(v || '').trim() || '—'
}

const typeStats = computed(() => {
  const map = new Map<string, number>()
  for (const e of equipment.value || []) {
    const k = normalizeKey(e.equipmentType)
    map.set(k, (map.get(k) || 0) + 1)
  }
  return Array.from(map.entries()).sort((a, b) => b[1] - a[1])
})

const statusStats = computed(() => {
  const map = new Map<string, number>()
  for (const e of equipment.value || []) {
    const k = normalizeKey(e.fundStatus || e.status)
    map.set(k, (map.get(k) || 0) + 1)
  }
  return Array.from(map.entries()).sort((a, b) => b[1] - a[1])
})

const filteredEquipment = computed(() => {
  let list = equipment.value
  if (typeFilter.value !== '__all__') {
    list = list.filter(e => normalizeKey(e.equipmentType) === typeFilter.value)
  }
  if (statusFilter.value !== '__all__') {
    list = list.filter(e => normalizeKey(e.fundStatus || e.status) === statusFilter.value)
  }
  if (!searchQuery.value) return list
  const q = searchQuery.value.toLowerCase()
  return list.filter(e =>
    e.name.toLowerCase().includes(q) ||
    (e.serialNumber && e.serialNumber.toLowerCase().includes(q)) ||
    (e.location && e.location.toLowerCase().includes(q)) ||
    ((isTools.value ? e.issuedTo : e.clientName) && (isTools.value ? e.issuedTo : e.clientName)!.toLowerCase().includes(q)) ||
    (e.equipmentType && e.equipmentType.toLowerCase().includes(q))
  )
})

// Pagination (50 per page)
const perPage = 50
const page = ref(1)
watch([searchQuery, activeTab, typeFilter, statusFilter], () => { page.value = 1 })
const totalPages = computed(() => Math.max(1, Math.ceil(filteredEquipment.value.length / perPage)))
const paginatedEquipment = computed(() => {
  const p = Math.min(Math.max(1, page.value), totalPages.value)
  const start = (p - 1) * perPage
  return filteredEquipment.value.slice(start, start + perPage)
})

function getStatusBadgeStyle(status: string): string {
  const s = status?.toLowerCase() || ''
  if (s === 'active' || s === 'на складе') return 'bg-green-50 text-green-700 border-green-100'
  if (s === 'pj' || s === 'выдано') return 'bg-yellow-50 text-yellow-700 border-yellow-100'
  if (s === 'decommission') return 'bg-red-50 text-red-700 border-red-100'
  return 'bg-gray-50 text-gray-600 border-gray-200'
}

function openCreate() {
  if (!can('sectionEquipmentCreate')) {
    toast.warning('Нет права создавать записи оборудования')
    return
  }
  editing.value = null
  editError.value = ''
  Object.assign(form, {
    tab: activeTab.value,
    equipmentType: '',
    fundStatus: '',
    name: '',
    serialNumber: '',
    location: '',
    status: '',
    clientName: '',
    notes: '',
    defect: '',
    processor: '',
    ram: '',
    diskInfo: '',
    osInfo: '',
    interfaces: '',
    completeness: '',
    faults: '',
    installPosition: '',
    powerSpecs: '',
    issuedTo: '',
    purchaseDate: null,
    issueDate: null,
  })
  editOpen.value = true
}

function openEdit(e: Equipment) {
  if (!can('sectionEquipmentEdit')) {
    toast.warning('Нет права редактировать оборудование')
    return
  }
  editing.value = e
  editError.value = ''
  Object.assign(form, {
    tab: e.tab,
    equipmentType: e.equipmentType || '',
    fundStatus: e.fundStatus || '',
    name: e.name || '',
    serialNumber: e.serialNumber || '',
    location: e.location || '',
    status: e.status || '',
    clientName: e.clientName || '',
    notes: e.notes || '',
    defect: e.defect || '',
    processor: e.processor || '',
    ram: e.ram || '',
    diskInfo: e.diskInfo || '',
    osInfo: e.osInfo || '',
    interfaces: e.interfaces || '',
    completeness: e.completeness || '',
    faults: e.faults || '',
    installPosition: e.installPosition || '',
    powerSpecs: e.powerSpecs || '',
    issuedTo: e.issuedTo || '',
    purchaseDate: e.purchaseDate || null,
    issueDate: e.issueDate || null,
  })
  editOpen.value = true
}

async function save() {
  if (editing.value && !can('sectionEquipmentEdit')) {
    editError.value = 'Нет права редактировать оборудование'
    return
  }
  if (!editing.value && !can('sectionEquipmentCreate')) {
    editError.value = 'Нет права создавать оборудование'
    return
  }
  saving.value = true
  editError.value = ''
  try {
    const isToolsTab = activeTab.value === 'tools_supplies'
    const payload: any = {
      tab: activeTab.value,
      equipmentType: String(form.equipmentType || '').trim(),
      fundStatus: String(form.fundStatus || '').trim(),
      name: String(form.name || '').trim(),
      serialNumber: String(form.serialNumber || '').trim(),
      location: String(form.location || '').trim(),
      status: String(form.status || '').trim(),
      // For tools/supplies we bind responsibility to employee (issuedTo) instead of clientName
      clientName: isToolsTab ? '' : String(form.clientName || '').trim(),
      notes: String(form.notes || '').trim(),
      defect: String(form.defect || '').trim(),
      processor: String(form.processor || '').trim(),
      ram: String(form.ram || '').trim(),
      diskInfo: String(form.diskInfo || '').trim(),
      osInfo: String(form.osInfo || '').trim(),
      interfaces: String(form.interfaces || '').trim(),
      completeness: String(form.completeness || '').trim(),
      faults: String(form.faults || '').trim(),
      installPosition: String(form.installPosition || '').trim(),
      powerSpecs: String(form.powerSpecs || '').trim(),
      issuedTo: isToolsTab ? String(form.issuedTo || '').trim() : String(form.issuedTo || '').trim(),
      purchaseDate: form.purchaseDate,
      issueDate: form.issueDate,
    }

    if (editing.value) {
      await api.equipment.update(editing.value.id, payload)
    } else {
      await api.equipment.create(payload)
    }
    editOpen.value = false
    await loadEquipment()
  } catch (e: any) {
    editError.value = e?.data?.error || e?.message || 'Не удалось сохранить оборудование'
  } finally {
    saving.value = false
  }
}

// Derived dropdown options from existing data — raw values for saving, Russian for display
const equipmentTypes = computed(() => {
  const s = new Set<string>()
  for (const e of equipment.value) if (e.equipmentType?.trim()) s.add(e.equipmentType.trim())
  return Array.from(s).sort((a, b) => trType(a).localeCompare(trType(b), 'ru'))
})

const fundStatuses = computed(() => {
  const s = new Set<string>()
  for (const e of equipment.value) {
    if (e.fundStatus?.trim()) s.add(e.fundStatus.trim())
    if (e.status?.trim()) s.add(e.status.trim())
  }
  return Array.from(s).sort((a, b) => trStatus(a).localeCompare(trStatus(b), 'ru'))
})

const locations = computed(() => {
  const s = new Set<string>()
  for (const e of equipment.value) if (e.location?.trim()) s.add(e.location.trim())
  return Array.from(s).sort((a, b) => a.localeCompare(b, 'ru'))
})

const clientNames = computed(() => {
  const s = new Set<string>()
  for (const e of equipment.value) if (e.clientName?.trim()) s.add(e.clientName.trim())
  return Array.from(s).sort((a, b) => a.localeCompare(b, 'ru'))
})

const issuedToNames = computed(() => {
  const s = new Set<string>()
  for (const e of equipment.value) if (e.issuedTo?.trim()) s.add(e.issuedTo.trim())
  return Array.from(s).sort((a, b) => a.localeCompare(b, 'ru'))
})

watch(activeTab, () => {
  typeFilter.value = '__all__'
  statusFilter.value = '__all__'
  loadEquipment()
})

onMounted(async () => {
  loadEquipment()
  try {
    const [objs, comps] = await Promise.all([
      api.serviceObjects.getAll(undefined, true),
      api.companies.getAll(true),
    ])
    serviceObjects.value = (objs || []).map((o: any) => ({
      id: Number(o.id ?? o.Id),
      name: String(o.name ?? o.Name ?? ''),
      address: String(o.address ?? o.Address ?? ''),
      legalEntity: String(o.legalEntity ?? o.LegalEntity ?? ''),
    })).sort((a: ObjOption, b: ObjOption) => a.name.localeCompare(b.name, 'ru'))
    companies.value = (comps || []).map((c: any) => ({
      id: Number(c.id ?? c.Id),
      name: String(c.name ?? c.Name ?? ''),
    })).sort((a: CompanyOption, b: CompanyOption) => a.name.localeCompare(b.name, 'ru'))
  } catch (e: any) {
    const s = e?.statusCode ?? e?.status ?? e?.response?.status
    if (s) {
      toast.error(`Справочники для формы: ошибка ${s} (объекты/компании).`)
    }
  }
})
</script>

<template>
  <div class="space-y-6 w-full">
    <!-- Header -->
    <div class="flex items-center justify-between gap-4">
      <p class="text-sm text-gray-500">
        Всего <span class="font-semibold text-gray-900">{{ filteredEquipment.length }}</span> активных позиций в реестре
      </p>
      <button
        v-if="can('sectionEquipmentCreate')"
        type="button"
        class="inline-flex items-center gap-2 bg-indigo-600 hover:bg-indigo-700 text-white px-4 py-2 rounded-lg text-sm font-medium transition-colors shadow-sm shrink-0"
        @click="openCreate"
      >
        <Plus :size="18" />
        Добавить оборудование
      </button>
    </div>

    <!-- Tabs Navigation -->
    <div class="flex flex-wrap items-center gap-2 border-b border-gray-100 pb-1">
      <button
        v-for="tab in tabs"
        :key="tab.id"
        @click="activeTab = tab.id"
        :class="[
          'flex items-center gap-2 px-4 py-2.5 rounded-lg text-sm font-medium transition-all',
          activeTab === tab.id
            ? 'bg-indigo-50 text-indigo-700 shadow-sm border border-indigo-100'
            : 'text-gray-500 hover:bg-gray-50 hover:text-gray-900 border border-transparent'
        ]"
      >
        <component :is="tab.icon" :size="16" />
        {{ tab.label }}
      </button>
    </div>

    <!-- Search Tool -->
    <div class="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
      <div class="relative">
        <Search class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" :size="18" />
        <input
          v-model="searchQuery"
          type="text"
          class="w-full pl-10 pr-4 py-2.5 bg-gray-50 border-none rounded-lg text-sm focus:ring-2 focus:ring-indigo-500/20 focus:bg-white transition-all text-gray-900"
          placeholder="Поиск по названию, серийному номеру, локации или типу..."
        />
      </div>
    </div>

    <!-- Filters (chips like old view) -->
    <div v-if="!loading && equipment.length > 0" class="bg-white p-4 rounded-xl border border-gray-200 shadow-sm space-y-3">
      <div class="text-[11px] font-bold text-gray-500 uppercase tracking-wider">Фильтры</div>

      <div class="flex items-center gap-2 overflow-x-auto pb-1">
        <button
          type="button"
          class="shrink-0 inline-flex items-center gap-2 px-3 py-1.5 rounded-full border text-xs font-semibold"
          :class="typeFilter === '__all__' ? 'bg-emerald-50 text-emerald-800 border-emerald-100' : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'"
          @click="typeFilter = '__all__'"
        >
          Все типы <span class="font-mono text-[11px] opacity-70">{{ equipment.length }}</span>
        </button>
        <button
          v-for="[t, n] in typeStats"
          :key="t"
          type="button"
          class="shrink-0 inline-flex items-center gap-2 px-3 py-1.5 rounded-full border text-xs font-semibold"
          :class="typeFilter === t ? 'bg-emerald-50 text-emerald-800 border-emerald-100' : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'"
          @click="typeFilter = t"
        >
          <span class="truncate max-w-[16rem]">{{ trType(t) }}</span>
          <span class="font-mono text-[11px] opacity-70">{{ n }}</span>
        </button>
      </div>

      <div class="flex items-center gap-2 overflow-x-auto pb-1">
        <button
          type="button"
          class="shrink-0 inline-flex items-center gap-2 px-3 py-1.5 rounded-full border text-xs font-semibold"
          :class="statusFilter === '__all__' ? 'bg-indigo-50 text-indigo-800 border-indigo-100' : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'"
          @click="statusFilter = '__all__'"
        >
          Все статусы
        </button>
        <button
          v-for="[s, n] in statusStats"
          :key="s"
          type="button"
          class="shrink-0 inline-flex items-center gap-2 px-3 py-1.5 rounded-full border text-xs font-semibold"
          :class="statusFilter === s ? 'bg-indigo-50 text-indigo-800 border-indigo-100' : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'"
          @click="statusFilter = s"
        >
          <span class="truncate max-w-[16rem]">{{ trStatus(s) }}</span>
          <span class="font-mono text-[11px] opacity-70">{{ n }}</span>
        </button>
      </div>
    </div>

    <!-- Main Grid -->
    <div v-if="loading" class="flex items-center justify-center py-24">
      <RefreshCw :size="32" class="animate-spin text-indigo-600" />
    </div>

    <div v-else-if="filteredEquipment.length === 0" class="text-center py-24 bg-white rounded-xl border border-gray-200">
      <div class="w-16 h-16 bg-gray-50 rounded-full flex items-center justify-center mx-auto mb-4">
        <Wrench :size="32" class="text-gray-300" />
      </div>
      <h3 class="text-lg font-semibold text-gray-900 mb-1">Оборудование не найдено</h3>
      <p class="text-sm text-gray-500 max-w-xs mx-auto">По вашему запросу ничего не найдено в данной категории. Попробуйте изменить параметры поиска.</p>
    </div>

    <!-- Equipment List -->
    <div v-else class="space-y-0">
      <div class="md:hidden space-y-2">
        <div
          v-for="e in paginatedEquipment"
          :key="'m'+e.id"
          @click="canEditEquipmentRow ? openEdit(e) : undefined"
          class="bg-white rounded-xl border border-gray-200 p-3.5"
          :class="canEditEquipmentRow ? 'active:bg-gray-50 cursor-pointer' : ''"
        >
          <div class="flex items-start justify-between gap-2 mb-1">
            <div class="min-w-0">
              <div class="font-bold text-gray-900 text-sm leading-snug">{{ e.name || '—' }}</div>
              <div v-if="e.serialNumber" class="text-[10px] text-gray-400 font-mono mt-0.5">{{ e.serialNumber }}</div>
            </div>
            <span :class="['shrink-0 px-2 py-0.5 rounded text-[10px] font-bold border', getStatusBadgeStyle(e.fundStatus)]">
              {{ trStatus(e.fundStatus || e.status) || '—' }}
            </span>
          </div>
          <div class="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-gray-500 mt-1">
            <span v-if="!isTools && e.equipmentType" class="bg-gray-50 px-1.5 py-0.5 rounded border border-gray-100 font-medium">{{ trType(e.equipmentType) }}</span>
            <span v-if="e.location" class="truncate max-w-[160px] italic">{{ e.location }}</span>
            <span v-if="!isTools && e.clientName" class="truncate max-w-[140px]">{{ e.clientName }}</span>
            <span v-if="isTools && e.issuedTo" class="truncate max-w-[140px]">{{ e.issuedTo }}</span>
          </div>
          <div v-if="e.defect" class="text-[11px] text-red-600 mt-1 truncate"><span class="font-bold">Дефект:</span> {{ e.defect }}</div>
        </div>
      </div>

      <!-- Desktop Table -->
      <div class="hidden md:block bg-white border border-gray-200 rounded-xl shadow-sm overflow-hidden">
        <div class="overflow-x-auto">
          <table class="min-w-[1100px] w-full text-left border-collapse">
          <thead class="bg-gray-50 border-b border-gray-200">
            <tr class="text-[11px] font-bold text-gray-500 uppercase tracking-wider">
              <th class="px-5 py-3 w-[360px]">Название</th>

              <!-- Tools/Supplies: exact fields from DB -->
              <template v-if="isTools">
                <th class="px-5 py-3 w-[180px]">Серийный номер</th>
                <th class="px-5 py-3 w-[200px]">Кому выдано</th>
                <th class="px-5 py-3 w-[220px]">Местоположение</th>
                <th class="px-5 py-3 w-[120px]">Статус</th>
              </template>

              <!-- Equipment: full view -->
              <template v-else>
                <th class="px-5 py-3 w-[180px]">Serial</th>
                <th class="px-5 py-3 w-[120px]">Тип</th>
                <th class="px-5 py-3 w-[160px]">Локация</th>
                <th class="px-5 py-3 w-[160px]">Клиент</th>
                <th class="px-5 py-3 w-[110px]">Статус</th>
                <th class="px-5 py-3 w-[140px]">CPU</th>
                <th class="px-5 py-3 w-[90px]">RAM</th>
                <th class="px-5 py-3 w-[140px]">Disk</th>
                <th class="px-5 py-3 w-[180px]">OS</th>
              </template>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-100 text-sm">
            <tr
              v-for="e in paginatedEquipment"
              :key="e.id"
              class="transition-colors"
              :class="canEditEquipmentRow ? 'hover:bg-gray-50/60 cursor-pointer' : ''"
              @click="canEditEquipmentRow ? openEdit(e) : undefined"
            >
              <td class="px-5 py-3.5">
                <div class="font-semibold text-gray-900 leading-snug">
                  {{ e.name || '—' }}
                </div>
                <div v-if="e.notes || e.defect" class="mt-1 text-[12px] text-gray-500 line-clamp-2">
                  <span v-if="e.defect" class="text-red-600 font-semibold">Дефект:</span>
                  <span v-if="e.defect">{{ e.defect }}</span>
                  <span v-if="e.defect && e.notes"> · </span>
                  <span v-if="e.notes">{{ e.notes }}</span>
                </div>
              </td>

              <!-- Tools/Supplies row -->
              <template v-if="isTools">
                <td class="px-5 py-3.5 font-mono text-[12px] text-gray-600">
                  {{ e.serialNumber || '—' }}
                </td>
                <td class="px-5 py-3.5 text-gray-700">
                  <span class="truncate inline-block max-w-[18rem]">{{ e.issuedTo || '—' }}</span>
                </td>
                <td class="px-5 py-3.5 text-gray-700">
                  <span class="truncate inline-block max-w-[20rem] italic">{{ e.location || '—' }}</span>
                </td>
                <td class="px-5 py-3.5">
                  <span :class="['inline-flex items-center px-2 py-1 rounded-md border text-[11px] font-semibold', getStatusBadgeStyle(e.fundStatus)]">
                    {{ trStatus(e.fundStatus || e.status) || '—' }}
                  </span>
                </td>
              </template>

              <!-- Equipment row -->
              <template v-else>
                <td class="px-5 py-3.5 font-mono text-[12px] text-gray-600">
                  {{ e.serialNumber || '—' }}
                </td>
                <td class="px-5 py-3.5 text-gray-700 font-medium">
                  {{ trType(e.equipmentType) || '—' }}
                </td>
                <td class="px-5 py-3.5 text-gray-700">
                  <span class="truncate inline-block max-w-[16rem] italic">{{ e.location || '—' }}</span>
                </td>
                <td class="px-5 py-3.5 text-gray-700">
                  <span class="truncate inline-block max-w-[16rem]">{{ e.clientName || '—' }}</span>
                </td>
                <td class="px-5 py-3.5">
                  <span :class="['inline-flex items-center px-2 py-1 rounded-md border text-[11px] font-semibold', getStatusBadgeStyle(e.fundStatus)]">
                    {{ trStatus(e.fundStatus || e.status) || '—' }}
                  </span>
                </td>
                <td class="px-5 py-3.5 text-gray-700">
                  <span class="truncate inline-block max-w-[12rem]">{{ e.processor || '—' }}</span>
                </td>
                <td class="px-5 py-3.5 text-gray-700">
                  {{ e.ram || '—' }}
                </td>
                <td class="px-5 py-3.5 text-gray-700">
                  <span class="truncate inline-block max-w-[12rem]">{{ e.diskInfo || '—' }}</span>
                </td>
                <td class="px-5 py-3.5 text-gray-700">
                  <span class="truncate inline-block max-w-[16rem]">{{ e.osInfo || '—' }}</span>
                </td>
              </template>
            </tr>
          </tbody>
        </table>
      </div>
      </div>
    </div>

    <!-- Pagination -->
    <div v-if="filteredEquipment.length > perPage" class="flex items-center justify-between px-4 py-3 border border-gray-200 bg-white rounded-lg shadow-sm">
      <div class="text-xs text-gray-500">
        Страница <span class="font-semibold text-gray-900">{{ page }}</span> из <span class="font-semibold text-gray-900">{{ totalPages }}</span>
      </div>
      <div class="flex items-center gap-1.5">
        <button
          type="button"
          class="px-3 py-1.5 text-xs font-medium border border-gray-200 rounded hover:bg-gray-50 disabled:opacity-50"
          :disabled="page <= 1"
          @click="page = 1"
        >«</button>
        <button
          type="button"
          class="px-3 py-1.5 text-xs font-medium border border-gray-200 rounded hover:bg-gray-50 disabled:opacity-50"
          :disabled="page <= 1"
          @click="page = Math.max(1, page - 1)"
        >Назад</button>
        <button
          type="button"
          class="px-3 py-1.5 text-xs font-medium border border-gray-200 rounded hover:bg-gray-50 disabled:opacity-50"
          :disabled="page >= totalPages"
          @click="page = Math.min(totalPages, page + 1)"
        >Вперёд</button>
        <button
          type="button"
          class="px-3 py-1.5 text-xs font-medium border border-gray-200 rounded hover:bg-gray-50 disabled:opacity-50"
          :disabled="page >= totalPages"
          @click="page = totalPages"
        >»</button>
      </div>
    </div>

    <!-- Edit/Create Modal -->
    <Teleport to="body">
      <div v-if="editOpen" class="fixed inset-0 z-50 bg-black/40 backdrop-blur-sm flex items-start sm:items-center justify-center overflow-y-auto p-0 sm:p-4" @click.self="editOpen = false">
        <div class="bg-white w-full sm:max-w-3xl sm:rounded-xl shadow-modal border-0 sm:border sm:border-gray-200 min-h-screen sm:min-h-0 sm:my-4 flex flex-col">
          <div class="px-5 py-4 border-b border-gray-100 flex items-center justify-between shrink-0 sticky top-0 bg-white z-10">
            <div class="font-semibold text-gray-900 text-sm truncate">
              {{ editing ? 'Редактирование оборудования' : 'Добавить оборудование' }}
            </div>
            <button class="p-2 text-gray-400 hover:text-gray-700 rounded-lg hover:bg-gray-100" @click="editOpen = false"><X :size="18" /></button>
          </div>

          <div class="p-5 space-y-4 flex-1 overflow-y-auto">
            <div v-if="editError" class="text-sm text-red-700 bg-red-50 border border-red-200 rounded-lg px-3 py-2 flex items-center gap-2">
              <ShieldAlert :size="16" />
              {{ editError }}
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div class="sm:col-span-2">
                <label class="block text-xs font-semibold text-gray-500 mb-1">Название</label>
                <input v-model="form.name" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
              </div>

              <div v-if="!isTools">
                <label class="block text-xs font-semibold text-gray-500 mb-1">Тип оборудования</label>
                <select v-model="form.equipmentType" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none">
                  <option value="">— Выберите тип —</option>
                  <option v-for="t in equipmentTypes" :key="t" :value="t">{{ trType(t) }}</option>
                </select>
              </div>
              <div>
                <label class="block text-xs font-semibold text-gray-500 mb-1">Статус</label>
                <select v-model="form.fundStatus" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none">
                  <option value="">— Выберите статус —</option>
                  <option v-for="s in fundStatuses" :key="s" :value="s">{{ trStatus(s) }}</option>
                </select>
              </div>

              <div>
                <label class="block text-xs font-semibold text-gray-500 mb-1">Серийный номер</label>
                <input v-model="form.serialNumber" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none font-mono" />
              </div>
              <div>
                <label class="block text-xs font-semibold text-gray-500 mb-1">{{ isTools ? 'Местоположение' : 'Локация (объект)' }}</label>
                <button type="button" @click="locationPickerOpen = true; locationPickerSearch = ''" class="w-full text-left px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg hover:bg-white transition-colors truncate">
                  {{ form.location || '— Выберите объект —' }}
                </button>
              </div>

              <div class="sm:col-span-2">
                <label class="block text-xs font-semibold text-gray-500 mb-1">{{ isTools ? 'Кому выдано' : 'Клиент (юрлицо)' }}</label>
                <template v-if="!isTools">
                  <button type="button" @click="clientPickerOpen = true; clientPickerSearch = ''" class="w-full text-left px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg hover:bg-white transition-colors truncate">
                    {{ form.clientName || '— Выберите клиента —' }}
                  </button>
                </template>
                <template v-else>
                  <select
                    v-model="form.issuedTo"
                    class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none"
                  >
                    <option value="">— Выберите сотрудника —</option>
                    <option v-for="n in issuedToNames" :key="n" :value="n">{{ n }}</option>
                  </select>
                </template>
              </div>

              <!-- Equipment fields -->
              <div v-if="!isTools">
                <label class="block text-xs font-semibold text-gray-500 mb-1">CPU</label>
                <input v-model="form.processor" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
              </div>
              <div v-if="!isTools">
                <label class="block text-xs font-semibold text-gray-500 mb-1">RAM</label>
                <input v-model="form.ram" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
              </div>
              <div v-if="!isTools">
                <label class="block text-xs font-semibold text-gray-500 mb-1">Disk</label>
                <input v-model="form.diskInfo" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
              </div>
              <div v-if="!isTools">
                <label class="block text-xs font-semibold text-gray-500 mb-1">OS</label>
                <input v-model="form.osInfo" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
              </div>

              <div class="sm:col-span-2">
                <label class="block text-xs font-semibold text-gray-500 mb-1">Заметки</label>
                <textarea v-model="form.notes" rows="2" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none resize-none"></textarea>
              </div>
              <div class="sm:col-span-2">
                <label class="block text-xs font-semibold text-gray-500 mb-1">Дефект / неисправность</label>
                <textarea v-model="form.defect" rows="2" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none resize-none"></textarea>
              </div>
            </div>
          </div>

          <div class="px-5 py-4 border-t border-gray-100 bg-gray-50 flex items-center justify-end gap-2 shrink-0 sticky bottom-0">
            <button class="px-4 py-2 text-sm font-semibold border border-gray-200 rounded-lg bg-white hover:bg-gray-50" @click="editOpen = false" :disabled="saving">Отмена</button>
            <button
              class="px-4 py-2 text-sm font-semibold rounded-lg bg-indigo-600 text-white hover:bg-indigo-700 disabled:opacity-50"
              @click="save"
              :disabled="saving || !String(form.name || '').trim()"
            >
              {{ saving ? 'Сохранение…' : 'Сохранить' }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Location Picker Modal -->
    <Teleport to="body">
      <div v-if="locationPickerOpen" class="fixed inset-0 z-[60] bg-black/40 backdrop-blur-sm flex items-center justify-center p-4" @click.self="locationPickerOpen = false">
        <div class="bg-white w-full max-w-lg rounded-xl shadow-modal border border-gray-200 overflow-hidden">
          <div class="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
            <div class="font-semibold text-gray-900 text-sm">Выберите объект (локацию)</div>
            <button class="p-2 text-gray-400 hover:text-gray-700 rounded-lg hover:bg-gray-100" @click="locationPickerOpen = false"><X :size="18" /></button>
          </div>
          <div class="p-4 space-y-3">
            <div class="relative">
              <Search class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" :size="16" />
              <input v-model="locationPickerSearch" class="w-full pl-9 pr-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" placeholder="Поиск по названию или адресу..." />
            </div>
            <div class="max-h-72 overflow-y-auto divide-y divide-gray-100 border border-gray-100 rounded-lg">
              <button
                v-for="o in filteredObjectsForPicker"
                :key="o.id"
                type="button"
                class="w-full text-left px-3 py-2.5 hover:bg-indigo-50 transition-colors"
                @click="pickLocation(o)"
              >
                <div class="text-sm font-medium text-gray-900 truncate">{{ o.name }}</div>
                <div v-if="o.address" class="text-xs text-gray-400 truncate">{{ o.address }}</div>
              </button>
              <div v-if="filteredObjectsForPicker.length === 0" class="px-4 py-6 text-center text-sm text-gray-400">Объекты не найдены</div>
            </div>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Client Picker Modal -->
    <Teleport to="body">
      <div v-if="clientPickerOpen" class="fixed inset-0 z-[60] bg-black/40 backdrop-blur-sm flex items-center justify-center p-4" @click.self="clientPickerOpen = false">
        <div class="bg-white w-full max-w-lg rounded-xl shadow-modal border border-gray-200 overflow-hidden">
          <div class="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
            <div class="font-semibold text-gray-900 text-sm">Выберите клиента (юрлицо)</div>
            <button class="p-2 text-gray-400 hover:text-gray-700 rounded-lg hover:bg-gray-100" @click="clientPickerOpen = false"><X :size="18" /></button>
          </div>
          <div class="p-4 space-y-3">
            <div class="relative">
              <Search class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" :size="16" />
              <input v-model="clientPickerSearch" class="w-full pl-9 pr-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" placeholder="Поиск по юрлицам..." />
            </div>
            <div class="max-h-72 overflow-y-auto divide-y divide-gray-100 border border-gray-100 rounded-lg">
              <button
                v-for="c in filteredCompaniesForPicker"
                :key="c.id"
                type="button"
                class="w-full text-left px-3 py-2.5 hover:bg-indigo-50 transition-colors"
                @click="pickClient(c)"
              >
                <div class="text-sm font-medium text-gray-900 truncate">{{ c.name }}</div>
              </button>
              <div v-if="filteredCompaniesForPicker.length === 0" class="px-4 py-6 text-center text-sm text-gray-400">Юрлица не найдены</div>
            </div>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
