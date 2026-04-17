<script setup lang="ts">
import { MapPin, Search, RefreshCw, Building2, ChevronRight, ShieldAlert } from 'lucide-vue-next'
import type { ServiceObject } from '~/types'

const api = useApi()
const toast = useToast()

const objects = ref<ServiceObject[]>([])
const loading = ref(true)
const searchQuery = ref('')

const editOpen = ref(false)
const saving = ref(false)
const editError = ref('')
const editing = ref<ServiceObject | null>(null)
const form = reactive({
  name: '',
  address: '',
  legalEntity: '',
  maintenanceStatus: '',
  maintenanceComment: '',
  directoriesOwner: '',
  sysAdmin: '',
  serverServices: '',
  isActive: true,
})

async function loadObjects() {
  loading.value = true
  try {
    objects.value = await api.serviceObjects.getAll()
  } catch (error: any) {
    console.error('Failed to load objects:', error)
    const s = error?.statusCode ?? error?.status ?? error?.response?.status
    toast.error(
      s
        ? `Не удалось загрузить объекты (код ${s}). Выйдите и войдите снова.`
        : 'Не удалось загрузить объекты.'
    )
  } finally {
    loading.value = false
  }
}

const filteredObjects = computed(() => {
  if (!searchQuery.value) return objects.value
  const q = searchQuery.value.toLowerCase()
  return objects.value.filter(o =>
    o.name.toLowerCase().includes(q) ||
    o.address.toLowerCase().includes(q) ||
    o.legalEntity.toLowerCase().includes(q)
  )
})

// Pagination (50 per page)
const perPage = 50
const page = ref(1)
watch([searchQuery], () => { page.value = 1 })
const totalPages = computed(() => Math.max(1, Math.ceil(filteredObjects.value.length / perPage)))
const paginatedObjects = computed(() => {
  const p = Math.min(Math.max(1, page.value), totalPages.value)
  const start = (p - 1) * perPage
  return filteredObjects.value.slice(start, start + perPage)
})

const statusStats = computed(() => {
  const map = new Map<string, number>()
  for (const o of filteredObjects.value || []) {
    const s = (o.maintenanceStatus || '—').trim() || '—'
    map.set(s, (map.get(s) || 0) + 1)
  }
  return Array.from(map.entries()).sort((a, b) => b[1] - a[1])
})

const fillStats = computed(() => {
  const list = filteredObjects.value || []
  const total = list.length || 0
  const filled = (key: 'directoriesOwner'|'sysAdmin'|'serverServices') =>
    list.filter(o => String((o as any)[key] || '').trim().length > 0).length
  return {
    total,
    directoriesOwner: { filled: filled('directoriesOwner') },
    sysAdmin: { filled: filled('sysAdmin') },
    serverServices: { filled: filled('serverServices') },
  }
})

function openEdit(o: ServiceObject) {
  editing.value = o
  form.name = o.name || ''
  form.address = o.address || ''
  form.legalEntity = o.legalEntity || ''
  form.maintenanceStatus = o.maintenanceStatus || ''
  form.maintenanceComment = o.maintenanceComment || ''
  form.directoriesOwner = o.directoriesOwner || ''
  form.sysAdmin = o.sysAdmin || ''
  form.serverServices = o.serverServices || ''
  form.isActive = !!o.isActive
  editError.value = ''
  editOpen.value = true
}

async function saveEdit() {
  if (!editing.value) return
  saving.value = true
  editError.value = ''
  try {
    await api.serviceObjects.update(editing.value.id, {
      name: form.name.trim(),
      address: form.address.trim(),
      legalEntity: form.legalEntity.trim(),
      maintenanceStatus: form.maintenanceStatus.trim(),
      maintenanceComment: form.maintenanceComment.trim(),
      directoriesOwner: form.directoriesOwner.trim(),
      sysAdmin: form.sysAdmin.trim(),
      serverServices: form.serverServices.trim(),
      isActive: form.isActive,
    })
    editOpen.value = false
    await loadObjects()
  } catch (e: any) {
    editError.value = e?.data?.error || e?.message || 'Не удалось сохранить объект'
  } finally {
    saving.value = false
  }
}

onMounted(() => {
  loadObjects()
})
</script>

