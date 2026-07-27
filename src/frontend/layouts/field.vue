<script setup lang="ts">
import {
  LayoutDashboard,
  Ticket,
  User2,
  ArrowLeft,
  Moon,
  Sun,
  CheckCircle,
  AlertCircle,
  AlertTriangle,
  Info,
  LogOut,
} from 'lucide-vue-next'
import type { ToastItem } from '~/composables/useToast'

const auth = useAuthStore()
if (import.meta.client) {
  auth.hydrate()
}

const route = useRoute()
const router = useRouter()
const toast = useToast()
const pageHeader = usePageHeader()
const theme = useTheme()
const { branding, load: loadBranding } = useSystemBranding()
const brandLogoSrc = computed(() => branding.value.logoUrl || '/favicon.svg')

const toastIcon: Record<string, any> = {
  success: CheckCircle,
  error: AlertCircle,
  warning: AlertTriangle,
  info: Info,
}

const navItems = [
  { to: '/field', label: 'Мои', icon: LayoutDashboard, match: (p: string) => p === '/field' || p === '/field/' },
  {
    to: '/field',
    label: 'Заявки',
    icon: Ticket,
    match: (p: string) => p.startsWith('/field/tickets') || p.startsWith('/field/report'),
  },
  {
    to: '/field/profile',
    label: 'Профиль',
    icon: User2,
    match: (p: string) => p.startsWith('/field/profile'),
  },
]

const headerTitle = computed(() => pageHeader.title.value || 'Выезд')

function onToastClick(t: ToastItem) {
  if (t.navigateTo) {
    void router.push(t.navigateTo)
  }
  toast.remove(t.id)
}

function handleLogout() {
  auth.logout()
  navigateTo('/auth/login')
}

onMounted(() => {
  theme.init()
  auth.hydrate()
  void loadBranding()
})

onBeforeUnmount(() => {
  pageHeader.clear()
})
</script>

<template>
  <div class="min-h-[100dvh] flex flex-col bg-[#F8F9FA] dark:bg-[#0f0f10] text-[14px] dark:text-gray-200" :style="branding.accentColor ? { '--brand-accent': branding.accentColor } : undefined">
    <!-- Header -->
    <header
      class="h-14 bg-white dark:bg-[#1a1a1d] border-b border-gray-200/80 dark:border-zinc-800 px-3 flex items-center justify-between shrink-0 sticky top-0 z-30"
    >
      <div class="flex items-center gap-2 min-w-0">
        <button
          v-if="pageHeader.showBack.value"
          type="button"
          class="p-2.5 -ml-1 text-gray-500 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-zinc-800 rounded-lg transition-colors shrink-0"
          @click="router.back()"
        >
          <ArrowLeft :size="20" />
        </button>
        <h1 class="text-[16px] font-bold text-zinc-900 dark:text-gray-100 truncate">
          {{ headerTitle }}
        </h1>
      </div>

      <div class="flex items-center gap-1 shrink-0">
        <button
          type="button"
          class="p-2.5 rounded-lg text-gray-500 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-zinc-800 transition-colors"
          :title="theme.isDark.value ? 'Светлая тема' : 'Тёмная тема'"
          @click="theme.toggle()"
        >
          <component :is="theme.isDark.value ? Sun : Moon" :size="18" />
        </button>
        <button
          type="button"
          class="p-2.5 rounded-lg text-red-500 hover:bg-red-50 dark:hover:bg-red-900/20 transition-colors"
          title="Выйти"
          @click="handleLogout"
        >
          <LogOut :size="18" />
        </button>
      </div>
    </header>

    <!-- Content -->
    <main class="flex-1 overflow-y-auto px-3 py-3 pb-[calc(4.5rem+env(safe-area-inset-bottom,0px))]">
      <slot />
    </main>

    <!-- Bottom nav -->
    <nav
      class="fixed bottom-0 inset-x-0 z-50 bg-white dark:bg-[#1a1a1d] border-t border-gray-200 dark:border-zinc-800 safe-bottom"
    >
      <div class="flex items-center justify-around h-14 max-w-lg mx-auto">
        <NuxtLink
          v-for="item in navItems"
          :key="item.label"
          :to="item.to"
          :class="[
            'flex flex-col items-center justify-center gap-0.5 w-full h-full text-[11px] font-medium transition-colors min-h-[44px]',
            item.match(route.path)
              ? 'text-indigo-600 dark:text-indigo-400'
              : 'text-gray-400 dark:text-gray-500 active:text-gray-600',
          ]"
        >
          <component :is="item.icon" :size="22" />
          <span>{{ item.label }}</span>
        </NuxtLink>
      </div>
    </nav>

    <!-- Toast host -->
    <Teleport to="body">
      <div class="fixed top-4 right-3 left-3 z-[100] flex flex-col gap-2 pointer-events-none max-w-sm mx-auto sm:ml-auto sm:mr-4 sm:left-auto">
        <TransitionGroup
          enter-active-class="transition-all duration-300 ease-out"
          leave-active-class="transition-all duration-200 ease-in"
          enter-from-class="opacity-0 translate-y-[-8px]"
          leave-to-class="opacity-0 translate-y-[-8px]"
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
              t.type === 'error' ? 'bg-red-50 border-red-200 text-red-800' : '',
              t.type === 'warning' ? 'bg-amber-50 border-amber-200 text-amber-800' : '',
              t.type === 'info' ? 'bg-blue-50 border-blue-200 text-blue-800' : '',
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
            </div>
          </div>
        </TransitionGroup>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.safe-bottom {
  padding-bottom: env(safe-area-inset-bottom, 0px);
}
</style>
