import { render, screen } from '@testing-library/react'
import { vi } from 'vitest'
import { GoogleSignInButton } from '@/components/portal/GoogleSignInButton'

// <GoogleLogin> (GIS) renderiza o botão oficial do Google e devolve um ID token.
vi.mock('@react-oauth/google', () => ({
  GoogleOAuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  GoogleLogin: () => <button>Sign in with Google</button>,
}))

describe('GoogleSignInButton', () => {
  it('renders sign-in button', () => {
    render(<GoogleSignInButton slug="test-slug" onSuccess={vi.fn()} />)
    expect(screen.getByRole('button', { name: /google/i })).toBeInTheDocument()
  })
})
