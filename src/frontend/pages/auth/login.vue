<script setup lang="ts">
import { KeyRound, User, Eye, EyeOff, ArrowRight, AlertCircle, RefreshCw } from 'lucide-vue-next'
import type { LoginRequest } from '~/types'

definePageMeta({
  layout: 'auth',
})

const api = useApi()
const auth = useAuthStore()
const router = useRouter()

const form = ref<LoginRequest>({
  username: '',
  password: '',
})

const loading = ref(false)
const error = ref('')
const showPassword = ref(false)

function homePath() {
  if (auth.isFieldEngineer) return '/field'
  if (auth.isClient) return '/portal'
  return '/'
}

onMounted(() => {
  auth.hydrate()
  if (auth.isLoggedIn) {
    router.push(homePath())
  }
})

async function handleLogin() {
  if (!form.value.username || !form.value.password) {
    error.value = 'Введите логин и пароль'
    return
  }

  loading.value = true
  error.value = ''

  try {
    const response = await api.auth.login({
      username: form.value.username,
      password: form.value.password,
    })

    auth.setAuth(response)
    if (response.role === 'field_engineer') {
      router.push('/field')
      return
    }
    if (response.role === 'client') {
      router.push('/portal')
      return
    }
    if (response.role === 'super_admin') {
      try {
        const { branding, load } = useSystemBranding()
        await load(true)
        if (!branding.value.onboardingCompleted && localStorage.getItem('onboarding_skip') !== '1') {
          router.push('/onboarding')
          return
        }
      } catch {
        /* fall through to home */
      }
    }
    router.push('/')
  } catch (e: any) {
    console.error('Login error:', e)
    if (e.response?.status === 401) {
      error.value = 'Неверный логин или пароль'
    } else if (e.response?.status === 400) {
      error.value = e.response?._data?.message || e.response?._data || 'Ошибка валидации'
    } else if (!e.response) {
      error.value = 'Не удалось подключиться к серверу. Проверьте, что бэкенд запущен.'
    } else {
      error.value = 'Произошла ошибка при входе'
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="relative min-h-[100dvh] w-full max-w-none bg-black text-white overflow-x-hidden">
    <!-- Тёмные «обои» на весь экран -->
    <div
      class="pointer-events-none absolute inset-0 opacity-[0.22] bg-[linear-gradient(to_right,rgba(255,255,255,0.07)_1px,transparent_1px),linear-gradient(to_bottom,rgba(255,255,255,0.07)_1px,transparent_1px)] bg-[size:56px_56px]"
    />
    <div
      class="pointer-events-none absolute inset-0 bg-[radial-gradient(900px_600px_at_20%_40%,rgba(255,255,255,0.06),transparent_55%)]"
    />

    <div
      class="relative z-10 mx-auto flex min-h-[100dvh] w-full max-w-[1600px] flex-col gap-14 px-6 py-12 sm:px-10 lg:grid lg:grid-cols-2 lg:items-center lg:gap-16 lg:px-14 xl:px-20 lg:py-0"
    >
    <!-- Слева: карточка входа поверх общего фона -->
    <main class="flex w-full min-w-0 items-center justify-center lg:justify-start">
      <div class="w-full max-w-md">
        <div class="mb-8 flex justify-center lg:hidden">
          <img src="/favicon.svg" alt="Ticket System" class="h-14 w-14 rounded-2xl shadow-lg ring-1 ring-white/20" />
        </div>

        <div
          class="rounded-[2rem] border border-white/10 bg-white p-8 shadow-[0_32px_80px_-20px_rgba(0,0,0,0.65)] sm:p-10"
        >
          <div class="mb-10">
            <h2 class="text-2xl font-black text-neutral-900 tracking-tight">Вход в систему</h2>
            <div class="mt-1.5 flex items-center gap-2">
              <div class="w-1.5 h-1.5 rounded-full bg-zinc-400" />
              <p class="text-sm text-zinc-500 font-medium">Введите учетные данные</p>
            </div>
          </div>

          <Transition name="fade">
            <div v-if="error" class="mb-5 bg-red-50 border border-red-100 p-3.5 rounded-xl flex items-start gap-3">
              <AlertCircle class="text-red-500 shrink-0 mt-0.5" :size="18" />
              <span class="text-sm text-red-700 font-medium">{{ error }}</span>
            </div>
          </Transition>

          <form class="space-y-6" @submit.prevent="handleLogin">
            <div class="flex flex-col sm:flex-row sm:items-center gap-2 sm:gap-6">
              <label for="username" class="sm:w-28 shrink-0 text-xs font-bold uppercase tracking-widest text-zinc-400"
                >Логин</label
              >
              <div class="flex-1 relative group">
                <div
                  class="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none transition-colors group-focus-within:text-neutral-950 text-zinc-400"
                >
                  <User class="h-5 w-5" />
                </div>
                <input
                  id="username"
                  v-model="form.username"
                  name="username"
                  type="text"
                  autocomplete="username"
                  required
                  class="block w-full pl-12 pr-4 py-4 bg-zinc-50 border border-zinc-200 rounded-2xl text-sm text-neutral-900 placeholder:text-zinc-400 focus:ring-4 focus:ring-neutral-950/5 focus:border-neutral-950 focus:bg-white transition-all outline-none"
                  placeholder="Email или логин"
                  :disabled="loading"
                />
              </div>
            </div>

            <div class="flex flex-col sm:flex-row sm:items-center gap-2 sm:gap-6">
              <label for="password" class="sm:w-28 shrink-0 text-xs font-bold uppercase tracking-widest text-zinc-400"
                >Пароль</label
              >
              <div class="flex-1 relative group">
                <div
                  class="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none transition-colors group-focus-within:text-neutral-950 text-zinc-400"
                >
                  <KeyRound class="h-5 w-5" />
                </div>
                <input
                  id="password"
                  v-model="form.password"
                  name="password"
                  :type="showPassword ? 'text' : 'password'"
                  autocomplete="current-password"
                  required
                  class="block w-full pl-12 pr-12 py-4 bg-zinc-50 border border-zinc-200 rounded-2xl text-sm text-neutral-900 placeholder:text-zinc-400 focus:ring-4 focus:ring-neutral-950/5 focus:border-neutral-950 focus:bg-white transition-all outline-none"
                  placeholder="Пароль"
                  :disabled="loading"
                />
                <button
                  type="button"
                  @click="showPassword = !showPassword"
                  class="absolute inset-y-0 right-0 pr-4 flex items-center text-zinc-400 hover:text-neutral-950 transition-colors"
                  :disabled="loading"
                  aria-label="Показать пароль"
                >
                  <Eye v-if="!showPassword" :size="20" />
                  <EyeOff v-else :size="20" />
                </button>
              </div>
            </div>

            <div class="flex flex-col sm:flex-row sm:items-center gap-6 pt-2">
              <div class="sm:w-28 shrink-0 hidden sm:block" />
              <button
                type="submit"
                class="flex-1 flex justify-center items-center gap-3 py-4 px-6 rounded-2xl text-sm font-black text-white bg-neutral-950 hover:bg-neutral-800 active:scale-[0.98] focus:outline-none focus:ring-4 focus:ring-neutral-950/10 transition-all disabled:opacity-50 shadow-xl shadow-neutral-950/10"
                :disabled="loading"
              >
                <RefreshCw v-if="loading" :size="20" class="animate-spin" />
                <template v-else>
                  Войти
                  <ArrowRight :size="20" />
                </template>
              </button>
            </div>

            <div class="pt-4 flex flex-col sm:flex-row sm:items-center gap-6">
              <div class="sm:w-28 shrink-0 hidden sm:block" />
              <p class="flex-1 text-center sm:text-left text-xs text-zinc-400 font-medium leading-relaxed">
                Нет доступа?
                <span class="text-zinc-900 border-b border-zinc-200">Обратитесь в IT-департамент</span>
              </p>
            </div>
          </form>
        </div>
      </div>
    </main>

    <!-- Справа: промо на том же тёмном фоне -->
    <aside class="flex min-h-0 w-full min-w-0 flex-col justify-center pb-4 lg:py-8">
      <div class="flex w-full flex-col items-end gap-10 text-right lg:gap-14">
        <div class="hidden items-center gap-4 lg:flex justify-end w-full">
          <div
            class="flex h-12 w-12 shrink-0 items-center justify-center overflow-hidden rounded-2xl bg-white/5 ring-1 ring-white/10"
          >
            <img src="/favicon.svg" alt="Ticket System" class="h-full w-full object-cover" />
          </div>
          <div class="text-sm font-bold uppercase tracking-[0.2em] text-white">Ticket System</div>
        </div>

        <h1
          class="w-full text-4xl font-black leading-[1.08] tracking-tighter sm:text-5xl lg:text-5xl xl:text-6xl 2xl:text-7xl"
        >
          <span class="block text-white">Система</span>
          <span class="block text-white">управления</span>
          <span class="mt-1 block text-zinc-500">заявками</span>
        </h1>

        <div class="h-1 w-24 rounded-full bg-zinc-600" />

        <p class="max-w-xl text-base font-light leading-relaxed text-zinc-300 sm:text-lg lg:text-xl">
          Эффективная работа <span class="font-semibold text-white">с обращениями клиентов</span> в едином интерфейсе.
        </p>
      </div>
    </aside>
    </div>
  </div>
</template>

<style scoped>
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
