export type ResourceType = 'Professional' | 'PhysicalSpace' | 'Equipment' | 'Court'

export interface Resource {
  id: string
  name: string
  type: ResourceType
  email?: string
  phone?: string
  specialty?: string
  bio?: string
  avatarUrl?: string
  isActive: boolean
  serviceIds: string[]
}

export interface UpsertResourceRequest {
  name: string
  type: ResourceType
  serviceIds: string[]
}
