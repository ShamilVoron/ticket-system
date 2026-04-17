<script setup lang="ts">
import { ChevronDown, Minus, ChevronUp, Zap, Paperclip, X } from 'lucide-vue-next'
import { useAuthStore } from '~/stores/auth'

const router = useRouter()
const api = useApi()
const auth = useAuthStore()
const staffPerm = useStaffPermissions()
const toast = useToast()
const pageHeader = usePageHeader()

onMounted(() => pageHeader.set('Новая заявка', true))
onUnmounted(() => pageHeader.clear())

type ClientOption   = { id: number; name: string }
type ObjectOption   = { id: number; name: string; address: string; clientId: number | null; maintenanceStatus?: string }
type EmployeeOption = { userId: string; fullName: string; role: string }
type EquipmentOption = {
  id: number
  tab: string
  equipmentType: string
  fundStatus: string
  name: string
  serialNumber: string
  clientName: string
  location: string
  faults: string
  notes: string
  status?: string
}

// ─── Static data ─────────────────────────────────────────────────────────────
type DepartmentOption = { value: string; label: string; desc: string }

/** Fallback, если API недоступен — синхронизировать с DepartmentsController на бэкенде */
const DEPARTMENTS_FALLBACK: DepartmentOption[] = [
  { value: 'Координатор',            label: 'Координатор',            desc: 'Распределение, эскалации' },
  { value: '1 линия',                label: '1 линия',                desc: 'Приём, консультации' },
  { value: '2 линия',                label: '2 линия',                desc: 'Сложные вопросы' },
  { value: 'Разработчики',           label: 'Разработчики',           desc: 'Доработки, баги' },
  { value: 'Выездные инженеры',      label: 'Выездные инженеры',      desc: 'Выезд, монтаж' },
  { value: 'Ремонт / сервис',        label: 'Ремонт / сервис',        desc: 'Подменки, склад, сервисный центр' },
  { value: 'Бухгалтерия',            label: 'Бухгалтерия',            desc: 'Счета, акты' },
  { value: 'Закупки',                label: 'Закупки',                desc: 'Внешние поставки, контрагенты' },
  { value: 'Системный администратор', label: 'Системный администратор', desc: 'Инфраструктура, сервера, сеть' },
]

const departments = ref<DepartmentOption[]>([...DEPARTMENTS_FALLBACK])

const REQUEST_TYPES = [
  'Ремонт', 'Подменное оборудование', 'Монтаж / Подключение', 'Поломка',
  'Настройка ПО', 'Настройка оборудования', 'Помощь с ПО', 'Сеть / Интернет',
  'Доступы', 'Консультация', 'Плановое ТО', 'Разработка / Доработка',
  'Документы / Счёт', 'Другое',
]

const PRIORITIES = [
  { value: 'Низкий', short: 'Низкий', hex: '#9ca3af', barColor: '#d1d5db', barClass: 'h-8', icon: ChevronDown, hint: 'ОС по задаче, косметика — не блокирует работу.' },
  { value: 'Средний', short: 'Средний', hex: '#6b7280', barColor: '#6b7280', barClass: 'h-12', icon: Minus, hint: 'Работать можно, но сильно мешает.' },
  { value: 'Высокий', short: 'Высокий', hex: '#eab308', barColor: '#eab308', barClass: 'h-16', icon: ChevronUp, hint: 'Работать невозможно или высокий риск простоя.' },
  { value: 'Критический', short: 'Критич.', hex: '#ef4444', barColor: '#ef4444', barClass: 'h-[5.25rem]', icon: Zap, hint: 'Работа остановлена, сервис недоступен.' },
]

const ROLE_LABEL: Record<string, string> = {
  coordinator:    'Координатор',
  super_admin:    'Супер-администратор',
  director:       'Директор',
  support:        '1 линия / Поддержка',
  support_l1:     'Сапорт 1 линия',
  support_l2:     'Сапорт 2 линия',
  field_engineer: 'Выездной инженер',
  head_engineers: 'Нач. отдела инженеров',
  head_support:   'Нач. отдела сапорта',
  head_dev:       'Нач. отдела разработки',
  sysadmin:       'Системный администратор',
  developer:      'Разработчик',
  accountant:     'Бухгалтерия',
  procurement:    'Закупки',
  head_repair:    'Нач. отдела ремонта',
  agent:          'Агент',
}

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
  other: 'Другое',
}

// ─── Data ─────────────────────────────────────────────────────────────────────
const clients    = ref<ClientOption[]>([])
const allObjects = ref<ObjectOption[]>([])
const employees  = ref<EmployeeOption[]>([])
const equipment  = ref<EquipmentOption[]>([])
const loading    = ref(false)
const success    = ref('')
const error      = ref('')

// ─── Form state ───────────────────────────────────────────────────────────────
const form = reactive({
  title:        '',
  requestType:  'Другое',
  softwareName: '',
  priority:     'Средний',
  department:   'Координатор',
  desiredAt:    '',
  details:      '',
  clientId:     0,
  objectId:     0,
  assignees:    [] as string[],

  isRepair:      false,
  equipmentType: '',
  equipmentId:   0,
  repairType:    '',
  repairCost:    '',
  repairFaults:  '',
  repairNotes:   '',
})

const attachedFiles = ref<File[]>([])
const fileInputRef = ref<HTMLInputElement | null>(null)
const handleFileChange = (e: Event) => {
  const target = e.target as HTMLInputElement
  if (target.files) {
    attachedFiles.value = [...attachedFiles.value, ...Array.from(target.files)]
  }
}
const removeFile = (index: number) => {
  attachedFiles.value.splice(index, 1)
}

type TaskLinkRow = { url: string; number: string; comment: string }

const isFieldEngineerDept = computed(() => form.department === 'Выездные инженеры')
/** Все сотрудники с формой заявки (не клиенты); canCreateTicket у многих линий выключен по правам */
const showCoordinatorExtras = computed(() => auth.isStaff && !isFieldEngineerDept.value)
const isRepairDept       = computed(() => form.department === 'Ремонт / сервис')

const showClientSection  = computed(() => !isFieldEngineerDept.value)
const showObjectSection  = computed(() => !isFieldEngineerDept.value)
/** Ремонт с привязкой к складу — только сотрудники (у клиента нет доступа к /api/Equipment). */
const showRepairSection  = computed(() => isRepairDept.value && auth.isStaff)
const showBriefSection   = computed(() => isFieldEngineerDept.value)
const showRequestType    = computed(() => !isFieldEngineerDept.value)
const showDesiredDate    = computed(() => !isFieldEngineerDept.value)

const useEngineerBrief = ref(false)

// ─── Brief: constants ────────────────────────────────────────────────────────
const BRIEF_TASK_TYPES = [
  'Ремонт',
  'Замена оборудования',
  'Предоставление подменного оборудования',
  'Настройка сервера',
  'Документы',
  'Другое',
]

// label → DB equipmentType slugs that match this category
const BRIEF_EQUIPMENT_MAP: { label: string; slugs: string[] }[] = [
  { label: 'Моноблок',     slugs: ['monoblok'] },
  { label: 'Миникомпьютер', slugs: ['minipc'] },
  { label: 'Ноутбук',      slugs: ['notebook'] },
  { label: 'Принтер',      slugs: ['printer'] },
  { label: 'Блок питания',  slugs: ['psu'] },
  { label: 'SSD / HDD',    slugs: ['ssd'] },
]
const BRIEF_EQUIPMENT_LABELS = BRIEF_EQUIPMENT_MAP.map(m => m.label)

// ─── Brief: reactive state ───────────────────────────────────────────────────
const brief = reactive({
  objectAddress: '',
  objectVenueExtra: '',
  contactPhones: '',
  agreedWith: '',
  taskLinks: [{ url: '', number: '', comment: '' }] as TaskLinkRow[],
})

const addTaskLinkRow = () => brief.taskLinks.push({ url: '', number: '', comment: '' })
const removeTaskLinkRow = (i: number) => {
  if (brief.taskLinks.length > 1) brief.taskLinks.splice(i, 1)
}

function extractTaskNumber(url: string): string {
  if (!url) return ''
  const u = url.trim()
  // example url  or  https://helpmerest.okdesk.ru//issues/98809
  const issuesMatch = u.match(/\/issues\/(\d+)/)
  if (issuesMatch) return issuesMatch[1]
  // example url
  const browseMatch = u.match(/\/browse\/([A-Z0-9]+-\d+)/)
  if (browseMatch) return browseMatch[1]
  return ''
}

function onTaskUrlBlur(row: TaskLinkRow) {
  if (row.url && !row.number) {
    const num = extractTaskNumber(row.url)
    if (num) row.number = num
  }
}

// Brief: task type selector (replaces free text banner)
const briefTaskType = ref('')
const briefTaskTypeOpen = ref(false)

// Brief: object selector
const briefObjectId = ref(0)
const briefObjectSearch = ref('')
const briefObjectOpen = ref(false)

const filteredBriefObjects = computed(() => {
  const q = briefObjectSearch.value.trim().toLowerCase()
  if (!q) return allObjects.value
  return allObjects.value.filter(o =>
    o.name.toLowerCase().includes(q) || (o.address || '').toLowerCase().includes(q)
  )
})
const briefSelectedObject = computed(() => allObjects.value.find(o => o.id === briefObjectId.value))
const briefAutoLegalEntity = computed(() => {
  const obj = briefSelectedObject.value
  if (!obj?.clientId) return ''
  return clients.value.find(c => c.id === obj.clientId)?.name ?? ''
})

// Address: auto-fill + override
const briefAddressEditing = ref(false)
const briefAddressOriginal = ref('')

watch(briefObjectId, (id) => {
  const obj = allObjects.value.find(o => o.id === id)
  form.objectId = id || 0
  if (obj?.clientId) form.clientId = obj.clientId
  const addr = obj?.address ?? ''
  brief.objectAddress = addr
  briefAddressOriginal.value = addr
  briefAddressEditing.value = false
})
const resetBriefAddress = () => {
  brief.objectAddress = briefAddressOriginal.value
  briefAddressEditing.value = false
}

// Phone: override
const briefPhoneEditing = ref(false)

// Brief: equipment rows (type → specific item from DB)
type BriefEquipRow = { type: string; equipmentId: number; equipmentName: string; serialNumber: string }
const briefEquipRows = ref<BriefEquipRow[]>([])
const briefEquipAddingType = ref('')
const briefEquipAddingTypeOpen = ref(false)
const briefEquipAddingItemSearch = ref('')
const briefEquipAddingItemOpen = ref(false)

const filteredBriefEquipTypes = computed(() => {
  const q = briefEquipAddingType.value.trim().toLowerCase()
  if (!q) return BRIEF_EQUIPMENT_LABELS
  return BRIEF_EQUIPMENT_LABELS.filter(t => t.toLowerCase().includes(q))
})

const replacementFundEquipment = computed(() =>
  equipment.value.filter(e => e.tab === 'replacement_fund' && e.status !== 'выдано')
)

const filteredBriefEquipItems = computed(() => {
  const mapping = BRIEF_EQUIPMENT_MAP.find(m => m.label === briefEquipAddingType.value)
  const slugs = mapping?.slugs ?? []
  const q = briefEquipAddingItemSearch.value.trim().toLowerCase()
  const usedIds = new Set(briefEquipRows.value.map(r => r.equipmentId))
  let list = replacementFundEquipment.value.filter(e => !usedIds.has(e.id))
  if (slugs.length) list = list.filter(e => slugs.includes(e.equipmentType))
  if (q) list = list.filter(e =>
    [e.name, e.serialNumber, e.clientName, e.location].join(' ').toLowerCase().includes(q)
  )
  return list
})

