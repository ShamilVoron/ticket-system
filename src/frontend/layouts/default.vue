<script setup lang="ts">
import { 
  LayoutDashboard, Ticket, Users, Wrench, Building2, 
  MapPin, Settings, LogOut, Menu, ChevronDown,
  FileSpreadsheet, BarChart3, Plus, Bell, RefreshCw, User2,
  CheckCircle, AlertCircle, AlertTriangle, Info, ArrowLeft,
  Moon, Sun, CalendarDays, MessageSquare
} from 'lucide-vue-next'
import { resolvePublicApiBaseUrl } from '~/utils/resolvePublicApiBaseUrl'
import { useMessengerSignalR } from '~/composables/useMessengerSignalR'
import { isUserActiveOnSite } from '~/composables/useSitePresence'
import type { ToastItem } from '~/composables/useToast'

const auth = useAuthStore()
if (import.meta.client) {
  auth.hydrate()
}
const { can } = useStaffPermissions()
const route = useRoute()
const router = useRouter()
const api = useApi()
const toast = useToast()
const pageHeader = usePageHeader()
const theme = useTheme()
const browserNotif = useBrowserNotifications()
const notifEnabled = browserNotif.enabled
const notifPermission = browserNotif.permission
const notifSecureContext = browserNotif.secureContext
const messengerUnreadTotal = useState('messengerUnread', () => 0)
const { branding, load: loadBranding } = useSystemBranding()
const brandLogoSrc = computed(() => branding.value.logoUrl || '/favicon.svg')
const brandTitle = computed(() => branding.value.companyName || 'Ticket System')

const bellTitle = computed(() => {
  if (!notifSecureContext.value) {
    return 'Уведомления недоступны: откройте сайт по HTTPS (сейчас соединение не защищено)'
  }
  if (notifPermission.value === 'unsupported') return 'Уведомления в браузере недоступны'
  if (notifPermission.value === 'denied') return 'Уведомления заблокированы — откройте настройки сайта (значок в адресной строке)'
  if (notifEnabled.value) {
    return 'Уведомления: на сайте — всплывающие внутри приложения; вне вкладки — в системе (если разрешено)'
  }
  return 'Включить системные уведомления, когда вкладка не активна (на сайте всегда показываются внутри)'
})

async function toggleNotifications() {
  const result = await browserNotif.toggle()
  if (result === 'insecure') {
    toast.warning(
      'Браузерные уведомления работают только по HTTPS или на localhost. Сейчас адрес «Не защищён» — настройте доступ по https:// (например, nginx + Let\'s Encrypt) или используйте для теста localhost.'
    )
  } else if (result === 'denied') {
    toast.warning('Браузер не разрешил уведомления. Разрешите их для этого сайта в настройках (значок замка или «i» слева от адреса).')
  } else if (result === 'unsupported') {
    toast.warning('Ваш браузер не поддерживает стандартные уведомления на этой странице.')
  }
}

async function loadMessengerUnread() {
  if (!auth.isStaff || !can('sidebarMessenger')) return
  try {
    const list = await api.messenger.listConversations()
    messengerUnreadTotal.value = list.reduce((sum: number, c: any) => sum + (c.unreadCount || 0), 0)
  } catch {
    messengerUnreadTotal.value = 0
  }
}

/** На активной вкладке — тост с подписью и ссылкой; иначе — системное уведомление (если включено), иначе тост. */
function notifyByPresence(p: {
  appHeadline: string
  appBody: string
  navigateTo?: string
  browserTitle: string
  browserBody: string
  browserTag: string
}) {
  const body = (p.appBody ?? '').trim().slice(0, 420)
  const browserBody = (p.browserBody ?? '').trim()
  const browserBodyShort =
    browserBody.slice(0, 160) + (browserBody.length > 160 ? '…' : '')

  if (isUserActiveOnSite()) {
    toast.info(body, {
      headline: p.appHeadline,
      navigateTo: p.navigateTo,
      durationMs: 22_000,
    })
    return
  }
  const canOs =
    notifSecureContext.value &&
    notifPermission.value === 'granted' &&
    notifEnabled.value
  if (canOs) {
    browserNotif.notify(p.browserTitle, browserBodyShort, {
      tag: p.browserTag,
      force: true,
    })
  } else {
    toast.info(body, {
      headline: p.appHeadline,
      navigateTo: p.navigateTo,
      durationMs: 22_000,
    })
  }
}

