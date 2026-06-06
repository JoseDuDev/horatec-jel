import { render, screen } from '@testing-library/react'
import { vi } from 'vitest'
import { GoogleSignInButton } from '@/components/portal/GoogleSignInButton'

vi.mock('@react-oauth/google', () => ({
  GoogleOAuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useGoogleLogin: () => vi.fn(),
}))

describe('GoogleSignInButton', () => {
  it('renders sign-in button', () => {
    render(<GoogleSignInButton slug="test-slug" onSuccess={vi.fn()} />)
    expect(screen.getByRole('button', { name: /entrar com google/i })).toBeInTheDocument()
  })
})
