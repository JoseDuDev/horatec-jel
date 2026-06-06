export type NotificationEventType =
  | 'BookingCreated'
  | 'BookingConfirmed'
  | 'BookingCancelled'
  | 'BookingCompleted'
  | 'BookingReminder'

export type NotificationChannel = 'WhatsApp' | 'Email'

export interface NotificationTemplate {
  id: string
  eventType: NotificationEventType
  channel: NotificationChannel
  subjectTemplate?: string
  bodyTemplate: string
  isActive: boolean
}
