<template>
  <img
    :src="url"
    :alt="name"
    class="inline-block object-contain"
    :style="style"
  />
</template>

<script setup lang="ts">
const props = defineProps<{
  url: string
  size?: number | string
}>()

const sizePx = computed(() => {
  const s = props.size
  if (s == null) return 20
  return typeof s === 'number' ? s : parseInt(s, 10) || 20
})

const style = computed(() => ({
  width: `${sizePx.value}px`,
  height: `${sizePx.value}px`,
  verticalAlign: 'middle',
}))

const name = computed(() => {
  const parts = props.url.split('/')
  return parts[parts.length - 1] || 'tux'
})
</script>