const selectBriefEquipType = (type: string) => {
  briefEquipAddingType.value = type
  briefEquipAddingTypeOpen.value = false
  briefEquipAddingItemSearch.value = ''
  briefEquipAddingItemOpen.value = true
}
const selectBriefEquipItem = (item: EquipmentOption) => {
  briefEquipRows.value.push({
    type: briefEquipAddingType.value,
    equipmentId: item.id,
    equipmentName: item.name,
    serialNumber: item.serialNumber || '',
  })
  briefEquipAddingType.value = ''
  briefEquipAddingItemSearch.value = ''
  briefEquipAddingItemOpen.value = false
}
const removeBriefEquipRow = (idx: number) => { briefEquipRows.value.splice(idx, 1) }

// Brief: task owner (employee selector)
const briefTaskOwnerId = ref('')
const briefTaskOwnerSearch = ref('')
const briefTaskOwnerOpen = ref(false)

const filteredBriefOwners = computed(() => {
  const q = briefTaskOwnerSearch.value.trim().toLowerCase()
  if (!q) return employees.value
  return employees.value.filter(e => e.fullName.toLowerCase().includes(q))
})
const briefTaskOwner = computed(() => employees.value.find(e => e.userId === briefTaskOwnerId.value))

// Brief / coordinator extras: who knows (userId[], popup)
const briefKnowledgeable = ref<string[]>([])
const briefKnowledgeSearch = ref('')
const briefKnowledgeOpen = ref(false)

const filteredBriefKnowledge = computed(() => {
  const q = briefKnowledgeSearch.value.trim().toLowerCase()
  if (!q) return employees.value
  return employees.value.filter(e => e.fullName.toLowerCase().includes(q))
})

// Brief: checkboxes
const briefActRequired = ref(false)
const briefExtraDiag = ref(false)

// Brief: deadline with validation
const briefDeadlineStart = ref('')
const briefDeadlineEnd = ref('')
const briefDeadlineError = ref('')

watch([briefDeadlineStart, briefDeadlineEnd], ([s, e]) => {
  const now = new Date()
  if (s && new Date(s) < now && (new Date(s).getTime() < now.getTime() - 60000)) {
    briefDeadlineError.value = 'Начало диапазона не может быть в прошлом'
    return
  }
  if (s && e && new Date(e) < new Date(s)) {
    briefDeadlineError.value = 'Конец диапазона не может быть раньше начала'
    return
  }
  briefDeadlineError.value = ''
})

function buildTaskLinksForPayload () {
  return brief.taskLinks
    .filter(l => l.url.trim() || l.number.trim() || (l.comment && l.comment.trim()))
    .map(l => ({
      url: l.url.trim(),
      number: l.number.trim(),
      comment: (l.comment || '').trim(),
    }))
}

function buildCoordinatorBriefJson (): string | undefined {
  if (!auth.isStaff) return undefined

  const links = buildTaskLinksForPayload()
  const knowledgeableUserIds = briefKnowledgeable.value.filter(id => id && id.trim())

  if (!showBriefSection.value) {
    if (links.length === 0 && knowledgeableUserIds.length === 0) return undefined
    return JSON.stringify({ taskLinks: links, knowledgeableUserIds })
  }

  const equipmentList = briefEquipRows.value.map(r => ({
    type: r.type, equipmentId: r.equipmentId,
    equipmentName: r.equipmentName, serialNumber: r.serialNumber,
  }))
  const payload: Record<string, unknown> = {
    taskType: briefTaskType.value,
    objectId: briefObjectId.value || null,
    objectName: briefSelectedObject.value?.name ?? '',
    objectAddress: brief.objectAddress.trim(),
    objectVenueExtra: brief.objectVenueExtra.trim(),
    legalEntity: briefAutoLegalEntity.value,
    contactPhones: brief.contactPhones.trim(),
    taskLinks: links,
    taskOwnerNote: briefTaskOwner.value?.fullName ?? '',
    agreedWith: brief.agreedWith.trim(),
    equipment: equipmentList,
    knowledgeableUserIds,
    actRequired: briefActRequired.value,
    extraDiagnostics: briefExtraDiag.value,
    deadlineStart: briefDeadlineStart.value,
    deadlineEnd: briefDeadlineEnd.value,
  }
  const has = links.length > 0 ||
    briefTaskType.value.length > 0 ||
    briefObjectId.value > 0 ||
    equipmentList.length > 0 ||
    knowledgeableUserIds.length > 0 ||
    briefActRequired.value || briefExtraDiag.value ||
    briefDeadlineStart.value.length > 0 ||
    [brief.objectAddress, brief.objectVenueExtra,
     brief.contactPhones, brief.agreedWith].some(s => s.trim().length > 0) ||
    (briefTaskOwner.value?.fullName ?? '').length > 0
  if (!has) return undefined
  return JSON.stringify(payload)
}

function resetBriefForm () {
  briefTaskType.value = ''
  brief.objectAddress = ''
  brief.objectVenueExtra = ''
  brief.contactPhones = ''
  brief.agreedWith = ''
  brief.taskLinks = [{ url: '', number: '', comment: '' }]
  briefObjectId.value = 0
  briefObjectSearch.value = ''
  briefAddressEditing.value = false
  briefAddressOriginal.value = ''
  briefPhoneEditing.value = false
  briefEquipRows.value = []
  briefEquipAddingType.value = ''
  briefEquipAddingItemSearch.value = ''
  briefTaskOwnerId.value = ''
  briefTaskOwnerSearch.value = ''
  briefKnowledgeable.value = []
  briefKnowledgeSearch.value = ''
  briefKnowledgeOpen.value = false
  briefActRequired.value = false
  briefExtraDiag.value = false
  briefDeadlineStart.value = ''
  briefDeadlineEnd.value = ''
  briefDeadlineError.value = ''
}

// ─── Searchable dropdown state ─────────────────────────────────────────────────
const clientSearch  = ref('')
const objectSearch  = ref('')
const deptSearch    = ref('')
const typeSearch    = ref('')
const assigneeSearch = ref('')
const equipmentSearch = ref('')
const equipmentTypeSearch = ref('')

const clientOpen  = ref(false)
const objectOpen  = ref(false)
const deptOpen    = ref(false)
const typeOpen    = ref(false)
const equipmentOpen = ref(false)
const equipmentTypeOpen = ref(false)

/** Объект из полного каталога (модалка), только для сотрудников */
const coordObjectModalOpen = ref(false)
const coordObjectModalSearch = ref('')

const filteredCoordModalObjects = computed(() => {
  let list = allObjects.value
  if (form.clientId) {
    list = list.filter(o => o.clientId != null && o.clientId === form.clientId)
  }
  const q = coordObjectModalSearch.value.trim().toLowerCase()
  if (!q) return list
  return list.filter(o => {
    const clientName = o.clientId ? (clients.value.find(c => c.id === o.clientId)?.name ?? '') : ''
    const hay = `${o.name} ${o.address ?? ''} ${clientName}`.toLowerCase()
    return hay.includes(q)
  })
})

const coordObjectLegalLabel = computed(() => {
  const o = selectedObject.value
  if (!o?.clientId) return ''
  return clients.value.find(c => c.id === o.clientId)?.name ?? ''
})

/** Бейдж «Юрлицо» только если в поле «Клиент» ещё не отражено то же юрлицо */
const showCoordObjectLegalBadge = computed(() => {
  const o = selectedObject.value
  if (!o?.clientId || !coordObjectLegalLabel.value) return false
  return form.clientId !== o.clientId
})

function selectCoordObject (o: ObjectOption | null) {
  if (!o) {
    form.objectId = 0
  } else {
    if (o.clientId) form.clientId = o.clientId
    form.objectId = o.id
  }
  coordObjectModalOpen.value = false
  coordObjectModalSearch.value = ''
}

// ─── Computed ─────────────────────────────────────────────────────────────────
const selectedClient  = computed(() => clients.value.find(c => c.id === form.clientId))
const selectedObject  = computed(() => allObjects.value.find(o => o.id === form.objectId))
const selectedEquipment = computed(() => equipment.value.find(e => e.id === form.equipmentId))
const selectedDept    = computed(() => departments.value.find(d => d.value === form.department))
const requiresSoftware = computed(() => ['Помощь с ПО', 'Настройка ПО'].includes(form.requestType))

const equipmentTypes = computed(() => {
  const s = new Set<string>()
  for (const e of equipment.value) {
    if (e.equipmentType?.trim()) s.add(e.equipmentType.trim())
  }
  const arr = Array.from(s).sort((a, b) => {
    const ta = EQUIP_TYPE_RU[a.toLowerCase()] || a
    const tb = EQUIP_TYPE_RU[b.toLowerCase()] || b
    return ta.localeCompare(tb, 'ru')
  })
  if (!arr.includes('other')) arr.push('other')
  return arr
})

const filteredEquipmentTypes = computed(() => {
  const q = equipmentTypeSearch.value.trim().toLowerCase()
  if (!q) return equipmentTypes.value
  return equipmentTypes.value.filter(t => {
    const label = EQUIP_TYPE_RU[t.toLowerCase()] || t
    return label.toLowerCase().includes(q)
  })
})

function selectEquipmentType(type: string) {
  form.equipmentType = type
  form.equipmentId = 0
  equipmentTypeOpen.value = false
  equipmentTypeSearch.value = ''
}

const filteredClients = computed(() => {
  const q = clientSearch.value.trim().toLowerCase()
  return q ? clients.value.filter(c => c.name.toLowerCase().includes(q)) : clients.value
})

const filteredObjects = computed(() => {
  const base = allObjects.value.filter(o => !o.clientId || o.clientId === form.clientId)
  const q = objectSearch.value.trim().toLowerCase()
  return q ? base.filter(o => o.name.toLowerCase().includes(q)) : base
})

const filteredDepts = computed(() => {
  const list = departments.value
  const q = deptSearch.value.trim().toLowerCase()
  return q ? list.filter(d => d.label.toLowerCase().includes(q) || d.desc.toLowerCase().includes(q)) : list
})

const filteredTypes = computed(() => {
  const q = typeSearch.value.trim().toLowerCase()
  return q ? REQUEST_TYPES.filter(t => t.toLowerCase().includes(q)) : REQUEST_TYPES
})

const filteredEquipment = computed(() => {
  const q = equipmentSearch.value.trim().toLowerCase()
  let base = equipment.value
  if (form.equipmentType) {
    base = base.filter(e => e.equipmentType === form.equipmentType)
  }
  if (!q) return base
  return base.filter(e =>
    [e.name, e.serialNumber, e.clientName, e.location, e.fundStatus]
      .join(' ')
      .toLowerCase()
      .includes(q)
  )
})

const employeeGroups = computed(() => {
  const q = assigneeSearch.value.trim().toLowerCase()
  let list = employees.value
  if (q) list = list.filter(e => e.fullName.toLowerCase().includes(q))
  const groups: Record<string, EmployeeOption[]> = {}
  for (const emp of list) {
    const key = ROLE_LABEL[emp.role] ?? emp.role
    if (!groups[key]) groups[key] = []
    groups[key].push(emp)
  }
  return groups
})

