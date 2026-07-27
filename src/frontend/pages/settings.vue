<script setup lang="ts">
import { Settings, Save, RefreshCw, AlertCircle, CheckCircle, Trash2, Plus, GripVertical, Pencil, X } from 'lucide-vue-next'
import type { SystemStatus } from '~/types'

const api = useApi()
const auth = useAuthStore()
const toast = useToast()

const activeTab = ref<'statuses' | 'sla' | 'telegram' | 'kb' | 'automation' | 'general'>('statuses')
const loading = ref(false)
const saving = ref(false)
const message = ref('')
const error = ref('')

// Knowledge Base
const kbCategories = ref<any[]>([])
const kbArticles = ref<any[]>([])
const newKbCategory = ref({ name: '', sortOrder: 0 })
const editingKbCategoryId = ref<number | null>(null)
const newKbArticle = ref({ title: '', body: '', tags: '', categoryId: null as number | null, isPublished: false })
const editingKbArticleId = ref<number | null>(null)

function startEditKbCategory(c: any) {
  editingKbCategoryId.value = c.id
  newKbCategory.value = { name: c.name, sortOrder: c.sortOrder ?? 0 }
}
function cancelEditKbCategory() {
  editingKbCategoryId.value = null
  newKbCategory.value = { name: '', sortOrder: 0 }
}
function startEditKbArticle(a: any) {
  editingKbArticleId.value = a.id
  newKbArticle.value = {
    title: a.title || '',
    body: a.body || '',
    tags: a.tags || '',
    categoryId: a.categoryId ?? null,
    isPublished: !!a.isPublished,
  }
}
function cancelEditKbArticle() {
  editingKbArticleId.value = null
  newKbArticle.value = { title: '', body: '', tags: '', categoryId: null, isPublished: false }
}

// Automation rules
const automationRules = ref<any[]>([])
const newAutomation = ref({
  name: '',
  isActive: true,
  trigger: 'ticket_created',
  conditionsJson: '{}',
  actionsJson: '[{"type":"notify_telegram","params":{"eventType":"automation"}}]',
})
const editingAutomationId = ref<number | null>(null)
const automationTriggers = [
  { value: 'ticket_created', label: 'Создание заявки' },
  { value: 'sla_80', label: 'SLA 80%' },
  { value: 'sla_breach', label: 'SLA breach' },
  { value: 'status_resolved', label: 'Статус «Решен»' },
  { value: 'vip_email_domain', label: 'VIP email domain' },
]
function startEditAutomation(r: any) {
  editingAutomationId.value = r.id
  newAutomation.value = {
    name: r.name,
    isActive: r.isActive,
    trigger: r.trigger,
    conditionsJson: r.conditionsJson || '{}',
    actionsJson: r.actionsJson || '[]',
  }
}
function cancelEditAutomation() {
  editingAutomationId.value = null
  newAutomation.value = {
    name: '',
    isActive: true,
    trigger: 'ticket_created',
    conditionsJson: '{}',
    actionsJson: '[{"type":"notify_telegram","params":{"eventType":"automation"}}]',
  }
}

// Statuses
const statuses = ref<SystemStatus[]>([])
const newStatus = ref({ name: '', colorClass: 'bg-blue-100 text-blue-700', sortOrder: 0, isActive: true, roleFilter: '', isDefault: false })
const editingStatusId = ref<number | null>(null)

function startEditStatus(s: any) {
  editingStatusId.value = s.id
  newStatus.value = { name: s.name, colorClass: s.colorClass, sortOrder: s.sortOrder, isActive: s.isActive, roleFilter: s.roleFilter || '', isDefault: s.isDefault }
}
function cancelEditStatus() {
  editingStatusId.value = null
  newStatus.value = { name: '', colorClass: 'bg-blue-100 text-blue-700', sortOrder: 0, isActive: true, roleFilter: '', isDefault: false }
}

// SLA
const slaPolicies = ref<any[]>([])
const newSla = ref({ priority: '*', requestType: '*', department: '*', clientCategory: '*', reactionMinutes: 60, resolutionMinutes: 240, isActive: true })
const editingSlaId = ref<number | null>(null)

function startEditSla(p: any) {
  editingSlaId.value = p.id
  newSla.value = { priority: p.priority, requestType: p.requestType, department: p.department, clientCategory: p.clientCategory || '*', reactionMinutes: p.reactionMinutes, resolutionMinutes: p.resolutionMinutes, isActive: p.isActive }
}
function cancelEditSla() {
  editingSlaId.value = null
  newSla.value = { priority: '*', requestType: '*', department: '*', clientCategory: '*', reactionMinutes: 60, resolutionMinutes: 240, isActive: true }
}
const slaDepartments = ref<any[]>([])

// Telegram
const telegramSettings = ref<any[]>([])
const newTelegram = ref({ eventType: 'new_ticket', isEnabled: true, chatId: '', template: '', targetType: 'chat', targetEmployeeId: '' })
const editingTelegramId = ref<number | null>(null)
const tgEmployees = ref<{ userId: string; fullName: string }[]>([])

// API-ключ интеграций (скрипты, миграция Okdesk) — только super_admin
const staffApiKeyStatus = ref<{ configured: boolean; boundUserId: string | null } | null>(null)
const staffApiKeyPickUserId = ref('')
const staffApiKeyGenerated = ref('')
const staffApiKeyBusy = ref(false)

// Okdesk sync settings
const okdeskSettings = ref({ url: '', token: '' })
const okdeskTesting = ref(false)
const okdeskImporting = ref(false)
const brandSettings = ref({ logoUrl: '', accentColor: '', companyName: '' })
const { load: reloadBranding } = useSystemBranding()

// IMAP email ingest
const imapSettings = ref({
  enabled: false,
  host: '',
  port: '993',
  user: '',
  password: '',
  useSsl: true,
})

const tgEventTypes = [
  { value: 'new_ticket', label: 'Новая заявка' },
  { value: 'status_changed', label: 'Смена статуса' },
  { value: 'assignee_changed', label: 'Назначен ответственный' },
  { value: 'field_report_added', label: 'Выездной акт добавлен' },
  { value: 'subtask_created', label: 'Подзадача создана' },
  { value: 'sla_80', label: 'SLA 80%' },
  { value: 'sla_breach', label: 'SLA breach' },
  { value: 'automation', label: 'Автоматизация' },
]
const tgTargetTypes = [
  { value: 'chat', label: 'Группа/чат (указать Chat ID)', desc: '' },
  { value: 'assignee', label: 'Ответственный (автоматически)', desc: 'Уведомление отправляется тому, кто назначен ответственным в каждой конкретной заявке' },
  { value: 'reporter', label: 'Создатель заявки (автоматически)', desc: 'Уведомление отправляется тому, кто создал заявку' },
  { value: 'employee', label: 'Конкретный сотрудник', desc: 'Выберите сотрудника из списка' },
]
const tgPlaceholderLabels: Record<string, string> = {
  id: '№ заявки', title: 'Тема', status: 'Статус', priority: 'Приоритет',
  department: 'Отдел', requestType: 'Тип запроса', clientName: 'Клиент',
  objectName: 'Объект', assignee: 'Ответственный', createdAt: 'Дата создания',
  oldStatus: 'Старый статус', oldAssignee: 'Пред. ответственный',
  engineerName: 'Инженер', visitDate: 'Дата визита', actionType: 'Тип работ',
  equipmentType: 'Тип оборудования', equipmentSerial: 'Серийный номер',
  workDone: 'Выполненные работы', fieldReport: 'Акт (сводка)',
  subtaskTitle: 'Подзадача', subtaskDescription: 'Описание подзадачи',
  subtaskStatus: 'Статус подзадачи', createdByName: 'Кто создал', subtask: 'Подзадача (сводка)',
}
const tgPlaceholders: Record<string, string[]> = {
  _common: ['id', 'title', 'status', 'priority', 'department', 'requestType', 'clientName', 'objectName', 'assignee', 'createdAt'],
  status_changed: ['oldStatus'],
  assignee_changed: ['oldAssignee'],
  field_report_added: ['engineerName', 'visitDate', 'actionType', 'equipmentType', 'equipmentSerial', 'workDone', 'fieldReport'],
  subtask_created: ['subtaskTitle', 'subtaskDescription', 'subtaskStatus', 'createdByName', 'subtask'],
}
function placeholdersForEvent(event: string): { key: string; label: string }[] {
  const keys = [...tgPlaceholders._common, ...(tgPlaceholders[event] || [])]
  return keys.map(k => ({ key: k, label: tgPlaceholderLabels[k] || k }))
}
function eventLabel(val: string): string {
  return tgEventTypes.find(e => e.value === val)?.label || val
}
function targetLabel(val: string): string {
  return tgTargetTypes.find(t => t.value === val)?.label || val
}
function employeeName(userId: string): string {
  return tgEmployees.value.find(e => e.userId === userId)?.fullName || userId
}
function insertPlaceholder(key: string) {
  const t = newTelegram.value.template
  const sep = t && !t.endsWith('\n') ? '\n' : ''
  newTelegram.value.template = t + sep + `{${key}}`
}
function startEditTelegram(t: any) {
  editingTelegramId.value = t.id
  newTelegram.value = { eventType: t.eventType, isEnabled: t.isEnabled, chatId: t.chatId || '', template: t.template || '', targetType: t.targetType || 'chat', targetEmployeeId: t.targetEmployeeId || '' }
}
function cancelEditTelegram() {
  editingTelegramId.value = null
  newTelegram.value = { eventType: 'new_ticket', isEnabled: true, chatId: '', template: '', targetType: 'chat', targetEmployeeId: '' }
}

