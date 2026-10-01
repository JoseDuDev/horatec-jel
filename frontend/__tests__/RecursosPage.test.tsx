import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import RecursosPage from '@/app/(admin)/admin/recursos/page'
import { resourcesApi } from '@/lib/api/resources'
import { servicesApi } from '@/lib/api/services'

vi.mock('@/lib/api/resources', () => ({
  resourcesApi: {
    list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(),
    addService: vi.fn(), removeService: vi.fn(), setImage: vi.fn(), removeImage: vi.fn(),
  },
}))
vi.mock('@/lib/api/services', () => ({ servicesApi: { list: vi.fn() } }))

describe('RecursosPage', () => {
  beforeEach(() => {
    vi.mocked(resourcesApi.list).mockResolvedValue([])
    vi.mocked(servicesApi.list).mockResolvedValue([])
  })

  it('shows the API refusal inside the dialog instead of failing silently', async () => {
    vi.mocked(resourcesApi.create).mockRejectedValue(
      new Error('Limite de recursos do plano atingido (2). Faça upgrade para cadastrar mais.')
    )
    render(<RecursosPage />)

    fireEvent.click(await screen.findByRole('button', { name: /novo recurso/i }))
    await userEvent.type(await screen.findByLabelText(/nome/i), 'Caio')
    fireEvent.click(screen.getByRole('button', { name: /salvar/i }))

    expect(await screen.findByText(/limite de recursos do plano/i)).toBeInTheDocument()
    // O diálogo segue aberto com o que foi digitado.
    expect(screen.getByLabelText(/nome/i)).toHaveValue('Caio')
  })

  it('uploads the chosen photo after the resource is created', async () => {
    vi.mocked(resourcesApi.create).mockResolvedValue('new-id')
    vi.mocked(resourcesApi.setImage).mockResolvedValue({ url: '/x.png' })
    render(<RecursosPage />)

    fireEvent.click(await screen.findByRole('button', { name: /novo recurso/i }))
    await userEvent.type(await screen.findByLabelText(/nome/i), 'Caio')

    const file = new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'caio.png', { type: 'image/png' })
    const input = document.querySelector('input[type=file]') as HTMLInputElement
    await userEvent.upload(input, file)

    await waitFor(() => expect(screen.getByAltText(/foto do recurso/i)).toBeInTheDocument())
    fireEvent.click(screen.getByRole('button', { name: /salvar/i }))

    await waitFor(() => expect(resourcesApi.setImage).toHaveBeenCalledWith('new-id', expect.any(File)))
  })
})
