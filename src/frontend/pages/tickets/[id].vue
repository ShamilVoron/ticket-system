<script setup lang="ts">
import { 
  ArrowLeft, Clock, User, Building2, MapPin, 
  MessageSquare, FileText, Paperclip, Send,
  CheckCircle, AlertCircle, Edit2, Check, X,
  RefreshCw, ListChecks, Link as LinkIcon, Download,
  ExternalLink, Copy, HelpCircle, Info, Hash, Briefcase, Plus, ChevronRight,
} from 'lucide-vue-next'
import type { Ticket, Comment, FieldReport, Subtask, Attachment, SystemStatus, EmployeeOption, TimelineItem } from '~/types'
import MessageReactions from '~/components/MessageReactions.vue'
import { resolvePublicApiBaseUrl } from '~/utils/resolvePublicApiBaseUrl'

const route = useRoute()
const router = useRouter()
const api = useApi()
const auth = useAuthStore()
const { can } = useStaffPermissions()
const toast = useToast()
const pageHeader = usePageHeader()

const ticketId = parseInt(route.params.id as string)

const ticket = ref<Ticket | null>(null)
const isMyTicket = computed(() => {
  if (!ticket.value) return false
  const uid = auth.userId
  if ((ticket.value as any).assigneeIds?.includes?.(uid)) return true
  return ticket.value.assignee === auth.fullName || (ticket.value.assignees || []).includes(auth.fullName)
})
const comments = ref<Comment[]>([])
const timeline = ref<TimelineItem[]>([])
const reports = ref<FieldReport[]>([])
const suggestingReply = ref(false)
const subtasks = ref<Subtask[]>([])
const attachments = ref<Attachment[]>([])
/** Блок «Файлы заявки»: вложения к заявке и к комментариям (подзадачи — только в карточках подзадач) */
const attachmentsForTicketFilesBlock = computed(() =>
  [...attachments.value]
    .filter((a) => a.subtaskId == null)
    .sort((x, y) => new Date(y.uploadedAt).getTime() - new Date(x.uploadedAt).getTime())
)
const statuses = ref<SystemStatus[]>([])
const employees = ref<EmployeeOption[]>([])

const apiBase = computed(() => {
  const cfg = useRuntimeConfig()
  return resolvePublicApiBaseUrl(cfg.public.apiBaseUrl as string | undefined)
})

function resolveMediaUrl(raw: string): string {
  const s = (raw || '').trim()
  if (!s) return ''
  if (/^https?:\/\//i.test(s)) {
    try {
      const u = new URL(s)
      const h = u.hostname.toLowerCase()
      if (h === 'localhost' || h === '127.0.0.1' || h === '[::1]') {
        return `${apiBase.value}${u.pathname}${u.search}`
      }
    } catch { /* ignore */ }
    return s
  }
  if (s.startsWith('/')) return `${apiBase.value}${s}`
  return s
}

const avatarByUserId = computed(() => {
  const map = new Map<string, string>()
  for (const e of employees.value || []) {
    const uid = (e.userId || '').trim()
    const av = resolveMediaUrl((e as any).avatarUrl || '')
    if (uid && av) map.set(uid, av)
  }
  return map
})

const avatarByName = computed(() => {
  const map = new Map<string, string>()
  for (const e of employees.value || []) {
    const name = (e.fullName || '').trim().toLowerCase()
    const av = resolveMediaUrl((e as any).avatarUrl || '')
    if (name && av && !map.has(name)) map.set(name, av)
  }
  return map
})

function normalizeNameForAvatar(raw: string): string {
  let s = (raw || '').trim().toLowerCase()
  if (!s) return ''
  // normalize whitespace
  s = s.replace(/\s+/g, ' ')
  // strip common role suffixes accidentally appended to authorName
  const suffixes = [
    ' супер-админ',
    ' супер админ',
    ' админ',
    ' координатор',
    ' сапорт 1 линия',
    ' сапорт 2 линия',
    ' выездной инженер',
    ' разработчик',
    ' директор',
  ]
  for (const suf of suffixes) {
    if (s.endsWith(suf)) {
      s = s.slice(0, -suf.length).trim()
      break
    }
  }
  return s
}

function resolveAvatarByLooseName(authorName: string): string {
  const n = normalizeNameForAvatar(authorName)
  if (!n) return ''
  const direct = avatarByName.value.get(n)
  if (direct) return direct
  // Loose match: employee fullName is prefix of authorName (or equals after stripping)
  for (const e of employees.value || []) {
    const en = normalizeNameForAvatar(e.fullName || '')
    if (!en) continue
    if (n === en || n.startsWith(en + ' ') || en.startsWith(n + ' ')) {
      const av = resolveMediaUrl((e as any).avatarUrl || '')
      if (av) return av
    }
  }
  return ''
}

function onAvatarError(e: Event) {
  const img = e.target as HTMLImageElement
  img.style.display = 'none'
  const next = img.nextElementSibling as HTMLElement | null
  if (next) next.style.display = ''
}

function resolveCommentAvatar(c: Comment): string {
  const uid = (c.authorUserId || '').trim()
  if (uid) return avatarByUserId.value.get(uid) || ''
  return resolveAvatarByLooseName(c.authorName || '')
}

const loading = ref(true)

const newComment = ref('')
const commentTextareaRef = ref<HTMLTextAreaElement | null>(null)
const commentInternal = useCookie('ticket_comment_internal', { default: () => false })
const sendingComment = ref(false)

const COMMENT_TEXTAREA_MIN_PX = 120
const COMMENT_TEXTAREA_MAX_PX = 400

function autoResizeComment() {
  const el = commentTextareaRef.value
  if (!el) return
  el.style.height = 'auto'
  const newHeight = Math.max(el.scrollHeight, COMMENT_TEXTAREA_MIN_PX)
  el.style.height = Math.min(newHeight, COMMENT_TEXTAREA_MAX_PX) + 'px'
}

const editedAltTitle = ref('')
const altTitleEditMode = ref(false)
const altTitleInputRef = ref<HTMLInputElement | null>(null)

const altTitleDirty = computed(() => {
  if (!ticket.value) return false
  return (editedAltTitle.value ?? '').trim() !== (ticket.value.alternativeTitle ?? '').trim()
})

/** Альт. название — по матрице прав (по умолчанию без выездных). */
const canEditAlternativeTitle = computed(() => auth.isStaff && can('ticketEditAlternativeTitle'))

const showAltSaveButton = computed(
  () =>
    canEditAlternativeTitle.value &&
    !!ticket.value &&
    altTitleEditMode.value &&
    altTitleDirty.value
)

function onAltTitleLineDblClick() {
  if (!canEditAlternativeTitle.value || !ticket.value) return
  if (altTitleEditMode.value) return
  editedAltTitle.value = ticket.value.alternativeTitle ?? ''
  altTitleEditMode.value = true
  nextTick(() => altTitleInputRef.value?.focus())
}

function onAltTitleBlur() {
  window.setTimeout(() => {
    if (!altTitleDirty.value) altTitleEditMode.value = false
  }, 200)
}

const editingProblem = ref(false)
const editProblemVal = ref('')
const savingProblem = ref(false)

async function saveProblem() {
  if (!can('ticketEditDescription') || !ticket.value) return
  savingProblem.value = true
  try {
    await api.tickets.updateProblem(ticketId, editProblemVal.value)
    ticket.value.problem = editProblemVal.value
    editingProblem.value = false
    toast.success('Описание обновлено')
  } catch { toast.error('Не удалось обновить описание') }
  finally { savingProblem.value = false }
}

const statusDropdownOpen = ref(false)
const openingTicketChat = ref(false)

async function openTicketChat() {
  if (!ticket.value || openingTicketChat.value) return
  openingTicketChat.value = true
  try {
    const { id } = await api.messenger.ensureTicketChat(ticketId)
    await navigateTo({ path: '/messenger', query: { c: id } })
  } catch (e: any) {
    toast.error(e?.data?.message || e?.message || 'Не удалось открыть чат по заявке')
  } finally {
    openingTicketChat.value = false
  }
}

// Attachments
const fileInputRef = ref<HTMLInputElement | null>(null)
const subtaskFileInputRef = ref<HTMLInputElement | null>(null)
const uploadingFiles = ref(false)
const commentPendingFiles = ref<File[]>([])
const commentFileRef = ref<HTMLInputElement | null>(null)

// Lightbox
const lightboxOpen = ref(false)
const lightboxUrl = ref('')
const lightboxImages = ref<string[]>([])
const lightboxIndex = ref(0)

function openLightbox(url: string, allImages?: string[]) {
  lightboxUrl.value = url
  if (allImages?.length) {
    lightboxImages.value = allImages
    lightboxIndex.value = allImages.indexOf(url)
    if (lightboxIndex.value < 0) lightboxIndex.value = 0
  } else {
    lightboxImages.value = [url]
    lightboxIndex.value = 0
  }
  lightboxOpen.value = true
}

function lightboxPrev() {
  if (lightboxIndex.value > 0) {
    lightboxIndex.value--
    lightboxUrl.value = lightboxImages.value[lightboxIndex.value]
  }
}

function lightboxNext() {
  if (lightboxIndex.value < lightboxImages.value.length - 1) {
    lightboxIndex.value++
    lightboxUrl.value = lightboxImages.value[lightboxIndex.value]
  }
}

function closeLightbox() {
  lightboxOpen.value = false
}

function onLightboxKeydown(e: KeyboardEvent) {
  if (!lightboxOpen.value) return
  if (e.key === 'Escape') closeLightbox()
  else if (e.key === 'ArrowLeft') lightboxPrev()
  else if (e.key === 'ArrowRight') lightboxNext()
}


// Coordinator field editing
const editingPriority = ref(false)
const editingRequestType = ref(false)
const editingDepartment = ref(false)
const editPriorityVal = ref('')
const editRequestTypeVal = ref('')
const editDepartmentVal = ref('')
const departments = ref<{ value: string; label: string }[]>([])
const savingFields = ref(false)

const PRIORITY_META = [
  { value: 'Низкий' },
  { value: 'Средний' },
  { value: 'Высокий' },
  { value: 'Критический' },
] as const

const DEPARTMENTS_FALLBACK: { value: string; label: string }[] = [
  { value: 'Координатор', label: 'Координатор' },
  { value: '1 линия', label: '1 линия' },
  { value: '2 линия', label: '2 линия' },
  { value: 'Разработчики', label: 'Разработчики' },
  { value: 'Выездные инженеры', label: 'Выездные инженеры' },
  { value: 'Ремонт / сервис', label: 'Ремонт / сервис' },
  { value: 'Бухгалтерия', label: 'Бухгалтерия' },
  { value: 'Закупки', label: 'Закупки' },
  { value: 'Системный администратор', label: 'Системный администратор' },
]
const REQUEST_TYPES = [
  'Ремонт', 'Подменное оборудование', 'Монтаж / Подключение', 'Поломка',
  'Настройка ПО', 'Настройка оборудования', 'Помощь с ПО', 'Сеть / Интернет',
  'Доступы', 'Консультация', 'Плановое ТО', 'Разработка / Доработка',
  'Документы / Счёт', 'Другое',
]

// Direct assignee change (coordinator)
const assigneeModalOpen = ref(false)
const assigneeSearch = ref('')
const selectedAssigneeIds = ref<string[]>([])

// Delegation (1st/2nd line, field engineers)
const delegateModalOpen = ref(false)
const delegateSearch = ref('')
const delegateTargetId = ref('')
const delegateReason = ref('')

// Subtasks
const newSubtaskTitle = ref('')
const newSubtaskDesc = ref('')
const newSubtaskStatus = ref('в процессе')
const newSubtaskKnowledgeable = ref<string[]>([])
const newSubtaskFiles = ref<File[]>([])
const subtaskKnowledgeOpen = ref(false)
const subtaskKnowledgeSearch = ref('')
const creatingSubtask = ref(false)

/** По умолчанию развёрнуто; в объекте только явные false — свернуто */
const subtaskDetailOpen = ref<Record<number, boolean>>({})
function isSubtaskDetailOpen(id: number) {
  return subtaskDetailOpen.value[id] !== false
}
function toggleSubtaskDetail(id: number) {
  const open = isSubtaskDetailOpen(id)
  subtaskDetailOpen.value = { ...subtaskDetailOpen.value, [id]: !open }
}

const reportDetailOpen = ref<Record<number, boolean>>({})
function isReportDetailOpen(id: number) {
  return reportDetailOpen.value[id] !== false
}
function toggleReportDetail(id: number) {
  const open = isReportDetailOpen(id)
  reportDetailOpen.value = { ...reportDetailOpen.value, [id]: !open }
}

/** Целые блоки «Подзадачи» / «Акты выезда» в правой колонке — по умолчанию свёрнуты */
const subtasksSectionOpen = ref(false)
const reportsSectionOpen = ref(false)

// Reports (Acts)
const reportModalOpen = ref(false)
const reportEditingId = ref<number | null>(null)
const reportForm = reactive({
  engineerName: auth.fullName,
  visitDate: new Date().toISOString().slice(0, 16),
  actionType: 'Осмотр / Диагностика',
  equipmentType: '',
  equipmentStatus: 'В работе',
  equipmentSerial: '',
  workDone: '',
  transferredTo: ''
})
const creatingReport = ref(false)
const reportActionTypes = ['Ремонт', 'Монтаж', 'Замена', 'Осмотр / Диагностика', 'Доставка', 'Другое']
const reportEquipStatuses = ['В работе', 'Требует ремонта', 'Списано', 'Подмена']

