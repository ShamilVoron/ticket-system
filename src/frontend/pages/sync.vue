<script setup lang="ts">
import { RefreshCw, Play, CheckCircle, AlertCircle, FileText } from 'lucide-vue-next'

const api = useApi()

const syncKey = ref('')
const tsvData = ref('')
const processing = ref(false)
const result = ref<any>(null)
const dryRun = ref(true)

const mapping = ref({
  companyName: 0,
  companyCode: 1,
  objectName: 2,
  objectCode: 3,
  maintenanceStatus: 4,
  maintenanceComment: 5,
  directoriesOwner: 6,
  sysAdmin: 7,
  serverServices: 8
})

const parsedRows = computed(() => {
  if (!tsvData.value.trim()) return []
  const lines = tsvData.value.trim().split('\n')
  return lines.map(line => {
    const cols = line.split('\t').map(c => c.trim())
    return {
      companyName: cols[mapping.value.companyName] || '',
      companyCode: cols[mapping.value.companyCode] || '',
      objectName: cols[mapping.value.objectName] || '',
      objectCode: cols[mapping.value.objectCode] || '',
      maintenanceStatus: cols[mapping.value.maintenanceStatus] || '',
      maintenanceComment: cols[mapping.value.maintenanceComment] || '',
      directoriesOwner: cols[mapping.value.directoriesOwner] || '',
      sysAdmin: cols[mapping.value.sysAdmin] || '',
      serverServices: cols[mapping.value.serverServices] || ''
    }
  })
})

const isValid = computed(() => {
  return syncKey.value.length > 0 && parsedRows.value.length > 0
})

async function runSync() {
  if (!isValid.value) return
  processing.value = true
  result.value = null
  
  try {
    const request = {
      source: 'web_sync_ui',
      dryRun: dryRun.value,
      rows: parsedRows.value
    }
    
    result.value = await api.googleSync.syncCompaniesObjects(request, syncKey.value)
  } catch (e: any) {
    result.value = {
      error: e.response?.data || e.message || 'Произошла ошибка при синхронизации'
    }
  } finally {
    processing.value = false
  }
}
</script>

