import type { AuthResponse, LoginRequest } from '~/types'
import { isLoopbackHost, resolvePublicApiBaseUrl } from '~/utils/resolvePublicApiBaseUrl'

/**
 * Старый бандл / payload с `http://localhost:5000` + страница на :3000 (в т.ч. SSH -L 3000) — разные origin,
 * loopback :5000 в браузере почти всегда неверный хост. Принудительно тот же origin, что у страницы.
 */
function browserSafeFetchUrl(baseURL: string, path: string): string {
  const url = `${baseURL}${path}`
  if (typeof window === 'undefined') return url
  try {
    const u = new URL(url, window.location.origin)
    if (u.origin !== window.location.origin && isLoopbackHost(u.hostname)) {
      return path.startsWith('/') ? path : `/${path}`
    }
  } catch {
    /* ok */
  }
  return url
}

export function useApi() {
  const auth = useAuthStore()
  const config = useRuntimeConfig()
  
  const getBaseURL = () =>
    resolvePublicApiBaseUrl(config.public.apiBaseUrl as string | undefined)
  
  async function fetch<T>(path: string, opts: any = {}): Promise<T> {
    if (import.meta.client) {
      auth.hydrate()
    }
    const baseURL = getBaseURL()
    const requestUrl = browserSafeFetchUrl(baseURL, path)
    const isFormData =
      typeof FormData !== 'undefined' &&
      opts?.body &&
      opts.body instanceof FormData

    // IMPORTANT: don't set Content-Type for FormData (browser must add boundary)
    const headers: Record<string, string> = {
      ...(isFormData ? {} : { 'Content-Type': 'application/json' }),
      ...(opts.headers || {}),
    }
    
    if (auth.token) {
      headers['Authorization'] = `Bearer ${auth.token}`
    }
    
    try {
      return await $fetch<T>(requestUrl, {
        ...opts,
        headers,
      })
    } catch (error: any) {
      if (error.response?.status === 401) {
        auth.logout()
        navigateTo('/auth/login')
      }
      throw error
    }
  }
  
  // Auth API
  const authApi = {
    login: (data: LoginRequest) => 
      fetch<AuthResponse>('/api/Auth/login', { method: 'POST', body: data }),
    
    register: (data: any) => 
      fetch<AuthResponse>('/api/Auth/register', { method: 'POST', body: data }),

    /** Только role=client: CompanyId для CreateTicket и подпись организации. */
    ticketContext: () =>
      fetch<{ companyId: number | null; companyName: string | null }>('/api/Auth/ticket-context'),
  }
  
  // Tickets API
  const ticketsApi = {
    getAll: () => 
      fetch<any[]>('/api/Tickets'),
    
    getPaged: (params: {
      page?: number
      pageSize?: number
      search?: string
      sortKey?: string
      sortOrder?: string
      statuses?: string[]
      departments?: string[]
      assignees?: string[]
      clientNames?: string[]
    }) => {
      const query = new URLSearchParams()
      if (params.page != null) query.set('page', String(params.page))
      if (params.pageSize != null) query.set('pageSize', String(params.pageSize))
      if (params.search != null) query.set('search', params.search)
      if (params.sortKey != null) query.set('sortKey', params.sortKey)
      if (params.sortOrder != null) query.set('sortOrder', params.sortOrder)
      if (params.statuses) params.statuses.forEach(v => query.append('statuses', v))
      if (params.departments) params.departments.forEach(v => query.append('departments', v))
      if (params.assignees) params.assignees.forEach(v => query.append('assignees', v))
      if (params.clientNames) params.clientNames.forEach(v => query.append('clientNames', v))
      return fetch<{ items: any[]; totalCount: number; page: number; pageSize: number }>(`/api/Tickets/paged?${query}`)
    },

    getStats: () =>
      fetch<{ totalToday: number; openToday: number; inProgressToday: number; repairToday: number }>('/api/Tickets/stats'),
    
    getById: (id: number) =>
      fetch<any>(`/api/Tickets/${id}`, { cache: 'no-store' }),
    
    create: (data: any) => 
      fetch<any>('/api/Tickets', { method: 'POST', body: data }),
    
    updateStatus: (id: number, status: string) => 
      fetch(`/api/Tickets/${id}/status`, { method: 'PATCH', body: { status } }),
    
    updateAssignee: (id: number, assignee: string, assignees?: string[]) => 
      fetch(`/api/Tickets/${id}/assignee`, { method: 'PATCH', body: { assignee, assignees } }),
    
    updateTitle: (id: number, title: string, alternativeTitle?: string) => 
      fetch(`/api/Tickets/${id}/title`, { method: 'PATCH', body: { title, alternativeTitle } }),
    
    updateProblem: (id: number, problem: string) =>
      fetch(`/api/Tickets/${id}/problem`, { method: 'PATCH', body: { problem } }),
    
    updateLinks: (id: number, taskLinksJson: string) => 
      fetch(`/api/Tickets/${id}/links`, { method: 'PATCH', body: { taskLinksJson } }),
    
    updateFields: (id: number, fields: { priority?: string; department?: string; requestType?: string }) => 
      fetch(`/api/Tickets/${id}/fields`, { method: 'PATCH', body: fields }),
    
    delegate: (id: number, delegatedFrom: string, delegatedTo: string, reason: string) => 
      fetch(`/api/Tickets/${id}/delegate`, { method: 'PATCH', body: { delegatedFrom, delegatedTo, reason } }),
    
    getComments: (id: number) => 
      fetch<any[]>(`/api/Tickets/${id}/comments`),
    
    addComment: (id: number, data: any) => 
      fetch<any>(`/api/Tickets/${id}/comments`, { method: 'POST', body: data }),
    
    toggleReaction: (ticketId: number, commentId: number, emoji: string) =>
      fetch<any>(`/api/Tickets/${ticketId}/comments/${commentId}/reactions`, { method: 'POST', body: { emoji } }),
    
    getReports: (id: number) => 
      fetch<any[]>(`/api/Tickets/${id}/reports`),
    
    addReport: (id: number, data: any) => 
      fetch<any>(`/api/Tickets/${id}/reports`, { method: 'POST', body: data }),

    updateReport: (id: number, reportId: number, data: any) =>
      fetch<any>(`/api/Tickets/${id}/reports/${reportId}`, { method: 'PATCH', body: data }),
    
    getSla: (id: number) => 
      fetch<any>(`/api/Tickets/${id}/sla`),
    
    getTimeline: (id: number) =>
      fetch<Array<{
        type: string
        at: string
        channel?: string | null
        authorName?: string | null
        text?: string | null
        isInternal?: boolean | null
        entityId?: number | null
        actionType?: string | null
        equipmentType?: string | null
      }>>(`/api/Tickets/${id}/timeline`),

    suggestFields: (data: { title?: string; problem?: string }) =>
      fetch<{ requestType?: string | null; priority?: string | null; department?: string | null }>(
        '/api/Tickets/suggest-fields',
        { method: 'POST', body: data }
      ),

    suggestReply: (id: number) =>
      fetch<{ suggestion: string; source: string }>(`/api/Tickets/${id}/suggest-reply`, { method: 'POST' }),

    getAttachments: (id: number) => 
      fetch<any[]>(`/api/tickets/${id}/attachments`),
    
    uploadAttachment: (id: number, formData: FormData) => 
      fetch<any>(`/api/tickets/${id}/attachments`, { 
        method: 'POST', 
        body: formData,
        headers: {} // Let browser set content-type for FormData
      }),

    markAsRead: (id: number) =>
      fetch(`/api/Tickets/${id}/read`, { method: 'POST' }),
  }
  
  // Subtasks API
  const subtasksApi = {
    getAll: (ticketId: number) => 
      fetch<any[]>(`/api/tickets/${ticketId}/subtasks`),
    
    create: (ticketId: number, data: any) => 
      fetch<any>(`/api/tickets/${ticketId}/subtasks`, { method: 'POST', body: data }),
    
    update: (ticketId: number, subtaskId: number, data: any) => 
      fetch<any>(`/api/tickets/${ticketId}/subtasks/${subtaskId}`, { method: 'PATCH', body: data }),
    
    delete: (ticketId: number, subtaskId: number) => 
      fetch(`/api/tickets/${ticketId}/subtasks/${subtaskId}`, { method: 'DELETE' }),
  }
  
  // Employees API
  const employeesApi = {
    getAll: () => 
      fetch<any[]>('/api/Employees'),

    /** Текущий сотрудник: профиль + PermissionsJson */
    getMe: () =>
      fetch<any>('/api/Employees/me'),
    
    getById: (userId: string) => 
      fetch<any>(`/api/Employees/${userId}`),
    
    createAccount: (data: any) => 
      fetch('/api/Employees/create-account', { method: 'POST', body: data }),
    
    changeLogin: (userId: string, newLogin: string) =>
      fetch(`/api/Employees/${userId}/change-login`, { method: 'POST', body: { newLogin } }),

    changePassword: (userId: string, oldPassword: string, newPassword: string) => 
      fetch(`/api/Employees/${userId}/change-password`, { method: 'POST', body: { oldPassword, newPassword } }),
    
    changeAvatar: (userId: string, formData: FormData) =>
      fetch<any>(`/api/Employees/${userId}/change-avatar`, {
        method: 'POST',
        body: formData,
        headers: {}, // Let browser set content-type
      }),

    updateProfile: (userId: string, data: any) => 
      fetch(`/api/Employees/${userId}/update-profile`, { method: 'POST', body: data }),
    
    delete: (userId: string) => 
      fetch(`/api/Employees/${userId}`, { method: 'DELETE' }),

    changeSchedule: (userId: string, newSchedule: string | null, newScheduleGridJson: string | null) =>
      fetch(`/api/Employees/${userId}/change-schedule`, { method: 'POST', body: { newSchedule, newScheduleGridJson } }),
  }
  
  // Companies API
  const companiesApi = {
    getAll: (includeInactive = false) => 
      fetch<any[]>(`/api/Companies?includeInactive=${includeInactive}`),
    
    create: (data: any) => 
      fetch<any>('/api/Companies', { method: 'POST', body: data }),
    
    update: (id: number, data: any) => 
      fetch(`/api/Companies/${id}`, { method: 'PUT', body: data }),
    
    delete: (id: number) => 
      fetch(`/api/Companies/${id}`, { method: 'DELETE' }),
  }
  
  // Clients API
  const clientsApi = {
    getAll: () => 
      fetch<any[]>('/api/Clients'),
  }

  const clientPortalApi = {
    getServiceObjects: () =>
      fetch<
        { id: number; name: string; address: string; maintenanceStatus: string; clientId: number | null }[]
      >('/api/ClientPortal/service-objects'),

    getTickets: () =>
      fetch<
        {
          id: number
          title: string
          status: string
          priority: string
          createdAt: string
          objectId: number | null
          requestType: string
        }[]
      >('/api/ClientPortal/tickets'),
  }
  
  // Service Objects API
  const serviceObjectsApi = {
    getAll: (clientId?: number, includeInactive = false) => {
      let url = `/api/ServiceObjects?includeInactive=${includeInactive}`
      if (clientId) url += `&clientId=${clientId}`
      return fetch<any[]>(url)
    },
    
    create: (data: any) => 
      fetch<any>('/api/ServiceObjects', { method: 'POST', body: data }),
    
    update: (id: number, data: any) => 
      fetch(`/api/ServiceObjects/${id}`, { method: 'PUT', body: data }),
  }
  
  // Equipment API
  const equipmentApi = {
    getAll: (tab?: string, equipmentType?: string, fundStatus?: string) => {
      let url = '/api/Equipment'
      const params = new URLSearchParams()
      if (tab) params.append('tab', tab)
      if (equipmentType) params.append('equipmentType', equipmentType)
      if (fundStatus) params.append('fundStatus', fundStatus)
      if (params.toString()) url += `?${params.toString()}`
      return fetch<any[]>(url)
    },
    
    create: (data: any) => 
      fetch<any>('/api/Equipment', { method: 'POST', body: data }),
    
    update: (id: number, data: any) => 
      fetch(`/api/Equipment/${id}`, { method: 'PUT', body: data }),
    
    delete: (id: number) => 
      fetch(`/api/Equipment/${id}`, { method: 'DELETE' }),
  }
  
  // Departments API
  const departmentsApi = {
    getAll: () => 
      fetch<any[]>('/api/Departments'),
  }
  
  // System Settings API
  const systemSettingsApi = {
    getStatuses: () => 
      fetch<any[]>('/api/SystemSettings/statuses'),
    
    saveStatus: (data: any) => 
      fetch('/api/SystemSettings/statuses', { method: 'POST', body: data }),
    
    deleteStatus: (id: number) => 
      fetch(`/api/SystemSettings/statuses/${id}`, { method: 'DELETE' }),
    
    getSla: () => 
      fetch<any[]>('/api/SystemSettings/sla'),
    
    saveSla: (data: any) => 
      fetch('/api/SystemSettings/sla', { method: 'POST', body: data }),
    
    deleteSla: (id: number) => 
      fetch(`/api/SystemSettings/sla/${id}`, { method: 'DELETE' }),
    
    getTelegram: () => 
      fetch<any[]>('/api/SystemSettings/telegram'),
    
    saveTelegram: (data: any) => 
      fetch('/api/SystemSettings/telegram', { method: 'POST', body: data }),
    
    deleteTelegram: (id: number) => 
      fetch(`/api/SystemSettings/telegram/${id}`, { method: 'DELETE' }),
    
    getSettings: () => 
      fetch<Record<string, string>>('/api/SystemSettings/settings'),
    
    saveSettings: (values: Record<string, string>) => 
      fetch('/api/SystemSettings/settings', { method: 'POST', body: { values } }),

    getRolePermissionDefaults: async () => {
      try {
        return await fetch<Record<string, Record<string, boolean>>>(
          '/api/SystemSettings/role-permission-defaults',
        )
      } catch (e: any) {
        const status = e?.response?.status ?? e?.statusCode ?? e?.status
        if (status === 404) return {}
        throw e
      }
    },

    saveRolePermissionDefaults: async (defaults: Record<string, Record<string, boolean>>) => {
      try {
        return await fetch('/api/SystemSettings/role-permission-defaults', {
          method: 'POST',
          body: { defaults },
        })
      } catch (e: any) {
        const status = e?.response?.status ?? e?.statusCode ?? e?.status
        if (status === 404) {
          return await fetch('/api/SystemSettings/settings', {
            method: 'POST',
            body: {
              values: { StaffRolePermissionDefaults: JSON.stringify(defaults) },
            },
          })
        }
        throw e
      }
    },

    getStaffApiKeyStatus: () =>
      fetch<{ configured: boolean; boundUserId: string | null }>(
        '/api/SystemSettings/staff-api-key/status',
      ),

    generateStaffApiKey: (userId: string) =>
      fetch<{ apiKey: string; userId: string; message: string }>(
        '/api/SystemSettings/staff-api-key',
        { method: 'POST', body: { userId } },
      ),

    revokeStaffApiKey: () =>
      fetch<void>('/api/SystemSettings/staff-api-key', { method: 'DELETE' }),

    testOkdeskConnection: () =>
      fetch<{ valid: boolean }>('/api/SystemSettings/okdesk/test-connection', { method: 'POST' }),

    importOkdesk: () =>
      fetch<{
        companiesFetched: number
        companiesUpserted: number
        issuesFetched: number
        issuesUpserted: number
        warning: string | null
      }>('/api/SystemSettings/okdesk/import', { method: 'POST' }),
  }
  
  // Reports API
  const reportsApi = {
    getRepairs: (params?: { month?: string; from?: string; to?: string; clientName?: string; equipmentType?: string; status?: string; repairType?: string }) => {
      let url = '/api/Reports/repairs'
      if (params) {
        const query = new URLSearchParams()
        Object.entries(params).forEach(([key, value]) => {
          if (value) query.append(key, value)
        })
        if (query.toString()) url += `?${query.toString()}`
      }
      return fetch<any>(url)
    },
  }
  
  // User Preferences API
  const preferencesApi = {
    get: (userId: string) => 
      fetch<any>(`/api/AgentPreferences/${userId}`),
    
    save: (data: any) => 
      fetch('/api/AgentPreferences', { method: 'POST', body: data }),
  }
  
  // Spreadsheets API
  const spreadsheetsApi = {
    getAll: () => 
      fetch<any[]>('/api/Spreadsheets'),
    
    getById: (id: number) => 
      fetch<any>(`/api/Spreadsheets/${id}`),
    
    create: (data: any) => 
      fetch<any>('/api/Spreadsheets', { method: 'POST', body: data }),
    
    updateMeta: (id: number, data: any) => 
      fetch(`/api/Spreadsheets/${id}`, { method: 'PATCH', body: data }),
    
    patchCells: (id: number, data: any) => 
      fetch(`/api/Spreadsheets/${id}/cells`, { method: 'PATCH', body: data }),
    
    delete: (id: number) => 
      fetch(`/api/Spreadsheets/${id}`, { method: 'DELETE' }),
    
    import: (id: number, formData: FormData) => 
      fetch<any>(`/api/Spreadsheets/${id}/import`, { method: 'POST', body: formData }),
  }
  
  // Messenger (staff-only API)
  const messengerApi = {
    listConversations: () =>
      fetch<
        {
          id: string
          isGroup: boolean
          title: string | null
          peerUserId: string | null
          displayName: string
          avatarUrl: string | null
          lastMessagePreview: string | null
          lastMessageAtUtc: string
          unreadCount: number
        }[]
      >('/api/Messenger/conversations'),

    getConversation: (id: string) =>
      fetch<{
        id: string
        isGroup: boolean
        title: string | null
        members: { userId: string; fullName: string; avatarUrl: string | null }[]
        lastMessageAtUtc: string
      }>(`/api/Messenger/conversations/${id}`),

    ensureDirect: (otherUserId: string) =>
      fetch<{ id: string }>('/api/Messenger/conversations/direct', {
        method: 'POST',
        body: { otherUserId },
      }),

    createGroup: (title: string, memberUserIds: string[]) =>
      fetch<{ id: string }>('/api/Messenger/conversations/group', {
        method: 'POST',
        body: { title, memberUserIds },
      }),

    getMessages: (id: string, before?: string, take = 80) => {
      const q = new URLSearchParams()
      if (before) q.set('before', before)
      q.set('take', String(take))
      return fetch<
        {
          id: string
          conversationId: string
          senderUserId: string
          senderFullName: string
          body: string
          createdAtUtc: string
          attachmentUrl?: string | null
          attachmentMimeType?: string | null
          attachmentFileName?: string | null
        }[]
      >(`/api/Messenger/conversations/${id}/messages?${q}`)
    },

    postMessage: (
      id: string,
      body: string,
      attachment?: { url: string; mimeType: string; fileName: string },
    ) =>
      fetch<{
        id: string
        conversationId: string
        senderUserId: string
        senderFullName: string
        body: string
        createdAtUtc: string
        attachmentUrl?: string | null
        attachmentMimeType?: string | null
        attachmentFileName?: string | null
      }>(`/api/Messenger/conversations/${id}/messages`, {
        method: 'POST',
        body: {
          body,
          attachmentUrl: attachment?.url ?? null,
          attachmentMimeType: attachment?.mimeType ?? null,
          attachmentFileName: attachment?.fileName ?? null,
        },
      }),

    uploadChatAttachment: (conversationId: string, file: File) => {
      const fd = new FormData()
      fd.append('file', file)
      return fetch<{
        url: string
        mimeType: string
        fileName: string
        sizeBytes: number
      }>(`/api/Messenger/conversations/${conversationId}/attachments`, { method: 'POST', body: fd })
    },

    deleteChatMessage: (conversationId: string, messageId: string) =>
      fetch<void>(`/api/Messenger/conversations/${conversationId}/messages/${messageId}`, { method: 'DELETE' }),
    
    toggleReaction: (conversationId: string, messageId: string, emoji: string) =>
      fetch<any>(`/api/Messenger/conversations/${conversationId}/messages/${messageId}/reactions`, { method: 'POST', body: { emoji } }),

    updateChatGroup: (
      conversationId: string,
      body: {
        title?: string | null
        addMemberUserIds?: string[] | null
        removeMemberUserIds?: string[] | null
      },
    ) =>
      fetch<{
        id: string
        isGroup: boolean
        title: string | null
        members: { userId: string; fullName: string; avatarUrl: string | null }[]
        lastMessageAtUtc: string
      }>(`/api/Messenger/conversations/${conversationId}/group`, { method: 'PATCH', body }),

    markAsRead: (conversationId: string) =>
      fetch<void>(`/api/Messenger/conversations/${conversationId}/read`, { method: 'POST' }),

    ensureDepartmentChannel: (departmentSlug: string) =>
      fetch<{ id: string }>('/api/Messenger/channels/department', {
        method: 'POST',
        body: { departmentSlug },
      }),

    ensureTicketChat: (ticketId: number) =>
      fetch<{ id: string }>(`/api/Messenger/conversations/ticket/${ticketId}`, {
        method: 'POST',
      }),

    searchMessages: (q: string) =>
      fetch<
        {
          messageId: string
          conversationId: string
          body: string
          createdAtUtc: string
          senderFullName: string
        }[]
      >(`/api/Messenger/search?q=${encodeURIComponent(q)}`),
  }
  
  // Google Sync API
  const googleSyncApi = {
    syncCompaniesObjects: (data: any, syncKey: string) => 
      fetch<any>('/api/sync/google/companies-objects', { 
        method: 'POST', 
        body: data,
        headers: { 'X-Sync-Key': syncKey }
      }),
  }

  const knowledgeBaseApi = {
    getCategories: () => fetch<any[]>('/api/KnowledgeBase/categories'),
    saveCategory: (data: any) => fetch('/api/KnowledgeBase/categories', { method: 'POST', body: data }),
    deleteCategory: (id: number) => fetch(`/api/KnowledgeBase/categories/${id}`, { method: 'DELETE' }),
    getArticles: () => fetch<any[]>('/api/KnowledgeBase/articles'),
    getPublished: () => fetch<any[]>('/api/KnowledgeBase/articles/published'),
    saveArticle: (data: any) => fetch('/api/KnowledgeBase/articles', { method: 'POST', body: data }),
    deleteArticle: (id: number) => fetch(`/api/KnowledgeBase/articles/${id}`, { method: 'DELETE' }),
    search: (q: string) => fetch<any[]>(`/api/KnowledgeBase/search?q=${encodeURIComponent(q)}`),
    suggest: (ticketTitle: string) =>
      fetch<any[]>(`/api/KnowledgeBase/suggest?ticketTitle=${encodeURIComponent(ticketTitle)}`),
  }

  const automationRulesApi = {
    getAll: () => fetch<any[]>('/api/AutomationRules'),
    save: (data: any) => fetch('/api/AutomationRules', { method: 'POST', body: data }),
    delete: (id: number) => fetch(`/api/AutomationRules/${id}`, { method: 'DELETE' }),
  }
  
  return {
    fetch,
    auth: authApi,
    tickets: ticketsApi,
    subtasks: subtasksApi,
    employees: employeesApi,
    companies: companiesApi,
    clients: clientsApi,
    clientPortal: clientPortalApi,
    serviceObjects: serviceObjectsApi,
    equipment: equipmentApi,
    departments: departmentsApi,
    systemSettings: systemSettingsApi,
    reports: reportsApi,
    preferences: preferencesApi,
    spreadsheets: spreadsheetsApi,
    googleSync: googleSyncApi,
    messenger: messengerApi,
    knowledgeBase: knowledgeBaseApi,
    automationRules: automationRulesApi,
  }
}
