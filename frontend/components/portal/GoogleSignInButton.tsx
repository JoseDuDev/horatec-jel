'use client'

import { GoogleOAuthProvider, GoogleLogin } from '@react-oauth/google'
import { portalApi } from '@/lib/api/portal'
import { usePortalAuthStore } from '@/store/portal-auth'

const GOOGLE_CLIENT_ID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID ?? ''

interface Props {
  slug: string
  onSuccess?: () => void
}

function SignIn({ slug, onSuccess }: Props) {
  const { setCustomerAuth } = usePortalAuthStore()

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
          const profile = await portalApi.profile(slug, tokens.accessToken)
          setCustomerAuth(profile, tokens.accessToken)
          document.cookie = `portal_access_token=${tokens.accessToken}; path=/; max-age=${60 * 60 * 24}`
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
