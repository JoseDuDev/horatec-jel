import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { TemplateEditor } from '@/components/notifications/TemplateEditor'
import type { NotificationTemplate } from '@/lib/types/notification'

const template: NotificationTemplate = {
  id: 't1',
  eventType: 'BookingCreated',
  channel: 'WhatsApp',
  bodyTemplate: 'Olá {{customer_name}}!',
  isActive: true,
}

describe('TemplateEditor', () => {
  it('renders template body', () => {
    render(<TemplateEditor template={template} onSave={vi.fn()} />)
    expect(screen.getByDisplayValue('Olá {{customer_name}}!')).toBeInTheDocument()
  })

  it('calls onSave with edited body', async () => {
    const onSave = vi.fn()
    render(<TemplateEditor template={template} onSave={onSave} />)
    const textarea = screen.getByRole('textbox')
    await userEvent.clear(textarea)
    await userEvent.type(textarea, 'Novo texto')
    fireEvent.click(screen.getByRole('button', { name: /salvar/i }))
    await waitFor(() => {
      expect(onSave).toHaveBeenCalledWith(expect.objectContaining({ bodyTemplate: 'Novo texto' }))
    })
  })
})
