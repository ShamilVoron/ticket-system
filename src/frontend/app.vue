<script setup>
const auth = useAuthStore()

onMounted(() => {
  auth.hydrate()
})
</script>

<template>
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
</template>