let pollInterval: any = null

async function loadData() {
  loading.value = true
  try {
    const results = await Promise.allSettled([
      api.tickets.getById(ticketId),
      api.tickets.getComments(ticketId),
      api.tickets.getReports(ticketId),
      api.subtasks.getAll(ticketId),
      api.tickets.getAttachments(ticketId),
      api.systemSettings.getStatuses(),
      api.tickets.getTimeline(ticketId),
    ])
    const [ticketData, commentsData, reportsData, subtasksData, attaches, statusList, timelineData] = results.map((r, i) => {
      if (r.status === 'fulfilled') return r.value
      console.error(`Ticket page load item ${i} failed:`, r.reason)
      return null
    })
    if (ticketData) {
      ticket.value = ticketData
      editedAltTitle.value = ticketData.alternativeTitle ?? ''
    }
    if (commentsData) comments.value = commentsData
    if (reportsData) reports.value = reportsData
    if (subtasksData) subtasks.value = subtasksData
    if (attaches) attachments.value = attaches
    if (statusList) statuses.value = statusList
    if (timelineData) timeline.value = timelineData as TimelineItem[]

    // Mark ticket as read when viewed
    if (ticketData) {
      try {
        await api.tickets.markAsRead(ticketId)
      } catch { /* ignore */ }
      /* Заголовок заявки только в теле страницы — в layout короткая подпись «Заявка» */
      pageHeader.set('', true)
    }

    if (auth.isStaff) {
      const emps = await api.employees.getAll()
      employees.value = emps.map((e: any) => ({
        userId: e.userId, fullName: e.fullName, role: e.role, avatarUrl: e.avatarUrl
      }))
    }
  } catch (error) {
    console.error('Failed to load data:', error)
  } finally {
    loading.value = false
  }
}

async function weakRefresh() {
  try {
    const [ticketData, commentsData, reportsData, subtasksData, attaches, timelineData] = await Promise.all([
      api.tickets.getById(ticketId),
      api.tickets.getComments(ticketId),
      api.tickets.getReports(ticketId),
      api.subtasks.getAll(ticketId),
      api.tickets.getAttachments(ticketId),
      api.tickets.getTimeline(ticketId),
    ])
    ticket.value = ticketData
    editedAltTitle.value = ticketData?.alternativeTitle ?? ''
    comments.value = commentsData
    reports.value = reportsData
    subtasks.value = subtasksData
    attachments.value = attaches
    timeline.value = timelineData as TimelineItem[]
  } catch { toast.error('Не удалось обновить данные тикета') }
}

const filteredDelegateEmployees = computed(() => {
  const q = delegateSearch.value.toLowerCase()
  let list = employees.value
  if (q) list = list.filter(e => e.fullName.toLowerCase().includes(q))
  return list
})

const filteredKnowledgeEmployees = computed(() => {
  const q = subtaskKnowledgeSearch.value.toLowerCase()
  let list = employees.value
  if (q) list = list.filter(e => e.fullName.toLowerCase().includes(q))
  return list
})

async function saveStatus(s: string) {
  if (!ticket.value || ticket.value.status === s) {
    statusDropdownOpen.value = false
    return
  }
  if (!isMyTicket.value && !can('ticketEditForeignStatus')) {
    toast.error('Нет права менять статус чужой заявки')
    statusDropdownOpen.value = false
    return
  }
  try {
    await api.tickets.updateStatus(ticketId, s)
    ticket.value.status = s
    toast.success('Статус обновлён')
  } catch { toast.error('Не удалось обновить статус') }
  statusDropdownOpen.value = false
}

async function saveAltTitle() {
  if (!canEditAlternativeTitle.value || !ticket.value || !altTitleDirty.value) return
  try {
    await api.tickets.updateTitle(ticketId, ticket.value.title, editedAltTitle.value)
    ticket.value.alternativeTitle = editedAltTitle.value
    altTitleEditMode.value = false
    toast.success('Заголовок сохранён')
  } catch { toast.error('Не удалось сохранить заголовок') }
}

async function handleFileUpload(e: Event) {
  const target = e.target as HTMLInputElement
  if (!target.files || !target.files.length) return
  
  uploadingFiles.value = true
  try {
    for(let i=0; i<target.files.length; i++) {
      const formData = new FormData()
      formData.append('file', target.files[i])
      formData.append('uploadedBy', auth.fullName)
      await api.tickets.uploadAttachment(ticketId, formData)
    }
    toast.success('Файлы загружены')
    await weakRefresh()
  } catch { toast.error('Ошибка загрузки файлов') }
  finally {
    uploadingFiles.value = false
    if(fileInputRef.value) fileInputRef.value.value = ''
  }
}

function onCommentPaste(e: ClipboardEvent) {
  const items = e.clipboardData?.items
  if (!items) return
  for (let i = 0; i < items.length; i++) {
    if (items[i].type.startsWith('image/')) {
      const file = items[i].getAsFile()
      if (file) {
        const named = new File([file], `paste_${Date.now()}.png`, { type: file.type })
        commentPendingFiles.value.push(named)
        toast.info('Фото вставлено из буфера')
      }
    }
  }
}

function removeCommentFile(idx: number) {
  commentPendingFiles.value.splice(idx, 1)
}

async function toggleCommentReaction(comment: Comment, emoji: string) {
  if (!can('canReactToTicketComments')) return
  try {
    const updated = await api.tickets.toggleReaction(ticketId, comment.id, emoji)
    const idx = comments.value.findIndex((c) => c.id === comment.id)
    if (idx !== -1) {
      comments.value[idx] = updated as Comment
    }
  } catch {
    toast.error('Не удалось изменить реакцию')
  }
}

/** Как на бэкенде: после комментария сотрудника «Открыт» → «В работе». */
const TICKET_STATUS_IN_PROGRESS = 'В работе'

function ticketStatusLooksOpen(status: string | undefined | null): boolean {
  const s = (status ?? '').trim().toLowerCase()
  if (!s) return false
  if (s === 'open') return true
  if (s === 'открыт' || s === 'открыта') return true
  const def = statuses.value.find((st) => st.isDefault && st.isActive)?.name?.trim().toLowerCase()
  return !!def && def === s
}

async function sendComment() {
  if (!newComment.value.trim() && commentPendingFiles.value.length === 0) return
  sendingComment.value = true
  const bumpStatusAfterComment =
    auth.isStaff && ticket.value && ticketStatusLooksOpen(ticket.value.status)
  try {
    const comment = await api.tickets.addComment(ticketId, {
      authorName: auth.fullName,
      authorRole: auth.role,
      text: newComment.value,
      isInternal: commentInternal.value,
      authorUserId: auth.userId,
    })

    if (commentPendingFiles.value.length > 0 && comment?.id) {
      for (const file of commentPendingFiles.value) {
        const formData = new FormData()
        formData.append('file', file)
        formData.append('uploadedBy', auth.fullName)
        formData.append('commentId', comment.id.toString())
        await api.tickets.uploadAttachment(ticketId, formData)
      }
    }

    newComment.value = ''
    commentPendingFiles.value = []
    nextTick(() => autoResizeComment())
    if (bumpStatusAfterComment && ticket.value) {
      ticket.value = { ...ticket.value, status: TICKET_STATUS_IN_PROGRESS }
    }
    await weakRefresh()
  } catch { toast.error('Не удалось отправить комментарий') } 
  finally {
    sendingComment.value = false
  }
}

async function suggestReply() {
  if (suggestingReply.value) return
  suggestingReply.value = true
  try {
    const res = await api.tickets.suggestReply(ticketId)
    if (res?.suggestion) {
      newComment.value = res.suggestion
      await nextTick()
      autoResizeComment()
      toast.success(res.source === 'openai' ? 'Черновик от AI' : 'Черновик из базы знаний')
    } else {
      toast.warning('Не удалось подобрать ответ')
    }
  } catch {
    toast.error('Не удалось получить подсказку')
  } finally {
    suggestingReply.value = false
  }
}

function commentFromTimeline(item: TimelineItem): Comment | undefined {
  if (item.type !== 'comment' || item.entityId == null) return undefined
  return comments.value.find((c) => c.id === item.entityId)
}

function timelineBadge(item: TimelineItem): { label: string; class: string } {
  if (item.type === 'created') return { label: 'Создана', class: 'bg-blue-100 text-blue-700 border-blue-200' }
  if (item.type === 'field_report') return { label: 'Акт', class: 'bg-orange-100 text-orange-700 border-orange-200' }
  if (item.channel === 'email') return { label: 'Email', class: 'bg-cyan-100 text-cyan-700 border-cyan-200' }
  return { label: 'Коммент', class: 'bg-indigo-100 text-indigo-700 border-indigo-200' }
}

function onCommentFileSelect(e: Event) {
  const target = e.target as HTMLInputElement
  if (target.files) {
    commentPendingFiles.value.push(...Array.from(target.files))
  }
  if (target) target.value = ''
}

async function createSubtask() {
  if (!can('ticketCreateSubtask')) return
  if (!isMyTicket.value && !can('ticketInteractForeign')) return
  if(!newSubtaskTitle.value.trim()) return
  creatingSubtask.value = true
  try {
    const created = await api.subtasks.create(ticketId, {
      title: newSubtaskTitle.value,
      description: newSubtaskDesc.value,
      status: newSubtaskStatus.value,
      knowledgeableUserIds: newSubtaskKnowledgeable.value
    })
    
    // Upload subtask files if any
    if (newSubtaskFiles.value.length > 0) {
      for (const file of newSubtaskFiles.value) {
        const formData = new FormData()
        formData.append('file', file)
        formData.append('uploadedBy', auth.fullName)
        formData.append('subtaskId', created.id.toString())
        await api.tickets.uploadAttachment(ticketId, formData)
      }
    }

    newSubtaskTitle.value = ''
    newSubtaskDesc.value = ''
    newSubtaskStatus.value = 'в процессе'
    newSubtaskKnowledgeable.value = []
    newSubtaskFiles.value = []
    toast.success('Подзадача создана')
    await weakRefresh()
  } catch { toast.error('Не удалось создать подзадачу') }
  finally {
    creatingSubtask.value = false
  }
}

function openReportModalCreate() {
  if (!can('ticketCreateExitActs')) return
  if (!isMyTicket.value && !can('ticketInteractForeign')) return
  reportEditingId.value = null
  reportForm.engineerName = auth.fullName || ''
  reportForm.visitDate = new Date().toISOString().slice(0, 16)
  reportForm.actionType = 'Осмотр / Диагностика'
  reportForm.equipmentType = ticket.value?.repairEquipmentType || ''
  reportForm.equipmentStatus = 'В работе'
  reportForm.equipmentSerial = ''
  reportForm.workDone = ''
  reportForm.transferredTo = ''
  reportModalOpen.value = true
}

function openReportModalEdit(r: FieldReport) {
  if (!can('ticketCreateExitActs')) return
  if (!isMyTicket.value && !can('ticketInteractForeign')) return
  reportEditingId.value = r.id
  reportForm.engineerName = r.engineerName || ''
  const vd = r.visitDate
  reportForm.visitDate =
    typeof vd === 'string' ? vd.slice(0, 16) : new Date(vd as string).toISOString().slice(0, 16)
  reportForm.actionType = r.actionType || 'Осмотр / Диагностика'
  reportForm.equipmentType = r.equipmentType || ticket.value?.repairEquipmentType || ''
  reportForm.equipmentSerial = r.equipmentSerial || ''
  reportForm.equipmentStatus = r.equipmentStatus || 'В работе'
  reportForm.workDone = r.workDone || ''
  reportForm.transferredTo = r.transferredTo || ''
  reportModalOpen.value = true
}

function closeReportModal() {
  reportModalOpen.value = false
  reportEditingId.value = null
}

async function saveReport() {
  if (!can('ticketCreateExitActs')) return
  if (!isMyTicket.value && !can('ticketInteractForeign')) return
  if (!reportForm.workDone.trim()) return
  creatingReport.value = true
  try {
    const payload = {
      engineerName: reportForm.engineerName,
      visitDate: reportForm.visitDate,
      actionType: reportForm.actionType,
      equipmentType: reportForm.equipmentType || ticket.value?.repairEquipmentType || '',
      equipmentSerial: reportForm.equipmentSerial,
      equipmentStatus: reportForm.equipmentStatus,
      workDone: reportForm.workDone,
      transferredTo: reportForm.transferredTo
    }
    if (reportEditingId.value != null) {
      await api.tickets.updateReport(ticketId, reportEditingId.value, payload)
      toast.success('Акт обновлён')
    } else {
      await api.tickets.addReport(ticketId, payload)
      toast.success('Отчёт добавлен')
    }
    closeReportModal()
    reportForm.workDone = ''
    reportForm.equipmentSerial = ''
    reportForm.transferredTo = ''
    await loadData()
  } catch {
    toast.error(reportEditingId.value != null ? 'Не удалось сохранить акт' : 'Не удалось добавить отчёт')
  } finally {
    creatingReport.value = false
  }
}

function handleSubtaskFileChange(e: Event) {
  const target = e.target as HTMLInputElement
  if (target.files) {
    newSubtaskFiles.value = [...newSubtaskFiles.value, ...Array.from(target.files)]
  }
}

function removeSubtaskFile(idx: number) {
  newSubtaskFiles.value.splice(idx, 1)
}

