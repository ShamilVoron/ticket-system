<script setup lang="ts">
withDefaults(
  defineProps<{
    rows?: number
  }>(),
  { rows: 6 },
)
</script>

<template>
  <div class="space-y-3" role="status" aria-label="Загрузка">
    <div
      v-for="i in rows"
      :key="i"
      class="bg-white dark:bg-zinc-900 rounded-lg border border-gray-200 dark:border-zinc-700 p-4 animate-pulse"
    >
      <div class="flex items-start gap-3">
        <div class="w-12 h-4 bg-gray-200 dark:bg-zinc-700 rounded" />
        <div class="flex-1 space-y-2">
          <div class="h-4 bg-gray-200 dark:bg-zinc-700 rounded w-3/4" />
          <div class="h-3 bg-gray-100 dark:bg-zinc-800 rounded w-1/2" />
        </div>
        <div class="w-16 h-5 bg-gray-200 dark:bg-zinc-700 rounded-full" />
      </div>
    </div>
  </div>
</template>