async function loadGeneralSettings() {
  try {
    const settings = await api.systemSettings.getSettings()
    okdeskSettings.value.url = settings.OkdeskApiUrl || ''
    okdeskSettings.value.token = settings.OkdeskApiToken || ''
    const enabledRaw = (settings.email_ingest_enabled || '').toLowerCase()
    imapSettings.value.enabled = enabledRaw === 'true' || enabledRaw === '1'
    imapSettings.value.host = settings.imap_host || ''
    imapSettings.value.port = settings.imap_port || '993'
    imapSettings.value.user = settings.imap_user || ''
    imapSettings.value.password = settings.imap_password || ''
    const sslRaw = (settings.imap_use_ssl || 'true').toLowerCase()
    imapSettings.value.useSsl = sslRaw === '' || sslRaw === 'true' || sslRaw === '1'
    brandSettings.value.logoUrl = settings.brand_logo_url || ''
    brandSettings.value.accentColor = settings.brand_accent_color || ''
    brandSettings.value.companyName = settings.company_name || ''
  } catch {
    // ignore
  }
}

async function loadData() {
  loading.value = true
  try {
    const [statusesData, slaData, tgData, deptsData, empsData, kbCats, kbArts, autoRules] = await Promise.all([
      api.systemSettings.getStatuses(),
      api.systemSettings.getSla(),
      api.systemSettings.getTelegram(),
      api.departments.getAll(),
      api.employees.getAll().catch(() => []),
      api.knowledgeBase.getCategories().catch(() => []),
      api.knowledgeBase.getArticles().catch(() => []),
      api.automationRules.getAll().catch(() => []),
    ])
    statuses.value = statusesData
    slaPolicies.value = slaData
    telegramSettings.value = tgData
    slaDepartments.value = deptsData
    tgEmployees.value = (empsData as any[]).map((e: any) => ({ userId: e.userId, fullName: e.fullName })).sort((a: any, b: any) => a.fullName.localeCompare(b.fullName, 'ru'))
    kbCategories.value = kbCats as any[]
    kbArticles.value = kbArts as any[]
    automationRules.value = autoRules as any[]
    if (auth.isSuperAdmin) {
      try {
        staffApiKeyStatus.value = await api.systemSettings.getStaffApiKeyStatus()
      } catch {
        staffApiKeyStatus.value = null
      }
      await loadGeneralSettings()
    }
  } catch (err) {
    error.value = 'Ошибка загрузки данных'
  } finally {
    loading.value = false
  }
}

async function saveBrandSettings() {
  saving.value = true
  try {
    await api.systemSettings.saveSettings({
      brand_logo_url: brandSettings.value.logoUrl.trim(),
      brand_accent_color: brandSettings.value.accentColor.trim(),
      company_name: brandSettings.value.companyName.trim(),
    })
    await reloadBranding(true)
    toast.success('Брендинг сохранён')
  } catch (e: any) {
    toast.error(e?.message || 'Ошибка сохранения')
  } finally {
    saving.value = false
  }
}

async function saveOkdeskSettings() {
  saving.value = true
  try {
    await api.systemSettings.saveSettings({
      OkdeskApiUrl: okdeskSettings.value.url.trim(),
      OkdeskApiToken: okdeskSettings.value.token.trim(),
    })
    toast.success('Настройки Okdesk сохранены')
  } catch (e: any) {
    toast.error(e?.message || 'Ошибка сохранения')
  } finally {
    saving.value = false
  }
}

async function saveImapSettings() {
  saving.value = true
  try {
    await api.systemSettings.saveSettings({
      email_ingest_enabled: imapSettings.value.enabled ? 'true' : 'false',
      imap_host: imapSettings.value.host.trim(),
      imap_port: String(imapSettings.value.port || '993').trim(),
      imap_user: imapSettings.value.user.trim(),
      imap_password: imapSettings.value.password,
      imap_use_ssl: imapSettings.value.useSsl ? 'true' : 'false',
    })
    toast.success('Настройки IMAP сохранены')
  } catch (e: any) {
    toast.error(e?.message || 'Ошибка сохранения')
  } finally {
    saving.value = false
  }
}

async function runOkdeskImport() {
  if (!confirm('Импортировать компании и открытые заявки из Okdesk?')) return
  okdeskImporting.value = true
  try {
    const r = await api.systemSettings.importOkdesk()
    const warn = r.warning ? ` (${r.warning})` : ''
    toast.success(
      `Okdesk: компаний ${r.companiesUpserted}/${r.companiesFetched}, заявок ${r.issuesUpserted}/${r.issuesFetched}${warn}`,
    )
  } catch (e: any) {
    toast.error(e?.message || 'Ошибка импорта Okdesk')
  } finally {
    okdeskImporting.value = false
  }
}

async function testOkdeskConnection() {
  okdeskTesting.value = true
  try {
    const r = await api.systemSettings.testOkdeskConnection()
    if (r.valid) toast.success('Подключение к Okdesk успешно')
    else toast.error('Не удалось подключиться к Okdesk')
  } catch (e: any) {
    toast.error(e?.message || 'Ошибка проверки подключения')
  } finally {
    okdeskTesting.value = false
  }
}

async function generateStaffApiKey() {
  if (!staffApiKeyPickUserId.value) {
    toast.warning('Выберите сотрудника')
    return
  }
  staffApiKeyBusy.value = true
  staffApiKeyGenerated.value = ''
  try {
    const r = await api.systemSettings.generateStaffApiKey(staffApiKeyPickUserId.value)
    staffApiKeyGenerated.value = r.apiKey
    staffApiKeyStatus.value = await api.systemSettings.getStaffApiKeyStatus()
    toast.success('Ключ создан — скопируйте и сохраните')
  } catch (e: any) {
    toast.error(e?.data?.error || e?.message || 'Не удалось создать ключ')
  } finally {
    staffApiKeyBusy.value = false
  }
}

async function revokeStaffApiKey() {
  if (!confirm('Отозвать API-ключ? Скрипты с этим ключом перестанут работать.')) return
  staffApiKeyBusy.value = true
  try {
    await api.systemSettings.revokeStaffApiKey()
    staffApiKeyStatus.value = await api.systemSettings.getStaffApiKeyStatus()
    staffApiKeyGenerated.value = ''
    toast.success('Ключ отозван')
  } catch (e: any) {
    toast.error(e?.message || 'Ошибка')
  } finally {
    staffApiKeyBusy.value = false
  }
}

