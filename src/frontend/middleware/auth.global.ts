export default defineNuxtRouteMiddleware(async (to) => {
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

  // Field engineer → simplified /field UI (keep /auth and /profile)
  if (auth.isFieldEngineer) {
    if (to.path.startsWith('/field') || to.path.startsWith('/auth') || to.path === '/profile') {
      return
    }

    const ticketMatch = to.path.match(/^\/tickets\/(\d+)$/)
    if (ticketMatch) {
      return navigateTo(`/field/tickets/${ticketMatch[1]}`)
    }

    return navigateTo('/field')
  }

  // Client → portal (tickets create/view still allowed)
  if (auth.isClient) {
    if (to.path === '/' || to.path === '/my') {
      return navigateTo('/portal')
    }

    const allowed =
      to.path.startsWith('/portal') ||
      to.path.startsWith('/tickets') ||
      to.path === '/profile'

    if (!allowed) {
      return navigateTo('/portal')
    }
  }

  // Super-admin primary setup: redirect until onboarding is completed
  // Разрешаем /employees и /settings во время мастера (ссылки «Открыть …» на шагах 2–3)
  if (
    import.meta.client &&
    auth.isSuperAdmin &&
    !to.path.startsWith('/onboarding') &&
    !to.path.startsWith('/auth') &&
    !to.path.startsWith('/employees') &&
    !to.path.startsWith('/settings')
  ) {
    try {
      const { branding, load } = useSystemBranding()
      // force: иначе кэш после skip/audit держит onboardingCompleted=true
      await load(true)
      const skipped = localStorage.getItem('onboarding_skip') === '1'
      if (!branding.value.onboardingCompleted && !skipped) {
        return navigateTo('/onboarding')
      }
    } catch {
      // ignore — don't block navigation
    }
  }
})