// ─── Watchers ─────────────────────────────────────────────────────────────────
watch(() => form.clientId, (newCid) => {
  const obj = allObjects.value.find(x => x.id === form.objectId)
  if (obj && obj.clientId === newCid) return
  form.objectId = 0
  objectSearch.value = ''
})

const resolveAssigneeName = (id: string) => {
  const emp = employees.value.find(e => e.userId === id)
  return emp?.fullName ?? id
}

watch(() => form.department, (d) => {
  if (d === 'Выездные инженеры') {
    useEngineerBrief.value = true
    form.isRepair = false
  } else if (d === 'Ремонт / сервис' && auth.isStaff) {
    form.isRepair = true
    useEngineerBrief.value = false
  } else {
    useEngineerBrief.value = false
    form.isRepair = false
  }
})

// Close dropdowns on outside click
const closeAll = () => {
  clientOpen.value = false
  objectOpen.value = false
  deptOpen.value   = false
  typeOpen.value   = false
  equipmentOpen.value = false
  coordObjectModalOpen.value = false
  briefTaskTypeOpen.value = false
  briefObjectOpen.value = false
  briefEquipAddingTypeOpen.value = false
  briefEquipAddingItemOpen.value = false
  briefTaskOwnerOpen.value = false
  briefKnowledgeOpen.value = false
}
onMounted(() => document.addEventListener('click', closeAll))
onUnmounted(() => document.removeEventListener('click', closeAll))

// ─── Load data ─────────────────────────────────────────────────────────────────
onMounted(async () => {
  if (import.meta.client && auth.isStaff) {
    await staffPerm.refresh()
    if (!staffPerm.can('newTicketVisible')) {
      toast.warning('Нет доступа к разделу «Новая заявка»')
      await router.replace('/')
      return
    }
  }
  try {
    if (auth.isStaff) {
      const [companies, objs, emps, depts, eq] = await Promise.all([
        api.companies.getAll(),
        api.serviceObjects.getAll(),
        api.employees.getAll(),
        api.departments.getAll().catch((): DepartmentOption[] => []),
        api.equipment.getAll().catch((): EquipmentOption[] => []),
      ])
      clients.value = companies.map(c => ({ id: c.id, name: c.name }))
      allObjects.value = objs.map(o => ({
        id: o.id,
        name: o.name,
        address: o.address ?? '',
        clientId: o.clientId ?? null,
        maintenanceStatus: o.maintenanceStatus,
      }))
      employees.value = emps.map((e: EmployeeOption) => ({
        userId: e.userId,
        fullName: e.fullName,
        role: e.role,
      }))
      equipment.value = eq as EquipmentOption[]
      if (Array.isArray(depts) && depts.length > 0) departments.value = depts
      if (clients.value.length && !form.clientId) form.clientId = clients.value[0].id
      const selfInList = employees.value.find(e => e.userId === auth.userId)
        || employees.value.find(e => e.fullName === auth.fullName)
      if (selfInList) form.assignees = [selfInList.userId]
    } else {
      const [ctx, objs] = await Promise.all([
        api.auth.ticketContext(),
        api.clientPortal.getServiceObjects(),
      ])
      if (ctx.companyId) {
        form.clientId = ctx.companyId
        clients.value = [{ id: ctx.companyId, name: ctx.companyName || 'Моя организация' }]
      } else {
        clients.value = []
      }
      allObjects.value = objs.map(o => ({
        id: o.id,
        name: o.name,
        address: o.address ?? '',
        clientId: o.clientId ?? null,
        maintenanceStatus: o.maintenanceStatus,
      }))
      employees.value = []
      equipment.value = []
    }
  } catch {
    toast.error('Не удалось загрузить справочники для формы')
  }
})