// ---- Statuses
async function saveStatus() {
  saving.value = true
  message.value = ''
  error.value = ''
  try {
    const payload = { ...newStatus.value, id: editingStatusId.value || undefined }
    await api.systemSettings.saveStatus(payload)
    message.value = editingStatusId.value ? 'Статус обновлён' : 'Статус добавлен'
    editingStatusId.value = null
    newStatus.value = { name: '', colorClass: 'bg-blue-100 text-blue-700', sortOrder: 0, isActive: true, roleFilter: '', isDefault: false }
    await loadData()
  } catch (err: any) {
    const detail = err?.data?.title || err?.data?.detail || err?.data?.message || err?.data || err?.message || ''
    error.value = 'Ошибка сохранения статуса' + (detail ? `: ${typeof detail === 'string' ? detail : JSON.stringify(detail)}` : '')
  } finally {
    saving.value = false
    setTimeout(() => message.value = '', 3000)
  }
}

async function deleteStatus(id: number) {
  if(!confirm('Удалить статус?')) return
  try {
    await api.systemSettings.deleteStatus(id)
    await loadData()
  } catch(e) {
    error.value = 'Ошибка удаления статуса'
  }
}

// ---- SLA
async function saveSla() {
  saving.value = true
  message.value = ''
  error.value = ''
  try {
    const payload = { ...newSla.value, id: editingSlaId.value || undefined }
    await api.systemSettings.saveSla(payload)
    message.value = editingSlaId.value ? 'SLA обновлён' : 'SLA добавлен'
    editingSlaId.value = null
    newSla.value = { priority: '*', requestType: '*', department: '*', clientCategory: '*', reactionMinutes: 60, resolutionMinutes: 240, isActive: true }
    await loadData()
  } catch(err: any) {
    const detail = err?.data?.title || err?.data?.detail || err?.data?.message || err?.data || err?.message || ''
    error.value = 'Ошибка сохранения SLA' + (detail ? `: ${typeof detail === 'string' ? detail : JSON.stringify(detail)}` : '')
  } finally {
    saving.value = false
    setTimeout(() => message.value = '', 3000)
  }
}

async function deleteSla(id: number) {
  if(!confirm('Удалить правило SLA?')) return
  try {
    await api.systemSettings.deleteSla(id)
    toast.success('SLA-правило удалено')
    await loadData()
  } catch { toast.error('Не удалось удалить SLA-правило') }
}

// ---- Telegram
async function saveTelegram(item?: any) {
  saving.value = true
  try {
    const payload = item || { ...newTelegram.value, id: editingTelegramId.value || undefined }
    await api.systemSettings.saveTelegram(payload)
    toast.success(editingTelegramId.value ? 'Правило обновлено' : 'Правило создано')
    cancelEditTelegram()
    await loadData()
  } catch(e: any) {
    const msg = e?.data?.message || e?.response?._data || 'Ошибка сохранения Telegram'
    toast.error(typeof msg === 'string' ? msg : 'Ошибка сохранения')
  } finally {
    saving.value = false
  }
}

async function toggleTelegramEnabled(t: any) {
  try {
    await api.systemSettings.saveTelegram({ ...t, isEnabled: t.isEnabled })
  } catch { toast.error('Не удалось обновить правило') }
}

async function deleteTelegram(id: number) {
  if(!confirm('Удалить правило уведомлений?')) return
  try {
    await api.systemSettings.deleteTelegram(id)
    toast.success('Telegram-правило удалено')
    await loadData()
  } catch { toast.error('Не удалось удалить Telegram-правило') }
}

// ---- Knowledge Base
async function saveKbCategory() {
  if (!newKbCategory.value.name.trim()) { toast.warning('Укажите название категории'); return }
  saving.value = true
  try {
    await api.knowledgeBase.saveCategory({
      id: editingKbCategoryId.value || undefined,
      name: newKbCategory.value.name.trim(),
      sortOrder: newKbCategory.value.sortOrder,
    })
    toast.success(editingKbCategoryId.value ? 'Категория обновлена' : 'Категория создана')
    cancelEditKbCategory()
    await loadData()
  } catch { toast.error('Ошибка сохранения категории') }
  finally { saving.value = false }
}
async function deleteKbCategory(id: number) {
  if (!confirm('Удалить категорию?')) return
  try {
    await api.knowledgeBase.deleteCategory(id)
    toast.success('Категория удалена')
    await loadData()
  } catch { toast.error('Не удалось удалить категорию') }
}
async function saveKbArticle() {
  if (!newKbArticle.value.title.trim()) { toast.warning('Укажите заголовок'); return }
  saving.value = true
  try {
    await api.knowledgeBase.saveArticle({
      id: editingKbArticleId.value || undefined,
      ...newKbArticle.value,
      title: newKbArticle.value.title.trim(),
    })
    toast.success(editingKbArticleId.value ? 'Статья обновлена' : 'Статья создана')
    cancelEditKbArticle()
    await loadData()
  } catch { toast.error('Ошибка сохранения статьи') }
  finally { saving.value = false }
}
async function deleteKbArticle(id: number) {
  if (!confirm('Удалить статью?')) return
  try {
    await api.knowledgeBase.deleteArticle(id)
    toast.success('Статья удалена')
    await loadData()
  } catch { toast.error('Не удалось удалить статью') }
}

// ---- Automation
async function saveAutomation() {
  if (!newAutomation.value.name.trim()) { toast.warning('Укажите название правила'); return }
  saving.value = true
  try {
    await api.automationRules.save({
      id: editingAutomationId.value || undefined,
      ...newAutomation.value,
      name: newAutomation.value.name.trim(),
    })
    toast.success(editingAutomationId.value ? 'Правило обновлено' : 'Правило создано')
    cancelEditAutomation()
    await loadData()
  } catch { toast.error('Ошибка сохранения правила') }
  finally { saving.value = false }
}
async function deleteAutomation(id: number) {
  if (!confirm('Удалить правило автоматизации?')) return
  try {
    await api.automationRules.delete(id)
    toast.success('Правило удалено')
    await loadData()
  } catch { toast.error('Не удалось удалить правило') }
}

const colorOptions = [
  { value: 'bg-gray-100 text-gray-700 border-gray-200', label: 'Серый' },
  { value: 'bg-blue-100 text-blue-700 border-blue-200', label: 'Синий' },
  { value: 'bg-sky-100 text-sky-800 border-sky-300', label: 'Голубой' },
  { value: 'bg-green-100 text-green-700 border-green-200', label: 'Зелёный' },
  { value: 'bg-yellow-100 text-yellow-700 border-yellow-200', label: 'Жёлтый' },
  { value: 'bg-orange-100 text-orange-800 border-orange-300', label: 'Оранжевый' },
  { value: 'bg-red-100 text-red-700 border-red-200', label: 'Красный' },
  { value: 'bg-purple-100 text-purple-700 border-purple-200', label: 'Фиолетовый' },
  { value: 'bg-violet-100 text-violet-800 border-violet-300', label: 'Фиолетовый' },
  { value: 'bg-indigo-100 text-indigo-700 border-indigo-200', label: 'Индиго' },
]

function statusColorLabel(colorClass: string): string {
  const exact = colorOptions.find((c) => c.value === colorClass)
  if (exact) return exact.label
  const s = (colorClass || '').toLowerCase()
  if (/violet|purple/.test(s)) return 'Фиолетовый'
  if (/indigo/.test(s)) return 'Индиго'
  if (/orange|amber/.test(s)) return 'Оранжевый'
  if (/yellow/.test(s)) return 'Жёлтый'
  if (/green|emerald/.test(s)) return 'Зелёный'
  if (/red|rose/.test(s)) return 'Красный'
  if (/sky|cyan|teal/.test(s)) return 'Голубой'
  if (/blue/.test(s)) return 'Синий'
  if (/gray|zinc|slate|neutral/.test(s)) return 'Серый'
  return 'Свой'
}

