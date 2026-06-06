import { apiFetch } from './client'
import type { NotificationTemplate, NotificationEventType, NotificationChannel } from '../types/notification'

export const notificationsApi = {
  list: () => apiFetch<NotificationTemplate[]>('/api/v1/notification-templates'),
  upsert: (data: {
    eventType: NotificationEventType
    channel: NotificationChannel
    bodyTemplate: string
    subjectTemplate?: string
  }) =>
    apiFetch<void>('/api/v1/notification-templates', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
}
