<script setup lang="ts">
defineProps<{
  title: string
  description?: string
}>()
</script>

<template>
  <div class="text-center py-16 px-6">
    <div class="w-14 h-14 bg-gray-50 dark:bg-zinc-800/60 rounded-full flex items-center justify-center mx-auto mb-4">
      <slot name="icon" />
    </div>
    <h3 class="text-lg font-semibold text-gray-900 dark:text-gray-100 mb-1">{{ title }}</h3>
    <p v-if="description" class="text-sm text-gray-500 dark:text-gray-400 max-w-sm mx-auto">
      {{ description }}
    </p>
    <div v-if="$slots.cta" class="mt-5 flex justify-center">
      <slot name="cta" />
    </div>
  </div>
</template>