const brandAccentPicker = computed({
  get: () => {
    const raw = (brandSettings.value.accentColor || '').trim()
    return /^#[0-9a-fA-F]{6}$/.test(raw) ? raw : '#4f46e5'
  },
  set: (v: string) => {
    brandSettings.value.accentColor = v
  },
})

onMounted(() => {
  loadData()
})
</script>

<template>
  <div class="space-y-6 max-w-7xl mx-auto w-full">
    <!-- Header -->
    <div class="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4">
      <div>
        <p class="text-sm text-gray-500">Определяйте статусы, политики SLA и уведомления</p>
      </div>
      <button 
        @click="loadData"
        class="inline-flex items-center gap-2 px-4 py-2 border border-gray-300 shadow-sm text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 transition-colors"
        :disabled="loading"
      >
        <RefreshCw :size="16" :class="{ 'animate-spin': loading }" />
        Обновить
      </button>
    </div>

    <!-- Tabs -->
    <div class="flex items-center gap-2 sm:gap-4 border-b border-gray-200 px-2 sm:px-0 overflow-x-auto">
      <button
        v-for="tab in ['statuses', 'sla', 'telegram', 'kb', 'automation', 'general']"
        :key="tab"
        @click="activeTab = tab as any"
        :class="[
          'px-3 sm:px-4 py-3 text-sm font-medium border-b-2 transition-colors whitespace-nowrap',
          activeTab === tab
            ? 'border-indigo-600 text-indigo-600' 
            : 'border-transparent text-gray-500 hover:text-gray-900 hover:border-gray-300'
        ]"
      >
        {{
          tab === 'statuses' ? 'Статусы заявок'
          : tab === 'sla' ? 'SLA Политики'
          : tab === 'telegram' ? 'Telegram'
          : tab === 'kb' ? 'База знаний'
          : tab === 'automation' ? 'Автоматизация'
          : 'Общие'
        }}
      </button>
    </div>

    <!-- Notification Toasts -->
    <div v-if="message" class="p-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm font-medium flex items-center gap-2">
      <CheckCircle :size="16"/> {{ message }}
    </div>
    <div v-if="error" class="p-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm font-medium flex items-center gap-2">
      <AlertCircle :size="16"/> {{ error }}
    </div>

    <!-- Statuses Tab -->
    <div v-if="activeTab === 'statuses'" class="space-y-6">
      
      <!-- New / Edit Status -->
      <div class="bg-white border text-sm border-gray-200 shadow-sm rounded-lg overflow-hidden">
         <div class="px-5 py-4 border-b border-gray-200 bg-gray-50/50 flex items-center justify-between">
           <h3 class="font-bold text-gray-900">{{ editingStatusId ? 'Редактирование статуса' : 'Добавить статус' }}</h3>
           <button v-if="editingStatusId" @click="cancelEditStatus" class="text-xs text-gray-500 hover:text-gray-700 font-medium flex items-center gap-1"><X :size="14" /> Отмена</button>
         </div>
         <div class="p-5 grid grid-cols-1 md:grid-cols-5 gap-4 items-end">
           <div class="md:col-span-2">
             <label class="block text-xs font-medium text-gray-700 mb-1">Название статуса</label>
             <input v-model="newStatus.name" type="text" class="w-full border border-gray-300 rounded-md px-3 py-2 focus:ring-1 focus:ring-indigo-500 focus:border-indigo-500" placeholder="В работе..." />
           </div>
           <div>
             <label class="block text-xs font-medium text-gray-700 mb-1">Цветовая тема</label>
             <select v-model="newStatus.colorClass" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:ring-1 focus:ring-indigo-500 focus:border-indigo-500">
               <option v-for="c in colorOptions" :key="c.value" :value="c.value">{{ c.label }}</option>
             </select>
           </div>
           <div>
             <label class="block text-xs font-medium text-gray-700 mb-1">Сортировка</label>
             <input v-model.number="newStatus.sortOrder" type="number" class="w-full border border-gray-300 rounded-md px-3 py-2 focus:ring-1 focus:ring-indigo-500 focus:border-indigo-500" />
           </div>
           <div>
             <button @click="saveStatus" :disabled="!newStatus.name || saving" class="w-full inline-flex justify-center items-center gap-2 text-white px-4 py-2 rounded-md transition-colors disabled:opacity-50" :class="editingStatusId ? 'bg-green-600 hover:bg-green-700' : 'bg-indigo-600 hover:bg-indigo-700'">
               <Save v-if="editingStatusId" :size="16"/> <Plus v-else :size="16"/>
               {{ editingStatusId ? 'Сохранить' : 'Добавить' }}
             </button>
           </div>
         </div>
      </div>

      <!-- List Statuses -->
      <div class="bg-white border text-sm border-gray-200 shadow-sm rounded-lg overflow-hidden">
         <div class="px-5 py-4 border-b border-gray-200 bg-gray-50/50 flex justify-between">
            <h3 class="font-bold text-gray-900">Существующие статусы</h3>
            <span class="text-xs text-gray-500">{{ statuses.length }} статусов</span>
         </div>
         <table class="w-full text-left">
           <thead class="bg-gray-50 border-b border-gray-200">
             <tr>
               <th class="px-5 py-3 font-semibold text-gray-900">Название</th>
               <th class="px-5 py-3 font-semibold text-gray-900">Тема (Цвет)</th>
               <th class="px-5 py-3 font-semibold text-gray-900">Порядок</th>
               <th class="px-5 py-3 text-right">Действия</th>
             </tr>
           </thead>
           <tbody class="divide-y divide-gray-100">
             <tr v-for="status in statuses" :key="status.id" class="hover:bg-gray-50">
               <td class="px-5 py-3 font-medium text-gray-900">{{ status.name }} <span v-if="status.isDefault" class="text-[10px] ml-2 text-indigo-700 bg-indigo-50 border border-indigo-200 px-2 py-0.5 rounded-full uppercase tracking-wider font-bold">Default</span></td>
               <td class="px-5 py-3">
                 <span :class="['px-2.5 py-0.5 rounded-md text-xs font-medium border flex w-max', status.colorClass]">
                   {{ statusColorLabel(status.colorClass) }}
                 </span>
               </td>
               <td class="px-5 py-3 text-gray-500">{{ status.sortOrder }}</td>
               <td class="px-5 py-3 text-right">
                 <div class="flex items-center justify-end gap-1">
                   <button @click="startEditStatus(status)" class="text-gray-400 hover:text-indigo-600 p-1 transition-colors" title="Редактировать"><Pencil :size="15"/></button>
                   <button @click="deleteStatus(status.id!)" class="text-gray-400 hover:text-red-600 p-1 transition-colors" title="Удалить"><Trash2 :size="15"/></button>
                 </div>
               </td>
             </tr>
             <tr v-if="statuses.length === 0">
                <td colspan="4" class="px-5 py-8 text-center text-gray-500 italic">Нет сконфигурированных статусов</td>
             </tr>
           </tbody>
         </table>
      </div>
    </div>

    <!-- SLA Tab -->
    <div v-if="activeTab === 'sla'" class="space-y-6">
      
      <!-- New SLA Info -->
       <div class="rounded-lg p-4 text-sm border bg-indigo-50 border-indigo-100 text-indigo-800 dark:bg-indigo-950/40 dark:border-indigo-800/60 dark:text-indigo-100">
         <p><strong>Политики SLA (Service Level Agreement)</strong> определяют нормативы времени (реакция и решение) для заявок. Вы можете использовать `*` (звездочку) в качестве маски для любых значений. Система выбирает наиболее специфичное правило при расчетах.</p>
       </div>

      <div class="bg-white border text-sm border-gray-200 shadow-sm rounded-lg overflow-hidden">
         <div class="px-5 py-4 border-b border-gray-200 bg-gray-50/50 flex items-center justify-between">
           <h3 class="font-bold text-gray-900">{{ editingSlaId ? 'Редактирование правила SLA' : 'Добавить правило SLA' }}</h3>
           <button v-if="editingSlaId" @click="cancelEditSla" class="text-xs text-gray-500 hover:text-gray-700 font-medium flex items-center gap-1"><X :size="14" /> Отмена</button>
         </div>
         <div class="p-5 grid grid-cols-1 sm:grid-cols-2 md:grid-cols-6 gap-4 items-end">
           <div>
             <label class="block text-xs font-medium text-gray-700 mb-1">Приоритет</label>
             <select v-model="newSla.priority" class="w-full border border-gray-300 rounded px-2 py-2 focus:border-indigo-500 focus:outline-none">
               <option value="*">Любой (*)</option>
               <option value="Низкий">Низкий</option>
               <option value="Средний">Средний</option>
               <option value="Высокий">Высокий</option>
               <option value="Критический">Критический</option>
             </select>
           </div>
           <div>
             <label class="block text-xs font-medium text-gray-700 mb-1">Отдел</label>
             <select v-model="newSla.department" class="w-full border border-gray-300 rounded px-2 py-2 focus:border-indigo-500 focus:outline-none">
               <option value="*">Любой (*)</option>
               <option v-for="d in slaDepartments" :key="d.value" :value="d.value">{{ d.label }}</option>
             </select>
           </div>
           <div>
             <label class="block text-xs font-medium text-gray-700 mb-1">Тип запроса</label>
             <input v-model="newSla.requestType" type="text" class="w-full border border-gray-300 rounded px-2 py-2 focus:border-indigo-500 focus:outline-none" />
           </div>
           <div>
             <label class="block text-xs font-medium text-gray-700 mb-1">Реакция (мин)</label>
             <input v-model.number="newSla.reactionMinutes" type="number" step="15" class="w-full border border-gray-300 rounded px-2 py-2 focus:border-indigo-500 focus:outline-none" />
           </div>
           <div>
             <label class="block text-xs font-medium text-gray-700 mb-1">Решение (мин)</label>
             <input v-model.number="newSla.resolutionMinutes" type="number" step="15" class="w-full border border-gray-300 rounded px-2 py-2 focus:border-indigo-500 focus:outline-none" />
           </div>
           <div>
             <button @click="saveSla" :disabled="saving" class="w-full inline-flex justify-center items-center gap-2 text-white px-4 py-2 rounded transition-colors disabled:opacity-50" :class="editingSlaId ? 'bg-green-600 hover:bg-green-700' : 'bg-indigo-600 hover:bg-indigo-700'">
               <Save v-if="editingSlaId" :size="16"/> <Plus v-else :size="16"/>
               {{ editingSlaId ? 'Сохранить' : 'Добавить' }}
             </button>
           </div>
         </div>
      </div>

      <div class="bg-white border text-sm border-gray-200 shadow-sm rounded-lg overflow-hidden">
         <table class="w-full text-left whitespace-nowrap">
           <thead class="bg-gray-50 border-b border-gray-200">
             <tr>
               <th class="px-5 py-3 font-semibold text-gray-900">Приоритет</th>
               <th class="px-5 py-3 font-semibold text-gray-900">Отдел</th>
               <th class="px-5 py-3 font-semibold text-gray-900">Тип запроса</th>
               <th class="px-5 py-3 font-semibold text-gray-900">Реакция</th>
               <th class="px-5 py-3 font-semibold text-gray-900">Решение</th>
               <th class="px-5 py-3 text-right">Действия</th>
             </tr>
           </thead>
           <tbody class="divide-y divide-gray-100">
             <tr v-for="policy in slaPolicies" :key="policy.id" class="hover:bg-gray-50">
               <td class="px-5 py-3 font-medium text-gray-900">
                 <span v-if="policy.priority === '*'" class="text-gray-400 font-bold border border-dotted px-2 py-0.5 rounded">*</span>
                 <span v-else>{{ policy.priority }}</span>
               </td>
               <td class="px-5 py-3 text-gray-700">
                 <span v-if="policy.department === '*'" class="text-gray-400 font-bold border border-dotted px-2 py-0.5 rounded">*</span>
                 <span v-else>{{ policy.department }}</span>
               </td>
               <td class="px-5 py-3 text-gray-700">
                  <span v-if="policy.requestType === '*'" class="text-gray-400 font-bold border border-dotted px-2 py-0.5 rounded">*</span>
                 <span v-else>{{ policy.requestType }}</span>
               </td>
               <td class="px-5 py-3 text-green-700 font-bold">{{ policy.reactionMinutes }} мин</td>
               <td class="px-5 py-3 text-indigo-700 font-bold">{{ policy.resolutionMinutes }} мин</td>
               <td class="px-5 py-3 text-right">
                 <div class="flex items-center justify-end gap-1">
                   <button @click="startEditSla(policy)" class="text-gray-400 hover:text-indigo-600 p-1 transition-colors" title="Редактировать"><Pencil :size="15"/></button>
                   <button @click="deleteSla(policy.id)" class="text-gray-400 hover:text-red-600 p-1 transition-colors" title="Удалить"><Trash2 :size="15"/></button>
                 </div>
               </td>
             </tr>
             <tr v-if="slaPolicies.length === 0">
                <td colspan="6" class="px-5 py-8 text-center text-gray-500 italic">Нет созданных SLA политик</td>
             </tr>
           </tbody>
         </table>
      </div>
    </div>

    <!-- Telegram Tab -->
    <div v-if="activeTab === 'telegram'" class="space-y-6">
       
       <div class="rounded-lg p-4 text-sm border bg-sky-50 border-sky-200 text-sky-900 dark:bg-sky-950/40 dark:border-sky-800/60 dark:text-sky-100">
         <p>
           Настройки уведомлений Telegram. Добавьте бота в нужную группу или чат.
           Шаблон собирается из плейсхолдеров
           <code class="rounded px-1 bg-sky-100/80 dark:bg-sky-900/80">{key}</code>
           — строки с нераспознанными плейсхолдерами удаляются.
         </p>
       </div>

       <!-- New / Edit Form -->
       <div class="bg-white border text-sm border-gray-200 shadow-sm rounded-lg overflow-hidden">
         <div class="px-5 py-4 border-b border-gray-200 bg-gray-50/50 flex items-center justify-between">
           <h3 class="font-bold text-gray-900">{{ editingTelegramId ? 'Редактировать правило' : 'Новое правило' }}</h3>
           <button v-if="editingTelegramId" @click="cancelEditTelegram" class="text-xs text-gray-500 hover:text-gray-700 font-medium">Отмена</button>
         </div>
         <div class="p-5 grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
               <label class="block text-xs font-medium text-gray-700 mb-1">Событие</label>
               <select v-model="newTelegram.eventType" class="w-full border border-gray-300 rounded px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none">
                 <option v-for="e in tgEventTypes" :key="e.value" :value="e.value">{{ e.label }}</option>
               </select>
            </div>
            <div>
               <label class="block text-xs font-medium text-gray-700 mb-1">Получатель</label>
               <select v-model="newTelegram.targetType" class="w-full border border-gray-300 rounded px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none">
                 <option v-for="t in tgTargetTypes" :key="t.value" :value="t.value">{{ t.label }}</option>
               </select>
               <p v-if="tgTargetTypes.find(t => t.value === newTelegram.targetType)?.desc" class="text-[11px] text-gray-400 mt-1">
                 {{ tgTargetTypes.find(t => t.value === newTelegram.targetType)?.desc }}
               </p>
            </div>
            <div>
               <label class="block text-xs font-medium text-gray-700 mb-1">Сотрудник</label>
               <select v-model="newTelegram.targetEmployeeId" class="w-full border border-gray-300 rounded px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none">
                 <option value="">— Все / не выбран —</option>
                 <option v-for="emp in tgEmployees" :key="emp.userId" :value="emp.userId">{{ emp.fullName }}</option>
               </select>
               <p class="text-[11px] text-gray-400 mt-1">Выберите сотрудника, которому отправлять уведомление</p>
            </div>
            <div>
               <label class="block text-xs font-medium text-gray-700 mb-1">Telegram Chat ID</label>
               <input v-model="newTelegram.chatId" type="text" placeholder="-100123456789" class="w-full border border-gray-300 rounded px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none" />
               <p class="text-[11px] text-gray-400 mt-1">ID чата/группы для отправки, или личный Chat ID сотрудника</p>
            </div>
            <div class="md:col-span-2">
               <label class="block text-xs font-medium text-gray-700 mb-1">Шаблон сообщения</label>
               <textarea v-model="newTelegram.template" rows="6" class="w-full border border-gray-300 rounded px-3 py-2 text-sm font-mono text-gray-600 focus:border-indigo-500 focus:outline-none" placeholder="📋 <b>Заявка #{id}</b>&#10;📌 {title}&#10;📊 Статус: <b>{status}</b>&#10;🏢 {clientName}"></textarea>
               <p class="text-[11px] text-gray-400 mt-1 mb-2">Нажмите на поле ниже — оно добавится в шаблон на новой строке</p>
               <div class="flex flex-wrap gap-1.5">
                 <button
                   v-for="p in placeholdersForEvent(newTelegram.eventType)"
                   :key="p.key"
                   type="button"
                   @click="insertPlaceholder(p.key)"
                   class="text-[11px] px-2 py-1 bg-gray-50 hover:bg-indigo-50 text-gray-700 hover:text-indigo-700 rounded-md border border-gray-200 hover:border-indigo-300 cursor-pointer transition-colors"
                 >{{ p.label }}</button>
               </div>
            </div>
         </div>
         <div class="px-5 py-4 bg-gray-50 border-t border-gray-100 flex justify-end gap-2">
            <button @click="() => saveTelegram()" :disabled="saving" class="inline-flex justify-center items-center gap-2 bg-indigo-600 text-white px-6 py-2 rounded-md font-medium text-sm hover:bg-indigo-700 transition-colors disabled:opacity-50">
               <Plus v-if="!editingTelegramId" :size="16"/>
               <Save v-else :size="16"/>
               {{ editingTelegramId ? 'Сохранить' : 'Добавить правило' }}
             </button>
         </div>
       </div>

       <!-- Existing Rules -->
       <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
         <div v-for="t in telegramSettings" :key="t.id" class="bg-white border border-gray-200 rounded-lg shadow-sm flex flex-col justify-between overflow-hidden">
           <div class="p-4">
             <div class="flex items-start justify-between mb-3">
               <span class="inline-flex items-center px-2 py-0.5 rounded text-[10px] uppercase font-bold tracking-wider bg-blue-100 text-blue-800">{{ eventLabel(t.eventType) }}</span>
               <label class="flex items-center cursor-pointer">
                 <div class="relative">
                   <input type="checkbox" v-model="t.isEnabled" class="sr-only" @change="toggleTelegramEnabled(t)">
                   <div :class="t.isEnabled ? 'bg-indigo-600' : 'bg-gray-200'" class="block w-8 h-4 rounded-full transition-colors"></div>
                   <div :class="t.isEnabled ? 'translate-x-full border-indigo-600' : 'translate-x-0 border-gray-300'" class="dot absolute left-[-2px] top-[-2px] bg-white w-5 h-5 rounded-full transition border shadow"></div>
                 </div>
               </label>
             </div>
             
             <div class="space-y-2 text-xs">
               <div class="flex items-center gap-2">
                 <span class="text-gray-400 font-medium w-20 shrink-0">Получатель:</span>
                 <span class="text-gray-700 font-semibold">{{ targetLabel(t.targetType || 'chat') }}</span>
               </div>
               <div v-if="t.chatId" class="flex items-center gap-2">
                 <span class="text-gray-400 font-medium w-20 shrink-0">Chat ID:</span>
                 <span class="font-mono text-gray-600 bg-gray-50 px-1.5 py-0.5 rounded border border-gray-100 break-all">{{ t.chatId }}</span>
               </div>
               <div v-if="t.targetEmployeeId" class="flex items-center gap-2">
                 <span class="text-gray-400 font-medium w-20 shrink-0">Сотрудник:</span>
                 <span class="text-gray-700 font-semibold">{{ employeeName(t.targetEmployeeId) }}</span>
               </div>
             </div>

             <div class="mt-3 p-2.5 bg-gray-50 rounded-lg border border-gray-100">
               <p class="text-[10px] font-bold text-gray-400 uppercase tracking-wider mb-1">Шаблон</p>
               <p class="text-xs text-gray-600 whitespace-pre-wrap leading-relaxed font-mono">{{ t.template || '(по умолчанию)' }}</p>
             </div>
           </div>
           <div class="px-4 py-3 border-t border-gray-100 bg-gray-50/50 flex justify-between items-center">
              <button @click="startEditTelegram(t)" class="text-xs text-indigo-600 font-medium hover:underline">Редактировать</button>
              <button @click="deleteTelegram(t.id)" class="text-xs text-red-500 font-medium hover:underline">Удалить</button>
           </div>
         </div>
         <div v-if="telegramSettings.length === 0" class="md:col-span-2 lg:col-span-3 text-center py-12 bg-white border border-gray-200 rounded-lg">
           <p class="text-gray-500 italic text-sm">Нет настроенных правил уведомлений Telegram</p>
         </div>
       </div>
    </div>

    <!-- General Tab -->
    <div v-if="activeTab === 'general'" class="space-y-6">
      <div
        v-if="auth.isSuperAdmin"
        class="bg-white border text-sm border-gray-200 shadow-sm rounded-lg overflow-hidden"
      >
        <div class="px-5 py-4 border-b border-gray-200 bg-gray-50/50">
          <h3 class="font-bold text-gray-900">Брендинг</h3>
          <p class="text-xs text-gray-500 mt-1">
            Логотип и акцентный цвет применяются в боковой панели и полевом интерфейсе.
          </p>
        </div>
        <div class="p-5 space-y-4">
          <div>
            <label class="block text-xs font-medium text-gray-700 mb-1">Название компании</label>
            <input
              v-model="brandSettings.companyName"
              type="text"
              class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm"
              placeholder="Ticket System"
            />
          </div>
          <div>
            <label class="block text-xs font-medium text-gray-700 mb-1">URL логотипа</label>
            <input
              v-model="brandSettings.logoUrl"
              type="url"
              class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm"
              placeholder="https://…"
            />
          </div>
          <div>
            <label class="block text-xs font-medium text-gray-700 mb-1">Акцентный цвет</label>
            <div class="flex items-center gap-3">
              <input
                v-model="brandAccentPicker"
                type="color"
                class="h-9 w-12 border border-gray-300 rounded cursor-pointer"
              />
              <input
                v-model="brandSettings.accentColor"
                type="text"
                class="flex-1 border border-gray-300 rounded-md px-3 py-2 text-sm font-mono"
                placeholder="#4f46e5"
              />
            </div>
          </div>
          <button
            type="button"
            class="px-4 py-2 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50"
            :disabled="saving"
            @click="saveBrandSettings"
          >
            Сохранить брендинг
          </button>
        </div>
      </div>

      <div
        v-if="auth.isSuperAdmin"
        class="bg-white border text-sm border-gray-200 shadow-sm rounded-lg overflow-hidden"
      >
        <div class="px-5 py-4 border-b border-gray-200 bg-gray-50/50">
          <h3 class="font-bold text-gray-900">API-ключ для интеграций</h3>
          <p class="text-xs text-gray-500 mt-1">
            Долгоживущий ключ вместо JWT: скрипты миграции (Okdesk), автоматизация. Права такие же, как у выбранного
            сотрудника. Передавайте заголовок
            <code class="rounded bg-gray-100 px-1">X-Api-Key</code>
            или
            <code class="rounded bg-gray-100 px-1">Authorization: Bearer ts_…</code>
          </p>
        </div>
        <div class="p-5 space-y-4">
          <div v-if="staffApiKeyStatus" class="text-sm">
            <span class="text-gray-600">Статус:</span>
            <span :class="staffApiKeyStatus.configured ? 'text-green-700 font-medium' : 'text-amber-700 font-medium'">
              {{ staffApiKeyStatus.configured ? 'ключ активен' : 'ключ не настроен' }}
            </span>
            <span v-if="staffApiKeyStatus.boundUserId" class="text-gray-500 text-xs ml-2 font-mono">
              (userId: {{ staffApiKeyStatus.boundUserId }})
            </span>
          </div>
          <div class="flex flex-wrap items-end gap-3">
            <div class="min-w-[220px] flex-1">
              <label class="block text-xs font-medium text-gray-700 mb-1">Сотрудник (владелец ключа)</label>
              <select
                v-model="staffApiKeyPickUserId"
                class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm"
              >
                <option value="">— выберите —</option>
                <option v-for="e in tgEmployees" :key="e.userId" :value="e.userId">{{ e.fullName }}</option>
              </select>
            </div>
            <button
              type="button"
              class="px-4 py-2 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50"
              :disabled="staffApiKeyBusy || !staffApiKeyPickUserId"
              @click="generateStaffApiKey"
            >
              Сгенерировать новый ключ
            </button>
            <button
              v-if="staffApiKeyStatus?.configured"
              type="button"
              class="px-4 py-2 rounded-md border border-red-300 text-red-700 text-sm hover:bg-red-50 disabled:opacity-50"
              :disabled="staffApiKeyBusy"
              @click="revokeStaffApiKey"
            >
              Отозвать ключ
            </button>
          </div>
          <div
            v-if="staffApiKeyGenerated"
            class="rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs dark:border-amber-800/50 dark:bg-amber-950/40"
          >
            <p class="font-semibold text-amber-900 dark:text-amber-100 mb-1">Сохраните ключ сейчас (больше не покажем):</p>
            <code class="block break-all font-mono text-gray-800 dark:text-gray-200 select-all">{{ staffApiKeyGenerated }}</code>
            <p class="text-gray-600 dark:text-gray-400 mt-2">В <code class="bg-white dark:bg-zinc-800 px-1 rounded">src/migration/.env</code>:
              <code class="bg-white dark:bg-zinc-800 px-1 rounded">TS_API_KEY=...</code>
            </p>
          </div>
        </div>
      </div>
      <div
        v-else
        class="bg-gray-50 border border-gray-200 rounded-lg p-4 text-sm text-gray-600"
      >
        Раздел API-ключа доступен только супер-администратору (вкладка «Общие»).
      </div>
      <div class="bg-white border text-sm border-gray-200 shadow-sm rounded-lg overflow-hidden">
        <div class="px-5 py-4 border-b border-gray-200 bg-gray-50/50">
          <h3 class="font-bold text-gray-900">Okdesk — двусторонняя синхронизация</h3>
          <p class="text-xs text-gray-500 mt-1">
            При изменении статуса, исполнителя, приоритета, названия, описания или комментария в Ticket System изменения отправляются в Okdesk.
          </p>
        </div>
        <div class="p-5 space-y-4">
          <div>
            <label class="block text-xs font-medium text-gray-700 mb-1">Okdesk API URL</label>
            <input
              v-model="okdeskSettings.url"
              type="text"
              placeholder="https://company.okdesk.ru"
              class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm"
            />
          </div>
          <div>
            <label class="block text-xs font-medium text-gray-700 mb-1">Okdesk API Token</label>
            <input
              v-model="okdeskSettings.token"
              type="password"
              placeholder="api_token"
              class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm"
            />
          </div>
          <div class="flex flex-wrap items-center gap-3">
            <button
              type="button"
              class="px-4 py-2 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50"
              :disabled="saving"
              @click="saveOkdeskSettings"
            >
              Сохранить настройки Okdesk
            </button>
            <button
              type="button"
              class="px-4 py-2 rounded-md border border-gray-300 text-gray-700 text-sm hover:bg-gray-50 disabled:opacity-50"
              :disabled="okdeskTesting || !okdeskSettings.url || !okdeskSettings.token"
              @click="testOkdeskConnection"
            >
              <span v-if="okdeskTesting" class="inline-flex items-center gap-2">
                <RefreshCw :size="14" class="animate-spin" /> Проверка…
              </span>
              <span v-else>Проверить подключение</span>
            </button>
            <button
              v-if="auth.isSuperAdmin"
              type="button"
              class="px-4 py-2 rounded-md border border-indigo-300 text-indigo-700 text-sm hover:bg-indigo-50 disabled:opacity-50"
              :disabled="okdeskImporting || !okdeskSettings.url || !okdeskSettings.token"
              @click="runOkdeskImport"
            >
              <span v-if="okdeskImporting" class="inline-flex items-center gap-2">
                <RefreshCw :size="14" class="animate-spin" /> Импорт…
              </span>
              <span v-else>Импорт из Okdesk</span>
            </button>
          </div>
        </div>
      </div>

      <div
        v-if="auth.isSuperAdmin"
        class="bg-white border text-sm border-gray-200 shadow-sm rounded-lg overflow-hidden"
      >
        <div class="px-5 py-4 border-b border-gray-200 bg-gray-50/50">
          <h3 class="font-bold text-gray-900">Email — IMAP ingest</h3>
          <p class="text-xs text-gray-500 mt-1">
            Фоновый опрос ящика каждые 60 сек: непрочитанные письма создают заявки (тип Email, отдел «Поддержка»)
            или добавляют комментарий к существующей по In-Reply-To.
          </p>
        </div>
        <div class="p-5 space-y-4">
          <label class="flex items-center gap-2 text-sm text-gray-700">
            <input v-model="imapSettings.enabled" type="checkbox" class="rounded border-gray-300" />
            Включить email ingest
          </label>
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="block text-xs font-medium text-gray-700 mb-1">IMAP host</label>
              <input
                v-model="imapSettings.host"
                type="text"
                placeholder="imap.example.com"
                class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label class="block text-xs font-medium text-gray-700 mb-1">Port</label>
              <input
                v-model="imapSettings.port"
                type="text"
                placeholder="993"
                class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label class="block text-xs font-medium text-gray-700 mb-1">User</label>
              <input
                v-model="imapSettings.user"
                type="text"
                autocomplete="off"
                class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label class="block text-xs font-medium text-gray-700 mb-1">Password</label>
              <input
                v-model="imapSettings.password"
                type="password"
                autocomplete="new-password"
                class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm"
              />
            </div>
          </div>
          <label class="flex items-center gap-2 text-sm text-gray-700">
            <input v-model="imapSettings.useSsl" type="checkbox" class="rounded border-gray-300" />
            Использовать SSL/TLS
          </label>
          <button
            type="button"
            class="px-4 py-2 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50"
            :disabled="saving"
            @click="saveImapSettings"
          >
            Сохранить IMAP
          </button>
        </div>
      </div>
    </div>

    <!-- Knowledge Base Tab -->
    <div v-if="activeTab === 'kb'" class="space-y-6">
      <div class="bg-white border text-sm border-gray-200 shadow-sm rounded-lg overflow-hidden">
        <div class="px-5 py-4 border-b border-gray-200 bg-gray-50/50 flex items-center justify-between">
          <h3 class="font-bold text-gray-900">{{ editingKbCategoryId ? 'Редактирование категории' : 'Категория' }}</h3>
          <button v-if="editingKbCategoryId" @click="cancelEditKbCategory" class="text-xs text-gray-500 hover:text-gray-700 font-medium flex items-center gap-1"><X :size="14" /> Отмена</button>
        </div>
        <div class="p-5 flex flex-wrap gap-3 items-end">
          <div class="flex-1 min-w-[12rem]">
            <label class="block text-xs font-medium text-gray-700 mb-1">Название</label>
            <input v-model="newKbCategory.name" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm" placeholder="Напр. Сеть" />
          </div>
          <div class="w-28">
            <label class="block text-xs font-medium text-gray-700 mb-1">Порядок</label>
            <input v-model.number="newKbCategory.sortOrder" type="number" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm" />
          </div>
          <button type="button" class="px-4 py-2 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50" :disabled="saving" @click="saveKbCategory">
            {{ editingKbCategoryId ? 'Сохранить' : 'Добавить' }}
          </button>
        </div>
        <ul class="divide-y divide-gray-100 border-t border-gray-100">
          <li v-for="c in kbCategories" :key="c.id" class="px-5 py-3 flex items-center justify-between gap-3">
            <span class="font-medium text-gray-900">{{ c.name }} <span class="text-xs text-gray-400">#{{ c.sortOrder }}</span></span>
            <div class="flex gap-2">
              <button type="button" class="text-xs text-indigo-600 hover:underline" @click="startEditKbCategory(c)">Изменить</button>
              <button type="button" class="text-xs text-red-600 hover:underline" @click="deleteKbCategory(c.id)">Удалить</button>
            </div>
          </li>
          <li v-if="!kbCategories.length" class="px-5 py-4 text-gray-500 text-sm">Категорий пока нет</li>
        </ul>
      </div>

      <div class="bg-white border text-sm border-gray-200 shadow-sm rounded-lg overflow-hidden">
        <div class="px-5 py-4 border-b border-gray-200 bg-gray-50/50 flex items-center justify-between">
          <h3 class="font-bold text-gray-900">{{ editingKbArticleId ? 'Редактирование статьи' : 'Статья' }}</h3>
          <button v-if="editingKbArticleId" @click="cancelEditKbArticle" class="text-xs text-gray-500 hover:text-gray-700 font-medium flex items-center gap-1"><X :size="14" /> Отмена</button>
        </div>
        <div class="p-5 space-y-3">
          <input v-model="newKbArticle.title" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm" placeholder="Заголовок" />
          <textarea v-model="newKbArticle.body" rows="4" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm" placeholder="Текст статьи" />
          <div class="flex flex-wrap gap-3">
            <input v-model="newKbArticle.tags" class="flex-1 min-w-[10rem] border border-gray-300 rounded-md px-3 py-2 text-sm" placeholder="Теги через запятую" />
            <select v-model="newKbArticle.categoryId" class="border border-gray-300 rounded-md px-3 py-2 text-sm">
              <option :value="null">Без категории</option>
              <option v-for="c in kbCategories" :key="c.id" :value="c.id">{{ c.name }}</option>
            </select>
            <label class="flex items-center gap-2 text-sm text-gray-700">
              <input v-model="newKbArticle.isPublished" type="checkbox" class="rounded border-gray-300" />
              Опубликовано
            </label>
            <button type="button" class="px-4 py-2 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50" :disabled="saving" @click="saveKbArticle">
              {{ editingKbArticleId ? 'Сохранить' : 'Добавить' }}
            </button>
          </div>
        </div>
        <ul class="divide-y divide-gray-100 border-t border-gray-100">
          <li v-for="a in kbArticles" :key="a.id" class="px-5 py-3 flex items-start justify-between gap-3">
            <div class="min-w-0">
              <div class="font-medium text-gray-900">{{ a.title }}
                <span v-if="a.isPublished" class="ml-2 text-[10px] uppercase tracking-wide text-green-700 bg-green-50 px-1.5 py-0.5 rounded">pub</span>
                <span v-else class="ml-2 text-[10px] uppercase tracking-wide text-gray-500 bg-gray-100 px-1.5 py-0.5 rounded">draft</span>
              </div>
              <div class="text-xs text-gray-500 truncate">{{ a.tags || '—' }} · {{ a.categoryName || 'без категории' }}</div>
            </div>
            <div class="flex gap-2 shrink-0">
              <button type="button" class="text-xs text-indigo-600 hover:underline" @click="startEditKbArticle(a)">Изменить</button>
              <button type="button" class="text-xs text-red-600 hover:underline" @click="deleteKbArticle(a.id)">Удалить</button>
            </div>
          </li>
          <li v-if="!kbArticles.length" class="px-5 py-4 text-gray-500 text-sm">Статей пока нет</li>
        </ul>
      </div>
    </div>

    <!-- Automation Tab -->
    <div v-if="activeTab === 'automation'" class="space-y-6">
      <div class="bg-white border text-sm border-gray-200 shadow-sm rounded-lg overflow-hidden">
        <div class="px-5 py-4 border-b border-gray-200 bg-gray-50/50 flex items-center justify-between">
          <h3 class="font-bold text-gray-900">{{ editingAutomationId ? 'Редактирование правила' : 'Правило автоматизации' }}</h3>
          <button v-if="editingAutomationId" @click="cancelEditAutomation" class="text-xs text-gray-500 hover:text-gray-700 font-medium flex items-center gap-1"><X :size="14" /> Отмена</button>
        </div>
        <div class="p-5 space-y-3">
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <input v-model="newAutomation.name" class="border border-gray-300 rounded-md px-3 py-2 text-sm" placeholder="Название" />
            <select v-model="newAutomation.trigger" class="border border-gray-300 rounded-md px-3 py-2 text-sm">
              <option v-for="t in automationTriggers" :key="t.value" :value="t.value">{{ t.label }}</option>
            </select>
          </div>
          <label class="flex items-center gap-2 text-sm text-gray-700">
            <input v-model="newAutomation.isActive" type="checkbox" class="rounded border-gray-300" />
            Активно
          </label>
          <div>
            <label class="block text-xs font-medium text-gray-700 mb-1">Conditions JSON</label>
            <textarea v-model="newAutomation.conditionsJson" rows="2" class="w-full font-mono text-xs border border-gray-300 rounded-md px-3 py-2" placeholder='{"emailDomain":"vip.com"}' />
          </div>
          <div>
            <label class="block text-xs font-medium text-gray-700 mb-1">Actions JSON</label>
            <textarea v-model="newAutomation.actionsJson" rows="3" class="w-full font-mono text-xs border border-gray-300 rounded-md px-3 py-2" placeholder='[{"type":"escalate_priority"}]' />
            <p class="text-[11px] text-gray-500 mt-1">
              Типы: assign_department, escalate_priority, set_priority, tag_title, notify_telegram, auto_close, set_setting
            </p>
          </div>
          <button type="button" class="px-4 py-2 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-50" :disabled="saving" @click="saveAutomation">
            {{ editingAutomationId ? 'Сохранить' : 'Добавить' }}
          </button>
        </div>
        <ul class="divide-y divide-gray-100 border-t border-gray-100">
          <li v-for="r in automationRules" :key="r.id" class="px-5 py-3 flex items-start justify-between gap-3">
            <div class="min-w-0">
              <div class="font-medium text-gray-900">{{ r.name }}
                <span v-if="r.isActive" class="ml-2 text-[10px] uppercase text-green-700 bg-green-50 px-1.5 py-0.5 rounded">on</span>
                <span v-else class="ml-2 text-[10px] uppercase text-gray-500 bg-gray-100 px-1.5 py-0.5 rounded">off</span>
              </div>
              <div class="text-xs text-gray-500">{{ automationTriggers.find(t => t.value === r.trigger)?.label || r.trigger }}</div>
            </div>
            <div class="flex gap-2 shrink-0">
              <button type="button" class="text-xs text-indigo-600 hover:underline" @click="startEditAutomation(r)">Изменить</button>
              <button type="button" class="text-xs text-red-600 hover:underline" @click="deleteAutomation(r.id)">Удалить</button>
            </div>
          </li>
          <li v-if="!automationRules.length" class="px-5 py-4 text-gray-500 text-sm">Правил пока нет</li>
        </ul>
      </div>
    </div>
  </div>
</template>

<style scoped>
/* toggler switch animation dot */
.dot { top: -0.1rem; left: -0.1rem; transition: transform 0.2s ease-in-out; }
</style>
