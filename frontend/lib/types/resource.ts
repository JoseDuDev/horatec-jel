export interface Resource {
  id: string
  name: string
  type: string
  serviceIds: string[]
  isActive: boolean
}

export interface UpsertResourceRequest {
  name: string
  type: string
  serviceIds: string[]
}
