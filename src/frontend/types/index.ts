// Auth Types
export interface AuthResponse {
  token: string
  userId: string
  fullName: string
  email: string
  role: string
  avatarUrl: string
}

export interface AuthState {
  token: string
  userId: string
  fullName: string
  email: string
  role: string
  avatarUrl: string
}

export interface LoginRequest {
  username?: string
  email?: string
  password: string
}

// Ticket Types
export interface Ticket {
  id: number
  createdAt: string
  assignee: string
  assignees: string[]
  title: string
  clientName: string
  problem: string
  status: string
  priority: string
  department: string
  requestType: string
  objectName: string
  objectId: number | null
  clientId: number
  okdeskId: number | null
  isFromOkdesk: boolean
  coordinatorBriefJson: string
  isRepair: boolean
  equipmentId: number | null
  repairType: string
  repairCost: number | null
  repairClientName: string
  repairEquipmentName: string
  repairSerialNumber: string
  repairLocation: string
  repairFaults: string
  repairNotes: string
  repairFundStatus: string
  repairEquipmentType: string
  taskLinksJson: string
  alternativeTitle: string
  createdByRole: string
  delegatedFrom: string
  delegatedTo: string
  delegationReason: string
  delegatedAt: string | null
  assigneeIds: string[] | null
  subtaskCount: number
  briefKnowledgeableUserIds: string[] | null
  commentTexts?: string[]
  taskLinkUrls?: string[]
  hasUnread?: boolean
}

export interface CreateTicketRequest {
  title: string
  requestType: string
  softwareName?: string
  priority: string
  department: string
  details?: string
  desiredAt?: string
  clientId?: number
  objectId?: number
  assignee?: string
  assignees?: string[]
  coordinatorBriefJson?: string
  isRepair?: boolean
  equipmentId?: number
  repairType?: string
  repairCost?: number
  repairFaults?: string
  repairNotes?: string
  createdByRole?: string
}

export interface Reaction {
  emoji: string
  userId: string
  userName: string
}

// Comment Types
export interface Comment {
  id: number
  ticketId: number
  authorName: string
  authorRole: string
  text: string
  isInternal: boolean
  createdAt: string
  authorAvatarUrl: string
  authorUserId: string
  reactions?: Reaction[]
}

export interface TimelineItem {
  type: 'created' | 'comment' | 'field_report' | string
  at: string
  channel?: string | null
  authorName?: string | null
  text?: string | null
  isInternal?: boolean | null
  entityId?: number | null
  actionType?: string | null
  equipmentType?: string | null
}

export interface CreateCommentRequest {
  authorName: string
  authorRole: string
  text: string
  isInternal: boolean
  authorUserId?: string
}

// Field Report Types
export interface FieldReport {
  id: number
  ticketId: number
  engineerName: string
  visitDate: string
  actionType: string
  equipmentType: string
  equipmentSerial: string
  equipmentStatus: string
  workDone: string
  transferredTo: string
}

export interface CreateFieldReportRequest {
  engineerName: string
  visitDate?: string
  actionType: string
  equipmentType: string
  equipmentSerial: string
  equipmentStatus: string
  workDone: string
  transferredTo: string
}

// Employee Types
export interface Employee {
  userId: string
  fullName: string
  role: string
  department: string
  login: string
  authEmail: string
  workSchedule: string
  workScheduleGridJson: string
  permissionsJson: string
  okdeskId?: number | null
}

export interface EmployeeDetails {
  userId: string
  fullName: string
  email: string
  role: string
  login: string
  avatarUrl: string
  workSchedule: string
  department: string
}

// Lightweight option used in pickers and UI maps
export interface EmployeeOption {
  userId: string
  fullName: string
  role: string
  avatarUrl?: string
}

// Company Types
export interface Company {
  id: number
  name: string
  email: string
  phone: string
  hqAddress: string
  externalCode: string
  isActive: boolean
  lastSyncedAtUtc: string | null
  syncSource: string
}

// Client Types
export interface Client {
  id: number
  fullName: string
}

// Service Object Types
export interface ServiceObject {
  id: number
  name: string
  address: string
  maintenanceStatus: string
  legalEntity: string
  description: string
  clientId: number | null
  externalCode: string
  isActive: boolean
  lastSyncedAtUtc: string | null
  syncSource: string
  maintenanceComment: string
  directoriesOwner: string
  sysAdmin: string
  serverServices: string
}

// Equipment Types
export interface Equipment {
  id: number
  tab: string
  equipmentType: string
  fundStatus: string
  name: string
  serialNumber: string
  location: string
  status: string
  clientName: string
  notes: string
  defect: string
  processor: string
  ram: string
  diskInfo: string
  osInfo: string
  interfaces: string
  completeness: string
  faults: string
  installPosition: string
  powerSpecs: string
  issuedTo: string
  purchaseDate: string | null
  issueDate: string | null
  createdAt: string
}

// Department Types
export interface Department {
  value: string
  label: string
  desc: string
}

// System Status Types
export interface SystemStatus {
  id: number
  name: string
  colorClass: string
  sortOrder: number
  roleFilter: string
  isDefault: boolean
  isActive: boolean
}

// Subtask Types
export interface Subtask {
  id: number
  ticketId: number
  title: string
  description: string
  status: string
  knowledgeableUserIds: string[]
  knowledgeableNames: string[]
  createdByUserId: string
  createdByName: string
  createdAt: string
}

// Attachment Types
export interface Attachment {
  id: number
  ticketId: number
  commentId: number | null
  subtaskId: number | null
  fileName: string
  url: string
  contentType: string
  fileSizeBytes: number
  uploadedBy: string
  uploadedAt: string
  okdeskId: number | null
}

// Report Types
export interface RepairReportItem {
  ticketId: number
  createdAt: string
  status: string
  equipmentId: number
  clientName: string
  equipmentType: string
  equipmentName: string
  serialNumber: string
  fundStatus: string
  location: string
  faults: string
  notes: string
  repairType: string
  repairCost: number | null
}

export interface RepairGroupSum {
  key: string
  count: number
  sum: number
}

export interface RepairReportSummary {
  totalCount: number
  totalCost: number
  byClient: RepairGroupSum[]
  byEquipmentType: RepairGroupSum[]
  byRepairType: RepairGroupSum[]
  byStatus: RepairGroupSum[]
}

export interface RepairReportResponse {
  items: RepairReportItem[]
  summary: RepairReportSummary
}

// User Preferences
export interface UserPreferences {
  userId: string
  theme: string
  backgroundUrl: string
  dashboardBlocks: string[]
  accentColor: string
  windowColor: string
  textColor: string
}
