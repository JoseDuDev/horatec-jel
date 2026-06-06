'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { platformLogin } from '@/lib/api/platform'
import { usePlatformAdminStore } from '@/store/platform-admin'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

export default function PlatformLoginPage() {
  const router = useRouter()
  const { login } = usePlatformAdminStore()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setError(null)
    try {
      const result = await platformLogin(email, password)
      login(email, result.accessToken)
      document.cookie = `platform_access_token=${result.accessToken}; path=/; max-age=86400; SameSite=Lax`
      router.push('/platform/tenants')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao autenticar')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-slate-900 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-lg w-full max-w-sm p-8">
        <div className="text-center mb-8">
          <h1 className="text-2xl font-bold text-slate-900">Horafy Platform</h1>
          <p className="text-sm text-slate-500 mt-1">Acesso restrito — administradores da plataforma</p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <Label htmlFor="email">E-mail</Label>
            <Input
              id="email"
              type="email"
              value={email}
              onChange={e => setEmail(e.target.value)}
              placeholder="admin@horafy.com.br"
              required
            />
          </div>
          <div>
            <Label htmlFor="password">Senha</Label>
            <Input
              id="password"
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              required
            />
          </div>

          {error && (
            <div className="p-3 bg-red-50 border border-red-200 rounded-lg text-sm text-red-700">
              {error}
            </div>
          )}

          <Button type="submit" className="w-full" disabled={loading}>
            {loading ? 'Autenticando...' : 'Entrar'}
          </Button>
        </form>
      </div>
    </div>
  )
}
