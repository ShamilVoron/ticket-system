<script setup lang="ts">
import { Plus, Trash2, Edit2, Check, X, FileSpreadsheet, Download, RefreshCw, Save, ArrowLeft } from 'lucide-vue-next'

definePageMeta({ middleware: ['staff-not-field-engineer'] })

const api = useApi()
const auth = useAuthStore()
const router = useRouter()
const staffPerm = useStaffPermissions()

const items = ref<any[]>([])
const loadingList = ref(false)

const activeItem = ref<any>(null)
const loadingDetail = ref(false)
const savingCells = ref(false)

const showCreateModal = ref(false)
const newForm = ref({ name: '', sourceKind: 0, googleUrl: '', rows: 20, cols: 10 })

// Cell grid state
const cells = ref<Record<string, any>>({})
const cellPatches = ref<Record<string, any>>({})

async function loadList() {
  loadingList.value = true
  try {
    items.value = await api.spreadsheets.getAll()
  } catch(e) {}
  finally { loadingList.value = false }
}

async function createItem() {
  if (!staffPerm.can('sectionSpreadsheetsEdit')) return
  try {
    const data = {
      name: newForm.value.name,
      googleSheetUrlOrId: newForm.value.sourceKind === 1 ? newForm.value.googleUrl : undefined,
      rows: newForm.value.sourceKind === 0 ? newForm.value.rows : undefined,
      cols: newForm.value.sourceKind === 0 ? newForm.value.cols : undefined
    }
    const created = await api.spreadsheets.create(data)
    items.value.unshift(created)
    showCreateModal.value = false
    openItem(created.id)
  } catch(e: any) {
    alert(e.response?.data?.error || 'Ошибка создания')
  }
}

async function deleteItem(id: number) {
  if (!staffPerm.can('sectionSpreadsheetsEdit')) return
  if(!confirm('Удалить таблицу?')) return
  try {
    await api.spreadsheets.delete(id)
    items.value = items.value.filter(x => x.id !== id)
    if(activeItem.value?.id === id) activeItem.value = null
  } catch(e) {}
}

async function openItem(id: number) {
  loadingDetail.value = true
  activeItem.value = null
  cells.value = {}
  cellPatches.value = {}
  try {
    const data = await api.spreadsheets.getById(id)
    activeItem.value = data
    cells.value = data.cells || {}
  } catch(e) {}
  loadingDetail.value = false
}

function updateCell(r: number, c: number, val: string) {
  if (!staffPerm.can('sectionSpreadsheetsEdit')) return
  const key = `${r},${c}`
  if(!cells.value[key]) cells.value[key] = { value: '' }
  cells.value[key].value = val
  cellPatches.value[key] = { ...cells.value[key] }
}

async function savePatches() {
  if (!staffPerm.can('sectionSpreadsheetsEdit')) return
  if(Object.keys(cellPatches.value).length === 0) return
  savingCells.value = true
  try {
    const patches = Object.keys(cellPatches.value).map(k => ({
      key: k, cell: cellPatches.value[k]
    }))
    await api.spreadsheets.patchCells(activeItem.value.id, { patches })
    cellPatches.value = {}
  } catch(e) {}
  savingCells.value = false
}

function formatDate(iso: string) {
  if(!iso) return ''
  return new Date(iso).toLocaleDateString()
}

onMounted(async () => {
  await staffPerm.refresh()
  if (!staffPerm.can('sectionSpreadsheetsView')) {
    await router.replace('/')
    return
  }
  loadList()
})
</script>