// ─── Submit ───────────────────────────────────────────────────────────────────
const submit = async () => {
  if (auth.isStaff && !staffPerm.can('newTicketCreate')) {
    toast.error('Нет права создавать заявки')
    return
  }
  if (!form.title.trim()) { error.value = 'Введите тему заявки'; return }
  if (isRepairDept.value && auth.isStaff) form.isRepair = true
  if (form.isRepair && !form.equipmentType) { error.value = 'Выберите категорию оборудования'; return }
  success.value = ''
  error.value   = ''
  loading.value = true
  try {
    const coordJson = buildCoordinatorBriefJson()
    const body: Record<string, unknown> = {
      title:        form.title,
      requestType:  briefTaskType.value || form.requestType,
      softwareName: requiresSoftware.value ? form.softwareName : '',
      priority:     form.priority,
      department:   form.department,
      details:      form.details,
      desiredAt:    form.desiredAt ? new Date(form.desiredAt).toISOString() : null,
      clientId:     form.clientId || null,
      objectId:     form.objectId || null,
      assignees:    auth.isStaff ? form.assignees : [],
      createdByRole: auth.role || '',
    }
    if (coordJson) body.coordinatorBriefJson = coordJson
    if (form.isRepair) {
      body.isRepair = true
      body.equipmentType = form.equipmentType
      body.equipmentTypeLabel = form.equipmentType ? (EQUIP_TYPE_RU[form.equipmentType.toLowerCase()] || form.equipmentType) : null
      body.equipmentId = form.equipmentId || null
      body.repairType = form.repairType
      body.repairCost = form.repairCost ? Number(form.repairCost) : null
      body.repairFaults = form.repairFaults
      body.repairNotes = form.repairNotes
    }

    const created = await api.tickets.create(body) as { id: number }
    success.value = `Заявка #${created.id} создана!`
    form.title = ''
    form.softwareName = ''
    form.details = ''
    form.objectId = 0
    form.assignees = []
    form.isRepair = false
    form.equipmentType = ''
    form.equipmentId = 0
    form.repairType = ''
    form.repairCost = ''
    form.repairFaults = ''
    form.repairNotes = ''
    resetBriefForm()

    // ─── Upload Attachments ──────────────────────────────────────────────────
    if (attachedFiles.value.length > 0) {
      try {
        for (const file of attachedFiles.value) {
          const formData = new FormData()
          formData.append('file', file)
          formData.append('uploadedBy', auth.fullName)
          await api.tickets.uploadAttachment(created.id, formData)
        }
        attachedFiles.value = []
      } catch {
        toast.warning('Заявка создана, но некоторые файлы не загрузились')
      }
    }

    setTimeout(() => {
      router.push(`/tickets/${created.id}`)
    }, 1200)
  } catch (e: any) {
    error.value = e?.data?.error || e?.response?._data?.error || 'Не удалось создать заявку'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="new-ticket-page w-full max-w-none min-w-0">
    <div class="rounded-2xl border border-gray-200 dark:border-zinc-700/80 bg-white dark:bg-[#121214] shadow-sm dark:shadow-none overflow-hidden">
      <form @submit.prevent="submit" class="px-4 sm:px-6 md:px-8 py-6 sm:py-8">
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-8 lg:gap-10 lg:items-start">
          <div :class="['space-y-6 min-w-0', auth.isStaff ? 'lg:col-span-8' : 'lg:col-span-12']">
            <section class="rounded-2xl border border-gray-200 dark:border-zinc-700/70 bg-gray-50/50 dark:bg-zinc-900/35 p-4 sm:p-5 space-y-4">
              <h2 class="text-[11px] font-bold text-gray-500 dark:text-gray-400 uppercase tracking-widest">Основное</h2>
              <p v-if="!auth.isStaff" class="text-xs text-gray-600 dark:text-gray-400 -mt-2 mb-0.5 leading-relaxed">
                Опишите обращение — с вами свяжутся из Ticket System.
              </p>
              <div>
                <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">Тема заявки <span class="text-red-500">*</span></label>
                <input
                  v-model="form.title"
                  required
                  placeholder="Кратко опишите проблему..."
                  class="w-full border border-gray-200 dark:border-zinc-600 rounded-xl px-3 py-2.5 text-sm bg-white dark:bg-[#141416] focus:outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500/30"
                />
              </div>
              <div v-if="!auth.isStaff" class="relative" @click.stop>
                <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">Отдел заявки</label>
          <button
            type="button"
            class="new-ticket-select w-full border border-gray-200 rounded-lg px-3 py-2 text-sm bg-white text-left flex items-center justify-between hover:border-green-400 focus:outline-none focus:border-green-400 transition-colors"
            @click="deptOpen = !deptOpen; clientOpen = objectOpen = typeOpen = equipmentOpen = equipmentTypeOpen = false"
          >
            <div class="min-w-0 truncate">
              <span class="font-medium">{{ selectedDept?.label }}</span>
              <span class="text-gray-600 text-xs ml-1 font-medium hidden sm:inline">{{ selectedDept?.desc }}</span>
            </div>
            <svg class="w-4 h-4 text-gray-400 flex-shrink-0 ml-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
            </svg>
          </button>
          <div v-if="deptOpen" class="absolute z-50 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg overflow-hidden">
            <div class="p-2 border-b border-gray-100">
              <input
                v-model="deptSearch"
                type="text"
                placeholder="Поиск отдела..."
                class="w-full text-sm px-2 py-1.5 border border-gray-200 rounded focus:outline-none focus:border-green-400"
                @click.stop
              />
            </div>
            <div class="max-h-[min(70vh,22rem)] overflow-y-auto overscroll-contain">
              <button
                v-for="d in filteredDepts"
                :key="d.value"
                type="button"
                class="w-full text-left px-3 py-2 text-sm hover:bg-green-50 transition-colors"
                :class="form.department === d.value ? 'bg-green-50 text-green-700 font-medium' : 'text-gray-700'"
                @click.stop="form.department = d.value; deptOpen = false; deptSearch = ''"
              >
                <span class="font-medium">{{ d.label }}</span>
                <span class="text-gray-600 text-xs ml-1 font-medium">— {{ d.desc }}</span>
              </button>
              <div v-if="filteredDepts.length === 0" class="px-3 py-2 text-sm text-gray-400 italic">Ничего не найдено</div>
            </div>
          </div>
        </div>

      <!-- Тип + срок (колонка) | Клиент + объект (колонка) -->
      <div
        v-if="showRequestType || showDesiredDate || showClientSection || showObjectSection"
        class="grid grid-cols-1 sm:grid-cols-2 gap-4 lg:items-start"
      >
        <div class="space-y-4 min-w-0">
          <!-- Тип обращения (searchable) -->
          <div v-if="showRequestType" class="relative" @click.stop>
            <label class="block text-sm font-medium text-gray-700 mb-1">Тип обращения</label>
            <button
              type="button"
              class="new-ticket-select w-full border border-gray-200 rounded-lg px-3 py-2 text-sm bg-white text-left flex items-center justify-between hover:border-green-400 focus:outline-none focus:border-green-400 transition-colors"
              @click="typeOpen = !typeOpen; clientOpen = objectOpen = deptOpen = equipmentOpen = equipmentTypeOpen = false"
            >
              <span>{{ form.requestType }}</span>
              <svg class="w-4 h-4 text-gray-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
              </svg>
            </button>
            <div v-if="typeOpen" class="absolute z-50 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg overflow-hidden">
              <div class="p-2 border-b border-gray-100">
                <input
                  v-model="typeSearch"
                  type="text"
                  placeholder="Поиск типа..."
                  class="w-full text-sm px-2 py-1.5 border border-gray-200 rounded focus:outline-none focus:border-green-400"
                  @click.stop
                />
              </div>
              <div class="max-h-48 overflow-y-auto">
                <button
                  v-for="t in filteredTypes"
                  :key="t"
                  type="button"
                  class="w-full text-left px-3 py-2 text-sm hover:bg-green-50 transition-colors"
                  :class="form.requestType === t ? 'bg-green-50 text-green-700 font-medium' : 'text-gray-700'"
                  @click.stop="form.requestType = t; typeOpen = false; typeSearch = ''"
                >{{ t }}</button>
                <div v-if="filteredTypes.length === 0" class="px-3 py-2 text-sm text-gray-400 italic">Ничего не найдено</div>
              </div>
            </div>
          </div>

          <div v-if="showDesiredDate" class="min-w-0">
            <label class="block text-sm font-medium text-gray-700 mb-1">Желаемый срок выполнения</label>
            <input
              v-model="form.desiredAt"
              type="datetime-local"
              class="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-green-400"
            />
          </div>
        </div>

        <div class="space-y-4 min-w-0">
        <!-- Клиент (searchable) -->
        <div v-if="showClientSection" class="relative" @click.stop>
          <label class="block text-sm font-medium text-gray-700 mb-1">Клиент</label>
          <button
            type="button"
            class="new-ticket-select w-full border border-gray-200 rounded-lg px-3 py-2 text-sm bg-white text-left flex items-center justify-between hover:border-green-400 focus:outline-none focus:border-green-400 transition-colors"
            @click="clientOpen = !clientOpen; typeOpen = objectOpen = deptOpen = equipmentOpen = equipmentTypeOpen = false"
          >
            <span class="truncate">{{ selectedClient?.name ?? '— Выберите клиента —' }}</span>
            <svg class="w-4 h-4 text-gray-400 flex-shrink-0 ml-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
            </svg>
          </button>
          <div v-if="clientOpen" class="absolute z-50 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg overflow-hidden">
            <div class="p-2 border-b border-gray-100">
              <input
                v-model="clientSearch"
                type="text"
                placeholder="Поиск клиента..."
                class="w-full text-sm px-2 py-1.5 border border-gray-200 rounded focus:outline-none focus:border-green-400"
                @click.stop
                autofocus
              />
            </div>
            <div class="max-h-56 overflow-y-auto">
              <button
                v-for="c in filteredClients"
                :key="c.id"
                type="button"
                class="w-full text-left px-3 py-2 text-sm hover:bg-green-50 transition-colors"
                :class="form.clientId === c.id ? 'bg-green-50 text-green-700 font-medium' : 'text-gray-700'"
                @click.stop="form.clientId = c.id; clientOpen = false; clientSearch = ''"
              >{{ c.name }}</button>
              <div v-if="filteredClients.length === 0" class="px-3 py-2 text-sm text-gray-400 italic">Ничего не найдено</div>
            </div>
          </div>
        </div>

        <!-- Объект: сотрудники — модалка со всем каталогом; клиент — список по выбранному юрлицу -->
        <div v-if="showObjectSection" class="relative min-w-0" @click.stop>
          <label class="block text-sm font-medium text-gray-700 mb-1">Объект / Точка</label>
          <template v-if="auth.isStaff">
            <button
              type="button"
              class="new-ticket-select w-full border border-gray-200 rounded-lg px-3 py-2 text-sm bg-white text-left flex items-center justify-between hover:border-green-400 focus:outline-none focus:border-green-400 transition-colors"
              @click="coordObjectModalOpen = true; coordObjectModalSearch = ''"
            >
              <span class="truncate">{{ selectedObject?.name ?? '— Выбрать в каталоге —' }}</span>
              <svg class="w-4 h-4 text-gray-400 flex-shrink-0 ml-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h7"/>
              </svg>
            </button>
            <div v-if="showCoordObjectLegalBadge" class="mt-1.5 flex flex-wrap gap-1.5 text-xs">
              <span class="bg-green-50 text-green-800 border border-green-200 px-2 py-0.5 rounded-full font-medium">Юрлицо: {{ coordObjectLegalLabel }}</span>
            </div>
            <p v-if="selectedObject?.address" class="text-xs text-gray-700 mt-1 font-medium">{{ selectedObject.address }}</p>
          </template>
          <template v-else>
            <button
              type="button"
              class="new-ticket-select w-full border border-gray-200 rounded-lg px-3 py-2 text-sm bg-white text-left flex items-center justify-between hover:border-green-400 focus:outline-none focus:border-green-400 transition-colors"
              @click="objectOpen = !objectOpen; clientOpen = typeOpen = deptOpen = equipmentOpen = equipmentTypeOpen = false"
            >
              <span class="truncate">{{ selectedObject?.name ?? '— Не выбран —' }}</span>
              <svg class="w-4 h-4 text-gray-400 flex-shrink-0 ml-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
              </svg>
            </button>
            <div v-if="objectOpen" class="absolute z-50 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg overflow-hidden">
              <div class="p-2 border-b border-gray-100">
                <input
                  v-model="objectSearch"
                  type="text"
                  placeholder="Поиск объекта..."
                  class="w-full text-sm px-2 py-1.5 border border-gray-200 rounded focus:outline-none focus:border-green-400"
                  @click.stop
                  autofocus
                />
              </div>
              <div class="max-h-56 overflow-y-auto">
                <button
                  type="button"
                  class="w-full text-left px-3 py-2 text-sm hover:bg-green-50 transition-colors"
                  :class="form.objectId === 0 ? 'bg-green-50 text-green-700 font-medium' : 'text-gray-500'"
                  @click.stop="form.objectId = 0; objectOpen = false; objectSearch = ''"
                >— Не выбран —</button>
                <button
                  v-for="o in filteredObjects"
                  :key="o.id"
                  type="button"
                  class="w-full text-left px-3 py-2 text-sm hover:bg-green-50 transition-colors"
                  :class="form.objectId === o.id ? 'bg-green-50 text-green-700 font-medium' : 'text-gray-700'"
                  @click.stop="form.objectId = o.id; objectOpen = false; objectSearch = ''"
                >
                  <span class="font-medium">{{ o.name }}</span>
                  <span v-if="o.maintenanceStatus" class="ml-2 text-xs text-gray-400">{{ o.maintenanceStatus }}</span>
                </button>
                <div v-if="filteredObjects.length === 0" class="px-3 py-2 text-sm text-gray-400 italic">Нет объектов для этого клиента</div>
              </div>
            </div>
            <p v-if="selectedObject?.address" class="text-xs text-gray-700 mt-1 font-medium">{{ selectedObject.address }}</p>
          </template>
        </div>
        </div>
      </div>

      <!-- Ремонт / закупка / продажа оборудования -->
      <div v-if="showRepairSection" class="rounded-xl border border-gray-200 bg-gray-50 p-4 space-y-3">
        <div class="text-sm font-semibold text-gray-800">Оборудование</div>
        <div class="space-y-3">
          <!-- Категория оборудования -->
          <div class="relative" @click.stop>
            <label class="block text-sm font-medium text-gray-700 mb-1">Категория <span class="text-red-500">*</span></label>
            <button
              type="button"
              class="new-ticket-select w-full border border-gray-200 rounded-lg px-3 py-2 text-sm bg-white text-left flex items-center justify-between hover:border-green-400 focus:outline-none focus:border-green-400 transition-colors"
              @click="equipmentTypeOpen = !equipmentTypeOpen; clientOpen = objectOpen = deptOpen = typeOpen = equipmentOpen = false"
            >
              <span class="truncate">
                {{ form.equipmentType ? (EQUIP_TYPE_RU[form.equipmentType.toLowerCase()] || form.equipmentType) : 'Выберите категорию...' }}
              </span>
              <svg class="w-4 h-4 text-gray-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
              </svg>
            </button>
            <div v-if="equipmentTypeOpen" class="absolute z-50 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg overflow-hidden">
              <div class="p-2 border-b border-gray-100">
                <input
                  v-model="equipmentTypeSearch"
                  type="text"
                  placeholder="Поиск категории..."
                  class="w-full text-sm px-2 py-1.5 border border-gray-200 rounded focus:outline-none focus:border-green-400"
                  @click.stop
                />
              </div>
              <div class="max-h-56 overflow-y-auto">
                <button
                  v-for="t in filteredEquipmentTypes"
                  :key="t"
                  type="button"
                  class="w-full text-left px-3 py-2 text-sm hover:bg-green-50 transition-colors"
                  :class="form.equipmentType === t ? 'bg-green-50 text-green-700 font-medium' : 'text-gray-700'"
                  @click.stop="selectEquipmentType(t)"
                >
                  {{ EQUIP_TYPE_RU[t.toLowerCase()] || t }}
                </button>
                <div v-if="filteredEquipmentTypes.length === 0" class="px-3 py-2 text-sm text-gray-400 italic">
                  Ничего не найдено
                </div>
              </div>
            </div>
          </div>

          <!-- Конкретное оборудование -->
          <div v-if="form.equipmentType" class="relative" @click.stop>
            <label class="block text-sm font-medium text-gray-700 mb-1">Позиция</label>
            <button
              type="button"
              class="new-ticket-select w-full border border-gray-200 rounded-lg px-3 py-2 text-sm bg-white text-left flex items-center justify-between hover:border-green-400 focus:outline-none focus:border-green-400 transition-colors"
              @click="equipmentOpen = !equipmentOpen; clientOpen = objectOpen = deptOpen = typeOpen = equipmentTypeOpen = false"
            >
              <span class="truncate">
                {{ selectedEquipment ? `${selectedEquipment.name} — ${selectedEquipment.serialNumber || '—'} — ${selectedEquipment.clientName || '—'} — ${selectedEquipment.location || ''}` : (form.equipmentType ? (EQUIP_TYPE_RU[form.equipmentType.toLowerCase()] || form.equipmentType) : 'Выберите оборудование...') }}
              </span>
              <svg class="w-4 h-4 text-gray-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
              </svg>
            </button>
            <div v-if="equipmentOpen" class="absolute z-50 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg overflow-hidden">
              <div class="p-2 border-b border-gray-100">
                <input
                  v-model="equipmentSearch"
                  type="text"
                  placeholder="Поиск по названию, серийнику, клиенту..."
                  class="w-full text-sm px-2 py-1.5 border border-gray-200 rounded focus:outline-none focus:border-green-400"
                  @click.stop
                />
              </div>
              <div class="max-h-56 overflow-y-auto">
                <button
                  type="button"
                  class="w-full text-left px-3 py-2 text-sm hover:bg-green-50 transition-colors"
                  :class="form.equipmentId === 0 ? 'bg-green-50 text-green-700 font-medium' : 'text-gray-700'"
                  @click.stop="form.equipmentId = 0; equipmentOpen = false; equipmentSearch = ''"
                >
                  <span class="font-semibold">Другое</span>
                </button>
                <button
                  v-for="e in filteredEquipment"
                  :key="e.id"
                  type="button"
                  class="w-full text-left px-3 py-2 text-sm hover:bg-green-50 transition-colors"
                  :class="form.equipmentId === e.id ? 'bg-green-50 text-green-700 font-medium' : 'text-gray-700'"
                  @click.stop="form.equipmentId = e.id; equipmentOpen = false; equipmentSearch = ''; form.repairFaults = e.faults || form.repairFaults; form.repairNotes = e.notes || form.repairNotes"
                >
                  <div class="flex items-center justify-between gap-2">
                    <div class="truncate">
                      <span class="font-semibold">{{ e.name }}</span>
                      <span class="text-gray-400"> • </span>
                      <span class="font-mono text-xs text-gray-500">{{ e.serialNumber || '—' }}</span>
                    </div>
                    <span class="text-xs text-gray-400 whitespace-nowrap">{{ e.clientName || '—' }}</span>
                  </div>
                  <div class="text-xs text-gray-500 mt-0.5 truncate">
                    {{ [e.fundStatus, e.location].filter(Boolean).join(' • ') }}
                  </div>
                </button>
                <div v-if="filteredEquipment.length === 0 && equipmentSearch.trim()" class="px-3 py-2 text-sm text-gray-400 italic">
                  Ничего не найдено
                </div>
              </div>
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div class="sm:col-span-2">
              <label class="block text-sm font-medium text-gray-700 mb-1">Вид работ / причина обращения</label>
              <input v-model="form.repairType" placeholder="Напр. замена платы / диагностика / закупка / продажа..."
                class="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-green-400 focus:ring-1 focus:ring-green-400" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Стоимость</label>
              <input v-model="form.repairCost" inputmode="decimal" placeholder="0"
                class="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-green-400 focus:ring-1 focus:ring-green-400" />
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Неисправности / пожелания</label>
              <textarea v-model="form.repairFaults" rows="2" placeholder="Что сломано / что нужно закупить / пожелания клиента..."
                class="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-green-400 focus:ring-1 focus:ring-green-400 resize-none" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Заметки</label>
              <textarea v-model="form.repairNotes" rows="2" placeholder="Комментарий..."
                class="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-green-400 focus:ring-1 focus:ring-green-400 resize-none" />
            </div>
          </div>
        </div>
      </div>

            </section>

            <section v-if="!auth.isStaff" class="rounded-2xl border border-gray-200 dark:border-zinc-700/70 bg-white dark:bg-[#141416]/80 p-4 sm:p-5">
              <div class="flex items-end justify-center gap-2 sm:gap-3 min-h-[6.5rem] px-1">
                <button
                  v-for="p in PRIORITIES"
                  :key="p.value"
                  type="button"
                  :title="`${p.value}: ${p.hint}`"
                  class="relative w-9 sm:w-10 shrink-0 rounded-full border-2 border-transparent transition-transform focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 dark:focus-visible:ring-offset-zinc-900"
                  :class="[
                    p.barClass,
                    form.priority === p.value
                      ? 'ring-2 ring-gray-800 dark:ring-gray-100 ring-offset-2 ring-offset-white dark:ring-offset-[#141416] z-10'
                      : 'opacity-90 hover:opacity-100'
                  ]"
                  :style="{ backgroundColor: p.barColor }"
                  @click="form.priority = p.value"
                >
                  <span
                    v-if="form.priority === p.value"
                    class="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 w-5 h-5 bg-gray-800 dark:bg-gray-950 flex items-center justify-center rounded-sm shadow-md"
                    aria-hidden="true"
                  >
                    <svg class="w-2.5 h-2.5 text-white" fill="none" stroke="currentColor" stroke-width="3" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M20 6L9 17l-5-5" />
                    </svg>
                  </span>
                </button>
              </div>
              <p class="text-center text-[11px] font-bold text-gray-600 dark:text-gray-400 uppercase tracking-widest mt-3">Приоритеты</p>
            </section>

      <!-- ПО (условно) -->
      <div v-if="requiresSoftware">
        <label class="block text-sm font-medium text-gray-700 mb-1">Какое ПО?</label>
        <input
          v-model="form.softwareName"
          placeholder="1С, Photoshop, Chrome..."
          class="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-green-400"
        />
      </div>


      <!-- Бриф для выездного инженера -->
      <div
        v-if="auth.isStaff && showBriefSection"
        class="border border-amber-200 rounded-xl bg-amber-50/40 p-4 space-y-4"
      >
        <div>
          <span class="text-sm font-semibold text-gray-800">Бриф для выездного инженера</span>
        </div>

        <div class="space-y-4 pt-1 border-t border-amber-100">

          <!-- Тип обращения (dropdown) -->
          <div class="relative" @click.stop>
            <label class="block text-xs font-medium text-gray-600 mb-1">Тип обращения</label>
            <button
              type="button"
              class="new-ticket-select w-full border border-gray-200 rounded-lg px-3 py-2 text-sm bg-white text-left flex items-center justify-between hover:border-green-400 transition-colors"
              @click="briefTaskTypeOpen = !briefTaskTypeOpen"
            >
              <span :class="briefTaskType ? 'text-gray-900' : 'text-gray-400'">{{ briefTaskType || '— Выберите тип —' }}</span>
              <svg class="w-4 h-4 text-gray-400 flex-shrink-0 ml-1" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/></svg>
            </button>
            <div v-if="briefTaskTypeOpen" class="absolute z-50 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg overflow-hidden">
              <button
                v-for="t in BRIEF_TASK_TYPES" :key="t" type="button"
                class="w-full text-left px-3 py-2 text-sm hover:bg-amber-50 transition-colors"
                :class="briefTaskType === t ? 'bg-amber-50 text-amber-700 font-medium' : 'text-gray-700'"
                @click.stop="briefTaskType = t; briefTaskTypeOpen = false"
              >{{ t }}</button>
            </div>
          </div>

          <!-- Объект (searchable dropdown) -->
          <div class="relative" @click.stop>
            <label class="block text-xs font-medium text-gray-600 mb-1">Объект обслуживания</label>
            <button
              type="button"
              class="new-ticket-select w-full border border-gray-200 rounded-lg px-3 py-2 text-sm bg-white text-left flex items-center justify-between hover:border-green-400 transition-colors"
              @click="briefObjectOpen = !briefObjectOpen"
            >
              <span class="truncate">{{ briefSelectedObject?.name ?? '— Выберите объект —' }}</span>
              <svg class="w-4 h-4 text-gray-400 flex-shrink-0 ml-1" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/></svg>
            </button>
            <div v-if="briefObjectOpen" class="absolute z-50 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg overflow-hidden">
              <div class="p-2 border-b border-gray-100">
                <input v-model="briefObjectSearch" type="text" placeholder="Поиск объекта…"
                  class="w-full text-sm px-2 py-1.5 border border-gray-200 rounded focus:outline-none focus:border-green-400" @click.stop autofocus />
              </div>
              <div class="max-h-56 overflow-y-auto">
                <button type="button" class="w-full text-left px-3 py-2 text-sm text-gray-500 hover:bg-green-50"
                  @click.stop="briefObjectId = 0; briefObjectOpen = false; briefObjectSearch = ''">— Не выбран —</button>
                <button v-for="o in filteredBriefObjects" :key="o.id" type="button"
                  class="w-full text-left px-3 py-2 text-sm hover:bg-green-50 transition-colors"
                  :class="briefObjectId === o.id ? 'bg-green-50 text-green-700 font-medium' : 'text-gray-700'"
                  @click.stop="briefObjectId = o.id; form.objectId = o.id; if (o.clientId) form.clientId = o.clientId; briefObjectOpen = false; briefObjectSearch = ''">
                  <span class="font-medium">{{ o.name }}</span>
                  <span v-if="o.address" class="text-xs text-gray-400 ml-1">— {{ o.address }}</span>
                </button>
                <div v-if="filteredBriefObjects.length === 0" class="px-3 py-2 text-sm text-gray-400 italic">Ничего не найдено</div>
              </div>
            </div>
            <div v-if="briefSelectedObject" class="mt-2 flex flex-wrap gap-2 text-xs">
              <span v-if="briefAutoLegalEntity" class="bg-green-50 text-green-700 border border-green-200 px-2 py-0.5 rounded-full font-medium">Юрлицо: {{ briefAutoLegalEntity }}</span>
              <span v-if="briefSelectedObject.maintenanceStatus" class="bg-gray-50 text-gray-600 border border-gray-200 px-2 py-0.5 rounded-full">{{ briefSelectedObject.maintenanceStatus }}</span>
            </div>
          </div>

          <!-- Адрес (auto-fill + replace button) -->
          <div>
            <div class="flex items-center justify-between mb-1">
              <label class="text-xs text-gray-500">Адрес</label>
              <div class="flex items-center gap-2">
                <button v-if="briefSelectedObject?.address && !briefAddressEditing" type="button"
                  class="text-[10px] text-amber-600 hover:text-amber-800 font-medium"
                  @click="briefAddressEditing = true; brief.objectAddress = ''">Заменить</button>
                <button v-if="briefAddressEditing && briefAddressOriginal" type="button"
                  class="text-[10px] text-green-600 hover:text-green-800 font-medium"
                  @click="resetBriefAddress">Сбросить</button>
              </div>
            </div>
            <div v-if="briefSelectedObject?.address && !briefAddressEditing"
              class="w-full border border-green-200 bg-green-50/50 rounded-lg px-3 py-2 text-sm text-gray-800">
              {{ brief.objectAddress }}
            </div>
            <input v-else v-model="brief.objectAddress" placeholder="г. Брест, ул. Машерова, 12"
              class="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-green-400" />
          </div>

          <!-- Доп. о точке -->
          <div>
            <label class="block text-xs text-gray-500 mb-1">Доп. о точке (ТЦ, этаж…)</label>
            <input v-model="brief.objectVenueExtra" placeholder="ТЦ Максимус, 2 этаж"
              class="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-green-400" />
          </div>

          <!-- Телефоны (+ replace button) -->
          <div>
            <div class="flex items-center justify-between mb-1">
              <label class="text-xs text-gray-500">Телефоны менеджера / ЧБР / директора</label>
              <button v-if="brief.contactPhones.trim()" type="button"
                class="text-[10px] text-amber-600 hover:text-amber-800 font-medium"
                @click="brief.contactPhones = ''">Очистить</button>
            </div>
            <input v-model="brief.contactPhones" placeholder="+375…"
              class="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-green-400" />
          </div>

          <!-- Ссылки на таск -->
          <div>
            <div class="flex items-center justify-between mb-1">
              <label class="text-xs font-medium text-gray-600">Ссылки на таск (URL + №)</label>
              <button type="button" class="text-xs text-green-700 hover:underline" @click="addTaskLinkRow">+ строка</button>
            </div>
            <div v-for="(row, idx) in brief.taskLinks" :key="idx" class="space-y-2 mb-3 pb-3 border-b border-amber-100 last:border-0 last:pb-0">
              <div class="flex flex-col sm:flex-row gap-2">
                <input v-model="row.url" type="url" placeholder="https://…"
                  class="flex-1 border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-green-400"
                  @blur="onTaskUrlBlur(row)" />
                <div class="flex gap-2">
                  <input v-model="row.number" placeholder="№ 44444"
                    class="w-full sm:w-28 border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-green-400" />
                  <button v-if="brief.taskLinks.length > 1" type="button" class="text-xs text-red-500 px-2" @click="removeTaskLinkRow(idx)">×</button>
                </div>
              </div>
              <details class="group">
                <summary class="list-none cursor-pointer flex items-center justify-between text-[10px] text-gray-500 hover:text-gray-700 select-none">
                  <span>Комментарий к ссылке</span>
                  <span class="text-gray-400 group-open:hidden">▼</span>
                  <span class="text-gray-400 hidden group-open:inline">▲</span>
                </summary>
                <input v-model="row.comment" placeholder="Контекст, зачем ссылка…"
                  class="w-full mt-1.5 border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-green-400" />
              </details>
            </div>
          </div>

          <!-- Чей таск (employee selector) -->
          <div class="relative" @click.stop>
            <label class="block text-xs font-medium text-gray-600 mb-1">Чей таск (от кого поступила заявка)</label>
            <button type="button"
              class="new-ticket-select w-full border border-gray-200 rounded-lg px-3 py-2 text-sm bg-white text-left flex items-center justify-between hover:border-green-400 transition-colors"
              @click="briefTaskOwnerOpen = !briefTaskOwnerOpen">
              <span class="truncate">{{ briefTaskOwner?.fullName ?? '— Выберите сотрудника —' }}</span>
              <svg class="w-4 h-4 text-gray-400 flex-shrink-0 ml-1" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/></svg>
            </button>
            <div v-if="briefTaskOwnerOpen" class="absolute z-50 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg overflow-hidden">
              <div class="p-2 border-b border-gray-100">
                <input v-model="briefTaskOwnerSearch" type="text" placeholder="Поиск сотрудника…"
                  class="w-full text-sm px-2 py-1.5 border border-gray-200 rounded focus:outline-none focus:border-green-400" @click.stop />
              </div>
              <div class="max-h-48 overflow-y-auto">
                <button v-for="emp in filteredBriefOwners" :key="emp.userId" type="button"
                  class="w-full text-left px-3 py-2 text-sm hover:bg-green-50 transition-colors"
                  :class="briefTaskOwnerId === emp.userId ? 'bg-green-50 text-green-700 font-medium' : 'text-gray-700'"
                  @click.stop="briefTaskOwnerId = emp.userId; briefTaskOwnerOpen = false; briefTaskOwnerSearch = ''">
                  <span>{{ emp.fullName }}</span>
                  <span class="text-xs text-gray-400 ml-1">{{ ROLE_LABEL[emp.role] ?? emp.role }}</span>
                </button>
              </div>
            </div>
          </div>

          <!-- С кем согласован -->
          <div>
            <label class="block text-xs text-gray-500 mb-1">С кем согласован таск</label>
            <input v-model="brief.agreedWith"
              class="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-green-400" />
          </div>

          <!-- Предмет (тип → конкретное оборудование из БД, несколько штук) -->
          <div>
            <label class="block text-xs font-medium text-gray-600 mb-1">Предмет (оборудование, позиции)</label>
            <!-- Added rows -->
            <div v-if="briefEquipRows.length" class="space-y-1.5 mb-3">
              <div v-for="(row, idx) in briefEquipRows" :key="idx"
                class="flex items-center gap-2 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
                <span class="text-xs font-semibold text-amber-700 bg-amber-100 px-1.5 py-0.5 rounded">{{ row.type }}</span>
                <span class="text-sm text-gray-800 flex-1 truncate">{{ row.equipmentName }}</span>
                <span v-if="row.serialNumber" class="text-[10px] text-gray-400 font-mono">{{ row.serialNumber }}</span>
                <button type="button" class="text-amber-500 hover:text-red-500 text-xs ml-1" @click="removeBriefEquipRow(idx)">×</button>
              </div>
            </div>
            <!-- Two-step add: 1) pick type, 2) pick specific item -->
            <div class="space-y-2" @click.stop>
              <!-- Step 1: type selector -->
              <div class="relative">
                <button type="button"
                  class="new-ticket-select w-full border border-dashed border-gray-300 rounded-lg px-3 py-2 text-sm bg-white text-left flex items-center justify-between hover:border-amber-400 transition-colors"
                  @click="briefEquipAddingTypeOpen = !briefEquipAddingTypeOpen; briefEquipAddingItemOpen = false">
                  <span :class="briefEquipAddingType ? 'text-gray-900' : 'text-gray-400'">
                    {{ briefEquipAddingType ? `Тип: ${briefEquipAddingType} — выберите из списка ↓` : '+ Добавить оборудование…' }}
                  </span>
                </button>
                <div v-if="briefEquipAddingTypeOpen" class="absolute z-50 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg max-h-52 overflow-y-auto">
                  <button v-for="t in filteredBriefEquipTypes" :key="t" type="button"
                    class="w-full text-left px-3 py-2 text-sm hover:bg-amber-50 transition-colors text-gray-700"
                    @click.stop="selectBriefEquipType(t)">{{ t }}</button>
                </div>
              </div>
              <!-- Step 2: specific item from DB (after type is selected) -->
              <div v-if="briefEquipAddingType" class="relative">
                <input v-model="briefEquipAddingItemSearch" type="text"
                  :placeholder="`Поиск ${briefEquipAddingType} в базе оборудования…`"
                  class="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-amber-400"
                  @focus="briefEquipAddingItemOpen = true" @click.stop />
                <div v-if="briefEquipAddingItemOpen" class="absolute z-50 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg max-h-56 overflow-y-auto">
                  <button v-for="item in filteredBriefEquipItems" :key="item.id" type="button"
                    class="w-full text-left px-3 py-2 text-sm hover:bg-amber-50 transition-colors text-gray-700"
                    @click.stop="selectBriefEquipItem(item)">
                    <div class="flex items-center justify-between gap-2">
                      <span class="font-medium truncate">{{ item.name }}</span>
                      <span class="text-[10px] text-gray-400 whitespace-nowrap font-mono">{{ item.serialNumber || '—' }}</span>
                    </div>
                    <div class="text-[11px] text-gray-500 mt-0.5 truncate">{{ [item.fundStatus, item.clientName, item.location].filter(Boolean).join(' • ') }}</div>
                  </button>
                  <div v-if="filteredBriefEquipItems.length === 0" class="px-3 py-2 text-sm text-gray-400 italic">Нет оборудования типа «{{ briefEquipAddingType }}»</div>
                </div>
                <button type="button" class="text-[10px] text-gray-400 hover:text-gray-600 mt-1"
                  @click="briefEquipAddingType = ''; briefEquipAddingItemSearch = ''; briefEquipAddingItemOpen = false">Отмена</button>
              </div>
            </div>
          </div>

          <!-- Кто в курсе (popup dropdown, not static) -->
          <details class="group relative" @click.stop>
            <summary class="list-none cursor-pointer select-none">
              <div class="flex items-center justify-between">
                <span class="block text-xs font-medium text-gray-600">Кто в курсе / ранее сталкивался с задачей</span>
                <span class="text-[10px] text-gray-400 group-open:hidden">▼</span>
                <span class="text-[10px] text-gray-400 hidden group-open:inline">▲</span>
              </div>
            </summary>
            <div class="mt-2">
              <div v-if="briefKnowledgeable.length" class="flex flex-wrap gap-1.5 mb-2">
                <span
                  v-for="uid in briefKnowledgeable"
                  :key="uid"
                  class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-blue-50 text-blue-700 border border-blue-200"
                >
                  {{ resolveAssigneeName(uid) }}
                  <button type="button" class="text-blue-500 hover:text-blue-800 ml-0.5" @click="briefKnowledgeable = briefKnowledgeable.filter(x => x !== uid)">×</button>
                </span>
              </div>
              <button type="button"
                class="w-full border border-dashed border-gray-300 rounded-lg px-3 py-2 text-sm text-left text-gray-400 hover:border-blue-400 transition-colors"
                @click="briefKnowledgeOpen = !briefKnowledgeOpen">
                {{ briefKnowledgeable.length ? '+ Добавить ещё…' : '+ Выбрать сотрудников…' }}
              </button>
              <div v-if="briefKnowledgeOpen" class="absolute z-50 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg overflow-hidden">
                <div class="p-2 border-b border-gray-100">
                  <input v-model="briefKnowledgeSearch" type="text" placeholder="Поиск сотрудника…"
                    class="w-full text-sm px-2 py-1.5 border border-gray-200 rounded focus:outline-none focus:border-blue-400" @click.stop />
                </div>
                <div class="max-h-48 overflow-y-auto">
                  <label
                    v-for="emp in filteredBriefKnowledge"
                    :key="emp.userId"
                    class="flex items-center gap-2.5 px-3 py-1.5 cursor-pointer hover:bg-gray-50 transition-colors"
                    :class="briefKnowledgeable.includes(emp.userId) ? 'bg-blue-50' : ''"
                  >
                    <input
                      type="checkbox"
                      :value="emp.userId"
                      v-model="briefKnowledgeable"
                      class="w-3.5 h-3.5 rounded accent-blue-600 flex-shrink-0"
                    />
                    <span class="text-sm text-gray-800 flex-1 truncate">{{ emp.fullName }}</span>
                    <span class="text-[10px] text-gray-400">{{ ROLE_LABEL[emp.role] ?? emp.role }}</span>
                  </label>
                  <div v-if="filteredBriefKnowledge.length === 0" class="px-3 py-2 text-sm text-gray-400 italic text-center">Нет сотрудников</div>
                </div>
              </div>
            </div>
          </details>

          <!-- Акт + диагностика -->
          <div class="flex flex-wrap gap-x-6 gap-y-2">
            <label class="flex items-center gap-2 cursor-pointer">
              <input v-model="briefActRequired" type="checkbox" class="w-4 h-4 rounded accent-amber-600" />
              <span class="text-sm text-gray-700 font-medium">Акт обязателен</span>
            </label>
            <label class="flex items-center gap-2 cursor-pointer">
              <input v-model="briefExtraDiag" type="checkbox" class="w-4 h-4 rounded accent-amber-600" />
              <span class="text-sm text-gray-700 font-medium">Доп. диагностика</span>
            </label>
          </div>

          <!-- Срок выезда (validated datetime + optional range) -->
          <div>
            <label class="block text-xs font-medium text-gray-600 mb-1">Срок выезда</label>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label class="block text-[10px] text-gray-400 mb-0.5">Дата и время</label>
                <input v-model="briefDeadlineStart" type="datetime-local"
                  class="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-green-400" />
              </div>
              <div>
                <label class="block text-[10px] text-gray-400 mb-0.5">До (диапазон, необязательно)</label>
                <input v-model="briefDeadlineEnd" type="datetime-local"
                  :min="briefDeadlineStart || undefined"
                  class="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none"
                  :class="briefDeadlineError ? 'border-red-300 focus:border-red-400' : 'border-gray-200 focus:border-green-400'" />
              </div>
            </div>
            <p v-if="briefDeadlineError" class="text-xs text-red-500 mt-1">{{ briefDeadlineError }}</p>
          </div>

        </div>
      </div>

      <!-- Прикрепление файлов -->
      <div class="space-y-2">
        <div class="flex flex-wrap items-center gap-x-3 gap-y-2">
          <span class="text-sm font-semibold text-gray-800 dark:text-gray-100">Прикрепить файлы</span>
          <input
            ref="fileInputRef"
            type="file"
            multiple
            accept="image/*,application/pdf,.doc,.docx,.xls,.xlsx,.txt,.zip,.rar"
            class="sr-only"
            @change="handleFileChange"
          />
          <button
            type="button"
            class="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 dark:border-zinc-600 bg-white dark:bg-[#141416] px-2.5 py-1.5 text-xs font-medium text-gray-700 dark:text-gray-200 shadow-sm hover:border-green-500/60 hover:bg-green-50/50 dark:hover:bg-zinc-800/80 hover:text-green-700 dark:hover:text-green-400 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-green-500/40"
            @click="fileInputRef?.click()"
          >
            <Paperclip class="w-3.5 h-3.5 shrink-0 opacity-80" />
            Выбрать файл
          </button>
          <span v-if="attachedFiles.length" class="text-xs text-gray-500 dark:text-gray-400">{{ attachedFiles.length }} выбрано</span>
          <span class="text-[10px] text-gray-400 dark:text-gray-500">PNG, PDF, DOC, XLSX, ZIP до 10MB</span>
        </div>

        <div v-if="attachedFiles.length > 0" class="flex flex-wrap gap-2">
          <div
            v-for="(file, idx) in attachedFiles"
            :key="idx"
            class="group relative inline-flex max-w-full items-center gap-1 rounded-md border border-gray-200 dark:border-zinc-600 bg-gray-50 dark:bg-zinc-900/60 pl-2 pr-7 py-1 text-[11px] text-gray-700 dark:text-gray-300"
          >
            <span class="truncate">{{ file.name }}</span>
            <button
              type="button"
              class="absolute right-0.5 top-1/2 -translate-y-1/2 rounded p-0.5 text-gray-400 hover:bg-red-100 hover:text-red-600 dark:hover:bg-red-950/50 dark:hover:text-red-400"
              :aria-label="'Удалить ' + file.name"
              @click.stop="removeFile(idx)"
            >
              <X class="w-3 h-3" />
            </button>
          </div>
        </div>
      </div>

      <!-- Описание -->
      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">Описание</label>
        <p class="text-xs text-gray-700 mb-1 font-medium">Краткий комментарий или дополнение; при включённом брифе попадёт в конец текста заявки после разделителя «---».</p>
        <textarea
          v-model="form.details"
          rows="4"
          placeholder="Подробное описание проблемы, шаги воспроизведения..."
          class="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:border-green-400 focus:ring-1 focus:ring-green-400"
        />
      </div>

      <!-- Кнопки -->
      <div class="flex flex-col sm:flex-row items-stretch sm:items-center gap-2 sm:gap-3 pt-1">
        <button
          type="submit"
          :disabled="loading || (auth.isStaff && !staffPerm.can('newTicketCreate'))"
          class="px-5 py-2.5 text-sm font-medium text-white rounded-lg disabled:opacity-50 transition-opacity text-center bg-[#23a836] hover:bg-[#1e922f]"
        >
          {{ loading ? 'Создаём...' : 'Создать заявку' }}
        </button>
        <NuxtLink to="/" class="text-sm text-gray-800 hover:text-gray-950 font-medium text-center py-2">Отмена</NuxtLink>
      </div>

      <!-- Feedback -->
      <div v-if="success" class="flex items-center gap-2 text-sm text-green-700 bg-green-50 border border-green-200 rounded-lg px-4 py-2.5">
        <svg class="w-4 h-4 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
        </svg>
        {{ success }}
      </div>
      <div v-if="error" class="text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-4 py-2.5">{{ error }}</div>

          </div>

          <aside v-if="auth.isStaff" class="lg:col-span-4 min-w-0">
            <div class="lg:sticky lg:top-6 rounded-2xl border border-gray-200 dark:border-zinc-700/70 bg-gray-50/90 dark:bg-zinc-900/45 p-4 sm:p-5 space-y-3 shadow-sm">
              <div class="relative" @click.stop>
                <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">Отдел заявки</label>
                <button
                  type="button"
                  class="new-ticket-select w-full border border-gray-200 dark:border-zinc-600 rounded-xl px-3 py-2 text-sm bg-white dark:bg-[#141416] text-left flex items-center justify-between hover:border-indigo-400 focus:outline-none focus:border-indigo-500 transition-colors"
                  @click="deptOpen = !deptOpen; clientOpen = objectOpen = typeOpen = equipmentOpen = equipmentTypeOpen = false"
                >
                  <div class="min-w-0 truncate">
                    <span class="font-medium">{{ selectedDept?.label }}</span>
                    <span class="text-gray-600 dark:text-gray-400 text-xs ml-1 font-medium hidden sm:inline">{{ selectedDept?.desc }}</span>
                  </div>
                  <svg class="w-4 h-4 text-gray-400 flex-shrink-0 ml-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
                  </svg>
                </button>
                <div v-if="deptOpen" class="absolute z-[60] mt-1 w-full bg-white dark:bg-[#1a1a1c] border border-gray-200 dark:border-zinc-600 rounded-xl shadow-lg overflow-hidden">
                  <div class="p-2 border-b border-gray-100 dark:border-zinc-700">
                    <input
                      v-model="deptSearch"
                      type="text"
                      placeholder="Поиск отдела..."
                      class="w-full text-sm px-2 py-1.5 border border-gray-200 dark:border-zinc-600 rounded-lg bg-white dark:bg-[#141416] focus:outline-none focus:border-indigo-500"
                      @click.stop
                    />
                  </div>
                  <div class="max-h-[min(70vh,22rem)] overflow-y-auto overscroll-contain">
                    <button
                      v-for="d in filteredDepts"
                      :key="d.value"
                      type="button"
                      class="w-full text-left px-3 py-2 text-sm hover:bg-indigo-50 dark:hover:bg-zinc-800 transition-colors"
                      :class="form.department === d.value ? 'bg-indigo-50 dark:bg-zinc-800 text-indigo-700 dark:text-indigo-300 font-medium' : 'text-gray-700 dark:text-gray-200'"
                      @click.stop="form.department = d.value; deptOpen = false; deptSearch = ''"
                    >
                      <span class="font-medium">{{ d.label }}</span>
                      <span class="text-gray-600 dark:text-gray-400 text-xs ml-1 font-medium">— {{ d.desc }}</span>
                    </button>
                    <div v-if="filteredDepts.length === 0" class="px-3 py-2 text-sm text-gray-400 italic">Ничего не найдено</div>
                  </div>
                </div>
              </div>

              <div class="flex flex-wrap items-center justify-between gap-2">
                <h2 class="text-[11px] font-bold text-gray-500 dark:text-gray-400 uppercase tracking-widest">Ответственные</h2>
                <div class="flex items-center gap-2 flex-shrink-0">
                  <span v-if="form.assignees.length" class="text-xs text-indigo-600 dark:text-indigo-400 font-medium bg-indigo-50 dark:bg-indigo-950/50 border border-indigo-200 dark:border-indigo-800 px-2 py-0.5 rounded-full">
                    {{ form.assignees.length }} выбрано
                  </span>
                  <button
                    v-if="form.assignees.length"
                    type="button"
                    class="text-xs text-gray-500 hover:text-red-500 transition-colors"
                    @click="form.assignees = []"
                  >Сбросить</button>
                </div>
              </div>
              <p class="text-xs text-gray-600 dark:text-gray-400 -mt-1 leading-snug">
                В списке все сотрудники; ищите по имени. Можно назначить людей из разных ролей — выбранные не сбрасываются при смене отдела заявки.
              </p>
              <div v-if="form.assignees.length" class="flex flex-wrap gap-1.5">
                <span
                  v-for="uid in form.assignees"
                  :key="uid"
                  class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-indigo-100 dark:bg-indigo-900/40 text-indigo-900 dark:text-indigo-100 border border-indigo-200 dark:border-indigo-800"
                >
                  {{ resolveAssigneeName(uid) }}
                  <button type="button" class="text-indigo-600 hover:text-red-600 ml-0.5" @click="form.assignees = form.assignees.filter(id => id !== uid)">×</button>
                </span>
              </div>
              <div class="relative">
                <svg class="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-gray-400 pointer-events-none" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-4.35-4.35M17 11A6 6 0 115 11a6 6 0 0112 0z"/>
                </svg>
                <input
                  v-model="assigneeSearch"
                  type="text"
                  placeholder="Поиск сотрудника..."
                  class="w-full pl-8 pr-3 py-2 text-sm border border-gray-200 dark:border-zinc-600 rounded-xl bg-white dark:bg-[#141416] focus:outline-none focus:border-indigo-500"
                />
              </div>
              <div class="border border-gray-200 dark:border-zinc-600 rounded-xl overflow-hidden divide-y divide-gray-100 dark:divide-zinc-700 bg-white dark:bg-[#141416] max-h-[min(50vh,22rem)] overflow-y-auto overscroll-contain">
                <template v-for="(emps, groupName) in employeeGroups" :key="groupName">
                  <div class="px-3 py-1.5 bg-gray-50 dark:bg-zinc-800/80 flex items-center justify-between">
                    <span class="assignee-group-title text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase tracking-wide">{{ groupName }}</span>
                    <span class="text-xs text-gray-600 dark:text-gray-400 font-medium">{{ emps.length }} чел.</span>
                  </div>
                  <label
                    v-for="emp in emps"
                    :key="emp.userId"
                    class="flex items-center gap-3 px-3 py-2 cursor-pointer hover:bg-gray-50 dark:hover:bg-zinc-800/80 transition-colors"
                    :class="form.assignees.includes(emp.userId) ? 'bg-indigo-50/80 dark:bg-indigo-950/30' : ''"
                  >
                    <input
                      type="checkbox"
                      :value="emp.userId"
                      v-model="form.assignees"
                      class="w-4 h-4 rounded accent-indigo-600 flex-shrink-0"
                    />
                    <span class="text-sm text-gray-800 dark:text-gray-200 flex-1 truncate">{{ emp.fullName }}</span>
                    <span v-if="form.assignees.includes(emp.userId)" class="text-indigo-500 text-xs flex-shrink-0">✓</span>
                  </label>
                </template>
                <div v-if="Object.keys(employeeGroups).length === 0" class="px-3 py-4 text-sm text-gray-400 italic text-center">
                  Нет сотрудников для выбранного отдела или по запросу
                </div>
              </div>
              <p class="text-xs text-gray-600 dark:text-gray-400 leading-snug">
                Отдел заявки влияет на маршрут и бриф, но не ограничивает, кого можно указать ответственным.
              </p>

              <div class="pt-4 mt-1 border-t border-gray-200 dark:border-zinc-600">
                <div class="flex items-end justify-center gap-2 sm:gap-3 min-h-[6.5rem] px-1">
                  <button
                    v-for="p in PRIORITIES"
                    :key="'aside-' + p.value"
                    type="button"
                    :title="`${p.value}: ${p.hint}`"
                    class="relative w-9 sm:w-10 shrink-0 rounded-full border-2 border-transparent transition-transform focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 dark:focus-visible:ring-offset-zinc-900"
                    :class="[
                      p.barClass,
                      form.priority === p.value
                        ? 'ring-2 ring-gray-800 dark:ring-gray-100 ring-offset-2 ring-offset-gray-50 dark:ring-offset-zinc-900 z-10'
                        : 'opacity-90 hover:opacity-100'
                    ]"
                    :style="{ backgroundColor: p.barColor }"
                    @click="form.priority = p.value"
                  >
                    <span
                      v-if="form.priority === p.value"
                      class="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 w-5 h-5 bg-gray-800 dark:bg-gray-950 flex items-center justify-center rounded-sm shadow-md"
                      aria-hidden="true"
                    >
                      <svg class="w-2.5 h-2.5 text-white" fill="none" stroke="currentColor" stroke-width="3" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M20 6L9 17l-5-5" />
                      </svg>
                    </span>
                  </button>
                </div>
                <p class="text-center text-[11px] font-bold text-gray-600 dark:text-gray-400 uppercase tracking-widest mt-3">Приоритеты</p>
              </div>

              <details
                v-if="showCoordinatorExtras"
                class="group rounded-xl border border-sky-200/80 dark:border-sky-900/50 bg-sky-50/50 dark:bg-sky-950/25 p-3 space-y-3"
              >
                <summary class="list-none cursor-pointer flex items-center justify-between gap-2 text-xs font-semibold text-gray-800 dark:text-gray-200 select-none">
                  <span>Дополнительно</span>
                  <span class="text-[10px] text-gray-400 shrink-0 group-open:hidden">▼</span>
                  <span class="text-[10px] text-gray-400 shrink-0 hidden group-open:inline">▲</span>
                </summary>
                <div class="mt-3 space-y-4 pt-1 border-t border-sky-200/60 dark:border-sky-900/40">
                  <div>
                    <div class="flex items-center justify-between mb-1">
                      <label class="block text-xs font-medium text-gray-600 dark:text-gray-400">Ссылки на таск (URL + №)</label>
                      <button type="button" class="text-xs text-green-700 dark:text-green-400 hover:underline" @click="addTaskLinkRow">+ строка</button>
                    </div>
                    <div v-for="(row, idx) in brief.taskLinks" :key="'coord-' + idx" class="space-y-2 mb-3 pb-3 border-b border-sky-100 dark:border-sky-900/40 last:border-0 last:pb-0">
                      <div class="flex flex-col gap-2">
                        <div class="flex flex-col sm:flex-row gap-2">
                          <input v-model="row.url" type="url" placeholder="https://…"
                            class="flex-1 border border-gray-200 dark:border-zinc-600 rounded-lg px-3 py-2 text-sm bg-white dark:bg-[#141416] focus:outline-none focus:border-green-400"
                            @blur="onTaskUrlBlur(row)" />
                          <div class="flex gap-2">
                            <input v-model="row.number" placeholder="№ таска"
                              class="w-full sm:w-28 border border-gray-200 dark:border-zinc-600 rounded-lg px-3 py-2 text-sm bg-white dark:bg-[#141416] focus:outline-none focus:border-green-400" />
                            <button v-if="brief.taskLinks.length > 1" type="button" class="text-xs text-red-500 px-2" @click="removeTaskLinkRow(idx)">×</button>
                          </div>
                        </div>
                        <div>
                          <label class="block text-[10px] text-gray-500 dark:text-gray-400 mb-1">Комментарий к ссылке</label>
                          <input v-model="row.comment" placeholder="Контекст, зачем ссылка…"
                            class="w-full border border-gray-200 dark:border-zinc-600 rounded-lg px-3 py-2 text-sm bg-white dark:bg-[#141416] focus:outline-none focus:border-green-400" />
                        </div>
                      </div>
                    </div>
                  </div>

                  <div class="relative" @click.stop>
                    <label class="block text-xs font-medium text-gray-600 dark:text-gray-400 mb-2">Кто в курсе / ранее сталкивался с задачей</label>
                    <div v-if="briefKnowledgeable.length" class="flex flex-wrap gap-1.5 mb-2">
                      <span
                        v-for="uid in briefKnowledgeable"
                        :key="uid"
                        class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-blue-50 dark:bg-blue-950/40 text-blue-700 dark:text-blue-300 border border-blue-200 dark:border-blue-800"
                      >
                        {{ resolveAssigneeName(uid) }}
                        <button type="button" class="text-blue-500 hover:text-blue-800 dark:hover:text-blue-300 ml-0.5" @click="briefKnowledgeable = briefKnowledgeable.filter(x => x !== uid)">×</button>
                      </span>
                    </div>
                    <button
                      type="button"
                      class="w-full border border-dashed border-gray-300 dark:border-zinc-600 rounded-lg px-3 py-2 text-sm text-left text-gray-500 dark:text-gray-400 hover:border-blue-400 transition-colors"
                      @click="briefKnowledgeOpen = !briefKnowledgeOpen"
                    >
                      {{ briefKnowledgeable.length ? '+ Добавить ещё…' : '+ Выбрать сотрудников…' }}
                    </button>
                    <div v-if="briefKnowledgeOpen" class="absolute z-50 mt-1 w-full bg-white dark:bg-[#1a1a1c] border border-gray-200 dark:border-zinc-600 rounded-lg shadow-lg overflow-hidden">
                      <div class="p-2 border-b border-gray-100 dark:border-zinc-700">
                        <input
                          v-model="briefKnowledgeSearch"
                          type="text"
                          placeholder="Поиск сотрудника…"
                          class="w-full text-sm px-2 py-1.5 border border-gray-200 dark:border-zinc-600 rounded bg-white dark:bg-[#141416] focus:outline-none focus:border-blue-400"
                          @click.stop
                        />
                      </div>
                      <div class="max-h-48 overflow-y-auto">
                        <label
                          v-for="emp in filteredBriefKnowledge"
                          :key="emp.userId"
                          class="flex items-center gap-2.5 px-3 py-1.5 cursor-pointer hover:bg-gray-50 dark:hover:bg-zinc-800 transition-colors"
                          :class="briefKnowledgeable.includes(emp.userId) ? 'bg-blue-50 dark:bg-blue-950/30' : ''"
                        >
                          <input
                            type="checkbox"
                            :value="emp.userId"
                            v-model="briefKnowledgeable"
                            class="w-3.5 h-3.5 rounded accent-blue-600 flex-shrink-0"
                          />
                          <span class="text-sm text-gray-800 dark:text-gray-200 flex-1 truncate">{{ emp.fullName }}</span>
                          <span class="text-[10px] text-gray-400">{{ ROLE_LABEL[emp.role] ?? emp.role }}</span>
                        </label>
                        <div v-if="filteredBriefKnowledge.length === 0" class="px-3 py-2 text-sm text-gray-400 italic text-center">Нет сотрудников</div>
                      </div>
                    </div>
                  </div>
                </div>
              </details>
            </div>
          </aside>

        </div>

    <Teleport to="body">
      <div
        v-if="coordObjectModalOpen"
        class="fixed inset-0 z-[100] flex items-start justify-center bg-black/50 backdrop-blur-sm p-4 pt-[8vh] overflow-y-auto"
        @click.self="coordObjectModalOpen = false"
      >
        <div class="bg-white rounded-2xl shadow-2xl w-full max-w-lg max-h-[min(80vh,32rem)] flex flex-col my-auto" @click.stop>
          <div class="flex items-center justify-between px-4 py-3 border-b border-gray-200">
            <h3 class="text-sm font-semibold text-gray-800">Объекты обслуживания</h3>
            <button type="button" class="text-gray-400 hover:text-gray-600 p-1" @click="coordObjectModalOpen = false">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/></svg>
            </button>
          </div>
          <div class="p-3 border-b border-gray-100">
            <input
              v-model="coordObjectModalSearch"
              type="text"
              placeholder="Поиск по названию или адресу…"
              class="w-full text-sm px-3 py-2 border border-gray-200 rounded-lg focus:outline-none focus:border-green-400"
              autofocus
            />
          </div>
          <div class="overflow-y-auto flex-1 min-h-0 p-2">
            <button
              type="button"
              class="w-full text-left px-3 py-2.5 text-sm rounded-lg hover:bg-gray-50 text-gray-500 mb-1"
              @click="selectCoordObject(null)"
            >— Не выбран —</button>
            <button
              v-for="o in filteredCoordModalObjects"
              :key="o.id"
              type="button"
              class="w-full text-left px-3 py-2.5 text-sm rounded-lg hover:bg-green-50 transition-colors border border-transparent hover:border-green-100"
              :class="form.objectId === o.id ? 'bg-green-50 border-green-200 text-green-800' : 'text-gray-800'"
              @click="selectCoordObject(o)"
            >
              <div class="font-medium">{{ o.name }}</div>
              <div v-if="o.address" class="text-xs text-gray-500 mt-0.5 truncate">{{ o.address }}</div>
              <div v-if="o.clientId && clients.find(c => c.id === o.clientId)" class="text-[10px] text-green-700 mt-1">
                {{ clients.find(c => c.id === o.clientId)?.name }}
              </div>
            </button>
            <p v-if="filteredCoordModalObjects.length === 0" class="text-sm text-gray-400 text-center py-6">Ничего не найдено</p>
          </div>
        </div>
      </div>
    </Teleport>

    </form>
    </div>
  </div>
