import { buildMergedPermState, defaultPermissionForRole } from '~/config/staffPermissionCatalog'

function normRole(r: string): string {
  return String(r || '')
    .trim()
    .toLowerCase()
}

/**
 * Права текущего пользователя из Employees.PermissionsJson + дефолты по роли (код + настройки из БД).
 */
export function useStaffPermissions() {
  const auth = useAuthStore()
  const api = useApi()

  const merged = ref<Record<string, boolean>>({})
  const loaded = ref(false)
  /** Глобальные дефолты по ролям (SystemSettings), общий кэш для страницы сотрудников и can(). */
  const roleDefaultsMap = useState<Record<string, Record<string, boolean>>>(
    'ticket-system-role-permission-defaults',
    () => ({}),
  )

  async function refresh() {
    if (!import.meta.client || !auth.isLoggedIn) {
      merged.value = {}
      loaded.value = false
      return
    }
    if (!auth.isStaff) {
      merged.value = {}
      loaded.value = true
      return
    }
    let perRole: Record<string, boolean> | undefined
    try {
      const map = await api.systemSettings.getRolePermissionDefaults()
      roleDefaultsMap.value = map && typeof map === 'object' ? map : {}
      const r = normRole(auth.role)
      perRole = roleDefaultsMap.value[r]
    } catch {
      roleDefaultsMap.value = {}
    }
    try {
      const me = await api.employees.getMe()
      merged.value = buildMergedPermState(String(me?.permissionsJson || ''), auth.role, perRole)
    } catch {
      merged.value = buildMergedPermState('{}', auth.role, perRole)
    }
    loaded.value = true
  }

  function can(key: string): boolean {
    if (!auth.isStaff) return defaultPermissionForRole(key, auth.role)
    if (!loaded.value) return defaultPermissionForRole(key, auth.role)
    if (Object.prototype.hasOwnProperty.call(merged.value, key)) {
      return merged.value[key]!
    }
    return defaultPermissionForRole(key, auth.role)
  }

  if (import.meta.client) {
    watch(
      () => auth.token,
      (t) => {
        if (t) void refresh()
        else {
          merged.value = {}
          loaded.value = false
          roleDefaultsMap.value = {}
        }
      },
      { immediate: true },
    )
  }

  return { merged, loaded, refresh, can, roleDefaultsMap }
}
