<script setup lang="ts">
const props = defineProps<{
  status: string
  colorClass?: string
}>()

function defaultStatusClass(status: string): string {
  const s = (status || '').trim()
  if (s === 'Открыт') return 'brutal-badge-green'
  if (s === 'В работе') return 'brutal-badge-yellow'
  if (s === 'Закрыт' || s === 'Решён' || s === 'Решено') {
    return 'brutal-badge bg-gray-100 text-gray-600 border-gray-200'
  }
  if (
    s === 'Ожидание' ||
    s === 'Ожидание клиента' ||
    s === 'Ожидание запчастей' ||
    s === 'Отложен' ||
    s === 'На согласовании'
  ) {
    return 'brutal-badge bg-orange-50 text-orange-700 border-orange-200'
  }
  return 'brutal-badge bg-gray-100 text-gray-700 border-gray-200'
}

const badgeClass = computed(() => {
  const custom = (props.colorClass || '').trim()
  if (custom) {
    // Allow either full brutal-badge* class or plain Tailwind color classes
    return custom.includes('brutal-badge') ? custom : `brutal-badge ${custom}`
  }
  return defaultStatusClass(props.status)
})
</script>

<template>
  <span :class="badgeClass">{{ status || '—' }}</span>
</template>