function ticketNotifyHeadline(kind: string): string {
  switch (kind) {
    case 'status':
      return 'Заявка · смена статуса'
    case 'assigned':
      return 'Заявка · назначение'
    case 'comment':
      return 'Заявка · комментарий'
    case 'created':
      return 'Заявка · новая'
    case 'field_report':
      return 'Заявка · акт выезда'
    case 'attachment':
      return 'Заявка · вложение'
    case 'subtask':
      return 'Заявка · подзадача'
    default:
      return 'Заявка · обновление'
  }
}

function onToastClick(t: ToastItem) {
  if (t.navigateTo) {
    void router.push(t.navigateTo)
  }
  toast.remove(t.id)
}

const { onSidebar, onChatMessage } = useMessengerSignalR()
onSidebar((p) => {
  void loadMessengerUnread()
})

// Бейдж в шапке часто растёт из MessengerSidebar без корректного sidebarEventKind/lastMessageSenderUserId;
// ChatMessage всегда содержит senderUserId и текст — надёжный источник для системного уведомления.
onChatMessage((msg) => {
  if (!auth.isStaff) return
  const sid = (msg.senderUserId ?? '').trim()
  const myId = (auth.userId ?? '').trim()
  if (!sid || sid.toLowerCase() === myId.toLowerCase()) return
  const body = (msg.body ?? '').trim()
  const att = (msg.attachmentFileName ?? '').trim()
  const preview = body || (att ? `Файл: ${att}` : '') || 'Новое сообщение'
  const who = (msg.senderFullName ?? '').trim() || 'Собеседник'
  const msgKey = (msg.id ?? '').trim() || `${Date.now()}-${Math.random().toString(36).slice(2, 9)}`
  const line = `${who}: ${preview}`
  notifyByPresence({
    appHeadline: 'Мессенджер · новое сообщение',
    appBody: line,
    navigateTo: `/messenger?c=${encodeURIComponent(msg.conversationId)}`,
    browserTitle: 'Мессенджер',
    browserBody: line,
    browserTag: `ticket-system-chat-${msg.conversationId}-${msgKey}`,
  })
})

const { connectionState: ticketHubState } = useTicketSignalR((payload) => {
  const actor = payload.actorUserId ?? ''
  if (actor && actor === auth.userId) return

  const recipients = payload.recipientUserIds
  if (recipients && recipients.length > 0) {
    if (!recipients.includes(auth.userId)) return
  } else if (!auth.isStaff) {
    return
  }

  const ticketId = payload.ticketId
  const rawMsg = payload.message?.trim()
  const text =
    rawMsg ||
    (ticketId != null ? `Заявка #${ticketId}` : 'Обновление заявок')
  const kind = payload.kind ?? 'generic'
  const headline = ticketNotifyHeadline(kind)
  const path = ticketId != null ? `/tickets/${ticketId}` : undefined
  notifyByPresence({
    appHeadline: headline,
    appBody: text,
    navigateTo: path,
    browserTitle: headline,
    browserBody: text,
    browserTag: ticketId != null ? `ticket-system-ticket-${kind}-${ticketId}-${Date.now()}` : `ticket-system-ticket-${kind}-${Date.now()}`,
  })
})

const showOfflineBanner = computed(() =>
  ticketHubState.value === 'Disconnected' || ticketHubState.value === 'Reconnecting'
)

const toastIcon: Record<string, any> = {
  success: CheckCircle,
  error: AlertCircle,
  warning: AlertTriangle,
  info: Info,
}
const sidebarOpen = ref(false)
const openGroups = ref<Record<string, boolean>>({
  clients: false,
  equipment: false,
})

watch(
  () => route.path,
  (p) => {
    openGroups.value.clients = p.startsWith('/companies') || p.startsWith('/objects')
    openGroups.value.equipment = p.startsWith('/equipment')
  },
  { immediate: true },
)

const mobileNavItems = computed(() => {
  const items = [
    { to: '/portal', label: 'Портал', icon: LayoutDashboard, show: auth.isClient },
    { to: '/', label: 'Заявки', icon: Ticket, show: !auth.isClient && can('sidebarAllTickets') },
    { to: '/my', label: 'Мои', icon: LayoutDashboard, show: auth.isStaff && can('sidebarMyTickets') },
    { to: '/messenger', label: 'Чат', icon: MessageSquare, show: auth.isStaff && can('sidebarMessenger') },
    {
      to: '/tickets/new',
      label: 'Создать',
      icon: Plus,
      isCreate: true,
      show: can('sidebarNewTicket') && can('newTicketVisible'),
    },
    { to: '/schedule', label: 'График', icon: CalendarDays, show: auth.isStaff && can('sidebarSchedule') },
    { to: '/profile', label: 'Профиль', icon: User2, show: true },
  ]
  return items.filter(i => i.show)
})

