<script setup lang="ts">
import { Building2, Users, MessageSquare, Check, ArrowRight, SkipForward, Plus } from 'lucide-vue-next'

definePageMeta({
  layout: 'auth',
  middleware: [
    function () {
      const auth = useAuthStore()
      if (!auth.isSuperAdmin) return navigateTo('/')
    },
  ],
})

const api = useApi()
const toast = useToast()
const router = useRouter()
const { branding, load: loadBranding } = useSystemBranding()

const step = ref(0)
const saving = ref(false)
const companyName = ref('')

const roleOptions = [
  { value: 'support_l1', label: 'Поддержка L1' },
  { value: 'support_l2', label: 'Поддержка L2' },
  { value: 'field_engineer', label: 'Выездной инженер' },
  { value: 'developer', label: 'Разработчик' },
  { value: 'coordinator', label: 'Координатор' },
]

const inviteForm = reactive({
  fullName: '',
  login: '',
  password: '',
  role: 'support_l1',
})
const creatingEmployee = ref(false)
const createdEmployees = ref<{ fullName: string; login: string; role: string }[]>([])

const steps = [
  { title: 'Название компании', hint: 'Отображается в интерфейсе и отчётах' },
  { title: 'Пригласите команду', hint: 'Создайте логин и пароль прямо здесь' },
  { title: 'Уведомления', hint: 'Telegram и email настраиваются позже в «Настройки»' },
]

const roleLabel = (slug: string) => roleOptions.find((r) => r.value === slug)?.label || slug

onMounted(async () => {
  await loadBranding(true)
  companyName.value = branding.value.companyName || ''
})

async function saveCompany() {
  const name = companyName.value.trim()
  if (!name) {
    toast.warning('Введите название компании')
    return
  }
  saving.value = true
  try {
    await api.systemSettings.saveSettings({ company_name: name })
    branding.value.companyName = name
    toast.success('Название сохранено')
    step.value = 1
  } catch (e: any) {
    toast.error(e?.message || 'Не удалось сохранить')
  } finally {
    saving.value = false
  }
}

async function createEmployeeNow() {
  const fullName = inviteForm.fullName.trim()
  const login = inviteForm.login.trim()
  const password = inviteForm.password.trim()
  const role = inviteForm.role.trim()

  if (!fullName) {
    toast.warning('Укажите ФИО')
    return
  }
  if (!login) {
    toast.warning('Укажите логин')
    return
  }
  if (password.length < 6) {
    toast.warning('Пароль не короче 6 символов')
    return
  }

  creatingEmployee.value = true
  try {
    await api.employees.createAccount({
      fullName,
      role,
      department: '',
      login,
      password,
    })
    createdEmployees.value.push({ fullName, login, role })
    inviteForm.fullName = ''
    inviteForm.login = ''
    inviteForm.password = ''
    toast.success(`Создан: ${fullName}. Может войти логином «${login}»`)
  } catch (e: any) {
    toast.error(e?.data || e?.message || 'Не удалось создать сотрудника')
  } finally {
    creatingEmployee.value = false
  }
}

async function finish(skip = false) {
  saving.value = true
  try {
    const values: Record<string, string> = { onboarding_completed: 'true' }
    if (!skip && companyName.value.trim() && !branding.value.companyName) {
      values.company_name = companyName.value.trim()
    }
    await api.systemSettings.saveSettings(values)
    branding.value.onboardingCompleted = true
    if (import.meta.client) {
      localStorage.setItem('onboarding_skip', '1')
    }
    await router.push('/')
  } catch (e: any) {
    toast.error(e?.message || 'Не удалось завершить')
  } finally {
    saving.value = false
  }
}

function nextFromInvite() {
  step.value = 2
}
</script>

