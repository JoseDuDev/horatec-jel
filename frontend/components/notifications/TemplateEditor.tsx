'use client'

import { useState } from 'react'
import type { NotificationTemplate } from '@/lib/types/notification'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'

const VARIABLES = ['{{customer_name}}', '{{service_name}}', '{{scheduled_at}}', '{{resource_name}}', '{{tenant_name}}']

const EVENT_LABEL: Record<string, string> = {
  BookingCreated: 'Agendamento Criado',
  BookingConfirmed: 'Agendamento Confirmado',
  BookingCancelled: 'Agendamento Cancelado',
  BookingCompleted: 'Agendamento Concluído',
  BookingReminder: 'Lembrete',
}

interface Props {
  template: NotificationTemplate
  onSave: (data: { bodyTemplate: string; subjectTemplate?: string }) => void
}

export function TemplateEditor({ template, onSave }: Props) {
  const [body, setBody] = useState(template.bodyTemplate)
  const [subject, setSubject] = useState(template.subjectTemplate ?? '')

  const insertVariable = (v: string) => setBody(prev => `${prev}${v}`)

  return (
    <div className="space-y-4 p-4 border rounded-lg">
      <div className="flex items-center gap-2">
        <span className="font-medium text-sm">{EVENT_LABEL[template.eventType] ?? template.eventType}</span>
        <Badge variant="outline">{template.channel}</Badge>
      </div>
      {template.channel === 'Email' && (
        <div>
          <Label>Assunto</Label>
          <input
            className="w-full border rounded px-3 py-2 text-sm mt-1"
            value={subject}
            onChange={e => setSubject(e.target.value)}
          />
        </div>
      )}
      <div>
        <Label>Corpo</Label>
        <textarea
          className="w-full border rounded px-3 py-2 text-sm mt-1 min-h-[100px] font-mono"
          value={body}
          onChange={e => setBody(e.target.value)}
        />
      </div>
      <div>
        <p className="text-xs text-slate-500 mb-2">Variáveis disponíveis:</p>
        <div className="flex flex-wrap gap-2">
          {VARIABLES.map(v => (
            <button
              key={v}
              type="button"
              onClick={() => insertVariable(v)}
              className="text-xs bg-slate-100 hover:bg-slate-200 px-2 py-1 rounded font-mono"
            >
              {v}
            </button>
          ))}
        </div>
      </div>
      <Button
        size="sm"
        onClick={() => onSave({ bodyTemplate: body, subjectTemplate: subject || undefined })}
      >
        Salvar
      </Button>
    </div>
  )
}
