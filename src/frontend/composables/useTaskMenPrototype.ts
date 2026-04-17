export type TaskMenTask = {
  id: string
  title: string
  creator: string
  createdAt: string
  activityAt: string
  status: 'Ждёт выполнения' | 'Выполняется' | 'Завершена' | 'Отложена'
  deadline: string | null
  assignee: string
  hasNewActivity?: boolean
  description: string
}

type TaskMenUser = { id: string; name: string }

type SortKey = keyof TaskMenTask
type SortDir = 'asc' | 'desc'

function fromDatetimeLocal(local: string): string | null {
  if (!local.trim()) return null
  const d = new Date(local)
  if (Number.isNaN(d.getTime())) return null
  return d.toISOString()
}

export function useTaskMenPrototype() {
  const auth = useAuthStore()
  const toast = useToast()

  const activeTab = ref<'tasks' | 'employees' | 'docs'>('tasks')
  const isUserMenuOpen = ref(false)
  const isFilterMenuOpen = ref(false)
  const isCreateModalOpen = ref(false)
  const isModalExpanded = ref(false)
  const isTaskSliderOpen = ref(false)
  const selectedTask = ref<TaskMenTask | null>(null)
  const selectedTaskIds = ref<string[]>([])
  const fileInputRef = ref<HTMLInputElement | null>(null)
  const descTextarea = ref<HTMLTextAreaElement | null>(null)

  const sortKey = ref<SortKey>('createdAt')
  const sortDir = ref<SortDir>('desc')

  const filters = reactive({
    search: '',
    status: '',
    assignee: '',
    creator: '',
  })

  const users = ref<TaskMenUser[]>([
    { id: 'u1', name: 'Демо Иван' },
    { id: 'u2', name: 'Демо Мария' },
    { id: 'u3', name: 'Демо Алексей' },
  ])

  const tasks = ref<TaskMenTask[]>([
    {
      id: 't1',
      title: 'Пример: настроить печать этикеток',
      creator: 'Демо Иван',
      createdAt: new Date(Date.now() - 86400000 * 2).toISOString(),
      activityAt: new Date(Date.now() - 3600000 * 5).toISOString(),
      status: 'Выполняется',
      deadline: new Date(Date.now() + 86400000).toISOString(),
      assignee: 'Демо Мария',
      hasNewActivity: true,
      description: 'Проверить драйвер и формат ZPL.',
    },
    {
      id: 't2',
      title: 'Пример: обновить прошивку ТСД',
      creator: 'Демо Мария',
      createdAt: new Date(Date.now() - 86400000 * 5).toISOString(),
      activityAt: new Date(Date.now() - 86400000).toISOString(),
      status: 'Ждёт выполнения',
      deadline: null,
      assignee: '',
      description: '',
    },
    {
      id: 't3',
      title: 'Пример: закрыть месяц в отчётах',
      creator: 'Демо Алексей',
      createdAt: new Date(Date.now() - 86400000 * 10).toISOString(),
      activityAt: new Date(Date.now() - 86400000 * 3).toISOString(),
      status: 'Завершена',
      deadline: new Date(Date.now() - 86400000).toISOString(),
      assignee: 'Демо Иван',
      description: 'Акт сверки отправлен.',
    },
  ])

  const currentUserDisplay = computed(() => {
    const name = (auth.fullName || 'Сотрудник').trim() || 'Сотрудник'
    const role = auth.role === 'super_admin' ? 'superadmin' : 'employee'
    return { name, role }
  })

  syncUsersWithAuth()

  function syncUsersWithAuth() {
    const name = (auth.fullName || '').trim()
    if (!name) return
    if (!users.value.some((u) => u.name === name)) {
      users.value = [{ id: auth.userId || `u-${name}`, name }, ...users.value]
    }
  }

  watch(
    () => auth.fullName,
    () => {
      syncUsersWithAuth()
    },
  )

  const hasActiveFilters = computed(
    () =>
      !!(filters.search.trim() || filters.status || filters.assignee || filters.creator),
  )

  function cmp(a: TaskMenTask, b: TaskMenTask, key: SortKey): number {
    const dir = sortDir.value === 'asc' ? 1 : -1
    switch (key) {
      case 'createdAt':
      case 'activityAt':
        return (new Date(a[key]).getTime() - new Date(b[key]).getTime()) * dir
      case 'deadline': {
        const ta = a.deadline ? new Date(a.deadline).getTime() : 0
        const tb = b.deadline ? new Date(b.deadline).getTime() : 0
        return (ta - tb) * dir
      }
      case 'id':
        return a.id.localeCompare(b.id, undefined, { numeric: true }) * dir
      default: {
        const va = String(a[key] ?? '')
        const vb = String(b[key] ?? '')
        return va.localeCompare(vb, 'ru') * dir
      }
    }
  }

  const filteredTasks = computed(() => {
    const q = filters.search.trim().toLowerCase()
    let list = tasks.value.filter((t) => {
      if (filters.status && t.status !== filters.status) return false
      if (filters.assignee && t.assignee !== filters.assignee) return false
      if (filters.creator && t.creator !== filters.creator) return false
      if (q) {
        const blob = `${t.title} ${t.creator} ${t.assignee} ${t.status}`.toLowerCase()
        if (!blob.includes(q)) return false
      }
      return true
    })
    const sk = sortKey.value
    list = [...list].sort((a, b) => cmp(a, b, sk))
    return list
  })

  const selectAll = computed({
    get() {
      const ids = filteredTasks.value.map((t) => t.id)
      return ids.length > 0 && ids.every((id) => selectedTaskIds.value.includes(id))
    },
    set(checked: boolean) {
      if (checked) {
        const set = new Set(selectedTaskIds.value)
        for (const t of filteredTasks.value) set.add(t.id)
        selectedTaskIds.value = [...set]
      } else {
        const drop = new Set(filteredTasks.value.map((t) => t.id))
        selectedTaskIds.value = selectedTaskIds.value.filter((id) => !drop.has(id))
      }
    },
  })

  const newTask = reactive({
    title: '',
    description: '',
    creator: '',
    assignee: '',
    deadline: '',
    observers: [] as string[],
  })

  function resetNewTask() {
    const defaultCreator =
      currentUserDisplay.value.role === 'superadmin'
        ? users.value[0]?.name || currentUserDisplay.value.name
        : currentUserDisplay.value.name
    newTask.title = ''
    newTask.description = ''
    newTask.creator = defaultCreator
    newTask.assignee = ''
    newTask.deadline = ''
    newTask.observers = []
  }

  watch(
    currentUserDisplay,
    () => {
      if (!newTask.creator && currentUserDisplay.value.name) {
        resetNewTask()
      }
    },
    { immediate: true },
  )

  function toggleUserMenu() {
    isUserMenuOpen.value = !isUserMenuOpen.value
  }

  function toggleFilterMenu() {
    isFilterMenuOpen.value = !isFilterMenuOpen.value
  }

  function resetFilters() {
    filters.search = ''
    filters.status = ''
    filters.assignee = ''
    filters.creator = ''
  }

  function mockAction(label: string) {
    toast.info(`Прототип: «${label}»`)
  }

  function formatDate(iso: string | undefined) {
    if (!iso) return '—'
    const d = new Date(iso)
    if (Number.isNaN(d.getTime())) return '—'
    return d.toLocaleString('ru-RU', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    })
  }

  function formatDeadline(iso: string | null) {
    if (!iso) return 'Нет срока'
    return formatDate(iso)
  }

  function getDeadlineStyle(iso: string | null) {
    if (!iso) return 'text-gray-600'
    const t = new Date(iso).getTime()
    if (Number.isNaN(t)) return 'text-gray-600'
    const now = Date.now()
    if (t < now) return 'text-red-600 font-medium'
    if (t < now + 86400000) return 'text-amber-600 font-medium'
    return 'text-gray-800'
  }

  function sortBy(key: SortKey) {
    if (sortKey.value === key) {
      sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
    } else {
      sortKey.value = key
      sortDir.value = key === 'title' || key === 'creator' || key === 'assignee' || key === 'status' ? 'asc' : 'desc'
    }
  }

  function getSortIcon(key: SortKey) {
    if (sortKey.value !== key) return 'ph-caret-up-down opacity-40'
    return sortDir.value === 'asc' ? 'ph-caret-up' : 'ph-caret-down'
  }

  function openModal() {
    resetNewTask()
    isModalExpanded.value = false
    isCreateModalOpen.value = true
  }

  function closeModal() {
    isCreateModalOpen.value = false
  }

  function toggleModalExpand() {
    isModalExpanded.value = !isModalExpanded.value
  }

  function openTask(task: TaskMenTask) {
    selectedTask.value = task
    isTaskSliderOpen.value = true
  }

  function closeTask() {
    isTaskSliderOpen.value = false
    selectedTask.value = null
  }

  function triggerFileUpload() {
    fileInputRef.value?.click()
  }

  function handleFileUpload(ev: Event) {
    const input = ev.target as HTMLInputElement
    const files = input.files
    if (files?.length) {
      toast.info(`Выбрано файлов: ${files.length} (в прототипе не загружаются)`)
    }
    input.value = ''
  }

  function removeObserver(idx: number) {
    newTask.observers.splice(idx, 1)
  }

  function insertFormat(kind: 'bold' | 'italic' | 'underline' | 'link') {
    const el = descTextarea.value
    const wrap: Record<typeof kind, [string, string]> = {
      bold: ['**', '**'],
      italic: ['*', '*'],
      underline: ['__', '__'],
      link: ['[', '](url)'],
    }
    const [a, b] = wrap[kind]
    if (!el) {
      newTask.description += `${a}${b}`
      return
    }
    const start = el.selectionStart ?? newTask.description.length
    const end = el.selectionEnd ?? start
    const text = newTask.description
    const sel = text.slice(start, end)
    const inserted = `${a}${sel || 'текст'}${b}`
    newTask.description = text.slice(0, start) + inserted + text.slice(end)
    nextTick(() => {
      el.focus()
      const pos = start + inserted.length
      el.setSelectionRange(pos, pos)
    })
  }

  function submitTask() {
    const title = newTask.title.trim()
    if (!title) {
      toast.warning('Укажите название задачи')
      return
    }
    const id = `t-${Date.now()}`
    const now = new Date().toISOString()
    const deadlineIso = fromDatetimeLocal(newTask.deadline)
    tasks.value.unshift({
      id,
      title,
      description: newTask.description.trim(),
      creator: newTask.creator.trim() || currentUserDisplay.value.name,
      assignee: newTask.assignee.trim(),
      createdAt: now,
      activityAt: now,
      status: 'Ждёт выполнения',
      deadline: deadlineIso,
      hasNewActivity: false,
    })
    toast.success('Задача добавлена (только в прототипе)')
    closeModal()
  }

  function backToTicketSystem() {
    void navigateTo('/')
  }

  return {
    activeTab,
    currentUserDisplay,
    isUserMenuOpen,
    toggleUserMenu,
    mockAction,
    users,
    filters,
    isFilterMenuOpen,
    toggleFilterMenu,
    resetFilters,
    hasActiveFilters,
    filteredTasks,
    formatDate,
    formatDeadline,
    getDeadlineStyle,
    sortBy,
    getSortIcon,
    selectedTaskIds,
    selectAll,
    isCreateModalOpen,
    isModalExpanded,
    toggleModalExpand,
    newTask,
    descTextarea,
    fileInputRef,
    openModal,
    closeModal,
    insertFormat,
    triggerFileUpload,
    handleFileUpload,
    removeObserver,
    submitTask,
    isTaskSliderOpen,
    selectedTask,
    openTask,
    closeTask,
    backToTicketSystem,
  }
}