function toggleKnowledge(userId: string) {
  const idx = newSubtaskKnowledgeable.value.indexOf(userId)
  if (idx === -1) newSubtaskKnowledgeable.value.push(userId)
  else newSubtaskKnowledgeable.value.splice(idx, 1)
}

const editingSubtaskId = ref<number | null>(null)
const editSubtaskDraft = reactive({
  title: '',
  description: '',
  status: 'в процессе',
  knowledgeableUserIds: [] as string[],
})
const editSubtaskKnowledgeOpen = ref(false)
const savingSubtaskEdit = ref(false)

function startEditSubtask(st: Subtask) {
  if (!can('ticketCreateSubtask')) return
  editingSubtaskId.value = st.id
  editSubtaskDraft.title = st.title
  editSubtaskDraft.description = st.description || ''
  editSubtaskDraft.status = st.status
  editSubtaskDraft.knowledgeableUserIds = [...(st.knowledgeableUserIds || [])]
  subtaskDetailOpen.value = { ...subtaskDetailOpen.value, [st.id]: true }
}

function cancelEditSubtask() {
  editingSubtaskId.value = null
  editSubtaskKnowledgeOpen.value = false
}

async function deleteSubtask(st: Subtask) {
  if (!can('ticketDeleteSubtask')) return
  if (!confirm(`Удалить подзадачу «${st.title}»?`)) return
  try {
    await api.subtasks.delete(ticketId, st.id)
    subtasks.value = subtasks.value.filter((s) => s.id !== st.id)
    toast.success('Подзадача удалена')
  } catch {
    toast.error('Не удалось удалить подзадачу')
  }
}

async function saveSubtaskEdit() {
  if (!can('ticketCreateSubtask')) return
  const id = editingSubtaskId.value
  if (!id || !editSubtaskDraft.title.trim()) return
  savingSubtaskEdit.value = true
  try {
    await api.subtasks.update(ticketId, id, {
      title: editSubtaskDraft.title.trim(),
      description: editSubtaskDraft.description.trim(),
      status: editSubtaskDraft.status,
      knowledgeableUserIds: editSubtaskDraft.knowledgeableUserIds,
    })
    toast.success('Подзадача сохранена')
    editingSubtaskId.value = null
    editSubtaskKnowledgeOpen.value = false
    await weakRefresh()
  } catch {
    toast.error('Не удалось сохранить подзадачу')
  } finally {
    savingSubtaskEdit.value = false
  }
}

function toggleEditKnowledge(userId: string) {
  const idx = editSubtaskDraft.knowledgeableUserIds.indexOf(userId)
  if (idx === -1) editSubtaskDraft.knowledgeableUserIds.push(userId)
  else editSubtaskDraft.knowledgeableUserIds.splice(idx, 1)
}

function getSubtaskStatusColor(status: string) {
  if (status === 'готово') return 'text-green-600 bg-green-50 border-green-100'
  if (status === 'не актуально') return 'text-gray-400 bg-gray-50 border-gray-100'
  return 'text-indigo-600 bg-indigo-50 border-indigo-100'
}

async function confirmDelegation() {
  if(!delegateTargetId.value) return
  try {
    await api.tickets.delegate(ticketId, auth.userId, delegateTargetId.value, delegateReason.value)
    delegateModalOpen.value = false
    delegateTargetId.value = ''
    delegateReason.value = ''
    toast.success('Тикет делегирован')
    await weakRefresh()
  } catch { toast.error('Не удалось делегировать тикет') }
}

async function saveField(field: 'priority' | 'requestType' | 'department', value: string) {
  const r = (auth.role || '').toLowerCase()
  const coord = ['coordinator', 'super_admin', 'director', 'head_support', 'head_engineers', 'head_dev', 'head_repair', 'sysadmin'].includes(r)
  if (!ticket.value || !coord || !can('ticketEditParameters')) return
  savingFields.value = true
  try {
    await api.tickets.updateFields(ticketId, { [field]: value })
    ;(ticket.value as any)[field] = value
    editingPriority.value = false
    editingRequestType.value = false
    editingDepartment.value = false
    toast.success('Поле обновлено')
  } catch { toast.error('Не удалось обновить поле') }
  finally { savingFields.value = false }
}

function startEditAssignees() {
  const r = (auth.role || '').toLowerCase()
  const coord = ['coordinator', 'super_admin', 'director', 'head_support', 'head_engineers', 'head_dev', 'head_repair', 'sysadmin'].includes(r)
  if (!coord || !can('ticketEditParameters')) return
  const ids = (ticket.value?.assigneeIds || []).filter(Boolean) as string[]
  if (ids.length) {
    selectedAssigneeIds.value = [...ids]
  } else {
    const names = ticket.value?.assignees || []
    selectedAssigneeIds.value = employees.value
      .filter(e => names.includes(e.fullName))
      .map(e => e.userId)
  }
  assigneeSearch.value = ''
  assigneeModalOpen.value = true
}

async function saveAssignees() {
  if (!ticket.value) return
  const ids = selectedAssigneeIds.value.filter(Boolean)
  if (!ids.length) return
  try {
    const first = ids[0] || ''
    await api.tickets.updateAssignee(ticketId, first, ids)
    assigneeModalOpen.value = false
    toast.success('Ответственные обновлены')
    await weakRefresh()
  } catch { toast.error('Не удалось обновить ответственных') }
}

const filteredAssigneeEmployees = computed(() => {
  const q = assigneeSearch.value.toLowerCase()
  let list = employees.value
  if (q) list = list.filter(e => e.fullName.toLowerCase().includes(q))
  return list
})

function toggleAssignee(userId: string) {
  const idx = selectedAssigneeIds.value.indexOf(userId)
  if (idx === -1) selectedAssigneeIds.value.push(userId)
  else selectedAssigneeIds.value.splice(idx, 1)
}

const isCoordinator = computed(() => {
  const r = (auth.role || '').toLowerCase()
  return ['coordinator', 'super_admin', 'director', 'head_support', 'head_engineers', 'head_dev', 'head_repair', 'sysadmin'].includes(r)
})

const canEditTicketParams = computed(() => isCoordinator.value && can('ticketEditParameters'))

const canInteractWithTicket = computed(() => {
  if (!auth.isStaff) return false
  if (isMyTicket.value) return true
  return can('ticketInteractForeign')
})

/** Делегирование — только 1/2 линия и выездные (как в ТЗ), и только если своя заявка или есть право взаимодействия с чужой. */
const canDelegateTicket = computed(() => {
  const r = (auth.role || '').toLowerCase()
  const roleOk = auth.isStaff && ['support', 'support_l1', 'support_l2', 'field_engineer'].includes(r)
  if (!roleOk) return false
  return isMyTicket.value || can('ticketInteractForeign')
})

const taskLinksJsonPairs = computed(() => {
  if (!ticket.value?.taskLinksJson) return []
  try {
    return JSON.parse(ticket.value.taskLinksJson)
  } catch {
    return []
  }
})

/** Блок ссылок: редактирование или уже есть ссылки (просмотр). */
const showTaskLinksBlock = computed(
  () => auth.isStaff && (can('ticketEditTaskLinks') || taskLinksJsonPairs.value.length > 0),
)

type LinkRow = { url: string; number: string; comment: string }
const editingLinks = ref(false)
const linkRows = ref<LinkRow[]>([])

function startEditLinks() {
  if (!can('ticketEditTaskLinks')) return
  if (ticket.value?.taskLinksJson) {
    try {
      const arr = JSON.parse(ticket.value.taskLinksJson)
      linkRows.value = arr.map((l: any) => ({ url: l.url || '', number: l.number || '', comment: l.comment || l.label || '' }))
    } catch { linkRows.value = [{ url: '', number: '', comment: '' }] }
  } else {
    linkRows.value = [{ url: '', number: '', comment: '' }]
  }
  editingLinks.value = true
}

function addLinkRow() { linkRows.value.push({ url: '', number: '', comment: '' }) }
function removeLinkRow(i: number) { if (linkRows.value.length > 1) linkRows.value.splice(i, 1) }

function extractTaskNumber(url: string): string {
  if (!url) return ''
  const u = url.trim()
  const issuesMatch = u.match(/\/issues\/(\d+)/)
  if (issuesMatch) return issuesMatch[1]
  const browseMatch = u.match(/\/browse\/([A-Z0-9]+-\d+)/)
  if (browseMatch) return browseMatch[1]
  return ''
}

function onTaskUrlBlur(row: LinkRow) {
  if (row.url && !row.number) {
    const num = extractTaskNumber(row.url)
    if (num) row.number = num
  }
}

async function saveLinks() {
  if (!can('ticketEditTaskLinks')) return
  try {
    const clean = linkRows.value.filter(l => l.url.trim() || l.number.trim())
    await api.tickets.updateLinks(ticketId, JSON.stringify(clean))
    if (ticket.value) ticket.value.taskLinksJson = JSON.stringify(clean)
    editingLinks.value = false
    toast.success('Ссылки сохранены')
  } catch { toast.error('Не удалось сохранить ссылки') }
}

function priorityBadge(p: string): string {
  const map: Record<string, string> = {
    'Низкий': 'bg-gray-100 text-gray-700 border-gray-300',
    'Средний': 'bg-gray-100 text-gray-700 border-gray-300',
    'Высокий': 'bg-yellow-100 text-yellow-800 border-yellow-300',
    'Критический': 'bg-red-100 text-red-800 border-red-300',
  }
  return map[p] || map['Низкий']
}

function getStatusColor(status: string): string {
  const statusObj = statuses.value.find(s => s.name === status)
  return statusObj?.colorClass || 'bg-gray-100 text-gray-700 border-gray-200'
}

function formatDate(iso: string): string {
  if(!iso) return '—'
  return new Date(iso).toLocaleDateString('ru-RU', { 
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
  })
}

function copyToTG() {
  if(!ticket.value) return
  const text = `📬 #${ticket.value.id}: ${ticket.value.title}\n\n📝 Описание:\n${ticket.value.problem}\n\n📍 Объект: ${ticket.value.objectName || '—'}\n👤 Клиент: ${ticket.value.clientName}`
  
  if (navigator.clipboard && window.isSecureContext) {
    navigator.clipboard.writeText(text).then(() => {
      toast.success('Скопировано для Telegram')
    }).catch(() => {
      fallbackCopy(text)
    })
  } else {
    fallbackCopy(text)
  }
}

function fallbackCopy(text: string) {
  const ta = document.createElement('textarea')
  ta.value = text
  ta.style.position = 'fixed'
  ta.style.left = '-9999px'
  ta.style.top = '-9999px'
  document.body.appendChild(ta)
  ta.focus()
  ta.select()
  try {
    document.execCommand('copy')
    toast.success('Скопировано для Telegram')
  } catch {
    toast.error('Не удалось скопировать')
  }
  document.body.removeChild(ta)
}

function getRoleLabel(role: string): string {
  const map: Record<string, string> = {
    'Coordinator': 'Координатор',
    'Engineer': 'Инженер',
    'Client': 'Клиент',
    'Admin': 'Администратор'
  }
  return map[role] || role
}

function onCommentKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
    e.preventDefault()
    sendComment()
  }
}

useTicketSignalR((payload) => {
  const changedId = payload.ticketId ?? null
  if (changedId === null || changedId === ticketId) weakRefresh()
})

onMounted(async () => {
  loadData()
  pollInterval = setInterval(weakRefresh, 30000)
  window.addEventListener('keydown', onLightboxKeydown)
  nextTick(() => autoResizeComment())
  try {
    const depts = await api.departments.getAll()
    const mapped = Array.isArray(depts)
      ? depts.map((d: any) => ({
          value: String(d.value ?? d.name ?? d.label ?? d),
          label: String(d.label ?? d.name ?? d.value ?? d),
        }))
      : []
    departments.value = mapped.length ? mapped : [...DEPARTMENTS_FALLBACK]
  } catch {
    departments.value = [...DEPARTMENTS_FALLBACK]
  }
})

onUnmounted(() => {
  if(pollInterval) clearInterval(pollInterval)
  pageHeader.clear()
  window.removeEventListener('keydown', onLightboxKeydown)
})
</script>

