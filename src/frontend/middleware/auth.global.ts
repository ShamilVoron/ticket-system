export default defineNuxtRouteMiddleware((to) => {
  const auth = useAuthStore()
  
  // Allow access to auth pages
  if (to.path.startsWith('/auth')) {
    return
  }
  
  // Check if user is logged in
  if (!auth.isLoggedIn) {
    auth.hydrate()
    if (!auth.isLoggedIn) {
      return navigateTo('/auth/login')
    }
  }
})
