<template>
  <div class="min-h-[100dvh] w-full max-w-none flex bg-[#F8F9FA] dark:bg-[#0f0f10]">
    <slot />
  </div>
</template>

<script setup lang="ts">
const theme = useTheme()
onMounted(() => theme.init())
</script>
