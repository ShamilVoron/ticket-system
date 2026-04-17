import { defineStore } from 'pinia'
import type { AuthResponse, AuthState } from '~/types'

const STORAGE_KEY = 'ticket-system-auth'

const STAFF_ROLES = [
  'support_l1', 'support_l2', 'developer', 'field_engineer', 'accountant',
  'head_engineers', 'head_support', 'head_dev', 'sysadmin',
  'coordinator', 'director', 'super_admin', 'procurement', 'head_repair', 'agent',
]

const ADMIN_ROLES = ['super_admin', 'coordinator', 'director']

export const useAuthStore = defineStore('auth', {
  state: (): AuthState => ({
    token: '',
    userId: '',
    fullName: '',
    email: '',
    role: '',
    avatarUrl: '',
  }),

  getters: {
    isLoggedIn: (s) => !!s.token,
    isStaff: (s) => STAFF_ROLES.includes(s.role),
    isAdmin: (s) => ADMIN_ROLES.includes(s.role),
    isSuperAdmin: (s) => s.role === 'super_admin',
    isClient: (s) => s.role === 'client',
    /** Выездной инженер — отдельный набор прав в навигации и в карточке заявки. */
    isFieldEngineer: (s) => s.role === 'field_engineer',

    roleLabel: (s) => {
      const map: Record<string, string> = {
        client: 'Клиент',
        support_l1: 'Поддержка L1',
        support_l2: 'Поддержка L2',
        developer: 'Разработчик',
        field_engineer: 'Выездной инженер',
        accountant: 'Бухгалтерия',
        head_engineers: 'Нач. инженеров',
        head_support: 'Нач. поддержки',
        head_dev: 'Нач. разработки',
        sysadmin: 'Сисадмин',
        coordinator: 'Координатор',
        director: 'Директор',
        super_admin: 'Супер Админ',
        procurement: 'Закупки',
        head_repair: 'Нач. ремонта',
        agent: 'Агент',
      }
      return map[s.role] || s.role
    },

    roleColor: (s) => {
      const map: Record<string, string> = {
        super_admin: 'brutal-badge-purple',
        coordinator: 'brutal-badge-pink',
        director: 'brutal-badge-cyan',
        support_l2: 'brutal-badge-green',
        support_l1: 'brutal-badge-yellow',
        developer: 'brutal-badge-cyan',
        field_engineer: 'brutal-badge-green',
        sysadmin: 'brutal-badge-purple',
      }
      return map[s.role] || 'brutal-badge bg-brutal-surface-elevated border-brutal-border text-brutal-secondary'
    },
  },

  actions: {
    hydrate() {
      if (import.meta.server) return
      try {
        const raw = localStorage.getItem(STORAGE_KEY)
        if (raw) {
          const data = JSON.parse(raw) as AuthState
          Object.assign(this, data)
        }
      } catch {
        localStorage.removeItem(STORAGE_KEY)
      }
    },

    setAuth(data: AuthResponse) {
      this.token = data.token
      this.userId = data.userId
      this.fullName = data.fullName
      this.email = data.email
      this.role = data.role
      this.avatarUrl = data.avatarUrl || ''
      if (import.meta.client) {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(this.$state))
      }
    },

    logout() {
      this.$reset()
      if (import.meta.client) {
        localStorage.removeItem(STORAGE_KEY)
      }
    },
  },
})
