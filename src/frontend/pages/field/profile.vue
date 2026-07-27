<script setup lang="ts">
import { LogOut, KeyRound, Moon, Sun, RefreshCw } from 'lucide-vue-next'

definePageMeta({
  layout: 'field',
  middleware: 'field',
})

const api = useApi()
const auth = useAuthStore()
const pageHeader = usePageHeader()
const toast = useToast()
const theme = useTheme()

const showPasswordForm = ref(false)
const oldPassword = ref('')
const newPassword = ref('')
const saving = ref(false)

async function changePassword() {
  if (!oldPassword.value.trim() || !newPassword.value.trim()) return
  saving.value = true
  try {
    await api.employees.changePassword(auth.userId, oldPassword.value, newPassword.value)
    oldPassword.value = ''
    newPassword.value = ''
    showPasswordForm.value = false
    toast.success('Пароль изменён')
  } catch (e: any) {
    toast.error(e?.data?.error || e?.message || 'Не удалось сменить пароль')
  } finally {
    saving.value = false
  }
}

function handleLogout() {
  auth.logout()
  navigateTo('/auth/login')
}

onMounted(() => {
  pageHeader.set('Профиль', false)
  theme.init()
})

onBeforeUnmount(() => {
  pageHeader.clear()
})
</script>

<template>
  <div class="max-w-lg mx-auto space-y-4">
    <div class="brutal-card p-5 space-y-4">
      <div class="flex items-center gap-4">
        <div
          class="w-14 h-14 rounded-full bg-indigo-100 dark:bg-indigo-900/40 flex items-center justify-center border border-indigo-200 dark:border-indigo-800 shrink-0"
        >
          <span class="text-indigo-700 dark:text-indigo-300 font-bold text-xl">
            {{ auth.fullName?.charAt(0)?.toUpperCase() || '?' }}
          </span>
        </div>
        <div class="min-w-0">
          <div class="font-bold text-lg text-gray-900 dark:text-gray-100 truncate">
            {{ auth.fullName || 'Пользователь' }}
          </div>
          <div class="mt-1">
            <span :class="auth.roleColor">{{ auth.roleLabel }}</span>
          </div>
          <div v-if="auth.email" class="text-sm text-gray-500 mt-1 truncate">
            {{ auth.email }}
          </div>
        </div>
      </div>
    </div>

    <!-- Theme -->
    <button
      type="button"
      class="brutal-card w-full p-4 flex items-center justify-between min-h-[56px] active:bg-gray-50 dark:active:bg-zinc-800"
      @click="theme.toggle()"
    >
      <div class="flex items-center gap-3">
        <component :is="theme.isDark.value ? Moon : Sun" :size="20" class="text-gray-500" />
        <span class="font-medium text-gray-800 dark:text-gray-200">
          {{ theme.isDark.value ? 'Тёмная тема' : 'Светлая тема' }}
        </span>
      </div>
      <span
        class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
        :class="theme.isDark.value ? 'bg-indigo-600' : 'bg-gray-200'"
      >
        <span
          class="inline-block h-5 w-5 transform rounded-full bg-white shadow transition-transform"
          :class="theme.isDark.value ? 'translate-x-[22px]' : 'translate-x-[2px]'"
        />
      </span>
    </button>

    <!-- Password -->
    <div class="brutal-card overflow-hidden">
      <button
        type="button"
        class="w-full p-4 flex items-center justify-between min-h-[56px] text-left"
        @click="showPasswordForm = !showPasswordForm"
      >
        <div class="flex items-center gap-3">
          <KeyRound :size="20" class="text-gray-500" />
          <span class="font-medium text-gray-800 dark:text-gray-200">Сменить пароль</span>
        </div>
        <span class="text-gray-400 text-sm">{{ showPasswordForm ? '▲' : '▼' }}</span>
      </button>

      <div v-if="showPasswordForm" class="px-4 pb-4 space-y-3 border-t border-gray-100 dark:border-zinc-700 pt-3">
        <input
          v-model="oldPassword"
          type="password"
          placeholder="Текущий пароль"
          class="brutal-input min-h-[48px]"
          autocomplete="current-password"
        />
        <input
          v-model="newPassword"
          type="password"
          placeholder="Новый пароль"
          class="brutal-input min-h-[48px]"
          autocomplete="new-password"
        />
        <button
          type="button"
          class="brutal-btn-primary w-full min-h-[48px]"
          :disabled="saving || !oldPassword.trim() || !newPassword.trim()"
          @click="changePassword"
        >
          <RefreshCw v-if="saving" :size="16" class="animate-spin" />
          {{ saving ? 'Сохранение…' : 'Сохранить пароль' }}
        </button>
      </div>
    </div>

    <button
      type="button"
      class="brutal-btn-danger w-full min-h-[52px] text-[15px]"
      @click="handleLogout"
    >
      <LogOut :size="18" />
      Выйти
    </button>
  </div>
</template>
