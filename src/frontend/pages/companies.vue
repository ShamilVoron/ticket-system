<script setup lang="ts">
import { Building2, Search, RefreshCw, Plus, MapPin, Phone, Mail, ChevronRight } from 'lucide-vue-next'
import type { Company } from '~/types'

const api = useApi()
const auth = useAuthStore()
const router = useRouter()
const toast = useToast()

const companies = ref<Company[]>([])
const loading = ref(true)
const searchQuery = ref('')

const editOpen = ref(false)
const saving = ref(false)
const editError = ref('')
const editing = ref<Company | null>(null)
const form = reactive({
  name: '',
  email: '',
  phone: '',
  hqAddress: '',
  externalCode: '',
  isActive: true,
})

async function loadCompanies() {
  loading.value = true
  try {
    companies.value = await api.companies.getAll()
   } catch (error: any) {
    console.error('Failed to load companies:', error)
    const s = error?.statusCode ?? error?.status ?? error?.response?.status
    toast.error(
      s
        ? `Не удалось загрузить юрлица (код ${s}). Выйдите и войдите снова; проверьте, что API обновлён.`
        : 'Не удалось загрузить юрлица. Проверьте сеть и авторизацию.'
    )
  } finally {
    loading.value = false
  }
}

function openEdit(c: Company) {
  editing.value = c
  form.name = c.name || ''
  form.email = c.email || ''
  form.phone = c.phone || ''
  form.hqAddress = c.hqAddress || ''
  form.externalCode = c.externalCode || ''
  form.isActive = !!c.isActive
  editError.value = ''
  editOpen.value = true
}

async function saveEdit() {
  if (!editing.value) return
  saving.value = true
  editError.value = ''
  try {
    await api.companies.update(editing.value.id, {
      name: form.name.trim(),
      email: form.email.trim(),
      phone: form.phone.trim(),
      hqAddress: form.hqAddress.trim(),
      externalCode: form.externalCode.trim(),
      isActive: form.isActive,
    })
    editOpen.value = false
    await loadCompanies()
  } catch (e: any) {
    editError.value = e?.data?.error || e?.message || 'Не удалось сохранить изменения'
  } finally {
    saving.value = false
  }
}

const filteredCompanies = computed(() => {
  if (!searchQuery.value) return companies.value
  const q = searchQuery.value.toLowerCase()
  return companies.value.filter(c =>
    c.name.toLowerCase().includes(q) ||
    (c.email && c.email.toLowerCase().includes(q)) ||
    (c.phone && c.phone.includes(q))
  )
})

// Pagination (50 per page)
const perPage = 50
const page = ref(1)
watch([searchQuery], () => { page.value = 1 })
const totalPages = computed(() => Math.max(1, Math.ceil(filteredCompanies.value.length / perPage)))
const paginatedCompanies = computed(() => {
  const p = Math.min(Math.max(1, page.value), totalPages.value)
  const start = (p - 1) * perPage
  return filteredCompanies.value.slice(start, start + perPage)
})

onMounted(() => {
  loadCompanies()
})
</script>

