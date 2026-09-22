import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { RentableItemForm } from '@/components/rentals/RentableItemForm'
import type { RentableItem } from '@/lib/types/rental'

const item: RentableItem = {
  id: 'f1e2d3c4-0000-0000-0000-000000000001',
  name: 'Furadeira',
  description: 'Com maleta',
  category: 'Ferramentas',
  quantity: 3,
  dailyRate: 30,
  securityDeposit: 100,
  bufferDays: 1,
  imageUrl: 'https://cdn/capa.jpg',
  isActive: true,
  images: [{ id: 'img-1', url: 'https://cdn/capa.jpg', sortOrder: 0 }],
}

describe('RentableItemForm', () => {
  it('exige o nome', async () => {
    render(<RentableItemForm onSubmit={vi.fn()} onCancel={vi.fn()} />)
    fireEvent.click(screen.getByRole('button', { name: /^salvar$/i }))
    await waitFor(() => {
      expect(screen.getByText(/nome obrigatório/i)).toBeInTheDocument()
    })
  })

  it('no cadastro, envia os campos e as fotos escolhidas', async () => {
    const onSubmit = vi.fn()
    render(<RentableItemForm onSubmit={onSubmit} onCancel={vi.fn()} />)

    await userEvent.type(screen.getByLabelText(/nome/i), 'Pula-pula')
    fireEvent.click(screen.getByRole('button', { name: /^salvar$/i }))

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({ name: 'Pula-pula', quantity: 1 }),
        [], // nenhuma foto escolhida
      )
    })
  })

  it('na edição, parte dos valores do item e mostra a galeria', () => {
    render(<RentableItemForm initial={item} onSubmit={vi.fn()} onCancel={vi.fn()} />)

    expect(screen.getByLabelText(/nome/i)).toHaveValue('Furadeira')
    expect(screen.getByLabelText(/estoque/i)).toHaveValue(3)
    // Item existente edita a galeria ali mesmo, em vez de acumular fotos para depois.
    expect(screen.getByAltText(/capa do item/i)).toBeInTheDocument()
  })

  it('na edição, o item pode ser desativado', async () => {
    const onSubmit = vi.fn()
    render(<RentableItemForm initial={item} onSubmit={onSubmit} onCancel={vi.fn()} />)

    await userEvent.click(screen.getByRole('checkbox', { name: /ativo/i }))
    fireEvent.click(screen.getByRole('button', { name: /^salvar$/i }))

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({ name: 'Furadeira', isActive: false }),
        [],
      )
    })
  })
})
