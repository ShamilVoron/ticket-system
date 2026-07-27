<script setup lang="ts">
import { RefreshCw, FileText } from 'lucide-vue-next'
import type { CreateFieldReportRequest, Ticket } from '~/types'

definePageMeta({
  layout: 'field',
  middleware: 'field',
})

const route = useRoute()
const router = useRouter()
const api = useApi()
const auth = useAuthStore()
const pageHeader = usePageHeader()
const toast = useToast()

const ticketId = computed(() => Number(route.params.ticketId))

const ticket = ref<Ticket | null>(null)
const loading = ref(true)
const submitting = ref(false)

const reportActionTypes = ['Ремонт', 'Монтаж', 'Замена', 'Осмотр / Диагностика', 'Доставка', 'Другое']
const reportEquipStatuses = ['В работе', 'Требует ремонта', 'Списано', 'Подмена']

const form = reactive<CreateFieldReportRequest>({
  engineerName: '',
  visitDate: new Date().toISOString().slice(0, 16),
  actionType: 'Осмотр / Диагностика',
  equipmentType: '',
  equipmentSerial: '',
  equipmentStatus: 'В работе',
  workDone: '',
  transferredTo: '',
})

async function loadTicket() {
  if (!ticketId.value || Number.isNaN(ticketId.value)) return
  loading.value = true
  try {
    ticket.value = await api.tickets.getById(ticketId.value)
    form.engineerName = auth.fullName || ''
    form.equipmentType = ticket.value?.repairEquipmentType || ''
    pageHeader.set(`Акт #${ticketId.value}`, true)
  } catch {
    toast.error('Не удалось загрузить заявку')
    ticket.value = null
  } finally {
    loading.value = false
  }
}

async function submit() {
  if (!form.workDone.trim() || submitting.value) return
  submitting.value = true
  try {
    const payload: CreateFieldReportRequest = {
      engineerName: form.engineerName || auth.fullName,
      visitDate: form.visitDate || undefined,
      actionType: form.actionType,
      equipmentType: form.equipmentType || ticket.value?.repairEquipmentType || '',
      equipmentSerial: form.equipmentSerial,
      equipmentStatus: form.equipmentStatus,
      workDone: form.workDone.trim(),
      transferredTo: form.transferredTo,
    }
    await api.tickets.addReport(ticketId.value, payload)
    toast.success('Акт выезда сохранён')
    await router.replace(`/field/tickets/${ticketId.value}`)
  } catch {
    toast.error('Не удалось сохранить акт')
  } finally {
    submitting.value = false
  }
}

onMounted(() => {
  void loadTicket()
})

onBeforeUnmount(() => {
  pageHeader.clear()
})

watch(ticketId, () => {
  void loadTicket()
})
</script>

<template>
  <div class="max-w-lg mx-auto space-y-4">
    <div v-if="loading" class="flex items-center justify-center py-20">
      <RefreshCw :size="28" class="animate-spin text-indigo-600" />
    </div>

    <template v-else-if="ticket">
      <div class="brutal-card p-4 space-y-1">
        <div class="flex items-center gap-2 text-orange-600">
          <FileText :size="18" />
          <span class="text-xs font-bold uppercase tracking-wider">Акт выезда</span>
        </div>
        <p class="font-semibold text-gray-900 dark:text-gray-100">
          #{{ ticket.id }} · {{ ticket.title }}
        </p>
      </div>

      <form class="brutal-card p-4 space-y-4" @submit.prevent="submit">
        <div class="grid grid-cols-1 gap-4">
          <div>
            <label class="block text-[11px] font-bold text-gray-400 uppercase tracking-wider mb-1.5">
              Инженер
            </label>
            <input v-model="form.engineerName" type="text" class="brutal-input min-h-[48px]" />
          </div>

          <div>
            <label class="block text-[11px] font-bold text-gray-400 uppercase tracking-wider mb-1.5">
              Дата и время
            </label>
            <input
              v-model="form.visitDate"
              type="datetime-local"
              class="brutal-input min-h-[48px]"
            />
          </div>

          <div>
            <label class="block text-[11px] font-bold text-gray-400 uppercase tracking-wider mb-1.5">
              Тип работ
            </label>
            <select v-model="form.actionType" class="brutal-select min-h-[48px]">
              <option v-for="t in reportActionTypes" :key="t" :value="t">{{ t }}</option>
            </select>
          </div>

          <div>
            <label class="block text-[11px] font-bold text-gray-400 uppercase tracking-wider mb-1.5">
              Статус оборудования
            </label>
            <select v-model="form.equipmentStatus" class="brutal-select min-h-[48px]">
              <option v-for="s in reportEquipStatuses" :key="s" :value="s">{{ s }}</option>
            </select>
          </div>

          <div>
            <label class="block text-[11px] font-bold text-gray-400 uppercase tracking-wider mb-1.5">
              Тип оборудования
            </label>
            <input
              v-model="form.equipmentType"
              type="text"
              placeholder="Напр. принтер, моноблок…"
              class="brutal-input min-h-[48px]"
            />
          </div>

          <div>
            <label class="block text-[11px] font-bold text-gray-400 uppercase tracking-wider mb-1.5">
              Серийный номер
            </label>
            <input
              v-model="form.equipmentSerial"
              type="text"
              placeholder="Если применимо…"
              class="brutal-input min-h-[48px]"
            />
          </div>

          <div>
            <label class="block text-[11px] font-bold text-gray-400 uppercase tracking-wider mb-1.5">
              Кому передано
            </label>
            <input
              v-model="form.transferredTo"
              type="text"
              placeholder="ФИО сотрудника клиента…"
              class="brutal-input min-h-[48px]"
            />
          </div>

          <div>
            <label class="block text-[11px] font-bold text-gray-400 uppercase tracking-wider mb-1.5">
              Что сделано *
            </label>
            <textarea
              v-model="form.workDone"
              rows="5"
              required
              placeholder="Опишите выполненные работы…"
              class="brutal-input min-h-[120px] resize-y"
            />
          </div>
        </div>

        <button
          type="submit"
          class="brutal-btn-primary w-full min-h-[52px] text-[15px]"
          :disabled="!form.workDone.trim() || submitting"
        >
          {{ submitting ? 'Сохранение…' : 'Сохранить акт' }}
        </button>
      </form>
    </template>

    <div v-else class="brutal-card p-8 text-center text-gray-500">
      Заявка не найдена
    </div>
  </div>
</template>
