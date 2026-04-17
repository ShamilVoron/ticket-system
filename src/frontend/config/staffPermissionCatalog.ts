/**
 * Каталог прав сотрудника (хранится в Employees.PermissionsJson).
 * Плоские ключи boolean; при отсутствии ключа — defaultPermissionForRole().
 */

export type PermissionSectionDef = {
  id: string
  title: string
  description?: string
  items: { key: string; label: string; hint?: string }[]
}

const ADMIN = new Set(['super_admin', 'coordinator', 'director'])
const STAFF = new Set([
  'support_l1', 'support_l2', 'developer', 'field_engineer', 'accountant',
  'head_engineers', 'head_support', 'head_dev', 'sysadmin',
  'coordinator', 'director', 'super_admin', 'procurement', 'head_repair', 'agent',
])

function normRole(r: string): string {
  return String(r || '')
    .trim()
    .toLowerCase()
}

function isStaffRole(r: string): boolean {
  const x = normRole(r)
  return x !== '' && x !== 'client' && STAFF.has(x)
}

function isAdminRole(r: string): boolean {
  return ADMIN.has(normRole(r))
}

function isFieldEngineer(r: string): boolean {
  return normRole(r) === 'field_engineer'
}

/** Разделы бокового меню */
export const STAFF_PERMISSION_SECTIONS: PermissionSectionDef[] = [
  {
    id: 'sidebar',
    title: '1. Боковое меню — видимость разделов',
    description: 'Что показывать в навигации. Если выключено — пункт скрыт (при подключённой проверке на клиенте).',
    items: [
      { key: 'sidebarNewTicket', label: 'Новая заявка (кнопка сверху)', hint: 'Блок «Новая заявка»' },
      { key: 'sidebarAllTickets', label: 'Все заявки' },
      { key: 'sidebarMyTickets', label: 'Мои заявки' },
      { key: 'sidebarMessenger', label: 'Мессенджер' },
      { key: 'sidebarClients', label: 'Клиенты (юрлица / объекты)' },
      { key: 'sidebarEquipment', label: 'Оборудование' },
      { key: 'sidebarSchedule', label: 'График работы' },
      { key: 'sidebarReports', label: 'Отчёты' },
      { key: 'sidebarSpreadsheets', label: 'Таблички' },
      { key: 'sidebarSettings', label: 'Настройки' },
      { key: 'sidebarEmployees', label: 'Сотрудники' },
    ],
  },
  {
    id: 'section',
    title: '2. Функционал в разделах',
    description: 'Уточнение внутри разделов. «Все / мои заявки» — без отдельных запретов в матрице.',
    items: [
      { key: 'sectionMessengerCreateGroups', label: 'Мессенджер — создавать и редактировать группы', hint: 'Кнопка «Новая группа» и правка состава' },
      { key: 'sectionEquipmentCreate', label: 'Оборудование — создавать записи' },
      { key: 'sectionEquipmentEdit', label: 'Оборудование — редактировать' },
      { key: 'sectionEquipmentDelete', label: 'Оборудование — удалять' },
      { key: 'sectionScheduleView', label: 'График — просмотр' },
      { key: 'sectionScheduleEdit', label: 'График — редактировать (зарезервировано)', hint: 'Позже: правка чужих/своих слотов' },
      { key: 'sectionScheduleCreate', label: 'График — создавать (зарезервировано)' },
      { key: 'sectionSpreadsheetsView', label: 'Таблички — просмотр' },
      { key: 'sectionSpreadsheetsEdit', label: 'Таблички — редактирование' },
      { key: 'canReactToTicketComments', label: 'Реагировать на комментарии в заявках' },
      { key: 'canReactToMessengerMessages', label: 'Реагировать на сообщения в мессенджере' },
    ],
  },
  {
    id: 'ticket',
    title: '3. Внутри карточки заявки',
    description: 'Отображение блоков и действия. Редактирование сотрудников в системе по-прежнему только у супер-админа (роль API).',
    items: [
      { key: 'ticketShowSubtasks', label: 'Показывать блок подзадач' },
      { key: 'ticketShowExitActs', label: 'Показывать акты выезда' },
      { key: 'ticketEditAlternativeTitle', label: 'Менять / добавлять альтернативное название' },
      { key: 'ticketEditParameters', label: 'Менять блок «Параметры заявки» (кроме прав по роли)' },
      { key: 'ticketEditDescription', label: 'Менять описание заявки' },
      { key: 'ticketEditTaskLinks', label: 'Менять / добавлять ссылки на таск' },
      { key: 'ticketCreateSubtask', label: 'Создавать подзадачи' },
      { key: 'ticketCreateExitActs', label: 'Создавать акты выезда' },
      { key: 'ticketEditForeignStatus', label: 'Изменять статус чужой заявки' },
      { key: 'ticketDeleteSubtask', label: 'Удалять подзадачи' },
      { key: 'ticketInteractForeign', label: 'Взаимодействовать с чужой заявкой (комментарии, файлы)' },
    ],
  },
  {
    id: 'newTicket',
    title: '4. Новая заявка',
    items: [
      { key: 'newTicketVisible', label: 'Видеть раздел «Новая заявка»' },
      { key: 'newTicketCreate', label: 'Создавать новую заявку (отправка формы)' },
    ],
  },
]

export const ALL_CATALOG_KEYS: string[] = STAFF_PERMISSION_SECTIONS.flatMap((s) => s.items.map((i) => i.key))

