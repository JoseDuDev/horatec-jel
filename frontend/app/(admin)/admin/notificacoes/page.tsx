'use client'

import { useEffect, useState } from 'react'
import { notificationsApi } from '@/lib/api/notifications'
import { TemplateEditor } from '@/components/notifications/TemplateEditor'
import type { NotificationTemplate, NotificationEventType, NotificationChannel } from '@/lib/types/notification'

export default function NotificacoesPage() {
  const [templates, setTemplates] = useState<NotificationTemplate[]>([])

  useEffect(() => {
    notificationsApi.list().then(setTemplates)
  }, [])

  const handleSave = async (
    eventType: NotificationEventType,
    channel: NotificationChannel,
    data: { bodyTemplate: string; subjectTemplate?: string }
  ) => {
    await notificationsApi.upsert({ eventType, channel, ...data })
    notificationsApi.list().then(setTemplates)
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Templates de Notificação</h1>
      <p className="text-sm text-slate-500">
        Configure as mensagens enviadas por WhatsApp ou Email em cada evento.
      </p>
      <div className="space-y-4">
        {templates.map(t => (
          <TemplateEditor
            key={t.id}
            template={t}
            onSave={data => handleSave(t.eventType, t.channel, data)}
          />
        ))}
        {templates.length === 0 && (
          <p className="text-slate-500 text-sm">Nenhum template configurado ainda.</p>
        )}
      </div>
    </div>
  )
}
