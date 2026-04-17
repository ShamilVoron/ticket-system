export default defineNuxtRouteMiddleware(() => {
  const auth = useAuthStore()
  if (import.meta.client) {
    auth.hydrate()
  }

  if (!auth.isLoggedIn) {
    return navigateTo('/auth/login')
  }

  if (!auth.isStaff) {
    return navigateTo('/')
  }
})