</template>

<style scoped>
/* Не даём светлому textColor с dashboard «бледнить» подписи и значения */
html:not(.dark) .new-ticket-page :deep(label) {
  color: #111827 !important;
  opacity: 1 !important;
}
html.dark .new-ticket-page :deep(label) {
  color: #f3f4f6 !important;
  opacity: 1 !important;
}

html:not(.dark) .new-ticket-page :deep(input:not([type="checkbox"]):not([type="radio"])),
html:not(.dark) .new-ticket-page :deep(textarea) {
  color: #111827 !important;
  -webkit-text-fill-color: #111827;
}
html.dark .new-ticket-page :deep(input:not([type="checkbox"]):not([type="radio"])),
html.dark .new-ticket-page :deep(textarea) {
  color: #e5e7eb !important;
  -webkit-text-fill-color: #e5e7eb;
}

html:not(.dark) .new-ticket-page :deep(input::placeholder),
html:not(.dark) .new-ticket-page :deep(textarea::placeholder) {
  color: #6b7280 !important;
  opacity: 1 !important;
  -webkit-text-fill-color: #6b7280;
}
html.dark .new-ticket-page :deep(input::placeholder),
html.dark .new-ticket-page :deep(textarea::placeholder) {
  color: #6b7280 !important;
  opacity: 1 !important;
  -webkit-text-fill-color: #6b7280;
}

