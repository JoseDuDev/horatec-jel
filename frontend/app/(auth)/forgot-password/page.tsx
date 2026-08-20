'use client'

import { Suspense, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import Link from 'next/link'
import { authApi } from '@/lib/api/auth'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useBrand } from '@/components/brand-provider'

const schema = z.object({
  email: z.string().min(1, 'Email obrigatório').email('Email inválido'),
})

type FormData = z.infer<typeof schema>

function ForgotPasswordForm() {
  const [error, setError] = useState<string | null>(null)
  const [sent, setSent] = useState(false)
  const [loading, setLoading] = useState(false)

  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  })

  const onSubmit = async (data: FormData) => {
    setError(null)
    setLoading(true)
    try {
      await authApi.forgotPassword(data.email)
      setSent(true)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Erro ao solicitar redefinição de senha')
    } finally {
      setLoading(false)
    }
  }

  if (sent) {
    return (
      <div className="space-y-4 text-center">
        <p className="text-sm text-slate-600">
          Se existir uma conta com este e-mail, enviamos um link para redefinir sua senha.
        </p>
        <Link href="/login" className="text-sm text-slate-500 underline">
          Voltar para o login
        </Link>
      </div>
    )
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <p className="text-sm text-slate-500">
        Informe seu e-mail e enviaremos um link para redefinir sua senha.
      </p>
      <div>
        <Label htmlFor="email">Email</Label>
        <Input id="email" type="email" {...register('email')} />
        {errors.email && <p className="text-sm text-red-500 mt-1">{errors.email.message}</p>}
      </div>
      {error && <p className="text-sm text-red-500">{error}</p>}
      <Button type="submit" className="w-full" disabled={loading}>
        {loading ? 'Enviando...' : 'Enviar link de redefinição'}
      </Button>
      <p className="text-sm text-center">
        <Link href="/login" className="text-slate-500 underline">
          Voltar para o login
        </Link>
      </p>
    </form>
  )
}

export default function ForgotPasswordPage() {
  const brand = useBrand()
  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle className="text-2xl text-center">{brand.name}</CardTitle>
          <p className="text-sm text-center text-slate-500">Esqueci minha senha</p>
        </CardHeader>
        <CardContent>
          <Suspense fallback={<div className="h-48 flex items-center justify-center">Carregando...</div>}>
            <ForgotPasswordForm />
          </Suspense>
        </CardContent>
      </Card>
    </div>
  )
}