<template>
  <div class="space-y-6 w-full">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <p class="text-sm text-gray-500">
        Всего <span class="font-semibold text-gray-900">{{ filteredObjects.length }}</span> активных локаций
      </p>
    </div>

    <!-- Stats -->
    <div class="grid grid-cols-1 lg:grid-cols-3 gap-3">
      <div class="bg-white p-3 rounded-xl border border-gray-200 shadow-sm">
        <div class="text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Статусы объектов</div>
        <div class="flex flex-wrap gap-2">
          <span v-for="[s, n] in statusStats" :key="s" class="inline-flex items-center gap-2 px-2 py-1 rounded-lg text-xs font-semibold border bg-gray-50 text-gray-700 border-gray-200">
            <span class="truncate max-w-[16rem]">{{ s }}</span>
            <span class="text-[11px] font-mono text-gray-500">{{ n }}</span>
          </span>
        </div>
      </div>
      <div class="bg-white p-3 rounded-xl border border-gray-200 shadow-sm">
        <div class="text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Заполненность (да / нет)</div>
        <div class="space-y-2 text-sm">
          <div class="flex items-center justify-between gap-3">
            <span class="text-gray-600 truncate">Справочники</span>
            <span class="font-mono text-gray-900 whitespace-nowrap">
              <span class="text-emerald-700">{{ fillStats.directoriesOwner.filled }}</span>/<span class="text-gray-400">{{ fillStats.total - fillStats.directoriesOwner.filled }}</span>
            </span>
          </div>
          <div class="flex items-center justify-between gap-3">
            <span class="text-gray-600 truncate">Сисадмин</span>
            <span class="font-mono text-gray-900 whitespace-nowrap">
              <span class="text-emerald-700">{{ fillStats.sysAdmin.filled }}</span>/<span class="text-gray-400">{{ fillStats.total - fillStats.sysAdmin.filled }}</span>
            </span>
          </div>
          <div class="flex items-center justify-between gap-3">
            <span class="text-gray-600 truncate">Server services</span>
            <span class="font-mono text-gray-900 whitespace-nowrap">
              <span class="text-emerald-700">{{ fillStats.serverServices.filled }}</span>/<span class="text-gray-400">{{ fillStats.total - fillStats.serverServices.filled }}</span>
            </span>
          </div>
          <div class="pt-1 text-[11px] text-gray-400">
            Формат: <span class="font-semibold text-gray-500">да</span>/<span class="font-semibold text-gray-500">нет</span>
          </div>
        </div>
      </div>
      <div class="bg-white p-3 rounded-xl border border-gray-200 shadow-sm">
        <div class="text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Подсказка</div>
        <div class="text-sm text-gray-600 leading-relaxed">
          Справочник объектов синхронизируется из таблицы. Здесь можно быстро искать и править комментарии/ответственных.
        </div>
      </div>
    </div>

    <!-- Search & Filter Bar -->
    <div class="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
      <div class="relative">
        <Search class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" :size="18" />
        <input
          v-model="searchQuery"
          type="text"
          class="w-full pl-10 pr-4 py-2.5 bg-gray-50 border-none rounded-lg text-sm focus:ring-2 focus:ring-indigo-500/20 focus:bg-white transition-all text-gray-900"
          placeholder="Поиск по названию, адресу, юридическому лицу..."
        />
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="loading" class="flex items-center justify-center py-24">
      <RefreshCw :size="32" class="animate-spin text-indigo-600" />
    </div>

    <!-- Empty State -->
    <div v-else-if="filteredObjects.length === 0" class="text-center py-24 bg-white rounded-xl border border-gray-200">
      <div class="w-16 h-16 bg-gray-50 rounded-full flex items-center justify-center mx-auto mb-4">
        <MapPin :size="32" class="text-gray-300" />
      </div>
      <h3 class="text-lg font-semibold text-gray-900 mb-1">Объекты не найдены</h3>
      <p class="text-sm text-gray-500">Попробуйте изменить поисковый запрос</p>
    </div>

    <!-- Objects List -->
    <div v-else class="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
      <div class="divide-y divide-gray-100">
        <button
          v-for="o in paginatedObjects"
          :key="o.id"
          type="button"
          class="w-full text-left px-4 py-3 hover:bg-gray-50/60 transition-colors group"
          @click="openEdit(o)"
        >
          <div class="flex items-start gap-4">
            <div class="w-10 h-10 rounded-lg bg-indigo-50 flex items-center justify-center text-indigo-600 shrink-0 group-hover:bg-indigo-600 group-hover:text-white transition-colors">
              <MapPin :size="18" />
            </div>

            <div class="min-w-0 flex-1">
              <div class="flex items-start justify-between gap-3">
                <div class="min-w-0">
                  <div class="font-bold text-gray-900 text-[15px] truncate">{{ o.name }}</div>
                  <div class="mt-0.5 text-[12px] text-gray-500 truncate italic">{{ o.address || '—' }}</div>
                  <div class="mt-2 inline-flex items-center gap-2 text-[12px] text-gray-600 bg-gray-50 px-2 py-1 rounded-lg border border-gray-100">
                    <Building2 :size="14" class="text-gray-400" />
                    <span class="font-medium truncate max-w-[52rem]">{{ o.legalEntity || '—' }}</span>
                  </div>
                </div>

                <div class="shrink-0 flex items-center gap-2">
                  <span
                    :class="[
                      'px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider border',
                      o.isActive ? 'bg-green-50 text-green-700 border-green-100' : 'bg-gray-50 text-gray-500 border-gray-200'
                    ]"
                  >
                    {{ o.isActive ? 'Активен' : 'Архив' }}
                  </span>
                  <ChevronRight :size="18" class="text-gray-300 group-hover:text-indigo-600 transition-colors" />
                </div>
              </div>

              <div class="mt-3 hidden md:grid md:grid-cols-3 gap-3">
                <div class="text-[11px] text-gray-600 bg-white border border-gray-100 rounded-lg p-2 leading-relaxed">
                  <div class="text-gray-400 uppercase font-bold tracking-tighter">Статус объекта</div>
                  <div class="font-semibold text-gray-800">{{ o.maintenanceStatus || '—' }}</div>
                  <div v-if="o.maintenanceComment" class="mt-1 text-gray-500 line-clamp-2 italic">
                    {{ o.maintenanceComment }}
                  </div>
                </div>

                <div class="text-[11px] text-gray-600 bg-white border border-gray-100 rounded-lg p-2 leading-relaxed">
                  <div class="text-gray-400 uppercase font-bold tracking-tighter">Справочники</div>
                  <div class="font-semibold text-gray-800 truncate">{{ o.directoriesOwner || '—' }}</div>
                  <div class="mt-2 text-gray-400 uppercase font-bold tracking-tighter">Сисадмин</div>
                  <div class="font-semibold text-gray-800 truncate">{{ o.sysAdmin || '—' }}</div>
                </div>

                <div class="text-[11px] text-gray-600 bg-white border border-gray-100 rounded-lg p-2 leading-relaxed">
                  <div class="text-gray-400 uppercase font-bold tracking-tighter">Server services</div>
                  <div class="font-semibold text-gray-800 line-clamp-3">{{ o.serverServices || '—' }}</div>
                </div>
              </div>
              
              <div class="md:hidden flex flex-wrap items-center gap-2 mt-2 text-[11px] text-gray-500">
                <span v-if="o.maintenanceStatus" class="bg-gray-50 px-1.5 py-0.5 rounded border border-gray-100 font-medium">{{ o.maintenanceStatus }}</span>
                <span v-if="o.sysAdmin" class="truncate max-w-[120px]">Сисадмин: {{ o.sysAdmin }}</span>
              </div>
            </div>
          </div>
        </button>
      </div>
    </div>

    <!-- Pagination -->
    <div v-if="filteredObjects.length > perPage" class="flex items-center justify-between px-4 py-3 border border-gray-200 bg-white rounded-lg shadow-sm">
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
        <div class="bg-white w-full max-w-2xl rounded-xl shadow-modal border border-gray-200 overflow-hidden">
          <div class="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
            <div class="font-semibold text-gray-900 text-sm truncate">Редактирование объекта</div>
            <button class="p-2 text-gray-400 hover:text-gray-700" @click="editOpen = false">✕</button>
          </div>
          <div class="p-5 space-y-4">
            <div v-if="editError" class="text-sm text-red-700 bg-red-50 border border-red-200 rounded-lg px-3 py-2 flex items-center gap-2">
              <ShieldAlert :size="16" />
              {{ editError }}
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div class="sm:col-span-2">
                <label class="block text-xs font-semibold text-gray-500 mb-1">Объект</label>
                <input v-model="form.name" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
              </div>
              <div class="sm:col-span-2">
                <label class="block text-xs font-semibold text-gray-500 mb-1">Адрес</label>
                <input v-model="form.address" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
              </div>
              <div class="sm:col-span-2">
                <label class="block text-xs font-semibold text-gray-500 mb-1">Юрлицо</label>
                <input v-model="form.legalEntity" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
              </div>

              <div class="sm:col-span-2">
                <label class="block text-xs font-semibold text-gray-500 mb-1">Статус объекта</label>
                <input v-model="form.maintenanceStatus" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
              </div>
              <div class="sm:col-span-2">
                <label class="block text-xs font-semibold text-gray-500 mb-1">Комментарий к статусу</label>
                <textarea v-model="form.maintenanceComment" rows="3" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none resize-none"></textarea>
              </div>

              <div>
                <label class="block text-xs font-semibold text-gray-500 mb-1">Ведение справочников</label>
                <input v-model="form.directoriesOwner" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
              </div>
              <div>
                <label class="block text-xs font-semibold text-gray-500 mb-1">Сисадмин</label>
                <input v-model="form.sysAdmin" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none" />
              </div>
              <div class="sm:col-span-2">
                <label class="block text-xs font-semibold text-gray-500 mb-1">Server services</label>
                <textarea v-model="form.serverServices" rows="3" class="w-full px-3 py-2 text-sm bg-gray-50 border border-gray-200 rounded-lg focus:bg-white focus:ring-2 focus:ring-indigo-500/20 outline-none resize-none"></textarea>
              </div>

              <div class="flex items-center gap-2">
                <input id="obj-active" type="checkbox" v-model="form.isActive" class="h-4 w-4" />
                <label for="obj-active" class="text-sm text-gray-700">Активен</label>
              </div>
            </div>
          </div>
          <div class="px-5 py-4 border-t border-gray-100 bg-gray-50 flex items-center justify-end gap-2">
            <button class="px-4 py-2 text-sm font-semibold border border-gray-200 rounded-lg bg-white hover:bg-gray-50" @click="editOpen = false" :disabled="saving">Отмена</button>
            <button class="px-4 py-2 text-sm font-semibold rounded-lg bg-indigo-600 text-white hover:bg-indigo-700 disabled:opacity-50" @click="saveEdit" :disabled="saving || !form.name.trim()">
              {{ saving ? 'Сохранение…' : 'Сохранить' }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
