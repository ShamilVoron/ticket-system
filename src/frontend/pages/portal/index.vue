<script setup lang="ts">
import { RefreshCw, Plus, Ticket, MapPin, Building2, LogOut } from 'lucide-vue-next'

definePageMeta({
  layout: 'default',
})

const api = useApi()
const auth = useAuthStore()
const router = useRouter()
const toast = useToast()
const pageHeader = usePageHeader()

onMounted(() => {
  pageHeader.set('Клиентский портал', false)
  if (!auth.isClient) {
    navigateTo('/')
    return
  }
  load()
})
onUnmounted(() => pageHeader.clear())

type PortalTicket = {
  id: number
  title: string
  status: string
  priority: string
  createdAt: string
  objectId: number | null
  requestType: string
}

type ServiceObject = {
  id: number
  name: string
  address: string
  maintenanceStatus: string
  clientId: number | null
}

const tickets = ref<PortalTicket[]>([])
const objects = ref<ServiceObject[]>([])
const companyName = ref('')
const loading = ref(true)

async function load() {
  loading.value = true
  try {
    const [tix, objs, ctx] = await Promise.all([
      api.clientPortal.getTickets(),
      api.clientPortal.getServiceObjects(),
      api.auth.ticketContext().catch(() => ({ companyId: null, companyName: null })),
    ])
    tickets.value = tix
    objects.value = objs
    companyName.value = ctx.companyName || ''
  } catch {
    toast.error('Не удалось загрузить данные портала')
  } finally {
    loading.value = false
  }
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function statusClass(status: string): string {
  if (status === 'Открыт') return 'bg-green-50 text-green-700 border-green-200'
  if (status === 'В работе') return 'bg-amber-50 text-amber-800 border-amber-200'
  if (status === 'Закрыт' || status === 'Решён' || status === 'Решено') {
    return 'bg-gray-50 text-gray-600 border-gray-200'
  }
  return 'bg-blue-50 text-blue-700 border-blue-200'
}

function objectName(objectId: number | null): string {
  if (!objectId) return '—'
  return objects.value.find(o => o.id === objectId)?.name || `#${objectId}`
}

function createTicket() {
  router.push('/tickets/new')
}

function openTicket(id: number) {
  router.push(`/tickets/${id}`)
}

function logout() {
  auth.logout()
  navigateTo('/auth/login')
}
</script>

<template>
  <div class="mx-auto w-full max-w-5xl space-y-6">
    <div class="flex flex-wrap items-start justify-between gap-4">
      <div>
        <p class="text-xs font-semibold uppercase tracking-wider text-gray-400">Клиентский портал</p>
        <h1 class="mt-1 text-2xl font-bold text-gray-900">
          {{ companyName || auth.fullName || 'Мои заявки' }}
        </h1>
        <p class="mt-1 text-sm text-gray-500">
          Просмотр заявок и объектов обслуживания вашей организации
        </p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <button
          type="button"
          class="inline-flex items-center gap-2 rounded-md border border-gray-300 bg-white px-3 py-2 text-sm text-gray-700 hover:bg-gray-50"
          :disabled="loading"
          @click="load"
        >
          <RefreshCw :size="16" :class="{ 'animate-spin': loading }" />
          Обновить
        </button>
        <button
          type="button"
          class="inline-flex items-center gap-2 rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700"
          @click="createTicket"
        >
          <Plus :size="16" />
          Создать заявку
        </button>
      </div>
    </div>

    <div v-if="loading" class="flex items-center justify-center py-24">
      <RefreshCw :size="28" class="animate-spin text-indigo-600" />
    </div>

    <template v-else>
      <!-- Tickets -->
      <section class="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
        <div class="flex items-center gap-2 border-b border-gray-100 px-5 py-4">
          <Ticket :size="18" class="text-indigo-600" />
          <h2 class="font-semibold text-gray-900">Мои заявки</h2>
          <span class="ml-auto text-xs text-gray-400">{{ tickets.length }}</span>
        </div>
        <div v-if="tickets.length === 0" class="px-5 py-10 text-center text-sm text-gray-500">
          Заявок пока нет. Нажмите «Создать заявку», чтобы отправить обращение.
        </div>
        <ul v-else class="divide-y divide-gray-100">
          <li
            v-for="t in tickets"
            :key="t.id"
            class="cursor-pointer px-5 py-4 transition-colors hover:bg-gray-50"
            @click="openTicket(t.id)"
          >
            <div class="flex flex-wrap items-start justify-between gap-2">
              <div class="min-w-0 flex-1">
                <div class="flex flex-wrap items-center gap-2">
                  <span class="font-mono text-xs text-gray-400">#{{ t.id }}</span>
                  <span
                    class="inline-flex rounded border px-2 py-0.5 text-[11px] font-medium"
                    :class="statusClass(t.status)"
                  >
                    {{ t.status }}
                  </span>
                  <span v-if="t.requestType" class="text-[11px] text-gray-400">{{ t.requestType }}</span>
                </div>
                <p class="mt-1 truncate font-medium text-gray-900">{{ t.title }}</p>
                <p class="mt-1 text-xs text-gray-500">
                  Объект: {{ objectName(t.objectId) }} · {{ formatDate(t.createdAt) }}
                </p>
              </div>
              <span class="shrink-0 text-xs text-gray-400">{{ t.priority }}</span>
            </div>
          </li>
        </ul>
      </section>

      <!-- Service objects -->
      <section class="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
        <div class="flex items-center gap-2 border-b border-gray-100 px-5 py-4">
          <Building2 :size="18" class="text-indigo-600" />
          <h2 class="font-semibold text-gray-900">Объекты обслуживания</h2>
          <span class="ml-auto text-xs text-gray-400">{{ objects.length }}</span>
        </div>
        <div v-if="objects.length === 0" class="px-5 py-10 text-center text-sm text-gray-500">
          Объекты обслуживания не найдены для вашей компании.
        </div>
        <ul v-else class="divide-y divide-gray-100">
          <li v-for="o in objects" :key="o.id" class="px-5 py-4">
            <div class="font-medium text-gray-900">{{ o.name }}</div>
            <div class="mt-1 flex flex-wrap items-center gap-3 text-xs text-gray-500">
              <span class="inline-flex items-center gap-1">
                <MapPin :size="12" />
                {{ o.address || 'Адрес не указан' }}
              </span>
              <span
                v-if="o.maintenanceStatus"
                class="rounded border border-gray-200 bg-gray-50 px-2 py-0.5"
              >
                {{ o.maintenanceStatus }}
              </span>
            </div>
          </li>
        </ul>
      </section>

      <div class="flex justify-end">
        <button
          type="button"
          class="inline-flex items-center gap-2 text-sm text-gray-500 hover:text-gray-800"
          @click="logout"
        >
          <LogOut :size="14" />
          Выйти
        </button>
      </div>
    </template>
  </div>
</template>