<template>
  <div class="space-y-6 max-w-5xl mx-auto w-full">
    <div class="flex items-center justify-between">
      <h1 class="text-2xl font-bold tracking-tight text-gray-900">Синхронизация объектов (TSV)</h1>
    </div>

    <div class="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
      <div class="p-6 space-y-6">
        <!-- Settings -->
        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">API Ключ синхронизации (X-Sync-Key)</label>
            <input v-model="syncKey" type="password" placeholder="Введите ключ..." class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:border-indigo-500" />
            <p class="text-xs text-gray-500 mt-1">Обязательное поле для авторизации</p>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Режим работы</label>
            <div class="flex items-center gap-4 mt-2">
              <label class="flex items-center gap-2 text-sm cursor-pointer">
                <input type="radio" v-model="dryRun" :value="true" class="accent-indigo-600">
                <span>Проверка (Dry Run)</span>
              </label>
              <label class="flex items-center gap-2 text-sm cursor-pointer">
                <input type="radio" v-model="dryRun" :value="false" class="accent-red-600">
                <span class="text-red-700 font-medium">Боевая запись</span>
              </label>
            </div>
          </div>
        </div>

        <!-- Mapping Configuration -->
        <div class="border rounded-lg bg-gray-50 p-4">
          <h3 class="text-sm font-semibold text-gray-800 mb-3">Соответствие колонок TSV (индексы с 0)</h3>
          <div class="grid grid-cols-3 sm:grid-cols-5 gap-3 text-sm">
            <div><label class="text-xs text-gray-500 block">Компания</label><input type="number" v-model="mapping.companyName" class="w-16 border px-2 py-1 rounded" min="0"></div>
            <div><label class="text-xs text-gray-500 block">Код комп.</label><input type="number" v-model="mapping.companyCode" class="w-16 border px-2 py-1 rounded" min="0"></div>
            <div><label class="text-xs text-gray-500 block">Объект</label><input type="number" v-model="mapping.objectName" class="w-16 border px-2 py-1 rounded" min="0"></div>
            <div><label class="text-xs text-gray-500 block">Код об.</label><input type="number" v-model="mapping.objectCode" class="w-16 border px-2 py-1 rounded" min="0"></div>
            <div><label class="text-xs text-gray-500 block">Статус ТО</label><input type="number" v-model="mapping.maintenanceStatus" class="w-16 border px-2 py-1 rounded" min="0"></div>
          </div>
        </div>

        <!-- TSV Input -->
        <div>
          <div class="flex items-center justify-between mb-1">
             <label class="block text-sm font-medium text-gray-700">Вставьте данные (Копировать-Вставить из Excel)</label>
             <span class="text-xs text-gray-500">{{ parsedRows.length }} строк распознано</span>
          </div>
          <textarea v-model="tsvData" rows="8" placeholder="Компания A\tКод1\tОбъект A\tКодОбъект1\tСтатус..." class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm font-mono whitespace-pre focus:outline-none focus:border-indigo-500 resize-none"></textarea>
        </div>

        <!-- Preview -->
        <div v-if="parsedRows.length > 0" class="border border-gray-200 rounded-lg overflow-hidden">
           <div class="bg-gray-50 px-4 py-2 border-b border-gray-200"><h4 class="text-sm font-semibold text-gray-700">Предпросмотр данных (первые 3 строки)</h4></div>
           <div class="overflow-x-auto">
             <table class="w-full text-left text-sm whitespace-nowrap">
               <thead>
                 <tr class="bg-white border-b">
                   <th class="px-3 py-2 font-medium text-gray-600">Компания</th>
                   <th class="px-3 py-2 font-medium text-gray-600">Код Комп.</th>
                   <th class="px-3 py-2 font-medium text-gray-600">Объект</th>
                   <th class="px-3 py-2 font-medium text-gray-600">Код Об.</th>
                   <th class="px-3 py-2 font-medium text-gray-600">Статус</th>
                 </tr>
               </thead>
               <tbody class="divide-y divide-gray-100">
                 <tr v-for="(row, i) in parsedRows.slice(0, 3)" :key="i" class="hover:bg-gray-50">
                   <td class="px-3 py-1.5">{{ row.companyName }}</td>
                   <td class="px-3 py-1.5 text-gray-500">{{ row.companyCode }}</td>
                   <td class="px-3 py-1.5">{{ row.objectName }}</td>
                   <td class="px-3 py-1.5 text-gray-500">{{ row.objectCode }}</td>
                   <td class="px-3 py-1.5">{{ row.maintenanceStatus }}</td>
                 </tr>
               </tbody>
             </table>
           </div>
        </div>
      </div>
      <div class="bg-gray-50 px-6 py-4 border-t border-gray-200 flex justify-end">
        <button @click="runSync" :disabled="!isValid || processing" class="inline-flex items-center gap-2 bg-indigo-600 text-white px-6 py-2.5 rounded shadow-sm hover:bg-indigo-700 font-medium disabled:opacity-50 transition-colors">
          <RefreshCw v-if="processing" :size="16" class="animate-spin" />
          <Play v-else :size="16"/>
          {{ processing ? 'Обработка...' : 'Запустить синхронизацию' }}
        </button>
      </div>
    </div>

    <!-- Results -->
    <div v-if="result" class="bg-white rounded-xl border border-gray-200 shadow-sm p-6">
       <div v-if="result.error" class="flex items-start gap-3 text-red-700 bg-red-50 p-4 rounded-lg border border-red-200">
         <AlertCircle :size="24" class="flex-shrink-0 mt-0.5" />
         <div>
           <h3 class="font-bold">Ошибка синхронизации</h3>
           <pre class="mt-1 text-sm whitespace-pre-wrap">{{ result.error.error || result.error || 'Неизвестная ошибка' }}</pre>
         </div>
       </div>
       <div v-else class="space-y-4">
         <div class="flex items-center gap-2 text-green-700 mb-4">
           <CheckCircle :size="24"/>
           <h3 class="text-xl font-bold">Успешно завершено <span v-if="result.dryRun" class="text-sm font-normal text-amber-600 bg-amber-100 px-2 py-0.5 rounded ml-2">Режим: Проверка</span></h3>
         </div>
         <div class="grid grid-cols-2 sm:grid-cols-4 gap-4">
           <div class="bg-gray-50 p-3 rounded border border-gray-100">
             <div class="text-[10px] text-gray-500 uppercase tracking-widest font-bold mb-1">Строк</div>
             <div class="text-2xl font-bold text-gray-900">{{ result.rowsTotal }}</div>
           </div>
           <div class="bg-green-50 p-3 rounded border border-green-100">
             <div class="text-[10px] text-green-700 text-opacity-80 uppercase tracking-widest font-bold mb-1">Компаний созд.</div>
             <div class="text-2xl font-bold text-green-800">{{ result.createdCompanies }}</div>
           </div>
           <div class="bg-indigo-50 p-3 rounded border border-indigo-100">
             <div class="text-[10px] text-indigo-700 text-opacity-80 uppercase tracking-widest font-bold mb-1">Компаний обн.</div>
             <div class="text-2xl font-bold text-indigo-800">{{ result.updatedCompanies }}</div>
           </div>
           <div class="bg-red-50 p-3 rounded border border-red-100">
             <div class="text-[10px] text-red-700 text-opacity-80 uppercase tracking-widest font-bold mb-1">Компаний откл.</div>
             <div class="text-2xl font-bold text-red-800">{{ result.deactivatedCompanies }}</div>
           </div>
           <div class="bg-gray-50 p-3 rounded border border-gray-100">
             <div class="text-[10px] text-gray-500 uppercase tracking-widest font-bold mb-1">Пропущено</div>
             <div class="text-2xl font-bold text-gray-900">{{ result.skippedRows }}</div>
           </div>
           <div class="bg-green-50 p-3 rounded border border-green-100">
             <div class="text-[10px] text-green-700 text-opacity-80 uppercase tracking-widest font-bold mb-1">Объектов созд.</div>
             <div class="text-2xl font-bold text-green-800">{{ result.createdObjects }}</div>
           </div>
           <div class="bg-indigo-50 p-3 rounded border border-indigo-100">
             <div class="text-[10px] text-indigo-700 text-opacity-80 uppercase tracking-widest font-bold mb-1">Объектов обн.</div>
             <div class="text-2xl font-bold text-indigo-800">{{ result.updatedObjects }}</div>
           </div>
           <div class="bg-red-50 p-3 rounded border border-red-100">
             <div class="text-[10px] text-red-700 text-opacity-80 uppercase tracking-widest font-bold mb-1">Объектов откл.</div>
             <div class="text-2xl font-bold text-red-800">{{ result.deactivatedObjects }}</div>
           </div>
         </div>
         
         <div v-if="result.errors && result.errors.length > 0" class="mt-6">
           <h4 class="text-sm font-bold text-red-700 mb-2">Ошибки валидации:</h4>
           <ul class="list-disc pl-5 text-sm text-red-600 space-y-1 bg-red-50 p-4 rounded-lg border border-red-100">
             <li v-for="(err, i) in result.errors.slice(0, 50)" :key="i">{{ err }}</li>
             <li v-if="result.errors.length > 50" class="font-medium">... и еще {{ result.errors.length - 50 }} ошибок</li>
           </ul>
         </div>
       </div>
    </div>
  </div>
</template>
