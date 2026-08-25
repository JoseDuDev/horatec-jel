'use client'

import { GoogleOAuthProvider, GoogleLogin } from '@react-oauth/google'
import { portalApi } from '@/lib/api/portal'
import { completePortalLogin } from '@/lib/portal-session'

const GOOGLE_CLIENT_ID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID ?? ''

interface Props {
  slug: string
  onSuccess?: () => void
}

function SignIn({ slug, onSuccess }: Props) {
  return (
    // O GIS (<GoogleLogin>) devolve um ID token (JWT) em `credential` — é exatamente o
    // que o backend valida (GoogleJsonWebSignature.ValidateAsync). NÃO usar useGoogleLogin,
    // que devolve um access_token opaco (não-JWT) e quebra a validação no servidor.
    <GoogleLogin
      onSuccess={async (credentialResponse) => {
        const idToken = credentialResponse.credential
        if (!idToken) return
        try {
          const tokens = await portalApi.loginWithGoogle(slug, idToken)
          // Persistência de tokens/perfil + cookie compartilhada com o login
          // por e-mail/celular (lib/portal-session.ts).
          await completePortalLogin(slug, tokens)
          onSuccess?.()
        } catch {
          console.error('Login failed')
        }
      }}
      onError={() => console.error('Google login failed')}
    />
  )
}

export function GoogleSignInButton(props: Props) {
  return (
    <GoogleOAuthProvider clientId={GOOGLE_CLIENT_ID}>
      <SignIn {...props} />
    </GoogleOAuthProvider>
  )
}