function toggleGroup(key: string) {
  openGroups.value[key] = !openGroups.value[key]
}

const profileOpen = ref(false)
const profileLogin = ref('')

const apiBase = computed(() => {
  const cfg = useRuntimeConfig()
  return resolvePublicApiBaseUrl(cfg.public.apiBaseUrl as string | undefined)
})

const avatarBroken = ref(false)
const resolvedAvatarUrl = computed(() => {
  const raw = (auth.avatarUrl || '').trim()
  if (!raw) return ''
  if (/^https?:\/\//i.test(raw)) return raw
  if (raw.startsWith('/')) return `${apiBase.value}${raw}`
  return raw
})
watch(() => auth.avatarUrl, () => { avatarBroken.value = false })
const showAvatar = computed(() => resolvedAvatarUrl.value && !avatarBroken.value)

function goProfile() {
  profileOpen.value = false
  navigateTo('/profile')
}

function handleLogout() {
  auth.logout()
  navigateTo('/auth/login')
}

function createTicket() {
  if (!auth.isSuperAdmin && !can('newTicketCreate')) {
    toast.warning('Нет права создавать заявку')
    return
  }
  navigateTo('/tickets/new')
}

watch(() => route.path, () => {
  sidebarOpen.value = false
  profileOpen.value = false
})

onMounted(() => {
  theme.init()
  auth.hydrate()
  void loadBranding()
  void loadMessengerUnread()
  // Try to load login for dropdown (if allowed)
  if (auth.userId) {
    void (async () => {
      try {
        const e = await api.employees.getById(auth.userId)
        profileLogin.value = String(e?.login || '').trim()
        // always refresh avatar from backend (it may change)
        if (String(e?.avatarUrl || '').trim()) auth.avatarUrl = String(e.avatarUrl).trim()
      } catch {
        profileLogin.value = ''
      }
    })()
  }
})
</script>

<template>
  <div class="min-h-screen flex bg-[#F8F9FA] dark:bg-[#0f0f10] text-[13px] dark:text-gray-200" :style="branding.accentColor ? { '--brand-accent': branding.accentColor } : undefined">
    <!-- Mobile overlay -->
    <Transition name="fade">
      <div
        v-if="sidebarOpen"
        class="fixed inset-0 bg-gray-900/60 backdrop-blur-sm z-40 lg:hidden"
        @click="sidebarOpen = false"
      />
    </Transition>

    <!-- Sidebar -->
    <aside
      :class="[
        'fixed lg:sticky top-0 left-0 z-50 h-screen w-[260px] flex flex-col bg-white dark:bg-[#0A0A0B] border-r border-zinc-200 dark:border-[#1F1F22] transition-transform duration-300 shrink-0',
        sidebarOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'
      ]"
    >
      <!-- Брендинг -->
      <NuxtLink
        :to="auth.isClient ? '/portal' : '/'"
        class="h-14 px-5 w-full flex items-center gap-3 border-b border-zinc-200/80 dark:border-[#1F1F22]/60 shrink-0 hover:bg-zinc-50 dark:hover:bg-white/[0.06] transition-colors"
      >
        <img :src="brandLogoSrc" alt="" class="w-7 h-7 rounded-md shrink-0 object-cover" />
        <h1 class="font-semibold text-[14px] text-zinc-900 dark:text-white tracking-wide truncate">{{ brandTitle }}</h1>
      </NuxtLink>

      <!-- Create Ticket Button -->
      <div
        v-if="can('sidebarNewTicket') && can('newTicketVisible')"
        class="px-4 py-4 shrink-0 border-b border-zinc-200/80 dark:border-[#1F1F22]/40"
      >
        <button 
          @click="createTicket"
          class="w-full inline-flex items-center justify-center gap-2 bg-zinc-900 hover:bg-zinc-800 dark:bg-[#0A0A0B] dark:hover:bg-zinc-800 text-white px-4 py-2.5 rounded-md text-[13px] font-medium transition-colors border border-zinc-800 dark:border-white/10"
        >
          <Plus :size="16" />
          Новая заявка
        </button>
      </div>

      <!-- Navigation -->
      <nav class="flex-1 overflow-y-auto py-4 flex flex-col gap-0.5 px-3">
        <p class="px-3 text-[11px] font-semibold text-zinc-400 dark:text-zinc-500 uppercase tracking-wider mb-1 mt-2">Основное</p>

        <NuxtLink
          v-if="auth.isClient"
          to="/portal"
          class="nav-link"
          :class="route.path.startsWith('/portal') ? 'active-link' : ''"
        >
          <LayoutDashboard class="w-4 h-4" />
          <span>Портал</span>
        </NuxtLink>

        <NuxtLink
          v-if="!auth.isClient && can('sidebarAllTickets')"
          to="/"
          class="nav-link"
          :class="route.path === '/' ? 'active-link' : ''"
        >
          <Ticket class="w-4 h-4" />
          <span>Все заявки</span>
        </NuxtLink>

        <NuxtLink
          v-if="auth.isStaff && can('sidebarMyTickets')"
          to="/my"
          class="nav-link"
          :class="route.path.startsWith('/my') ? 'active-link' : ''"
        >
          <LayoutDashboard class="w-4 h-4" />
          <span>Мои заявки</span>
        </NuxtLink>

        <NuxtLink
          v-if="auth.isStaff && can('sidebarMessenger')"
          to="/messenger"
          class="nav-link"
          :class="route.path.startsWith('/messenger') ? 'active-link' : ''"
        >
          <MessageSquare class="w-4 h-4" />
          <span>Мессенджер</span>
          <span
            v-if="messengerUnreadTotal > 0"
            class="ml-auto shrink-0 inline-flex items-center justify-center min-w-[1.25rem] h-5 px-1 rounded-full bg-blue-500 text-white text-[10px] font-bold"
          >
            {{ messengerUnreadTotal > 99 ? '99+' : messengerUnreadTotal }}
          </span>
        </NuxtLink>

        <p class="px-3 text-[11px] font-semibold text-zinc-400 dark:text-zinc-500 uppercase tracking-wider mb-1 mt-5">Справочники</p>

        <!-- Клиенты (админы + выездные инженеры) -->
        <div v-if="can('sidebarClients')">
          <button
            type="button"
            class="nav-link justify-between"
            @click="toggleGroup('clients')"
          >
            <div class="flex items-center gap-3">
              <Building2 class="w-4 h-4" />
              <span>Клиенты</span>
            </div>
            <ChevronDown class="w-3.5 h-3.5 nav-arrow" :class="openGroups.clients ? 'open' : ''" />
          </button>
          <div v-show="openGroups.clients" class="pl-9 flex flex-col gap-0.5 mt-0.5">
            <NuxtLink to="/companies" class="nav-sublink">Юрлица</NuxtLink>
            <NuxtLink to="/objects" class="nav-sublink">Объекты</NuxtLink>
          </div>
        </div>

        <!-- Оборудование (админы + выездные инженеры) -->
        <div v-if="can('sidebarEquipment')">
          <button
            type="button"
            class="nav-link justify-between"
            @click="toggleGroup('equipment')"
          >
            <div class="flex items-center gap-3">
              <Wrench class="w-4 h-4" />
              <span>Оборудование</span>
            </div>
            <ChevronDown class="w-3.5 h-3.5 nav-arrow" :class="openGroups.equipment ? 'open' : ''" />
          </button>
          <div v-show="openGroups.equipment" class="pl-9 flex flex-col gap-0.5 mt-0.5">
            <NuxtLink to="/equipment" class="nav-sublink">Реестр</NuxtLink>
          </div>
        </div>

        <!-- Сотрудники (не выездные инженеры) -->
        <div v-if="auth.isStaff && can('sidebarEmployees')">
          <NuxtLink
            to="/employees"
            class="nav-link"
            :class="route.path.startsWith('/employees') ? 'active-link' : ''"
          >
            <Users class="w-4 h-4" />
            <span>Сотрудники</span>
          </NuxtLink>
        </div>

        <!-- График работы -->
        <NuxtLink
          v-if="auth.isStaff && can('sidebarSchedule')"
          to="/schedule"
          class="nav-link"
          :class="route.path.startsWith('/schedule') ? 'active-link' : ''"
        >
          <CalendarDays class="w-4 h-4" />
          <span>График работы</span>
        </NuxtLink>

        <p class="px-3 text-[11px] font-semibold text-zinc-400 dark:text-zinc-500 uppercase tracking-wider mb-1 mt-5">Аналитика и Файлы</p>

        <NuxtLink
          v-if="auth.isStaff && can('sidebarReports')"
          to="/reports"
          class="nav-link"
          :class="route.path.startsWith('/reports') ? 'active-link' : ''"
        >
          <BarChart3 class="w-4 h-4" />
          <span>Отчёты</span>
        </NuxtLink>

        <NuxtLink
          v-if="auth.isStaff && can('sidebarSpreadsheets')"
          to="/spreadsheets"
          class="nav-link"
          :class="route.path.startsWith('/spreadsheets') ? 'active-link' : ''"
        >
          <FileSpreadsheet class="w-4 h-4" />
          <span>Таблички Excel</span>
        </NuxtLink>

        <p class="px-3 text-[11px] font-semibold text-zinc-400 dark:text-zinc-500 uppercase tracking-wider mb-1 mt-5">Система</p>

        <NuxtLink
          v-if="can('sidebarSettings')"
          to="/settings"
          class="nav-link"
          :class="route.path.startsWith('/settings') ? 'active-link' : ''"
        >
          <Settings class="w-4 h-4" />
          <span>Настройки</span>
        </NuxtLink>
      </nav>

      <!-- Sidebar bottom profile removed: keep top-right only -->
    </aside>

    <!-- Main Content -->
    <div class="flex-1 min-w-0 flex flex-col">
      <div
        v-if="showOfflineBanner"
        class="shrink-0 px-3 sm:px-6 py-1.5 text-center text-[11px] sm:text-xs font-medium tracking-wide text-amber-900 dark:text-amber-100 bg-amber-100/95 dark:bg-amber-950/80 border-b border-amber-200/80 dark:border-amber-800/60"
        role="status"
      >
        Нет связи — обновления могут задержаться
      </div>
      <!-- Top Bar -->
      <header class="h-14 bg-white dark:bg-[#1a1a1d] border-b border-zinc-200/80 dark:border-zinc-800 px-3 sm:px-6 flex items-center justify-between shrink-0 sticky top-0 z-30">
        <div class="flex items-center gap-2 sm:gap-3 min-w-0">
          <button 
            class="lg:hidden p-2.5 -ml-1 text-gray-500 dark:text-gray-400 hover:bg-gray-100 active:bg-gray-200 rounded-lg transition-colors shrink-0"
            @click="sidebarOpen = !sidebarOpen"
          >
            <Menu :size="22" />
          </button>

          <button
            v-if="pageHeader.showBack.value"
            @click="router.back()"
            class="p-1.5 -ml-1 text-gray-400 dark:text-gray-500 hover:text-gray-900 dark:hover:text-gray-100 hover:bg-gray-100 rounded-lg transition-colors shrink-0"
          >
            <ArrowLeft :size="18" />
          </button>
          <h2 class="text-[15px] sm:text-[17px] font-bold text-zinc-900 dark:text-gray-100 truncate">
            {{ pageHeader.title.value || (route.path === '/' ? 'Все заявки' : (route.path.startsWith('/my') ? 'Мои заявки' : route.path.startsWith('/messenger') ? 'Мессенджер' : route.path.startsWith('/employees') ? 'Сотрудники' : route.path.startsWith('/reports') ? 'Отчёты' : route.path.startsWith('/settings') ? 'Настройки' : route.path.startsWith('/companies') ? 'Юрлица' : route.path.startsWith('/objects') ? 'Объекты' : route.path.startsWith('/equipment') ? 'Оборудование' : route.path.startsWith('/spreadsheets') ? 'Таблички' : route.path.startsWith('/schedule') ? 'График работы' : route.path.startsWith('/profile') ? 'Профиль' : /^\/tickets\/\d+$/.test(route.path) ? 'Заявка' : '')) }}
          </h2>
        </div>

        <!-- Right Side Actions -->
        <div class="flex items-center gap-1.5 sm:gap-3 shrink-0">
          <button
            type="button"
            @click="toggleNotifications"
            :title="bellTitle"
            :class="[
              'relative p-2.5 rounded-lg transition-colors',
              !notifSecureContext || notifPermission === 'denied'
                ? 'text-amber-600 dark:text-amber-400 hover:bg-amber-50 dark:hover:bg-amber-900/25'
                : notifEnabled
                  ? 'text-indigo-600 dark:text-indigo-400 hover:bg-indigo-50 dark:hover:bg-indigo-900/30'
                  : 'text-gray-400 dark:text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-700'
            ]"
          >
            <Bell :size="20" />
            <span
              :class="[
                'absolute top-2 right-2 w-2 h-2 rounded-full border-2 border-white dark:border-[#1a1a1d] transition-colors',
                !notifSecureContext || notifPermission === 'denied'
                  ? 'bg-amber-500'
                  : notifEnabled
                    ? 'bg-green-500'
                    : 'bg-gray-300 dark:bg-gray-600'
              ]"
            />
          </button>
          
          <!-- Profile Dropdown -->
          <div class="relative">
            <button
              class="flex items-center gap-1.5 sm:gap-2 px-2 sm:px-3 py-1.5 rounded-lg hover:bg-gray-100 active:bg-gray-200 transition-colors border border-transparent hover:border-gray-200 max-w-[40vw] sm:max-w-none"
              @click="profileOpen = !profileOpen"
            >
              <span class="text-[12px] sm:text-[13px] font-semibold text-zinc-800 dark:text-gray-200 truncate">
                {{ auth.fullName || 'Пользователь' }}
              </span>
              <span class="text-zinc-400 dark:text-zinc-500 text-xs">▼</span>
            </button>

            <Transition name="dropdown">
              <div v-if="profileOpen" class="absolute right-0 top-full mt-2 w-60 bg-white dark:bg-[#1e1e21] rounded-xl shadow-lg border border-gray-100 dark:border-zinc-700 z-50 overflow-hidden">
                <div class="px-4 py-3 border-b border-gray-50 dark:border-zinc-700 bg-gray-50/50 dark:bg-zinc-800/50">
                  <div class="flex items-center gap-3">
                    <div class="w-9 h-9 rounded-full bg-indigo-100 overflow-hidden flex items-center justify-center border border-indigo-200">
                      <img v-if="showAvatar" :src="resolvedAvatarUrl" class="w-full h-full object-cover" alt="" @error="avatarBroken = true" />
                      <span v-else class="text-indigo-700 font-bold text-xs">{{ auth.fullName?.charAt(0)?.toUpperCase() || '?' }}</span>
                    </div>
                    <div class="min-w-0">
                      <div class="font-semibold text-gray-900 dark:text-gray-100 text-sm truncate">{{ auth.fullName }}</div>
                      <div v-if="profileLogin" class="text-[11px] text-gray-500 dark:text-gray-400 truncate">Логин: <span class="font-mono">{{ profileLogin }}</span></div>
                      <div class="text-[11px] text-gray-500 dark:text-gray-400 truncate">Роль: <span class="font-semibold">{{ auth.roleLabel }}</span></div>
                      <div class="text-xs text-gray-500 dark:text-gray-400 truncate">{{ auth.email }}</div>
                    </div>
                  </div>
                </div>
                <div class="p-2 space-y-1">
                  <div class="flex items-center justify-between px-3 py-2">
                    <div class="flex items-center gap-2 text-sm font-medium text-gray-700 dark:text-gray-300">
                      <component :is="theme.isDark.value ? Moon : Sun" :size="16" />
                      {{ theme.isDark.value ? 'Тёмная тема' : 'Светлая тема' }}
                    </div>
                    <button
                      @click="theme.toggle()"
                      class="relative inline-flex h-5 w-9 items-center rounded-full transition-colors"
                      :class="theme.isDark.value ? 'bg-indigo-600' : 'bg-gray-200'"
                    >
                      <span
                        class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform shadow-sm"
                        :class="theme.isDark.value ? 'translate-x-[18px]' : 'translate-x-[2px]'"
                      />
                    </button>
                  </div>
                  <button @click="goProfile" class="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-zinc-700 transition-colors">
                    <User2 :size="16" /> Профиль
                  </button>
                  <button @click="handleLogout" class="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20 transition-colors">
                    <LogOut :size="16" /> Выйти
                  </button>
                </div>
              </div>
            </Transition>
          </div>
        </div>
      </header>

      <!-- Content -->
      <main class="flex-1 overflow-y-auto p-3 sm:p-6 lg:p-8 pb-20 lg:pb-8 dark:bg-[#0f0f10]">
        <slot />
      </main>
    </div>

    <!-- Mobile Bottom Navigation -->
    <nav class="fixed bottom-0 inset-x-0 z-50 bg-white dark:bg-[#1a1a1d] border-t border-gray-200 dark:border-zinc-800 lg:hidden safe-bottom">
      <div class="flex items-center justify-around h-14 max-w-lg mx-auto">
        <NuxtLink
          v-for="item in mobileNavItems"
          :key="item.to"
          :to="item.to"
          :class="[
            'flex flex-col items-center justify-center gap-0.5 w-full h-full text-[10px] font-medium transition-colors relative',
            (item as any).isCreate
              ? 'text-white'
              : route.path === item.to || (item.to !== '/' && route.path.startsWith(item.to))
                ? 'text-indigo-600'
                : 'text-gray-400 active:text-gray-600'
          ]"
        >
          <div
            v-if="(item as any).isCreate"
            class="absolute -top-4 w-12 h-12 bg-indigo-600 rounded-full flex items-center justify-center shadow-lg shadow-indigo-200"
          >
            <component :is="item.icon" :size="22" class="text-white" />
          </div>
          <template v-else>
            <div class="relative">
              <component :is="item.icon" :size="20" />
              <span
                v-if="item.to === '/messenger' && messengerUnreadTotal > 0"
                class="absolute -top-1 -right-1.5 min-w-[14px] h-3.5 px-0.5 rounded-full bg-blue-500 text-white text-[8px] font-bold flex items-center justify-center"
              >
                {{ messengerUnreadTotal > 99 ? '99+' : messengerUnreadTotal }}
              </span>
            </div>
            <span>{{ item.label }}</span>
          </template>
          <span v-if="(item as any).isCreate" class="mt-5 text-gray-500">{{ item.label }}</span>
        </NuxtLink>
      </div>
    </nav>

    <!-- Toast Notifications -->
    <Teleport to="body">
      <div class="fixed top-4 right-4 z-[100] flex flex-col gap-2 pointer-events-none max-w-sm w-full">
        <TransitionGroup
          enter-active-class="transition-all duration-300 ease-out"
          leave-active-class="transition-all duration-200 ease-in"
          enter-from-class="opacity-0 translate-x-8"
          leave-to-class="opacity-0 translate-x-8"
          move-class="transition-all duration-200"
        >
          <div
            v-for="t in toast.toasts.value"
            :key="t.id"
            role="button"
            tabindex="0"
            :class="[
              'pointer-events-auto flex items-start gap-2.5 px-4 py-3 rounded-xl shadow-lg border text-sm cursor-pointer select-none',
              t.navigateTo ? 'ring-1 ring-blue-400/30' : '',
              t.type === 'success' ? 'bg-emerald-50 border-emerald-200 text-emerald-800' : '',
              t.type === 'error'   ? 'bg-red-50 border-red-200 text-red-800' : '',
              t.type === 'warning' ? 'bg-amber-50 border-amber-200 text-amber-800' : '',
              t.type === 'info'    ? 'bg-blue-50 border-blue-200 text-blue-800' : '',
            ]"
            @click="onToastClick(t)"
            @keydown.enter.prevent="onToastClick(t)"
            @keydown.space.prevent="onToastClick(t)"
          >
            <component
              :is="toastIcon[t.type]"
              :size="18"
              class="shrink-0 mt-0.5"
              :class="{
                'text-emerald-500': t.type === 'success',
                'text-red-500': t.type === 'error',
                'text-amber-500': t.type === 'warning',
                'text-blue-500': t.type === 'info',
              }"
            />
            <div class="min-w-0 flex-1 leading-snug">
              <div v-if="t.headline" class="font-semibold text-[13px] mb-0.5">{{ t.headline }}</div>
              <div class="text-[13px] font-medium opacity-95 break-words">{{ t.message }}</div>
              <div v-if="t.navigateTo" class="text-[11px] mt-1.5 opacity-70">
                Нажмите, чтобы открыть
              </div>
            </div>
          </div>
        </TransitionGroup>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

.dropdown-enter-active, .dropdown-leave-active { transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1); }
.dropdown-enter-from, .dropdown-leave-to { opacity: 0; transform: translateY(-10px) scale(0.95); }

.nav-arrow {
  transition: transform 0.2s ease;
}
.nav-arrow.open {
  transform: rotate(180deg);
}

.safe-bottom {
  padding-bottom: env(safe-area-inset-bottom, 0px);
}
</style>
