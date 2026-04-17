<script setup lang="ts">
import { RefreshCw, Save, KeyRound, User2, AtSign, Upload, ShieldAlert } from 'lucide-vue-next'
import { resolvePublicApiBaseUrl } from '~/utils/resolvePublicApiBaseUrl'

const api = useApi()
const auth = useAuthStore()

const apiBase = computed(() => {
  const cfg = useRuntimeConfig()
  return resolvePublicApiBaseUrl(cfg.public.apiBaseUrl as string | undefined)
})

type EmployeeProfile = {
  userId: string
  fullName: string
  role: string
  department: string
  login: string
  email: string
  avatarUrl: string
}

const loading = ref(true)
const saving = ref(false)
const errorMsg = ref('')
const successMsg = ref('')

const profile = ref<EmployeeProfile | null>(null)

function staffRoleLabel(role: string): string {
  const map: Record<string, string> = {
    super_admin: 'Супер-админ',
    support_l1: 'Поддержка L1',
    support_l2: 'Поддержка L2',
    developer: 'Разработчик',
    field_engineer: 'Выездной инженер',
    sysadmin: 'Сисадмин',
    coordinator: 'Координатор',
    director: 'Директор',
    accountant: 'Бухгалтерия',
    head_engineers: 'Нач. инженеров',
    head_support: 'Нач. поддержки',
    head_dev: 'Нач. разработки',
    procurement: 'Закупки',
    head_repair: 'Нач. ремонта',
    agent: 'Агент',
  }
  const k = String(role || '').trim()
  return map[k] || k || '—'
}

const fullName = ref('')
const department = ref('')
const login = ref('')

const oldPassword = ref('')
const newPassword = ref('')

const avatarUploading = ref(false)
const avatarInputRef = ref<HTMLInputElement | null>(null)

async function loadProfile() {
  loading.value = true
  errorMsg.value = ''
  successMsg.value = ''
  try {
    const data = await api.employees.getById(auth.userId)
    profile.value = {
      userId: data.userId,
      fullName: data.fullName || '',
      role: data.role || '',
      department: data.department || '',
      login: data.login || '',
      email: data.email || '',
      avatarUrl: data.avatarUrl || '',
    }
    fullName.value = profile.value.fullName
    department.value = profile.value.department
    login.value = profile.value.login
  } catch (e: any) {
    errorMsg.value = 'Не удалось загрузить профиль'
  } finally {
    loading.value = false
  }
}

async function saveProfile() {
  if (!profile.value) return
  saving.value = true
  errorMsg.value = ''
  successMsg.value = ''
  try {
    await api.employees.updateProfile(profile.value.userId, {
      fullName: fullName.value.trim(),
      department: department.value.trim(),
    })
    // refresh
    await loadProfile()
    auth.fullName = fullName.value.trim()
    successMsg.value = 'Профиль сохранён'
  } catch (e: any) {
    errorMsg.value = e?.data?.error || e?.message || 'Ошибка сохранения'
  } finally {
    saving.value = false
  }
}

async function saveLogin() {
  if (!profile.value) return
  saving.value = true
  errorMsg.value = ''
  successMsg.value = ''
  try {
    await api.employees.changeLogin(profile.value.userId, login.value.trim())
    await loadProfile()
    successMsg.value = 'Логин обновлён'
  } catch (e: any) {
    errorMsg.value = e?.data?.error || e?.message || 'Ошибка смены логина'
  } finally {
    saving.value = false
  }
}

async function savePassword() {
  if (!profile.value) return
  if (!oldPassword.value.trim() || !newPassword.value.trim()) return
  saving.value = true
  errorMsg.value = ''
  successMsg.value = ''
  try {
    await api.employees.changePassword(profile.value.userId, oldPassword.value, newPassword.value)
    oldPassword.value = ''
    newPassword.value = ''
    successMsg.value = 'Пароль изменён'
  } catch (e: any) {
    errorMsg.value = e?.data?.error || e?.message || 'Ошибка смены пароля'
  } finally {
    saving.value = false
  }
}

async function onAvatarPicked(e: Event) {
  const input = e.target as HTMLInputElement
  if (!input.files || input.files.length === 0 || !profile.value) return
  avatarUploading.value = true
  errorMsg.value = ''
  successMsg.value = ''
  try {
    const fd = new FormData()
    fd.append('file', input.files[0])
    const res = await api.employees.changeAvatar(profile.value.userId, fd)
    const newUrl = String(res?.avatarUrl || '').trim()
    if (newUrl) {
      auth.avatarUrl = newUrl
    }
    await loadProfile()
    successMsg.value = 'Аватар обновлён'
  } catch (e: any) {
    errorMsg.value = e?.data?.error || e?.message || 'Ошибка загрузки аватара'
  } finally {
    avatarUploading.value = false
    if (avatarInputRef.value) avatarInputRef.value.value = ''
  }
}