<template>
  <div class="min-h-[100dvh] w-full bg-[#F8F9FA] dark:bg-[#0f0f10]">
    <div class="max-w-xl mx-auto py-8 sm:py-12 px-4">
      <div class="mb-8">
        <p class="text-xs font-bold uppercase tracking-widest text-indigo-600 mb-2">Первый запуск</p>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-gray-100">Настройка Ticket System</h1>
        <p class="text-sm text-gray-500 mt-1">Несколько шагов, чтобы система была готова к работе</p>
      </div>

      <div class="flex items-center gap-2 mb-8">
        <div
          v-for="(s, i) in steps"
          :key="i"
          class="flex-1 h-1.5 rounded-full transition-colors"
          :class="i <= step ? 'bg-[var(--brand-accent,#4f46e5)]' : 'bg-gray-200 dark:bg-zinc-700'"
        />
      </div>

      <div class="bg-white dark:bg-zinc-900 rounded-xl border border-gray-200 dark:border-zinc-700 shadow-sm p-6 sm:p-8">
        <!-- Step 0: company name -->
        <div v-if="step === 0" class="space-y-5">
          <div class="flex items-center gap-3">
            <div class="w-10 h-10 rounded-lg bg-indigo-50 dark:bg-indigo-900/30 flex items-center justify-center text-indigo-600">
              <Building2 :size="20" />
            </div>
            <div>
              <h2 class="font-semibold text-gray-900 dark:text-gray-100">{{ steps[0].title }}</h2>
              <p class="text-xs text-gray-500">{{ steps[0].hint }}</p>
            </div>
          </div>
          <input
            v-model="companyName"
            type="text"
            class="w-full border border-gray-300 dark:border-zinc-600 dark:bg-zinc-800 rounded-lg px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500/30 focus:border-indigo-500"
            placeholder="Например: IT Cafe"
            @keydown.enter="saveCompany"
          />
          <div class="flex items-center justify-between gap-3 pt-2">
            <button
              type="button"
              class="text-sm text-gray-500 hover:text-gray-700 inline-flex items-center gap-1.5"
              :disabled="saving"
              @click="finish(true)"
            >
              <SkipForward :size="14" />
              Пропустить
            </button>
            <button
              type="button"
              class="inline-flex items-center gap-2 px-4 py-2.5 rounded-lg bg-indigo-600 text-white text-sm font-semibold hover:bg-indigo-700 disabled:opacity-50"
              :disabled="saving"
              @click="saveCompany"
            >
              Далее
              <ArrowRight :size="16" />
            </button>
          </div>
        </div>

        <!-- Step 1: create employees here -->
        <div v-else-if="step === 1" class="space-y-5">
          <div class="flex items-center gap-3">
            <div class="w-10 h-10 rounded-lg bg-indigo-50 dark:bg-indigo-900/30 flex items-center justify-center text-indigo-600">
              <Users :size="20" />
            </div>
            <div>
              <h2 class="font-semibold text-gray-900 dark:text-gray-100">{{ steps[1].title }}</h2>
              <p class="text-xs text-gray-500">{{ steps[1].hint }}</p>
            </div>
          </div>

          <p class="text-sm text-gray-600 dark:text-gray-300">
            Заполните поля и нажмите <strong>«Добавить»</strong>. Сотрудник сразу сможет войти на этот же сайт своим логином и паролем.
          </p>

          <div class="space-y-3">
            <div>
              <label class="block text-xs font-medium text-gray-600 dark:text-gray-400 mb-1">ФИО</label>
              <input
                v-model="inviteForm.fullName"
                type="text"
                class="w-full border border-gray-300 dark:border-zinc-600 dark:bg-zinc-800 rounded-lg px-3 py-2.5 text-sm"
                placeholder="Иван Иванов"
              />
            </div>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label class="block text-xs font-medium text-gray-600 dark:text-gray-400 mb-1">Логин</label>
                <input
                  v-model="inviteForm.login"
                  type="text"
                  class="w-full border border-gray-300 dark:border-zinc-600 dark:bg-zinc-800 rounded-lg px-3 py-2.5 text-sm"
                  placeholder="ivan"
                  autocomplete="off"
                />
              </div>
              <div>
                <label class="block text-xs font-medium text-gray-600 dark:text-gray-400 mb-1">Пароль</label>
                <input
                  v-model="inviteForm.password"
                  type="text"
                  class="w-full border border-gray-300 dark:border-zinc-600 dark:bg-zinc-800 rounded-lg px-3 py-2.5 text-sm"
                  placeholder="минимум 6 символов"
                  autocomplete="new-password"
                />
              </div>
            </div>
            <div>
              <label class="block text-xs font-medium text-gray-600 dark:text-gray-400 mb-1">Роль</label>
              <select
                v-model="inviteForm.role"
                class="w-full border border-gray-300 dark:border-zinc-600 dark:bg-zinc-800 rounded-lg px-3 py-2.5 text-sm"
              >
                <option v-for="r in roleOptions" :key="r.value" :value="r.value">{{ r.label }}</option>
              </select>
            </div>
            <button
              type="button"
              class="w-full inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-lg border border-indigo-600 text-indigo-600 dark:text-indigo-400 dark:border-indigo-500 text-sm font-semibold hover:bg-indigo-50 dark:hover:bg-indigo-950/40 disabled:opacity-50"
              :disabled="creatingEmployee"
              @click="createEmployeeNow"
            >
              <Plus :size="16" />
              {{ creatingEmployee ? 'Создаём…' : 'Добавить сотрудника' }}
            </button>
          </div>

          <div
            v-if="createdEmployees.length"
            class="rounded-lg border border-gray-200 dark:border-zinc-700 overflow-hidden"
          >
            <div class="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-gray-500 bg-gray-50 dark:bg-zinc-800/80">
              Создано сейчас: {{ createdEmployees.length }}
            </div>
            <ul class="divide-y divide-gray-100 dark:divide-zinc-800">
              <li
                v-for="(e, i) in createdEmployees"
                :key="i"
                class="px-3 py-2.5 text-sm flex items-center justify-between gap-2"
              >
                <span class="font-medium text-gray-900 dark:text-gray-100 truncate">{{ e.fullName }}</span>
                <span class="text-xs text-gray-500 shrink-0">{{ e.login }} · {{ roleLabel(e.role) }}</span>
              </li>
            </ul>
          </div>

          <div class="flex items-center justify-between gap-3 pt-2">
            <button
              type="button"
              class="text-sm text-gray-500 hover:text-gray-700"
              @click="nextFromInvite"
            >
              Пропустить шаг
            </button>
            <button
              type="button"
              class="inline-flex items-center gap-2 px-4 py-2.5 rounded-lg bg-indigo-600 text-white text-sm font-semibold hover:bg-indigo-700"
              @click="nextFromInvite"
            >
              Далее
              <ArrowRight :size="16" />
            </button>
          </div>
        </div>

        <!-- Step 2: telegram / email hint -->
        <div v-else class="space-y-5">
          <div class="flex items-center gap-3">
            <div class="w-10 h-10 rounded-lg bg-indigo-50 dark:bg-indigo-900/30 flex items-center justify-center text-indigo-600">
              <MessageSquare :size="20" />
            </div>
            <div>
              <h2 class="font-semibold text-gray-900 dark:text-gray-100">{{ steps[2].title }}</h2>
              <p class="text-xs text-gray-500">{{ steps[2].hint }}</p>
            </div>
          </div>
          <div class="rounded-lg bg-gray-50 dark:bg-zinc-800/60 border border-gray-100 dark:border-zinc-700 p-4 text-sm text-gray-600 dark:text-gray-300 space-y-2">
            <p><strong>Telegram:</strong> бот и шаблоны уведомлений — вкладка «Telegram» в настройках.</p>
            <p><strong>Email:</strong> IMAP-приём писем — вкладка «Общее» в настройках.</p>
          </div>
          <div class="flex items-center justify-between gap-3 pt-2">
            <NuxtLink
              to="/settings"
              class="text-sm text-indigo-600 hover:text-indigo-700 font-medium"
            >
              Открыть настройки →
            </NuxtLink>
            <button
              type="button"
              class="inline-flex items-center gap-2 px-4 py-2.5 rounded-lg bg-indigo-600 text-white text-sm font-semibold hover:bg-indigo-700 disabled:opacity-50"
              :disabled="saving"
              @click="finish(false)"
            >
              <Check :size="16" />
              Готово
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
