<script setup>
const auth = useAuthStore()
const theme = useTheme()

onMounted(() => {
  auth.hydrate()
  theme.init()
})
</script>

<template>
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
</template>