const LEGACY_MAP: Record<string, string> = {
  canAccessMessenger: 'sidebarMessenger',
  canAccessClients: 'sidebarClients',
  canAccessEquipment: 'sidebarEquipment',
  canAccessEmployees: 'sidebarEmployees',
  canAccessReports: 'sidebarReports',
  canAccessSpreadsheets: 'sidebarSpreadsheets',
  canAccessSettings: 'sidebarSettings',
  canCreateTickets: 'newTicketCreate',
  canAddEquipment: 'sectionEquipmentCreate',
  canViewSpreadsheets: 'sectionSpreadsheetsView',
  canEditSpreadsheets: 'sectionSpreadsheetsEdit',
  canCreateSpreadsheets: 'sectionSpreadsheetsEdit',
  canEditWorkSchedule: 'sectionScheduleEdit',
}

/** Значение по умолчанию для ключа, если в JSON ничего нет — по роли (как раньше в layout / заявках). */
export function defaultPermissionForRole(key: string, role: string): boolean {
  const r = normRole(role)
  if (r === 'client') {
    if (key.startsWith('sidebar')) {
      if (key === 'sidebarAllTickets' || key === 'sidebarNewTicket') return true
      return false
    }
    if (key === 'newTicketVisible' || key === 'newTicketCreate') return true
    return false
  }

  if (r === 'super_admin') return true

  const staff = isStaffRole(r)
  const admin = isAdminRole(r)
  const fe = isFieldEngineer(r)

  switch (key) {
    case 'sidebarNewTicket':
    case 'newTicketVisible':
    case 'newTicketCreate':
      return staff
    case 'sidebarAllTickets':
    case 'sidebarMyTickets':
      return staff
    case 'sidebarMessenger':
      return staff
    case 'sidebarClients':
    case 'sidebarEquipment':
      return admin || fe
    case 'sidebarSchedule':
      return staff
    case 'sidebarReports':
    case 'sidebarSpreadsheets':
    case 'sidebarEmployees':
      return staff && !fe
    case 'sidebarSettings':
      return admin

    case 'sectionMessengerCreateGroups':
      return staff
    case 'sectionEquipmentCreate':
    case 'sectionEquipmentEdit':
    case 'sectionEquipmentDelete':
      return admin || fe
    case 'sectionScheduleView':
      return staff
    case 'sectionScheduleEdit':
    case 'sectionScheduleCreate':
      return false
    case 'sectionSpreadsheetsView':
      return staff && !fe
    case 'sectionSpreadsheetsEdit':
      return staff && !fe
    case 'canReactToTicketComments':
    case 'canReactToMessengerMessages':
      return staff

    case 'ticketShowSubtasks':
    case 'ticketShowExitActs':
    case 'ticketEditDescription':
    case 'ticketEditTaskLinks':
    case 'ticketCreateSubtask':
    case 'ticketCreateExitActs':
    case 'ticketEditForeignStatus':
    case 'ticketDeleteSubtask':
    case 'ticketInteractForeign':
      return staff
    case 'ticketEditAlternativeTitle':
    case 'ticketEditParameters':
      return staff && !fe

    default:
      return false
  }
}

/** Подмешать старые ключи canAccess* → новые. */
export function applyLegacyPermissionAliases(obj: Record<string, boolean>): void {
  for (const [legacy, modern] of Object.entries(LEGACY_MAP)) {
    if (Object.prototype.hasOwnProperty.call(obj, legacy) && !Object.prototype.hasOwnProperty.call(obj, modern)) {
      obj[modern] = obj[legacy]!
    }
  }
}

/**
 * @param serverDefaultsForRole — переопределения из БД (SystemSettings) для роли; если ключа нет — берётся defaultPermissionForRole.
 */
export function buildMergedPermState(
  json: string,
  roleSlug: string,
  serverDefaultsForRole?: Record<string, boolean> | null,
): Record<string, boolean> {
  let rawObj: Record<string, unknown> = {}
  try {
    const raw = (json || '').trim()
    if (raw) {
      const v = JSON.parse(raw)
      if (v && typeof v === 'object' && !Array.isArray(v)) rawObj = v as Record<string, unknown>
    }
  } catch {
    rawObj = {}
  }

  const coerce = (val: unknown): boolean => {
    if (typeof val === 'boolean') return val
    if (val === 1 || val === '1') return true
    if (val === 0 || val === '0') return false
    if (typeof val === 'string') {
      const s = val.trim().toLowerCase()
      if (s === 'true' || s === 'yes') return true
      if (s === 'false' || s === 'no') return false
    }
    return false
  }

  const fromJson: Record<string, boolean> = {}
  for (const [k, val] of Object.entries(rawObj)) {
    if (typeof val === 'object' && val !== null) continue
    fromJson[k] = coerce(val)
  }

  applyLegacyPermissionAliases(fromJson)

  const next: Record<string, boolean> = { ...fromJson }
  for (const k of ALL_CATALOG_KEYS) {
    if (!Object.prototype.hasOwnProperty.call(next, k)) {
      if (
        serverDefaultsForRole &&
        Object.prototype.hasOwnProperty.call(serverDefaultsForRole, k)
      ) {
        next[k] = !!serverDefaultsForRole[k]
      } else {
        next[k] = defaultPermissionForRole(k, roleSlug)
      }
    }
  }
  return next
}