<template>
  <div class="flex flex-col w-full gap-4" :class="activeItem ? 'h-[calc(100vh-5rem)] lg:h-[calc(100vh-6.5rem)]' : ''">
    <div class="flex items-center justify-between shrink-0">
      <h1 class="text-2xl font-bold tracking-tight text-gray-900">Таблицы</h1>
      <button v-if="!activeItem && staffPerm.can('sectionSpreadsheetsEdit')" @click="showCreateModal = true" class="inline-flex items-center gap-2 bg-indigo-600 text-white px-4 py-2 rounded shadow-sm hover:bg-indigo-700 font-medium text-sm transition-colors">
        <Plus :size="16"/> Создать
      </button>
      <button v-if="activeItem" @click="activeItem = null" class="inline-flex items-center gap-2 text-gray-600 hover:text-gray-900 font-medium text-sm transition-colors">
        <ArrowLeft :size="16" /> Назад к списку
      </button>
    </div>

    <!-- LIST VIEW -->
    <div v-if="!activeItem">
      <div v-if="loadingList" class="flex justify-center py-10"><RefreshCw class="animate-spin text-indigo-500" /></div>
      <div v-else-if="items.length === 0" class="text-center py-12 bg-white rounded-lg border border-gray-200">
         <FileSpreadsheet :size="32" class="mx-auto text-gray-300 mb-3" />
         <p class="text-gray-500">Нет сохраненных таблиц</p>
      </div>
      <div v-else class="bg-white border text-left border-gray-200 rounded-lg shadow-sm overflow-hidden text-sm">
        <table class="w-full text-left">
          <thead class="bg-gray-50 border-b border-gray-200">
            <tr>
              <th class="px-4 py-3 font-semibold text-gray-900">Название</th>
              <th class="px-4 py-3 font-semibold text-gray-900 hidden md:table-cell">Тип</th>
              <th class="px-4 py-3 font-semibold text-gray-900 hidden sm:table-cell">Создал</th>
              <th class="px-4 py-3 font-semibold text-gray-900">Обновлено</th>
              <th class="px-4 py-3 text-right">Действия</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-100">
            <tr v-for="it in items" :key="it.id" class="hover:bg-gray-50 transition-colors cursor-pointer group" @click="openItem(it.id)">
               <td class="px-4 py-3 font-medium text-indigo-600">{{ it.name || 'Без названия' }}</td>
               <td class="px-4 py-3 hidden md:table-cell"><span :class="['px-2 py-0.5 rounded text-xs font-semibold', it.sourceKind === 1 ? 'bg-green-100 text-green-800' : 'bg-blue-100 text-blue-800']">{{ it.sourceKind === 1 ? 'Google Sheets' : 'Внутренняя' }}</span></td>
               <td class="px-4 py-3 hidden sm:table-cell text-gray-600">{{ it.createdByName }}</td>
               <td class="px-4 py-3 text-gray-500">{{ formatDate(it.updatedAt) }}</td>
               <td class="px-4 py-3 text-right">
                 <button v-if="staffPerm.can('sectionSpreadsheetsEdit')" @click.stop="deleteItem(it.id)" class="text-gray-400 hover:text-red-600 opacity-0 group-hover:opacity-100 transition-opacity p-1"><Trash2 :size="16"/></button>
               </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- DETAIL VIEW -->
    <div v-if="activeItem" class="flex flex-col flex-1 min-h-0">
       <div class="bg-white border border-gray-200 rounded-xl shadow-sm p-4 md:p-5 mb-3 flex flex-col sm:flex-row sm:items-center justify-between gap-3 shrink-0">
          <div>
            <h2 class="text-lg font-bold text-gray-900 flex items-center gap-2">
              <FileSpreadsheet :size="20" class="text-indigo-500"/> {{ activeItem.name }}
              <span :class="['px-2 py-0.5 rounded text-[10px] uppercase font-bold tracking-wider', activeItem.sourceKind === 1 ? 'bg-green-100 text-green-800' : 'bg-blue-100 text-blue-800']">{{ activeItem.sourceKind === 1 ? 'Google' : 'Local' }}</span>
            </h2>
            <p class="text-xs text-gray-500 mt-1">Создатель: {{ activeItem.createdByName }} • Изменено: {{ formatDate(activeItem.updatedAt) }}</p>
          </div>
          <div class="flex items-center gap-3">
             <button v-if="activeItem.sourceKind === 0 && staffPerm.can('sectionSpreadsheetsEdit')" @click="savePatches" :disabled="Object.keys(cellPatches).length === 0 || savingCells" class="inline-flex items-center gap-2 px-4 py-2 bg-indigo-600 text-white text-sm font-medium rounded hover:bg-indigo-700 disabled:opacity-50">
               <Save :size="16"/> {{ savingCells ? 'Сохранение...' : 'Сохранить изменения' }} <span v-if="Object.keys(cellPatches).length > 0">({{ Object.keys(cellPatches).length }})</span>
             </button>
             <a v-if="activeItem.sourceKind === 0" :href="`/api/Spreadsheets/${activeItem.id}/export`" class="inline-flex items-center gap-2 px-3 py-2 bg-gray-100 text-gray-700 hover:bg-gray-200 text-sm font-medium rounded transition-colors"><Download :size="16"/> Экспорт</a>
             <a v-if="activeItem.sourceKind === 1" :href="`https://docs.google.com/spreadsheets/d/${activeItem.googleSheetId}`" target="_blank" class="inline-flex items-center gap-2 px-4 py-2 bg-green-600 text-white hover:bg-green-700 text-sm font-medium rounded transition-colors">Открыть в Google</a>
          </div>
       </div>

       <!-- Local Excel Grid Wrapper -->
       <div v-if="activeItem.sourceKind === 0" class="bg-white border border-gray-200 rounded-lg shadow-sm flex-1 min-h-0 overflow-auto">
         <div class="inline-block min-w-full">
           <table class="w-full border-collapse">
             <thead class="sticky top-0 z-10">
                <tr>
                   <th class="w-10 bg-gray-100 border-r border-b border-gray-300"></th>
                   <th v-for="c in activeItem.cols" :key="'col'+c" class="min-w-[100px] w-32 bg-gray-100 border-r border-b border-gray-300 text-center font-normal text-xs text-gray-500 py-1 select-none">
                     {{ String.fromCharCode(64 + c) }}
                   </th>
                </tr>
             </thead>
             <tbody>
                <tr v-for="r in activeItem.rows" :key="'row'+r">
                   <td class="bg-gray-100 border-r border-b border-gray-300 text-center font-normal text-xs text-gray-500 py-1 select-none w-10 sticky left-0 z-[1]">
                     {{ r }}
                   </td>
                   <td v-for="c in activeItem.cols" :key="'cell'+r+'-'+c" class="border-r border-b border-gray-200 relative p-0 focus-within:ring-2 focus-within:ring-inset focus-within:ring-indigo-500 z-0 focus-within:z-10">
                     <input 
                        type="text" 
                        class="w-full h-full px-2 py-1.5 text-sm bg-transparent border-none focus:outline-none min-h-[32px]"
                        :readonly="!staffPerm.can('sectionSpreadsheetsEdit')"
                        :value="cells[`${r-1},${c-1}`]?.value || ''" 
                        @input="e => updateCell(r-1, c-1, (e.target as HTMLInputElement).value)"
                     />
                   </td>
                </tr>
             </tbody>
           </table>
         </div>
       </div>
       
       <!-- Google Embed -->
       <div v-if="activeItem.sourceKind === 1" class="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden flex-1 min-h-0">
          <iframe :src="`https://docs.google.com/spreadsheets/d/${activeItem.googleSheetId}/edit?widget=true`" class="w-full h-full border-none"></iframe>
       </div>
    </div>
    
    <!-- Create Modal -->
    <Teleport to="body">
       <div v-if="showCreateModal" class="fixed inset-0 z-50 flex justify-center items-center bg-gray-900/50 backdrop-blur-sm p-4">
         <div class="bg-white rounded-xl shadow-xl w-full max-w-md overflow-hidden">
           <div class="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
              <h3 class="text-lg font-bold text-gray-900">Создать таблицу</h3>
              <button @click="showCreateModal = false" class="text-gray-400 hover:text-gray-600"><X :size="20"/></button>
           </div>
           <div class="p-6 space-y-4">
              <div>
                 <label class="block text-sm font-medium text-gray-700 mb-1">Название</label>
                 <input v-model="newForm.name" class="w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:border-indigo-500"/>
              </div>
              <div>
                 <label class="block text-sm font-medium text-gray-700 mb-2">Тип</label>
                 <div class="flex gap-4">
                    <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
                       <input type="radio" v-model="newForm.sourceKind" :value="0" class="accent-indigo-600"/> Внутренняя (сетка)
                    </label>
                    <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
                       <input type="radio" v-model="newForm.sourceKind" :value="1" class="accent-indigo-600"/> Google Sheets
                    </label>
                 </div>
              </div>
              <template v-if="newForm.sourceKind === 1">
                 <div>
                    <label class="block text-sm font-medium text-gray-700 mb-1">Ссылка на Google Таблицу</label>
                    <input v-model="newForm.googleUrl" placeholder="https://docs.google.com/spreadsheets/d/..." class="w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:border-indigo-500"/>
                 </div>
              </template>
              <template v-if="newForm.sourceKind === 0">
                 <div class="flex gap-4">
                    <div class="flex-1">
                       <label class="block text-sm font-medium text-gray-700 mb-1">Строки</label>
                       <input type="number" v-model="newForm.rows" class="w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:border-indigo-500"/>
                    </div>
                    <div class="flex-1">
                       <label class="block text-sm font-medium text-gray-700 mb-1">Колонки</label>
                       <input type="number" v-model="newForm.cols" class="w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:border-indigo-500"/>
                    </div>
                 </div>
              </template>
           </div>
           <div class="px-6 py-4 bg-gray-50 border-t border-gray-100 flex justify-end gap-3">
              <button @click="showCreateModal = false" class="px-4 py-2 text-sm font-medium text-gray-600 hover:text-gray-900">Отмена</button>
              <button @click="createItem" class="px-4 py-2 text-sm font-medium text-white bg-indigo-600 rounded hover:bg-indigo-700">Создать</button>
           </div>
         </div>
       </div>
    </Teleport>
  </div>
</template>