<template>
  <div class="space-y-4 sm:space-y-6 w-full max-w-none mx-auto px-2 sm:px-4 lg:px-6 xl:px-8 pb-64 sm:pb-72 lg:pb-80">
    <!-- Шапка: staff — тема клиента в h1 + строка «Альт:»; двойной щелчок — редактирование альт. названия -->
    <div class="flex items-start justify-between pt-1 sm:pt-2 gap-3">
      <div class="flex flex-1 min-w-0 items-start gap-x-2 gap-y-1 sm:gap-x-3">
        <span class="text-xs sm:text-sm font-mono text-gray-400 dark:text-zinc-500 shrink-0 pt-0.5">#{{ String(ticketId).padStart(6, '0') }}</span>
        <div
          class="flex min-w-0 flex-1 flex-col gap-0.5"
          :class="canEditAlternativeTitle && ticket ? 'cursor-text' : ''"
          :title="canEditAlternativeTitle && ticket ? 'Дважды щёлкните, чтобы изменить альтернативное название' : undefined"
          @dblclick="onAltTitleLineDblClick"
        >
          <div class="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1.5">
            <h1 v-if="ticket" class="text-xs sm:text-sm font-bold text-gray-900 dark:text-gray-100 truncate min-w-0 max-w-[min(100%,42rem)] sm:max-w-[min(100%,50rem)]">
              {{ canEditAlternativeTitle ? ticket.title : (ticket.alternativeTitle || ticket.title) }}
            </h1>
            <span
              v-if="ticket?.okdeskId"
              class="shrink-0 inline-flex items-center rounded-md bg-blue-50 px-1.5 py-0.5 text-[10px] font-semibold text-blue-700 ring-1 ring-inset ring-blue-700/10 dark:bg-blue-900/30 dark:text-blue-300"
              title="Заявка импортирована из Okdesk"
            >
              Okdesk #{{ ticket.okdeskId }}
            </span>
            <div v-if="canEditAlternativeTitle && ticket && altTitleEditMode" class="flex shrink-0 items-center gap-1.5">
              <input
                ref="altTitleInputRef"
                v-model="editedAltTitle"
                type="text"
                placeholder="Альтернативное название"
                class="alt-title-input w-[10rem] sm:w-[11.9rem] shrink-0 px-2 py-1 text-[11px] leading-tight bg-white dark:bg-zinc-900 rounded-md outline-none text-gray-900 dark:text-gray-100 placeholder:text-[10px] placeholder:text-gray-400 dark:placeholder:text-zinc-500 focus:ring-2 focus:ring-indigo-500/45"
                @blur="onAltTitleBlur"
              />
              <button
                v-if="showAltSaveButton"
                type="button"
                class="shrink-0 rounded-md bg-indigo-600 px-2 py-1 text-[9px] font-bold uppercase tracking-wide text-white transition-colors hover:bg-indigo-700"
                @mousedown.prevent
                @click="saveAltTitle"
              >
                Сохранить
              </button>
            </div>
          </div>
          <p
            v-if="canEditAlternativeTitle && ticket && !altTitleEditMode && (ticket.alternativeTitle || '').trim()"
            class="max-w-[min(100%,42rem)] text-[11px] leading-snug text-gray-700 break-words sm:max-w-[min(100%,50rem)] sm:text-xs dark:text-gray-200"
          >
            <span class="font-semibold text-indigo-600 dark:text-violet-400">Альт:</span>
            <span class="ml-1">{{ ticket.alternativeTitle }}</span>
          </p>
        </div>
      </div>

      <div v-if="ticket" class="flex items-center gap-2 shrink-0 pt-0.5">
        <button
          v-if="auth.isStaff"
          type="button"
          class="flex items-center gap-1.5 px-2.5 sm:px-3 py-1.5 rounded-lg text-[10px] sm:text-xs font-bold uppercase tracking-wider border border-indigo-200 bg-indigo-50 text-indigo-700 hover:bg-indigo-100 dark:border-indigo-800 dark:bg-indigo-950/40 dark:text-indigo-300 transition-colors shadow-sm"
          :disabled="openingTicketChat"
          @click="openTicketChat"
        >
          <MessageSquare :size="14" />
          {{ openingTicketChat ? 'Открываем…' : 'Обсудить в чате' }}
        </button>
        <div class="relative">
          <button 
            v-if="auth.isStaff && (isMyTicket || can('ticketEditForeignStatus'))"
            @click="statusDropdownOpen = !statusDropdownOpen" 
            :class="['flex items-center gap-1.5 px-2.5 sm:px-3 py-1.5 rounded-lg text-[10px] sm:text-xs font-bold uppercase tracking-wider border hover:brightness-95 active:brightness-90 transition-all shadow-sm', getStatusColor(ticket.status)]"
          >
            {{ ticket.status }}
            <span class="opacity-50">▼</span>
          </button>
          <div v-if="statusDropdownOpen" class="absolute z-50 right-0 mt-2 w-56 bg-white border border-gray-200 rounded-xl shadow-xl overflow-hidden ring-1 ring-black/5">
            <button v-for="s in statuses" :key="s.name" @click="saveStatus(s.name)" class="w-full text-left px-4 py-3 text-sm hover:bg-gray-50 active:bg-gray-100 transition-colors border-b border-gray-50 last:border-0 flex items-center justify-between group">
              <span :class="{'font-bold text-indigo-600': s.name === ticket.status}">{{ s.name }}</span>
              <div v-if="s.name === ticket.status" class="w-1.5 h-1.5 rounded-full bg-indigo-600"></div>
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Main Grid -->
    <div v-if="loading && !ticket" class="flex items-center justify-center py-40">
        <RefreshCw :size="32" class="animate-spin text-indigo-600" />
    </div>

    <div v-else-if="ticket" class="grid grid-cols-1 lg:grid-cols-[29fr_11fr] xl:grid-cols-[49fr_11fr] gap-4 sm:gap-6 items-start">
      
      <!-- MOBILE: Quick ticket info (shown only on mobile, before main content) -->
      <div class="lg:hidden space-y-3">
        <div class="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
          <div class="flex items-center justify-between mb-3">
            <div class="flex items-center gap-2">
              <span :class="['font-bold text-sm px-2 py-0.5 rounded-lg border', priorityBadge(ticket.priority)]">{{ ticket.priority }}</span>
              <span class="text-[10px] px-1.5 bg-gray-100 text-gray-500 rounded font-bold uppercase">{{ ticket.requestType }}</span>
            </div>
            <span class="text-xs text-gray-400 font-mono">{{ formatDate(ticket.createdAt).split(',')[0] }}</span>
          </div>
          <div class="space-y-2 text-sm">
            <div v-if="auth.isStaff && ticket.clientName" class="flex items-center gap-2 text-gray-700">
              <Building2 :size="14" class="text-gray-400 shrink-0" />
              <span class="truncate">{{ ticket.clientName }}</span>
            </div>
            <div v-if="ticket.objectName" class="flex items-center gap-2 text-gray-700">
              <MapPin :size="14" class="text-gray-400 shrink-0" />
              <span class="truncate">{{ ticket.objectName }}</span>
            </div>
            <div v-if="ticket.assignees?.length || ticket.assignee" class="flex flex-wrap gap-1">
              <span
                v-for="(a, idx) in (ticket.assignees || [])" :key="a"
                :class="[
                  'inline-flex items-center px-2 py-0.5 rounded-lg text-xs font-bold',
                  idx === 0
                    ? 'bg-indigo-50 text-indigo-700 border-2 border-indigo-400 ring-1 ring-indigo-200'
                    : 'bg-green-50 text-green-700 border border-green-100'
                ]"
              >{{ a }}</span>
            </div>
            <div v-if="ticket.department" class="text-xs text-gray-500">Отдел: {{ ticket.department }}</div>
          </div>
        </div>
      </div>

      <!-- LEFT COLUMN -->
      <div class="min-w-0 space-y-4 sm:space-y-6">
        
        <!-- ОПИСАНИЕ -->
        <div class="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div class="px-5 py-4 border-b border-gray-50 flex items-center justify-between">
            <div class="flex items-center gap-2">
              <span class="text-xs font-bold text-gray-400 uppercase tracking-widest text-[#2f32a5]">Описание</span>
              <span class="text-[10px] pb-0.5 px-1.5 bg-gray-100 text-gray-500 rounded font-bold uppercase tracking-tighter">{{ ticket.requestType }}</span>
            </div>
            <div class="flex items-center gap-3">
              <button v-if="editingProblem" @click="saveProblem" :disabled="savingProblem" class="text-[10px] font-bold text-green-600 hover:underline uppercase tracking-widest disabled:opacity-50">сохранить</button>
              <button v-if="editingProblem" @click="editingProblem = false" class="text-[10px] font-bold text-gray-400 hover:text-red-500 uppercase tracking-widest">отмена</button>
              <button v-if="auth.isStaff && ticket.createdByRole !== 'client' && can('ticketEditDescription') && !editingProblem" @click="editingProblem = true; editProblemVal = ticket.problem || ''" class="text-[10px] font-bold text-indigo-600 hover:underline uppercase tracking-widest">изменить</button>
              <button @click="copyToTG" class="flex items-center gap-1.5 text-[10px] font-bold text-gray-400 hover:text-indigo-600 uppercase tracking-widest transition-colors">
                <Copy :size="14" /> Копировать для TG
              </button>
            </div>
          </div>
          <div class="p-5">
            <div v-if="editingProblem" class="space-y-2">
              <textarea v-model="editProblemVal" rows="6" class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none resize-none"></textarea>
            </div>
            <div v-else class="prose prose-sm max-w-none text-gray-700 whitespace-pre-wrap leading-relaxed">
              {{ ticket.problem || 'Описание отсутствует' }}
            </div>
          </div>
        </div>

        <!-- ССЫЛКИ НА TASK -->
        <div v-if="showTaskLinksBlock" class="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div class="px-5 py-4 border-b border-gray-50 flex items-center justify-between">
            <div class="flex items-center gap-2">
              <LinkIcon :size="14" class="text-indigo-500" />
              <span class="text-xs font-bold text-gray-400 uppercase tracking-widest text-[#2f32a5]">Ссылки на task</span>
            </div>
            <div class="flex items-center gap-3">
              <button v-if="editingLinks" @click="saveLinks" class="text-[10px] font-bold text-green-600 hover:underline uppercase tracking-widest">сохранить</button>
              <button v-if="editingLinks" @click="editingLinks = false" class="text-[10px] font-bold text-gray-400 hover:text-red-500 uppercase tracking-widest">отмена</button>
              <button v-if="!editingLinks && can('ticketEditTaskLinks')" @click="startEditLinks" class="text-[10px] font-bold text-indigo-600 hover:underline uppercase tracking-widest">изменить</button>
            </div>
          </div>
          <div class="p-4">
            <template v-if="editingLinks">
              <div class="space-y-3">
                <div class="flex items-center justify-between">
                  <label class="text-xs font-medium text-gray-600">Ссылки на таск (URL + №)</label>
                  <button type="button" @click="addLinkRow" class="text-xs text-green-700 hover:underline">+ строка</button>
                </div>
                <div v-for="(row, idx) in linkRows" :key="idx" class="space-y-2 pb-3 border-b border-gray-100 last:border-0 last:pb-0">
                  <div class="flex flex-col sm:flex-row gap-2">
                    <input v-model="row.url" type="url" placeholder="https://…" class="flex-1 border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-indigo-400" @blur="onTaskUrlBlur(row)" />
                    <div class="flex gap-2">
                      <input v-model="row.number" placeholder="№ таска" class="w-full sm:w-28 border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-indigo-400" />
                      <button v-if="linkRows.length > 1" type="button" @click="removeLinkRow(idx)" class="text-xs text-red-500 px-2 hover:text-red-700">×</button>
                    </div>
                  </div>
                  <details class="group">
                    <summary class="list-none cursor-pointer flex items-center justify-between text-[10px] text-gray-500 hover:text-gray-700 select-none">
                      <span>Комментарий к ссылке</span>
                      <span class="text-gray-400 group-open:hidden">▼</span>
                      <span class="text-gray-400 hidden group-open:inline">▲</span>
                    </summary>
                    <input v-model="row.comment" placeholder="Контекст, зачем ссылка…" class="w-full mt-1.5 border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-indigo-400" />
                  </details>
                </div>
              </div>
            </template>
            <template v-else>
              <div
                v-if="taskLinksJsonPairs.length === 0"
                class="w-full text-center py-5 sm:py-6 rounded-xl border border-dashed border-gray-300/70 dark:border-zinc-500/45 bg-transparent"
              >
                <p class="text-xs text-gray-400 dark:text-zinc-500 italic px-2">Связанные задачи не найдены</p>
              </div>
              <div v-else class="space-y-2">
                 <a v-for="(l, i) in taskLinksJsonPairs" :key="i" :href="l.url" target="_blank" class="flex items-center justify-between p-3 bg-gray-50 rounded-lg border border-gray-100 hover:border-indigo-100 hover:bg-indigo-50/30 transition-all group">
                   <div class="flex items-center gap-3 min-w-0">
                      <ExternalLink :size="14" class="text-gray-400 group-hover:text-indigo-500 shrink-0" />
                      <div class="min-w-0">
                        <span class="text-sm text-indigo-600 font-medium truncate block underline decoration-indigo-200 underline-offset-4">{{ l.url }}</span>
                        <span v-if="l.comment" class="text-[10px] text-gray-400 block mt-0.5">{{ l.comment }}</span>
                      </div>
                   </div>
                   <span v-if="l.number" class="text-[10px] font-mono text-gray-400 bg-white px-1.5 py-0.5 rounded border shrink-0 ml-2">№ {{ l.number }}</span>
                 </a>
              </div>
            </template>
          </div>
        </div>

        <!-- ЛЕНТА -->
        <div class="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div class="px-5 py-4 border-b border-gray-50 bg-gray-50/20 flex items-center justify-between gap-2">
            <span class="text-xs font-bold text-gray-400 uppercase tracking-widest text-[#2f32a5]">Лента</span>
          </div>
          <div class="divide-y divide-gray-50">
            <template v-for="(item, idx) in timeline" :key="`${item.type}-${item.entityId ?? idx}-${item.at}`">
              <!-- Comment (rich) -->
              <div
                v-if="item.type === 'comment' && commentFromTimeline(item)"
                :class="['w-full px-3 sm:px-5 py-3.5 sm:py-5 flex gap-2.5 sm:gap-4 transition-colors', commentFromTimeline(item)!.isInternal ? 'bg-yellow-50/30' : '']"
              >
                <div class="w-8 h-8 sm:w-10 sm:h-10 flex-shrink-0 rounded-full border border-gray-100 bg-white overflow-hidden flex items-center justify-center text-gray-400 font-bold text-[10px] sm:text-xs shadow-sm">
                  <img
                    v-if="resolveCommentAvatar(commentFromTimeline(item)!)"
                    :src="resolveCommentAvatar(commentFromTimeline(item)!)"
                    class="w-full h-full object-cover"
                    alt=""
                    @error="onAvatarError"
                  />
                  <span v-else>
                    {{ commentFromTimeline(item)!.authorName.charAt(0).toUpperCase() }}
                  </span>
                </div>
                <div class="flex-1 min-w-0 w-full max-w-none">
                  <div class="flex items-start sm:items-center justify-between mb-1 gap-1">
                    <div class="flex flex-wrap items-center gap-1 sm:gap-2 min-w-0 flex-1">
                      <span class="font-bold text-gray-900 dark:text-gray-100 text-sm break-words sm:truncate sm:max-w-[min(100%,28rem)]">{{ commentFromTimeline(item)!.authorName }}</span>
                      <span class="text-[9px] sm:text-[10px] text-gray-400 font-bold uppercase tracking-tight hidden sm:inline">{{ commentFromTimeline(item)!.authorRole }}</span>
                      <span :class="['text-[8px] sm:text-[9px] uppercase font-bold tracking-wider px-1 py-0.5 rounded border', timelineBadge(item).class]">{{ timelineBadge(item).label }}</span>
                      <span v-if="commentFromTimeline(item)!.isInternal" class="text-[8px] sm:text-[9px] uppercase font-bold tracking-wider text-yellow-600 bg-yellow-100 px-1 py-0.5 rounded border border-yellow-200">Внутр.</span>
                    </div>
                    <span class="text-[10px] sm:text-[11px] text-gray-500 font-semibold shrink-0 whitespace-nowrap">{{ formatDate(item.at) }}</span>
                  </div>
                  <div class="text-[13px] sm:text-sm text-gray-800 whitespace-pre-wrap leading-relaxed w-full max-w-none break-words">{{ commentFromTimeline(item)!.text }}</div>

                  <div v-if="attachments.filter(a => a.commentId === commentFromTimeline(item)!.id).length > 0" class="mt-4 flex flex-wrap gap-2 w-full">
                    <template v-for="att in attachments.filter(a => a.commentId === commentFromTimeline(item)!.id)" :key="att.id">
                      <button v-if="(att.contentType || '').includes('image')" @click="openLightbox(resolveMediaUrl(att.url), attachments.filter(a => a.commentId === commentFromTimeline(item)!.id && (a.contentType || '').includes('image')).map(a => resolveMediaUrl(a.url)))" class="group block w-full max-w-full cursor-pointer text-left">
                        <div class="w-full aspect-[4/3] max-h-64 sm:max-h-80 bg-gray-100 rounded-lg overflow-hidden border border-gray-100 group-hover:border-indigo-300 transition-all shadow-sm">
                          <img :src="resolveMediaUrl(att.url)" class="w-full h-full object-cover group-hover:scale-105 transition-transform" :alt="att.fileName" />
                        </div>
                      </button>
                      <div v-else-if="(att.contentType || '').includes('video')" class="w-full max-w-full bg-gray-50 rounded-lg overflow-hidden border border-gray-100 shadow-sm">
                        <video :src="resolveMediaUrl(att.url)" controls class="w-full h-32 object-cover bg-black"></video>
                        <div class="px-2 py-1 text-[10px] font-bold text-gray-600 truncate">{{ att.fileName }}</div>
                      </div>
                      <a v-else :href="resolveMediaUrl(att.url)" target="_blank" class="inline-flex items-center gap-2 px-3 py-1.5 bg-white border border-gray-100 rounded-lg text-xs text-gray-600 hover:text-indigo-600 hover:border-indigo-100 transition-all font-medium shadow-sm max-w-full min-w-0">
                        <Download :size="12" class="text-gray-400 shrink-0" />
                        <span class="truncate min-w-0">{{ att.fileName }}</span>
                      </a>
                    </template>
                  </div>
                  <MessageReactions
                    :reactions="(commentFromTimeline(item)!.reactions || []) as any"
                    :current-user-id="auth.userId"
                    :can-add="can('canReactToTicketComments')"
                    @toggle="(emoji: string) => toggleCommentReaction(commentFromTimeline(item)!, emoji)"
                  />
                </div>
              </div>

              <!-- Created / field report / fallback comment -->
              <div v-else class="w-full px-3 sm:px-5 py-3.5 sm:py-4 flex gap-2.5 sm:gap-4">
                <div class="w-8 h-8 sm:w-10 sm:h-10 flex-shrink-0 rounded-full border border-gray-100 bg-gray-50 flex items-center justify-center text-gray-400 shadow-sm">
                  <Clock v-if="item.type === 'created'" :size="16" />
                  <FileText v-else-if="item.type === 'field_report'" :size="16" />
                  <MessageSquare v-else :size="16" />
                </div>
                <div class="flex-1 min-w-0">
                  <div class="flex items-start justify-between gap-2 mb-1">
                    <div class="flex flex-wrap items-center gap-1.5 min-w-0">
                      <span :class="['text-[8px] sm:text-[9px] uppercase font-bold tracking-wider px-1 py-0.5 rounded border', timelineBadge(item).class]">{{ timelineBadge(item).label }}</span>
                      <span v-if="item.authorName" class="font-bold text-gray-900 dark:text-gray-100 text-sm">{{ item.authorName }}</span>
                      <span v-if="item.equipmentType" class="text-[10px] text-gray-400">{{ item.equipmentType }}</span>
                    </div>
                    <span class="text-[10px] sm:text-[11px] text-gray-500 font-semibold shrink-0 whitespace-nowrap">{{ formatDate(item.at) }}</span>
                  </div>
                  <div class="text-[13px] sm:text-sm text-gray-700 whitespace-pre-wrap leading-relaxed break-words">{{ item.text || (item.type === 'created' ? 'Заявка создана' : '') }}</div>
                </div>
              </div>
            </template>
            <div v-if="timeline.length === 0" class="text-center py-16">
              <MessageSquare :size="48" class="mx-auto text-gray-100 mb-4" />
              <p class="text-sm text-gray-400 italic">Событий пока нет</p>
            </div>
          </div>
        </div>

      </div>

      <!-- RIGHT COLUMN -->
      <div class="min-w-0 space-y-4 sm:space-y-6">
        
        <!-- ПАРАМЕТРЫ ЗАЯВКИ -->
        <div class="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div class="px-5 py-4 border-b border-gray-50">
            <span class="text-xs font-bold text-gray-400 uppercase tracking-widest text-[#2f32a5]">Параметры заявки</span>
          </div>
          <div class="p-5 space-y-5">
            <div>
              <div class="flex items-center justify-between mb-1.5">
                <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest">Приоритет</label>
                <button v-if="canEditTicketParams && !editingPriority" @click="editingPriority = true; editPriorityVal = ticket.priority" class="text-[10px] font-bold text-indigo-600 hover:underline uppercase tracking-widest">изменить</button>
              </div>
              <div v-if="editingPriority" class="space-y-2">
                <select v-model="editPriorityVal" class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none">
                  <option v-for="p in PRIORITY_META" :key="p.value" :value="p.value">{{ p.value }}</option>
                </select>
                <div class="flex gap-2">
                  <button @click="saveField('priority', editPriorityVal)" :disabled="savingFields" class="px-3 py-1.5 bg-indigo-600 text-white text-[10px] font-bold rounded-lg disabled:opacity-50">Сохранить</button>
                  <button @click="editingPriority = false" class="px-3 py-1.5 text-gray-400 text-[10px] font-bold">Отмена</button>
                </div>
              </div>
              <div v-else>
                <span :class="['inline-flex items-center px-2.5 py-1 rounded-lg text-sm font-bold border', priorityBadge(ticket.priority)]">{{ ticket.priority }}</span>
              </div>
            </div>
            
            <div>
              <div class="flex items-center justify-between mb-1.5">
                <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest">Тип обращения</label>
                <button v-if="canEditTicketParams && !editingRequestType" @click="editingRequestType = true; editRequestTypeVal = ticket.requestType" class="text-[10px] font-bold text-indigo-600 hover:underline uppercase tracking-widest">изменить</button>
              </div>
              <div v-if="editingRequestType" class="space-y-2">
                <select v-model="editRequestTypeVal" class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none">
                  <option v-for="t in REQUEST_TYPES" :key="t" :value="t">{{ t }}</option>
                </select>
                <div class="flex gap-2">
                  <button @click="saveField('requestType', editRequestTypeVal)" :disabled="savingFields" class="px-3 py-1.5 bg-indigo-600 text-white text-[10px] font-bold rounded-lg disabled:opacity-50">Сохранить</button>
                  <button @click="editingRequestType = false" class="px-3 py-1.5 text-gray-400 text-[10px] font-bold">Отмена</button>
                </div>
              </div>
              <div v-else class="text-sm font-bold text-gray-900">{{ ticket.requestType }}</div>
            </div>

            <div>
              <div class="flex items-center justify-between mb-1.5">
                <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest">Отдел</label>
                <button v-if="canEditTicketParams && !editingDepartment" @click="editingDepartment = true; editDepartmentVal = ticket.department" class="text-[10px] font-bold text-indigo-600 hover:underline uppercase tracking-widest">изменить</button>
              </div>
              <div v-if="editingDepartment" class="space-y-2">
                <select v-model="editDepartmentVal" class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none">
                  <option v-for="d in departments" :key="d.value" :value="d.value">{{ d.label }}</option>
                </select>
                <div class="flex gap-2">
                  <button @click="saveField('department', editDepartmentVal)" :disabled="savingFields" class="px-3 py-1.5 bg-indigo-600 text-white text-[10px] font-bold rounded-lg disabled:opacity-50">Сохранить</button>
                  <button @click="editingDepartment = false" class="px-3 py-1.5 text-gray-400 text-[10px] font-bold">Отмена</button>
                </div>
              </div>
              <div v-else class="text-sm font-bold text-gray-900">{{ ticket.department || '—' }}</div>
            </div>

            <div>
              <div class="flex items-center justify-between mb-1.5">
                <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest">Ответственные</label>
                <div class="flex gap-2">
                  <button v-if="canEditTicketParams" @click="startEditAssignees" class="text-[10px] font-bold text-indigo-600 hover:underline uppercase tracking-widest">изменить</button>
                  <button v-if="canDelegateTicket" @click="delegateModalOpen = true" class="text-[10px] font-bold text-green-600 hover:underline uppercase tracking-widest">делегировать</button>
                </div>
              </div>
              <div class="flex flex-wrap gap-1.5">
                 <span
                   v-for="(a, idx) in (ticket.assignees || [])" :key="a"
                   :class="[
                     'inline-flex items-center px-2 py-0.5 rounded-lg text-xs font-bold',
                     idx === 0
                       ? 'bg-indigo-50 text-indigo-700 border-2 border-indigo-400 ring-1 ring-indigo-200'
                       : 'bg-green-50 text-green-700 border border-green-100'
                   ]"
                 >{{ a }}</span>
                 <span v-if="!ticket.assignees?.length && !ticket.assignee" class="text-sm text-gray-400 italic">Не назначен</span>
              </div>
              <div
                v-if="(ticket.delegatedTo || '').trim()"
                class="mt-3 rounded-lg border border-indigo-100 bg-indigo-50/70 px-3 py-2.5 dark:border-indigo-500/35 dark:bg-indigo-950/45"
              >
                <div class="text-xs font-semibold text-indigo-900 dark:text-indigo-200">
                  {{ (ticket.delegatedFrom || '—').trim() }} → {{ ticket.delegatedTo }}
                </div>
                <div class="mt-1.5 text-[11px] leading-snug text-indigo-800 dark:text-indigo-100/85 whitespace-pre-wrap break-words">
                  {{ (ticket.delegationReason || '').trim() || 'Без объяснения причин' }}
                </div>
              </div>
            </div>

            <div>
              <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-1.5">Клиент</label>
              <div class="text-sm font-bold text-gray-900 flex items-center gap-2">
                <Building2 :size="14" class="text-[#212353]" /> {{ ticket.clientName }}
              </div>
            </div>

            <div>
              <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-1.5">Объект</label>
              <div class="text-sm font-bold text-gray-900 flex items-center gap-2">
                <MapPin :size="14" class="text-[#212353]" /> {{ ticket.objectName || '—' }}
              </div>
            </div>

            <div class="pt-2">
              <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-1.5">Дата создания</label>
              <div class="text-sm font-medium text-gray-900 flex items-center gap-2">
                <Clock :size="14" class="text-gray-400" /> {{ formatDate(ticket.createdAt) }}
              </div>
            </div>
          </div>
        </div>

        <!-- ПОДЗАДАЧИ -->
        <div v-if="auth.isStaff && can('ticketShowSubtasks')" class="bg-white dark:bg-zinc-900/30 rounded-xl border border-gray-200 dark:border-zinc-700/60 shadow-sm overflow-hidden">
          <div
            class="px-5 py-4 flex items-center justify-between gap-2"
            :class="subtasksSectionOpen ? 'border-b border-gray-50 dark:border-zinc-800' : ''"
          >
            <button
              type="button"
              class="flex flex-1 items-center gap-2 min-w-0 text-left rounded-lg -m-1 p-1 hover:bg-gray-50 dark:hover:bg-zinc-800/50 transition-colors"
              @click="subtasksSectionOpen = !subtasksSectionOpen"
            >
              <ChevronRight
                :size="18"
                class="text-gray-400 dark:text-zinc-500 shrink-0 transition-transform duration-200"
                :class="{ 'rotate-90': subtasksSectionOpen }"
              />
              <ListChecks :size="16" class="text-indigo-600 shrink-0" />
              <span class="text-xs font-bold text-gray-400 uppercase tracking-widest text-[#2f32a5]">Подзадачи</span>
            </button>
            <button
              v-if="can('ticketCreateSubtask')"
              type="button"
              class="text-[10px] font-bold text-indigo-600 hover:underline uppercase tracking-widest shrink-0"
              @click="subtasksSectionOpen = true; nextTick(() => document.getElementById('subtask-form')?.scrollIntoView({ behavior: 'smooth' }))"
            >
              добавить
            </button>
          </div>
          <div v-show="subtasksSectionOpen" class="p-5">
             <div v-if="subtasks.length === 0" class="text-gray-400 text-[11px] italic py-2">Подзадач нет</div>
             <div v-else class="space-y-4">
               <div v-for="st in subtasks" :key="st.id" class="p-3 rounded-lg bg-gray-50 dark:bg-zinc-900/40 border border-gray-100 dark:border-zinc-700/60 hover:border-indigo-100 dark:hover:border-indigo-500/40 transition-all group">
                 <div class="flex items-start justify-between gap-2">
                   <button
                     type="button"
                     class="flex flex-1 items-start gap-2.5 min-w-0 text-left rounded-lg -m-1 p-1 hover:bg-gray-100/90 dark:hover:bg-zinc-800/60 transition-colors"
                     @click="toggleSubtaskDetail(st.id)"
                   >
                     <CheckCircle :size="16" :class="st.status === 'готово' ? 'text-green-500' : 'text-gray-300 dark:text-zinc-500'" class="shrink-0 mt-0.5" />
                     <div class="min-w-0">
                       <div class="text-sm font-bold text-gray-900 dark:text-gray-100 leading-tight">{{ st.title }}</div>
                       <div class="flex items-center gap-2 mt-1 flex-wrap">
                          <span :class="['text-[9px] font-bold uppercase tracking-tighter px-1.5 py-0.5 rounded border', getSubtaskStatusColor(st.status)]">{{ st.status }}</span>
                          <span class="text-[9px] text-gray-400 dark:text-zinc-500 uppercase font-bold">{{ formatDate(st.createdAt) }}</span>
                       </div>
                     </div>
                   </button>
                   <div class="flex items-center gap-0.5 shrink-0">
                     <button
                       v-if="can('ticketCreateSubtask')"
                       type="button"
                       class="text-[10px] font-bold text-indigo-600 dark:text-indigo-400 hover:underline uppercase tracking-widest px-1.5 py-1"
                       @click.stop="startEditSubtask(st)"
                     >
                       изменить
                     </button>
                     <button
                       v-if="can('ticketDeleteSubtask')"
                       type="button"
                       class="text-[10px] font-bold text-red-600 dark:text-red-400 hover:underline uppercase tracking-widest px-1.5 py-1"
                       @click.stop="deleteSubtask(st)"
                     >
                       удалить
                     </button>
                     <button
                       type="button"
                       class="p-1 rounded-md text-gray-300 dark:text-zinc-500 hover:bg-gray-200/50 dark:hover:bg-zinc-700/50 transition-colors"
                       @click.stop="toggleSubtaskDetail(st.id)"
                     >
                       <ChevronRight
                         :size="14"
                         class="transition-transform duration-200"
                         :class="{ 'rotate-90': isSubtaskDetailOpen(st.id) }"
                       />
                     </button>
                   </div>
                 </div>

                 <div v-show="isSubtaskDetailOpen(st.id)" class="mt-2 space-y-2">
                   <template v-if="editingSubtaskId === st.id">
                     <input v-model="editSubtaskDraft.title" class="w-full px-3 py-2 text-sm bg-white dark:bg-zinc-900 border border-gray-200 dark:border-zinc-600 rounded-lg focus:ring-2 focus:ring-indigo-500/20 focus:outline-none" />
                     <select v-model="editSubtaskDraft.status" class="w-full px-3 py-2 text-xs bg-white dark:bg-zinc-900 border border-gray-200 dark:border-zinc-600 rounded-lg focus:ring-2 focus:ring-indigo-500/20 focus:outline-none">
                       <option value="в процессе">В процессе</option>
                       <option value="готово">Готово</option>
                       <option value="не актуально">Не актуально</option>
                     </select>
                     <div class="relative">
                       <button type="button" @click="editSubtaskKnowledgeOpen = !editSubtaskKnowledgeOpen" class="w-full px-3 py-2 text-xs bg-white dark:bg-zinc-900 border border-gray-200 dark:border-zinc-600 rounded-lg text-left text-gray-500 dark:text-zinc-400 flex items-center justify-between">
                         <span class="truncate">{{ editSubtaskDraft.knowledgeableUserIds.length ? `Выбрано: ${editSubtaskDraft.knowledgeableUserIds.length}` : 'Кто в курсе?' }}</span>
                         <span>▼</span>
                       </button>
                       <div v-if="editSubtaskKnowledgeOpen" class="absolute z-50 bottom-full mb-2 w-full max-w-xs right-0 bg-white dark:bg-zinc-900 border border-gray-200 dark:border-zinc-600 rounded-xl shadow-2xl overflow-hidden ring-1 ring-black/5">
                         <div class="p-2 border-b border-gray-50 dark:border-zinc-700">
                           <input v-model="subtaskKnowledgeSearch" placeholder="Поиск..." class="w-full px-2 py-1.5 text-xs bg-gray-50 dark:bg-zinc-800 border-transparent rounded-lg focus:ring-0 outline-none" />
                         </div>
                         <div class="max-h-40 overflow-y-auto divide-y divide-gray-50 dark:divide-zinc-800">
                           <button v-for="e in filteredKnowledgeEmployees" :key="e.userId" type="button" @click="toggleEditKnowledge(e.userId)" class="w-full text-left px-3 py-2 text-xs hover:bg-indigo-50 dark:hover:bg-indigo-950/40 flex items-center justify-between transition-colors">
                             <span>{{ e.fullName }}</span>
                             <Check v-if="editSubtaskDraft.knowledgeableUserIds.includes(e.userId)" :size="12" class="text-indigo-600" />
                           </button>
                         </div>
                       </div>
                     </div>
                     <textarea v-model="editSubtaskDraft.description" rows="3" placeholder="Описание..." class="w-full px-3 py-2 text-sm bg-white dark:bg-zinc-900 border border-gray-200 dark:border-zinc-600 rounded-lg focus:ring-2 focus:ring-indigo-500/20 focus:outline-none resize-none"></textarea>
                     <div class="flex flex-wrap gap-2 pt-1">
                       <button type="button" @click="saveSubtaskEdit" :disabled="savingSubtaskEdit || !editSubtaskDraft.title.trim()" class="px-4 py-2 bg-indigo-600 text-white text-[10px] font-bold rounded-lg hover:bg-indigo-700 disabled:opacity-50 uppercase tracking-widest">
                         {{ savingSubtaskEdit ? '...' : 'Сохранить' }}
                       </button>
                       <button type="button" @click="cancelEditSubtask" class="px-4 py-2 text-gray-500 dark:text-zinc-400 text-[10px] font-bold uppercase tracking-widest hover:text-gray-800 dark:hover:text-zinc-200">
                         Отмена
                       </button>
                     </div>
                   </template>
                   <template v-else>
                     <div v-if="st.description" class="text-xs text-gray-600 dark:text-zinc-300 w-full bg-white/50 dark:bg-zinc-950/50 p-2 rounded leading-relaxed border border-gray-50 dark:border-zinc-700/50">
                       {{ st.description }}
                     </div>

                     <div v-if="st.knowledgeableUserIds?.length" class="flex flex-wrap gap-1">
                       <span v-for="name in st.knowledgeableNames" :key="name" class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded bg-white dark:bg-zinc-800 border border-gray-100 dark:border-zinc-600 text-[9px] font-bold text-gray-500 dark:text-zinc-400">
                         <User :size="10" /> {{ name }}
                       </span>
                     </div>

                     <div v-if="attachments.filter(a => a.subtaskId === st.id).length > 0" class="flex flex-wrap gap-1.5">
                       <template v-for="att in attachments.filter(a => a.subtaskId === st.id)" :key="att.id">
                         <button v-if="(att.contentType || '').includes('image')" type="button" @click.stop="openLightbox(resolveMediaUrl(att.url))" class="inline-flex items-center gap-1.5 px-2 py-1 bg-white dark:bg-zinc-800 border border-gray-100 dark:border-zinc-600 rounded text-[9px] text-indigo-600 dark:text-indigo-400 font-bold hover:border-indigo-200 transition-all cursor-pointer">
                           <Paperclip :size="10" /> {{ att.fileName }}
                         </button>
                         <a v-else :href="resolveMediaUrl(att.url)" target="_blank" class="inline-flex items-center gap-1.5 px-2 py-1 bg-white dark:bg-zinc-800 border border-gray-100 dark:border-zinc-600 rounded text-[9px] text-indigo-600 dark:text-indigo-400 font-bold hover:border-indigo-200 transition-all" @click.stop>
                           <Paperclip :size="10" /> {{ att.fileName }}
                         </a>
                       </template>
                     </div>
                   </template>
                 </div>
               </div>
             </div>

             <!-- New Subtask Form -->
             <div v-if="can('ticketCreateSubtask')" id="subtask-form" class="mt-6 pt-5 border-t border-gray-100 space-y-3">
                <div class="flex items-center justify-between mb-1">
                   <span class="text-[10px] font-bold text-indigo-400 uppercase tracking-widest">Новая подзадача</span>
                </div>
                
                <input v-model="newSubtaskTitle" placeholder="Заголовок задачи..." class="w-full px-3 py-2 text-sm bg-gray-50 border-transparent rounded-lg focus:ring-2 focus:ring-indigo-500/20 focus:bg-white transition-all outline-none" />
                
                <div class="flex flex-col sm:flex-row gap-3">
                   <select v-model="newSubtaskStatus" class="flex-1 px-3 py-2 text-xs bg-gray-50 border-transparent rounded-lg focus:ring-2 focus:ring-indigo-500/20 focus:bg-white transition-all outline-none">
                      <option value="в процессе">В процессе</option>
                      <option value="готово">Готово</option>
                      <option value="не актуально">Не актуально</option>
                   </select>
                   
                   <div class="relative flex-1">
                      <button @click="subtaskKnowledgeOpen = !subtaskKnowledgeOpen" class="w-full px-3 py-2 text-xs bg-gray-50 border-transparent rounded-lg text-left text-gray-500 flex items-center justify-between">
                         <span class="truncate">{{ newSubtaskKnowledgeable.length ? `Выбрано: ${newSubtaskKnowledgeable.length}` : 'Кто в курсе?' }}</span>
                         <span>▼</span>
                      </button>
                      <div v-if="subtaskKnowledgeOpen" class="absolute z-50 bottom-full mb-2 w-64 right-0 bg-white border border-gray-200 rounded-xl shadow-2xl overflow-hidden ring-1 ring-black/5">
                        <div class="p-2 border-b border-gray-50">
                           <input v-model="subtaskKnowledgeSearch" placeholder="Поиск..." class="w-full px-2 py-1.5 text-xs bg-gray-50 border-transparent rounded-lg focus:ring-0 outline-none" />
                        </div>
                        <div class="max-h-40 overflow-y-auto divide-y divide-gray-50">
                           <button v-for="e in filteredKnowledgeEmployees" :key="e.userId" @click="toggleKnowledge(e.userId)" class="w-full text-left px-3 py-2 text-xs hover:bg-indigo-50 flex items-center justify-between transition-colors">
                              <span>{{ e.fullName }}</span>
                              <Check v-if="newSubtaskKnowledgeable.includes(e.userId)" :size="12" class="text-indigo-600" />
                           </button>
                        </div>
                      </div>
                   </div>
                </div>

                <textarea v-model="newSubtaskDesc" rows="2" placeholder="Доп. информация..." class="w-full px-3 py-2 text-sm bg-gray-50 border-transparent rounded-lg focus:ring-2 focus:ring-indigo-500/20 focus:bg-white transition-all outline-none resize-none"></textarea>
                
                <div class="flex items-center justify-between gap-3">
                   <div class="flex items-center gap-2">
                     <button @click="subtaskFileInputRef?.click()" class="p-2 bg-gray-50 text-gray-400 hover:text-indigo-600 rounded-lg border border-transparent hover:border-indigo-100 transition-all">
                        <Paperclip :size="16" />
                     </button>
                     <input ref="subtaskFileInputRef" type="file" multiple class="hidden" @change="handleSubtaskFileChange" accept="image/*,application/pdf,.doc,.docx,.xls,.xlsx,.txt,.zip,.rar" />
                     <span v-if="newSubtaskFiles.length" class="text-[10px] font-bold text-indigo-600 bg-indigo-50 px-2 py-1 rounded">{{ newSubtaskFiles.length }} ф.</span>
                   </div>
                   
                   <button @click="createSubtask" :disabled="!newSubtaskTitle.trim() || creatingSubtask" class="px-6 py-2 bg-indigo-600 text-white text-[10px] font-bold rounded-lg hover:bg-indigo-700 disabled:opacity-50 uppercase tracking-widest transition-all shadow-md shadow-indigo-100">
                     {{ creatingSubtask ? '...' : 'Создать подзадачу' }}
                   </button>
                </div>

                <!-- Files Preview -->
                <div v-if="newSubtaskFiles.length" class="flex flex-wrap gap-2 mt-2">
                   <div v-for="(f, i) in newSubtaskFiles" :key="i" class="flex items-center gap-2 px-2 py-1 bg-indigo-50/50 rounded border border-indigo-100 text-[10px] text-indigo-700">
                      <span class="truncate max-w-[100px]">{{ f.name }}</span>
                      <button @click="removeSubtaskFile(i)" class="text-indigo-300 hover:text-indigo-600"><X :size="10"/></button>
                   </div>
                </div>
             </div>
          </div>
        </div>

        <!-- АКТЫ ВЫЕЗДА -->
        <div v-if="auth.isStaff && can('ticketShowExitActs')" class="bg-white dark:bg-zinc-900/30 rounded-xl border border-gray-200 dark:border-zinc-700/60 shadow-sm overflow-hidden">
          <div
            class="px-5 py-4 flex items-center justify-between gap-2"
            :class="reportsSectionOpen ? 'border-b border-gray-50 dark:border-zinc-800' : ''"
          >
            <button
              type="button"
              class="flex flex-1 items-center gap-2 min-w-0 text-left rounded-lg -m-1 p-1 hover:bg-gray-50 dark:hover:bg-zinc-800/50 transition-colors"
              @click="reportsSectionOpen = !reportsSectionOpen"
            >
              <ChevronRight
                :size="18"
                class="text-gray-400 dark:text-zinc-500 shrink-0 transition-transform duration-200"
                :class="{ 'rotate-90': reportsSectionOpen }"
              />
              <FileText :size="14" class="text-orange-500 shrink-0" />
              <span class="text-xs font-bold text-gray-400 uppercase tracking-widest text-[#2f32a5]">Акты выезда</span>
            </button>
            <button
              v-if="can('ticketCreateExitActs') && (isMyTicket || can('ticketInteractForeign'))"
              type="button"
              class="text-[10px] font-bold text-orange-600 hover:underline uppercase tracking-widest shrink-0"
              @click="reportsSectionOpen = true; openReportModalCreate()"
            >
              добавить акт
            </button>
          </div>
          <div v-show="reportsSectionOpen" class="p-5">
             <div v-if="reports.length === 0" class="text-gray-400 text-[11px] italic py-2">Актов еще нет</div>
             <div v-else class="space-y-3">
                <div v-for="r in reports" :key="r.id" class="p-3 bg-gray-50 dark:bg-zinc-900/40 rounded-lg border border-gray-100 dark:border-zinc-700/60">
                  <div class="flex items-start justify-between gap-2">
                    <button
                      type="button"
                      class="flex flex-1 min-w-0 items-start gap-2 text-left rounded-lg -m-1 p-1 hover:bg-gray-100/90 dark:hover:bg-zinc-800/60 transition-colors"
                      @click="toggleReportDetail(r.id)"
                    >
                      <div class="min-w-0 flex-1">
                        <div class="flex items-center justify-between gap-2 mb-1">
                          <span class="text-xs font-bold text-gray-900 dark:text-gray-100">{{ r.engineerName }}</span>
                          <span class="text-[9px] text-gray-400 dark:text-zinc-500 shrink-0">{{ formatDate(r.visitDate).split(',')[0] }}</span>
                        </div>
                        <p v-if="r.workDone && !isReportDetailOpen(r.id)" class="text-[10px] text-gray-600 dark:text-zinc-400 line-clamp-2 leading-relaxed">{{ r.workDone }}</p>
                      </div>
                    </button>
                    <div class="flex items-center gap-0.5 shrink-0">
                      <button
                        v-if="can('ticketCreateExitActs') && (isMyTicket || can('ticketInteractForeign'))"
                        type="button"
                        class="text-[10px] font-bold text-orange-600 dark:text-orange-400 hover:underline uppercase tracking-widest px-1.5 py-1"
                        @click.stop="reportsSectionOpen = true; openReportModalEdit(r)"
                      >
                        изменить
                      </button>
                      <button
                        type="button"
                        class="p-1 rounded-md text-gray-300 dark:text-zinc-500 hover:bg-gray-200/50 dark:hover:bg-zinc-700/50 transition-colors"
                        @click.stop="toggleReportDetail(r.id)"
                      >
                        <ChevronRight
                          :size="14"
                          class="transition-transform duration-200"
                          :class="{ 'rotate-90': isReportDetailOpen(r.id) }"
                        />
                      </button>
                    </div>
                  </div>
                  <div v-show="isReportDetailOpen(r.id)" class="mt-2 pt-2 border-t border-gray-100 dark:border-zinc-700/80 space-y-2">
                    <p v-if="r.workDone" class="text-[11px] text-gray-800 dark:text-zinc-200 whitespace-pre-wrap leading-relaxed">{{ r.workDone }}</p>
                    <dl class="grid gap-1.5 text-[10px]">
                      <div v-if="r.actionType" class="flex justify-between gap-3">
                        <dt class="text-gray-400 dark:text-zinc-500 shrink-0">Тип работ</dt>
                        <dd class="font-medium text-gray-800 dark:text-zinc-200 text-right">{{ r.actionType }}</dd>
                      </div>
                      <div v-if="r.equipmentStatus" class="flex justify-between gap-3">
                        <dt class="text-gray-400 dark:text-zinc-500 shrink-0">Статус оборудования</dt>
                        <dd class="font-medium text-gray-800 dark:text-zinc-200 text-right">{{ r.equipmentStatus }}</dd>
                      </div>
                      <div v-if="r.equipmentType" class="flex justify-between gap-3">
                        <dt class="text-gray-400 dark:text-zinc-500 shrink-0">Тип оборудования</dt>
                        <dd class="font-medium text-gray-800 dark:text-zinc-200 text-right">{{ r.equipmentType }}</dd>
                      </div>
                      <div v-if="r.equipmentSerial" class="flex justify-between gap-3">
                        <dt class="text-gray-400 dark:text-zinc-500 shrink-0">Серийный номер</dt>
                        <dd class="font-mono font-medium text-gray-800 dark:text-zinc-200 text-right break-all">{{ r.equipmentSerial }}</dd>
                      </div>
                      <div v-if="r.transferredTo" class="flex justify-between gap-3">
                        <dt class="text-gray-400 dark:text-zinc-500 shrink-0">Передано</dt>
                        <dd class="font-medium text-gray-800 dark:text-zinc-200 text-right">{{ r.transferredTo }}</dd>
                      </div>
                    </dl>
                  </div>
                </div>
             </div>
          </div>
        </div>

        <!-- ФАЙЛЫ ЗАЯВКИ -->
        <div class="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div class="px-5 py-4 border-b border-gray-50 flex items-center justify-between">
            <span class="text-xs font-bold text-gray-400 uppercase tracking-widest text-[#2f32a5]">Файлы заявки</span>
            <button v-if="canInteractWithTicket" @click="fileInputRef?.click()" class="text-[10px] font-bold text-indigo-600 hover:underline uppercase tracking-widest">загрузить</button>
          </div>
          <div class="p-4">
             <div
               v-if="attachmentsForTicketFilesBlock.length === 0"
               class="w-full text-center py-6 rounded-xl border border-dashed border-gray-300/70 dark:border-zinc-500/45 bg-transparent"
             >
                <p class="text-[10px] text-gray-400 dark:text-zinc-500 font-bold uppercase tracking-wider px-2">Файлы не загружены</p>
             </div>
             <div v-else class="space-y-2.5">
                <template v-for="a in attachmentsForTicketFilesBlock" :key="a.id">
                  <button v-if="a.contentType?.includes('image')" @click="openLightbox(resolveMediaUrl(a.url), attachmentsForTicketFilesBlock.filter(x => x.contentType?.includes('image')).map(x => resolveMediaUrl(x.url)))" class="block group w-full text-left cursor-pointer">
                    <div class="w-full h-24 bg-gray-100 rounded-lg overflow-hidden border border-gray-100 group-hover:border-indigo-300 transition-all shadow-sm">
                      <img :src="resolveMediaUrl(a.url)" class="w-full h-full object-cover group-hover:scale-105 transition-transform" />
                    </div>
                  </button>
                  <a v-else :href="resolveMediaUrl(a.url)" target="_blank" class="block group">
                    <div class="flex items-center gap-2 p-2.5 bg-gray-50 border border-gray-100 rounded-lg group-hover:border-indigo-200 transition-all">
                      <div class="shrink-0 text-gray-400"><FileText :size="18" /></div>
                      <div class="flex-1 min-w-0">
                        <div class="text-[10px] font-bold text-gray-900 truncate uppercase tracking-tighter">{{ a.fileName }}</div>
                        <div class="text-[8px] text-gray-400 font-mono">{{ (a.fileSizeBytes / 1024).toFixed(0) }} КБ</div>
                      </div>
                      <Download :size="14" class="text-gray-400" />
                    </div>
                  </a>
                </template>
             </div>
          </div>
        </div>

      </div>

    </div>

    <!-- Modals -->
    <Teleport to="body">
      <!-- Delegation Modal -->
      <div v-if="delegateModalOpen" class="fixed inset-0 z-[100] flex items-center justify-center bg-gray-900/60 backdrop-blur-md p-4">
        <div class="bg-white rounded-2xl shadow-2xl w-full max-w-md overflow-hidden ring-1 ring-black/5 scale-up">
          <div class="px-6 py-5 border-b border-gray-50 flex items-center justify-between bg-gray-50/30">
            <h3 class="text-lg font-bold text-gray-900 tracking-tight">Делегировать заявку</h3>
            <button @click="delegateModalOpen = false" class="text-gray-400 hover:text-gray-600 transition-colors"><X :size="20"/></button>
          </div>
          <div class="p-6 space-y-6">
            <div>
              <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-2">Кому (Сотрудник)</label>
              <div class="relative">
                <input v-model="delegateSearch" placeholder="Поиск по имени..." class="w-full bg-gray-50 border-transparent rounded-xl px-4 py-2.5 text-sm focus:ring-2 focus:ring-indigo-500/20 focus:bg-white transition-all text-gray-900" />
                <div v-if="delegateSearch || delegateTargetId" class="mt-2 border border-gray-100 rounded-xl max-h-48 overflow-y-auto bg-white shadow-xl">
                   <button v-for="e in filteredDelegateEmployees" :key="e.userId" @click="delegateTargetId = e.userId; delegateSearch = e.fullName" class="w-full text-left px-4 py-3 text-sm hover:bg-indigo-50 border-b border-gray-50 last:border-0 transition-colors" :class="{'bg-indigo-50 text-indigo-700': delegateTargetId === e.userId}">
                     <div class="font-bold">{{ e.fullName }}</div>
                     <div class="text-[10px] text-gray-400 font-bold uppercase tracking-tight">{{ getRoleLabel(e.role) }}</div>
                   </button>
                </div>
              </div>
            </div>
            <div>
               <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-2">Причина делегирования</label>
               <textarea v-model="delegateReason" rows="4" class="w-full bg-gray-50 border-transparent rounded-xl px-4 py-3 text-sm focus:ring-2 focus:ring-indigo-500/20 focus:bg-white transition-all text-gray-900 resize-none shadow-inner" placeholder="Укажите причину для других сотрудников..."></textarea>
            </div>
          </div>
          <div class="px-6 py-5 bg-gray-50/50 border-t border-gray-50 flex justify-end gap-3">
             <button @click="delegateModalOpen = false" class="px-5 py-2.5 text-xs font-bold text-gray-400 hover:text-gray-600 uppercase tracking-widest transition-colors">Отмена</button>
             <button @click="confirmDelegation" :disabled="!delegateTargetId || !delegateReason.trim()" class="px-6 py-2.5 text-xs font-bold text-white bg-indigo-600 hover:bg-indigo-700 rounded-xl disabled:opacity-50 transition-all shadow-lg shadow-indigo-100 uppercase tracking-widest">Подтвердить</button>
          </div>
        </div>
      </div>

      <!-- Assignee Modal (Coordinator direct change) -->
      <div v-if="assigneeModalOpen" class="fixed inset-0 z-[100] flex items-center justify-center bg-gray-900/60 backdrop-blur-md p-4">
        <div class="bg-white rounded-2xl shadow-2xl w-full max-w-md overflow-hidden ring-1 ring-black/5 scale-up">
          <div class="px-6 py-5 border-b border-gray-50 flex items-center justify-between bg-gray-50/30">
            <h3 class="text-lg font-bold text-gray-900 tracking-tight">Изменить ответственных</h3>
            <button @click="assigneeModalOpen = false" class="text-gray-400 hover:text-gray-600 transition-colors"><X :size="20"/></button>
          </div>
          <div class="p-4 space-y-3">
            <input v-model="assigneeSearch" placeholder="Поиск по имени..." class="w-full bg-gray-50 border-transparent rounded-xl px-4 py-2.5 text-sm focus:ring-2 focus:ring-indigo-500/20 focus:bg-white transition-all" />
            <div v-if="selectedAssigneeIds.length" class="flex flex-wrap gap-1.5">
              <span v-for="uid in selectedAssigneeIds" :key="uid" class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-800 border border-green-300">
                {{ employees.find(e => e.userId === uid)?.fullName || uid }}
                <button @click="toggleAssignee(uid)" class="text-green-600 hover:text-green-900 ml-0.5">×</button>
              </span>
            </div>
            <div class="border border-gray-100 rounded-xl max-h-60 overflow-y-auto bg-white">
              <label v-for="e in filteredAssigneeEmployees" :key="e.userId" class="flex items-center gap-3 px-4 py-2.5 cursor-pointer hover:bg-indigo-50 border-b border-gray-50 last:border-0 transition-colors" :class="{'bg-green-50': selectedAssigneeIds.includes(e.userId)}">
                <input type="checkbox" :checked="selectedAssigneeIds.includes(e.userId)" @change="toggleAssignee(e.userId)" class="w-4 h-4 rounded accent-green-600" />
                <div class="flex-1 min-w-0">
                  <div class="font-bold text-sm text-gray-900">{{ e.fullName }}</div>
                  <div class="text-[10px] text-gray-400 font-bold uppercase tracking-tight">{{ getRoleLabel(e.role) }}</div>
                </div>
              </label>
            </div>
          </div>
          <div class="px-6 py-5 bg-gray-50/50 border-t border-gray-50 flex justify-end gap-3">
            <button @click="assigneeModalOpen = false" class="px-5 py-2.5 text-xs font-bold text-gray-400 hover:text-gray-600 uppercase tracking-widest transition-colors">Отмена</button>
            <button @click="saveAssignees" :disabled="selectedAssigneeIds.length === 0" class="px-6 py-2.5 text-xs font-bold text-white bg-indigo-600 hover:bg-indigo-700 rounded-xl disabled:opacity-50 transition-all shadow-lg shadow-indigo-100 uppercase tracking-widest">Сохранить</button>
          </div>
        </div>
      </div>

      <!-- Create / Edit Report Modal (Act) -->
      <div v-if="reportModalOpen" class="fixed inset-0 z-[100] flex items-center justify-center bg-gray-900/60 backdrop-blur-md p-4">
        <div class="bg-white rounded-2xl shadow-2xl w-full max-w-lg overflow-hidden ring-1 ring-black/5 scale-up">
          <div class="px-6 py-5 border-b border-gray-50 flex items-center justify-between bg-orange-50/30">
            <div class="flex items-center gap-3">
               <FileText :size="20" class="text-orange-500" />
               <h3 class="text-lg font-bold text-gray-900 tracking-tight">{{ reportEditingId != null ? 'Редактировать акт' : 'Добавить акт выезда' }}</h3>
            </div>
            <button type="button" @click="closeReportModal" class="text-gray-400 hover:text-gray-600 transition-colors"><X :size="20"/></button>
          </div>
          <div class="p-6 space-y-5 max-h-[70vh] overflow-y-auto">
            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-1.5">Инженер</label>
                <input v-model="reportForm.engineerName" class="w-full bg-gray-50 border-transparent rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-orange-500/20 focus:bg-white transition-all outline-none" />
              </div>
              <div>
                <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-1.5">Дата и время</label>
                <input v-model="reportForm.visitDate" type="datetime-local" class="w-full bg-gray-50 border-transparent rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-orange-500/20 focus:bg-white transition-all outline-none" />
              </div>
            </div>

            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-1.5">Тип работ</label>
                <select v-model="reportForm.actionType" class="w-full bg-gray-50 border-transparent rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-orange-500/20 focus:bg-white transition-all outline-none">
                   <option v-for="t in reportActionTypes" :key="t" :value="t">{{ t }}</option>
                </select>
              </div>
              <div>
                <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-1.5">Статус оборудования</label>
                <select v-model="reportForm.equipmentStatus" class="w-full bg-gray-50 border-transparent rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-orange-500/20 focus:bg-white transition-all outline-none">
                   <option v-for="s in reportEquipStatuses" :key="s" :value="s">{{ s }}</option>
                </select>
              </div>
            </div>

            <div>
              <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-1.5">Тип оборудования</label>
              <input v-model="reportForm.equipmentType" placeholder="Напр. принтер, моноблок…" class="w-full bg-gray-50 border-transparent rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-orange-500/20 focus:bg-white transition-all outline-none" />
            </div>

            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-1.5">Серийный номер</label>
                <input v-model="reportForm.equipmentSerial" placeholder="Если применимо..." class="w-full bg-gray-50 border-transparent rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-orange-500/20 focus:bg-white transition-all outline-none" />
              </div>
              <div>
                <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-1.5">Кому передано</label>
                <input v-model="reportForm.transferredTo" placeholder="ФИО сотрудника клиента..." class="w-full bg-gray-50 border-transparent rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-orange-500/20 focus:bg-white transition-all outline-none" />
              </div>
            </div>

            <div>
               <label class="block text-[10px] font-bold text-gray-400 uppercase tracking-widest mb-1.5">Что сделано</label>
               <textarea v-model="reportForm.workDone" rows="4" class="w-full bg-gray-50 border-transparent rounded-lg px-4 py-3 text-sm focus:ring-2 focus:ring-orange-500/20 focus:bg-white transition-all text-gray-900 resize-none shadow-inner" placeholder="Опишите выполненные работы..."></textarea>
            </div>
          </div>
          <div class="px-6 py-5 bg-gray-50/50 border-t border-gray-50 flex justify-end gap-3">
             <button type="button" @click="closeReportModal" class="px-5 py-2.5 text-xs font-bold text-gray-400 hover:text-gray-600 uppercase tracking-widest transition-colors">Отмена</button>
             <button type="button" @click="saveReport" :disabled="!reportForm.workDone.trim() || creatingReport" class="px-6 py-2.5 text-xs font-bold text-white bg-orange-600 hover:bg-orange-700 rounded-xl disabled:opacity-50 transition-all shadow-lg shadow-orange-100 uppercase tracking-widest">
                {{ creatingReport ? '...' : (reportEditingId != null ? 'Сохранить' : 'Создать акт') }}
             </button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Панель комментария: только под левой колонкой (как сетка заявки), без перекрытия правой; фон лёгкий, клики мимо — сквозь пустую область -->
    <Teleport to="body">
      <div
        class="fixed z-40 bottom-14 lg:bottom-0 left-0 right-0 lg:left-[260px] pointer-events-none"
      >
        <div
          class="w-full max-w-none mx-auto px-2 sm:px-4 lg:px-6 xl:px-8 min-w-0 pointer-events-none"
        >
          <div
            class="grid grid-cols-1 lg:grid-cols-[29fr_11fr] xl:grid-cols-[49fr_11fr] gap-4 sm:gap-6 items-end pointer-events-none"
          >
            <div
              v-if="canInteractWithTicket"
              class="pointer-events-auto py-2 sm:py-3 px-3 sm:px-4 flex flex-col gap-2 w-full min-w-0 rounded-t-xl border-t border-gray-200/60 dark:border-zinc-700/45 bg-white/65 dark:bg-[#0f0f10]/55 backdrop-blur-md shadow-[0_-6px_24px_rgba(0,0,0,0.05)] dark:shadow-[0_-6px_28px_rgba(0,0,0,0.28)]"
            >
            <div class="w-full min-w-0 max-w-none flex flex-col gap-1">
              <textarea
                ref="commentTextareaRef"
                v-model="newComment"
                rows="1"
                placeholder="Оставьте комментарий... (Ctrl+Enter — отправить)"
                class="w-full max-w-none min-w-0 block box-border bg-gray-50 dark:bg-[#141416] border border-gray-200 dark:border-zinc-700 rounded-xl px-3 sm:px-4 py-2 sm:py-2.5 text-sm focus:ring-2 focus:ring-indigo-500/20 focus:bg-white dark:focus:bg-[#1e1e21] focus:border-indigo-300 dark:focus:border-indigo-500/50 transition-all text-gray-900 dark:text-gray-100 resize-none min-h-[120px] max-h-[400px] overflow-y-auto"
                @keydown="onCommentKeydown"
                @paste="onCommentPaste"
                @input="autoResizeComment"
              />
              <div v-if="commentPendingFiles.length" class="flex flex-wrap gap-2">
                <div v-for="(f, i) in commentPendingFiles" :key="i" class="flex items-center gap-2 px-2 py-1 bg-indigo-50/50 rounded border border-indigo-100 text-[10px] text-indigo-700">
                  <span class="truncate max-w-[min(100vw-8rem,240px)] sm:max-w-xs">{{ f.name }}</span>
                  <button @click="removeCommentFile(i)" class="text-indigo-300 hover:text-indigo-600"><X :size="10"/></button>
                </div>
              </div>
            </div>

            <div class="flex items-center justify-between gap-2 sm:gap-3 w-full min-w-0">
              <div class="flex items-center gap-2 sm:gap-3 min-w-0 flex-wrap">
                <label v-if="auth.isStaff" class="relative inline-flex items-center cursor-pointer group shrink-0">
                  <input type="checkbox" v-model="commentInternal" class="sr-only peer">
                  <div class="w-9 h-5 bg-gray-200 dark:bg-zinc-600 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-yellow-400 transition-colors"></div>
                  <span class="ml-2 text-[10px] sm:text-xs font-bold text-gray-400 dark:text-zinc-400 group-hover:text-gray-900 dark:group-hover:text-zinc-200 uppercase tracking-wide whitespace-nowrap">Внутренний</span>
                </label>

                <input type="file" ref="commentFileRef" multiple class="hidden" @change="onCommentFileSelect" accept="image/*,application/pdf,.doc,.docx,.xls,.xlsx,.txt,.zip,.rar" />
                <button type="button" @click="commentFileRef?.click()" class="p-2 text-gray-400 dark:text-zinc-400 hover:text-indigo-600 dark:hover:text-indigo-400 active:bg-gray-100 dark:active:bg-zinc-800 rounded-lg transition-colors shrink-0" title="Прикрепить файл">
                  <Paperclip :size="18"/>
                </button>
                <button
                  v-if="auth.isStaff"
                  type="button"
                  @click="suggestReply"
                  :disabled="suggestingReply"
                  class="inline-flex items-center gap-1 px-2.5 py-1.5 text-[10px] sm:text-[11px] font-bold uppercase tracking-wide text-indigo-600 dark:text-indigo-400 hover:bg-indigo-50 dark:hover:bg-indigo-950/40 rounded-lg disabled:opacity-50 transition-colors shrink-0"
                  title="Подсказать ответ"
                >
                  <HelpCircle :size="14" />
                  <span class="hidden sm:inline">{{ suggestingReply ? '…' : 'Подсказать ответ' }}</span>
                </button>
                <input ref="fileInputRef" type="file" multiple class="hidden" @change="handleFileUpload" />
                <span v-if="uploadingFiles" class="text-[10px] text-gray-400 dark:text-zinc-500 flex items-center gap-1 font-bold shrink-0"><RefreshCw :size="12" class="animate-spin"/></span>
              </div>

              <button
                @click="sendComment"
                :disabled="(!newComment.trim() && commentPendingFiles.length === 0) || sendingComment || uploadingFiles"
                class="inline-flex items-center gap-1.5 px-4 sm:px-6 py-2 bg-indigo-600 text-white font-bold text-[11px] sm:text-xs rounded-xl hover:bg-indigo-700 active:bg-indigo-800 disabled:opacity-50 transition-all shadow-md shadow-indigo-100 dark:shadow-none uppercase tracking-widest shrink-0"
              >
                <Send :size="14" /> <span class="hidden sm:inline">{{ sendingComment ? 'Связь...' : 'Отправить' }}</span>
              </button>
            </div>
            </div>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Lightbox -->
    <Teleport to="body">
      <Transition name="fade">
        <div v-if="lightboxOpen" class="fixed inset-0 z-[200] flex items-center justify-center bg-black/90 backdrop-blur-sm" @click.self="closeLightbox">
          <button @click="closeLightbox" class="absolute top-4 right-4 z-10 p-2 text-white/70 hover:text-white bg-white/10 hover:bg-white/20 rounded-full transition-all">
            <X :size="24" />
          </button>

          <button v-if="lightboxImages.length > 1 && lightboxIndex > 0" @click="lightboxPrev" class="absolute left-4 z-10 p-3 text-white/70 hover:text-white bg-white/10 hover:bg-white/20 rounded-full transition-all">
            <ArrowLeft :size="24" />
          </button>

          <img :src="lightboxUrl" class="max-w-[90vw] max-h-[90vh] object-contain rounded-lg shadow-2xl select-none" @click.stop />

          <button v-if="lightboxImages.length > 1 && lightboxIndex < lightboxImages.length - 1" @click="lightboxNext" class="absolute right-4 z-10 p-3 text-white/70 hover:text-white bg-white/10 hover:bg-white/20 rounded-full transition-all">
            <ChevronRight :size="24" />
          </button>

          <div v-if="lightboxImages.length > 1" class="absolute bottom-6 left-1/2 -translate-x-1/2 text-white/60 text-sm font-mono">
            {{ lightboxIndex + 1 }} / {{ lightboxImages.length }}
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity 0.3s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

.scale-up {
  animation: scale-up 0.2s cubic-bezier(0.16, 1, 0.3, 1);
}

@keyframes scale-up {
  from { transform: scale(0.95); opacity: 0; }
  to { transform: scale(1); opacity: 1; }
}

.prose {
  color: #374151; /* gray-700 */
}
</style>

<style>
/* Явная фиолетовая обводка (перебивает глобальные стили input) */
.alt-title-input {
  border: 2px solid #4f46e5 !important;
  box-sizing: border-box;
}
html.dark .alt-title-input {
  border-color: #a78bfa !important;
}
</style>