html:not(.dark) .new-ticket-page :deep(button.new-ticket-select),
html:not(.dark) .new-ticket-page :deep(button.new-ticket-select span) {
  color: #111827 !important;
}
html.dark .new-ticket-page :deep(button.new-ticket-select),
html.dark .new-ticket-page :deep(button.new-ticket-select span) {
  color: #e5e7eb !important;
}

.new-ticket-page :deep(button[type="submit"]) {
  color: #fff !important;
  -webkit-text-fill-color: #fff;
}

html:not(.dark) .new-ticket-page :deep(.assignee-group-title) {
  color: #374151 !important;
  opacity: 1 !important;
}
html.dark .new-ticket-page :deep(.assignee-group-title) {
  color: #9ca3af !important;
  opacity: 1 !important;
}

/* Dark-mode surface / text / border safety overrides scoped to this page */
html.dark .new-ticket-page :deep(.bg-gray-50),
html.dark .new-ticket-page :deep(.bg-white) {
  background-color: #141416 !important;
}
html.dark .new-ticket-page :deep(.border-gray-200),
html.dark .new-ticket-page :deep(.border-gray-100) {
  border-color: #2a2a2e !important;
}
html.dark .new-ticket-page :deep(.text-gray-900) { color: #f3f4f6 !important; }
html.dark .new-ticket-page :deep(.text-gray-800) { color: #e5e7eb !important; }
html.dark .new-ticket-page :deep(.text-gray-700) { color: #d1d5db !important; }
html.dark .new-ticket-page :deep(.text-gray-600) { color: #9ca3af !important; }
html.dark .new-ticket-page :deep(.text-gray-500) { color: #9ca3af !important; }
</style>