const profileAvatarBroken = ref(false)
const avatarSrc = computed(() => {
  const p = profile.value
  const raw = (p?.avatarUrl || auth.avatarUrl || '').trim()
  if (!raw) return ''
  if (/^https?:\/\//i.test(raw)) return raw
  if (raw.startsWith('/')) return `${apiBase.value}${raw}`
  return raw
})
const showProfileAvatar = computed(() => avatarSrc.value && !profileAvatarBroken.value)
watch(avatarSrc, () => { profileAvatarBroken.value = false })

onMounted(() => {
  auth.hydrate()
  void loadProfile()
})
</script>

<template>
  <div class="max-w-4xl mx-auto w-full space-y-6">
    <div class="flex items-center justify-between gap-3">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-gray-900">Профиль</h1>
        <p class="text-sm text-gray-500 mt-1">Настройки аккаунта</p>
      </div>
      <button
        type="button"
        class="inline-flex items-center gap-2 px-3 py-2 text-sm font-semibold border border-gray-200 rounded-lg bg-white hover:bg-gray-50"
        :disabled="loading"
        @click="loadProfile"
        title="Обновить"
      >
        <RefreshCw :size="16" :class="{ 'animate-spin': loading }" />
        Обновить
      </button>
    </div>

    <div v-if="errorMsg" class="bg-red-50 border border-red-200 text-red-800 rounded-lg px-4 py-3 text-sm flex items-center gap-2">
      <ShieldAlert :size="16" />
      <span>{{ errorMsg }}</span>
    </div>
    <div v-if="successMsg" class="bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-lg px-4 py-3 text-sm">
      {{ successMsg }}
    </div>

    <div v-if="loading && !profile" class="flex items-center justify-center py-24">
      <RefreshCw :size="28" class="animate-spin text-indigo-600" />
    </div>

    <div v-else-if="profile" class="grid grid-cols-1 lg:grid-cols-3 gap-6">
      <!-- Avatar card -->
      <div class="bg-white border border-gray-200 rounded-xl shadow-sm p-5">
        <div class="flex items-center gap-3 mb-4">
          <div class="w-14 h-14 rounded-full bg-gray-100 border border-gray-200 overflow-hidden flex items-center justify-center">
            <img v-if="showProfileAvatar" :src="avatarSrc" class="w-full h-full object-cover" alt="" @error="profileAvatarBroken = true" />
            <span v-else class="text-gray-500 font-bold text-lg">{{ (profile.fullName || '?').charAt(0).toUpperCase() }}</span>
          </div>
          <div class="min-w-0">
            <div class="font-semibold text-gray-900 truncate">{{ profile.fullName }}</div>
            <div class="text-xs text-gray-500 truncate">{{ staffRoleLabel(profile.role) }}</div>
          </div>
        </div>

        <input ref="avatarInputRef" type="file" accept="image/*" class="hidden" @change="onAvatarPicked" />
        <button
          type="button"
          class="w-full inline-flex items-center justify-center gap-2 px-3 py-2 text-sm font-semibold bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 disabled:opacity-50"
          :disabled="avatarUploading"
          @click="avatarInputRef?.click()"
        >
          <Upload :size="16" />
          {{ avatarUploading ? 'Загрузка...' : 'Загрузить аватар' }}
        </button>
      </div>

      <!-- Profile fields -->
      <div class="lg:col-span-2 space-y-6">
        <div class="bg-white border border-gray-200 rounded-xl shadow-sm p-5 space-y-4">
          <div class="flex items-center gap-2 text-sm font-semibold text-gray-900">
            <User2 :size="16" class="text-gray-400" />
            Данные профиля
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label class="block text-xs font-semibold text-gray-500 mb-1">ФИО</label>
              <input v-model="fullName" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-gray-500 mb-1">Подразделение</label>
              <input v-model="department" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
            </div>
          </div>

          <div class="flex items-center justify-end">
            <button
              type="button"
              class="inline-flex items-center gap-2 px-4 py-2 text-sm font-semibold bg-gray-900 text-white rounded-lg hover:bg-gray-800 disabled:opacity-50"
              :disabled="saving"
              @click="saveProfile"
            >
              <Save :size="16" />
              Сохранить
            </button>
          </div>
        </div>

        <div class="bg-white border border-gray-200 rounded-xl shadow-sm p-5 space-y-4">
          <div class="flex items-center gap-2 text-sm font-semibold text-gray-900">
            <AtSign :size="16" class="text-gray-400" />
            Логин
          </div>
          <div class="flex flex-col sm:flex-row gap-3">
            <input v-model="login" class="flex-1 px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
            <button
              type="button"
              class="inline-flex items-center justify-center gap-2 px-4 py-2 text-sm font-semibold bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 disabled:opacity-50"
              :disabled="saving || !login.trim()"
              @click="saveLogin"
            >
              <Save :size="16" />
              Обновить
            </button>
          </div>
          <div class="text-xs text-gray-500">Email: <span class="font-mono">{{ profile.email }}</span></div>
        </div>

        <div class="bg-white border border-gray-200 rounded-xl shadow-sm p-5 space-y-4">
          <div class="flex items-center gap-2 text-sm font-semibold text-gray-900">
            <KeyRound :size="16" class="text-gray-400" />
            Смена пароля
          </div>
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label class="block text-xs font-semibold text-gray-500 mb-1">Старый пароль</label>
              <input v-model="oldPassword" type="password" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-gray-500 mb-1">Новый пароль</label>
              <input v-model="newPassword" type="password" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
            </div>
          </div>
          <div class="flex items-center justify-end">
            <button
              type="button"
              class="inline-flex items-center justify-center gap-2 px-4 py-2 text-sm font-semibold bg-gray-900 text-white rounded-lg hover:bg-gray-800 disabled:opacity-50"
              :disabled="saving || !oldPassword.trim() || !newPassword.trim()"
              @click="savePassword"
            >
              <Save :size="16" />
              Изменить пароль
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