<template>
  <div class="space-y-6 w-full">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <p class="text-sm text-gray-500">
        Всего <span class="font-semibold text-gray-900">{{ filteredCompanies.length }}</span> зарегистрированных организаций
      </p>
    </div>

    <!-- Search Tool -->
    <div class="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
      <div class="relative">
        <Search class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" :size="18" />
        <input
          v-model="searchQuery"
          type="text"
          class="w-full pl-10 pr-4 py-2.5 bg-gray-50 border-none rounded-lg text-sm focus:ring-2 focus:ring-indigo-500/20 focus:bg-white transition-all text-gray-900"
          placeholder="Поиск по названию, email, телефону или адресу..."
        />
      </div>
    </div>

    <!-- Main Content -->
    <div class="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
      <div v-if="loading" class="flex items-center justify-center py-24">
        <RefreshCw :size="32" class="animate-spin text-indigo-600" />
      </div>

      <div v-else-if="filteredCompanies.length === 0" class="text-center py-24 px-6">
        <div class="w-16 h-16 bg-gray-50 rounded-full flex items-center justify-center mx-auto mb-4">
          <Building2 :size="32" class="text-gray-300" />
        </div>
        <h3 class="text-lg font-semibold text-gray-900 mb-1">Компании не найдены</h3>
        <p class="text-sm text-gray-500 max-w-xs mx-auto">По вашему запросу ничего не найдено. Попробуйте изменить параметры поиска или добавить новую компанию.</p>
      </div>

      <template v-else>
      <!-- Mobile Cards -->
      <div class="md:hidden divide-y divide-gray-100">
        <div
          v-for="c in paginatedCompanies"
          :key="c.id"
          @click="openEdit(c)"
          class="px-4 py-3 active:bg-gray-50 cursor-pointer"
        >
          <div class="flex items-start justify-between gap-2 mb-1">
            <div class="min-w-0">
              <div class="font-bold text-gray-900 text-sm truncate">{{ c.name }}</div>
              <div v-if="c.externalCode" class="text-[10px] text-gray-400 font-mono">{{ c.externalCode }}</div>
            </div>
            <span
              :class="[
                'shrink-0 px-2 py-0.5 rounded text-[10px] font-bold uppercase border',
                c.isActive ? 'bg-green-50 text-green-700 border-green-100' : 'bg-gray-50 text-gray-400 border-gray-200'
              ]"
            >{{ c.isActive ? 'Активна' : 'Архив' }}</span>
          </div>
          <div class="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-gray-500 mt-1">
            <span v-if="c.email" class="truncate max-w-[180px]">{{ c.email }}</span>
            <span v-if="c.phone">{{ c.phone }}</span>
          </div>
          <div v-if="c.hqAddress" class="text-xs text-gray-400 truncate mt-1 italic">{{ c.hqAddress }}</div>
        </div>
      </div>

      <!-- Desktop Table -->
      <div class="hidden md:block overflow-x-auto">
        <table class="w-full text-left border-collapse">
          <thead>
            <tr class="bg-gray-50/50 border-b border-gray-100">
              <th class="px-5 py-3 text-xs font-bold text-gray-400 uppercase tracking-wider">Компания</th>
              <th class="px-5 py-3 text-xs font-bold text-gray-400 uppercase tracking-wider">Контакты</th>
              <th class="px-5 py-3 text-xs font-bold text-gray-400 uppercase tracking-wider">Адрес HQ</th>
              <th class="px-5 py-3 text-xs font-bold text-gray-400 uppercase tracking-wider">Статус</th>
              <th class="px-5 py-3 w-10"></th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-50 text-sm">
            <tr v-for="c in paginatedCompanies" :key="c.id" class="hover:bg-gray-50/50 transition-colors group cursor-pointer" @click="openEdit(c)">
              <td class="px-5 py-3.5">
                <div class="flex items-center gap-3">
                  <div class="w-10 h-10 rounded-lg bg-indigo-50 flex items-center justify-center text-indigo-600 group-hover:bg-indigo-600 group-hover:text-white transition-colors">
                    <Building2 :size="20" />
                  </div>
                  <div>
                    <div class="font-bold text-gray-900 group-hover:text-indigo-600 transition-colors">{{ c.name }}</div>
                    <div v-if="c.externalCode" class="text-[10px] text-gray-400 font-mono tracking-tighter uppercase">{{ c.externalCode }}</div>
                  </div>
                </div>
              </td>
              <td class="px-5 py-3.5">
                <div class="flex flex-col gap-1">
                  <a v-if="c.email" :href="`mailto:${c.email}`" class="flex items-center gap-2 text-gray-600 hover:text-indigo-600 transition-colors" @click.stop>
                    <Mail :size="12" class="text-gray-400" />
                    {{ c.email }}
                  </a>
                  <div v-if="c.phone" class="flex items-center gap-2 text-gray-600">
                    <Phone :size="12" class="text-gray-400" />
                    {{ c.phone }}
                  </div>
                </div>
              </td>
              <td class="px-5 py-3.5 text-gray-500">
                <div v-if="c.hqAddress" class="flex items-center gap-2">
                  <MapPin :size="14" class="text-gray-300" />
                  <span class="max-w-[460px] truncate italic">{{ c.hqAddress }}</span>
                </div>
                <span v-else class="text-gray-300">—</span>
              </td>
              <td class="px-5 py-3.5">
                <span
                  :class="[
                    'px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider border',
                    c.isActive ? 'bg-green-50 text-green-700 border-green-100' : 'bg-gray-50 text-gray-400 border-gray-200'
                  ]"
                >
                  {{ c.isActive ? 'Активна' : 'Архив' }}
                </span>
              </td>
              <td class="px-5 py-3.5 text-right">
                <ChevronRight :size="18" class="text-gray-300 group-hover:text-indigo-600 transition-colors" />
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      </template>
    </div>

    <!-- Pagination -->
    <div v-if="filteredCompanies.length > perPage" class="flex items-center justify-between px-4 py-3 border border-gray-200 bg-white rounded-lg shadow-sm">
      <div class="text-xs text-gray-500">
        Страница <span class="font-semibold text-gray-900">{{ page }}</span> из <span class="font-semibold text-gray-900">{{ totalPages }}</span>
      </div>
      <div class="flex items-center gap-1.5">
        <button
          type="button"
          class="px-3 py-1.5 text-xs font-medium border border-gray-200 rounded hover:bg-gray-50 disabled:opacity-50"
          :disabled="page <= 1"
          @click="page = 1"
        >«</button>
        <button
          type="button"
          class="px-3 py-1.5 text-xs font-medium border border-gray-200 rounded hover:bg-gray-50 disabled:opacity-50"
          :disabled="page <= 1"
          @click="page = Math.max(1, page - 1)"
        >Назад</button>
        <button
          type="button"
          class="px-3 py-1.5 text-xs font-medium border border-gray-200 rounded hover:bg-gray-50 disabled:opacity-50"
          :disabled="page >= totalPages"
          @click="page = Math.min(totalPages, page + 1)"
        >Вперёд</button>
        <button
          type="button"
          class="px-3 py-1.5 text-xs font-medium border border-gray-200 rounded hover:bg-gray-50 disabled:opacity-50"
          :disabled="page >= totalPages"
          @click="page = totalPages"
        >»</button>
      </div>
    </div>

    <!-- Edit Modal -->
    <Teleport to="body">
      <div v-if="editOpen" class="fixed inset-0 z-50 bg-black/40 backdrop-blur-sm flex items-center justify-center p-4" @click.self="editOpen = false">
        <div class="bg-white w-full max-w-xl rounded-xl shadow-modal border border-gray-200 overflow-hidden">
          <div class="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
            <div class="font-semibold text-gray-900 text-sm truncate">Редактирование юрлица</div>
            <button class="p-2 text-gray-400 hover:text-gray-700" @click="editOpen = false">
              ✕
            </button>
          </div>
          <div class="p-5 space-y-4">
            <div v-if="editError" class="text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
              {{ editError }}
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div class="sm:col-span-2">
                <label class="block text-xs font-semibold text-gray-500 mb-1">Компания</label>
                <input v-model="form.name" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
              </div>
              <div>
                <label class="block text-xs font-semibold text-gray-500 mb-1">Email</label>
                <input v-model="form.email" type="email" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
              </div>
              <div>
                <label class="block text-xs font-semibold text-gray-500 mb-1">Телефон</label>
                <input v-model="form.phone" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
              </div>
              <div class="sm:col-span-2">
                <label class="block text-xs font-semibold text-gray-500 mb-1">Адрес HQ</label>
                <input v-model="form.hqAddress" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
              </div>
              <div>
                <label class="block text-xs font-semibold text-gray-500 mb-1">Код</label>
                <input v-model="form.externalCode" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
              </div>
              <div class="flex items-end">
                <label class="inline-flex items-center gap-2 text-sm text-gray-700 select-none">
                  <input type="checkbox" v-model="form.isActive" class="h-4 w-4" />
                  Активна
                </label>
              </div>
            </div>
          </div>
          <div class="px-5 py-4 border-t border-gray-100 bg-gray-50 flex items-center justify-end gap-2">
            <button class="px-4 py-2 text-sm font-semibold border border-gray-200 rounded-lg bg-white hover:bg-gray-50" @click="editOpen = false" :disabled="saving">
              Отмена
            </button>
            <button class="px-4 py-2 text-sm font-semibold rounded-lg bg-indigo-600 text-white hover:bg-indigo-700 disabled:opacity-50" @click="saveEdit" :disabled="saving || !form.name.trim()">
              {{ saving ? 'Сохранение…' : 'Сохранить' }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
