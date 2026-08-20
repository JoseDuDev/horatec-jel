'use client'

import { Suspense, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useRouter, useSearchParams } from 'next/navigation'
import Link from 'next/link'
import { authApi } from '@/lib/api/auth'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useBrand } from '@/components/brand-provider'

const schema = z
  .object({
    password: z
      .string()
      .min(8, 'Senha deve ter no mínimo 8 caracteres')
      .regex(/[A-Z]/, 'Senha deve conter ao menos uma letra maiúscula')
      .regex(/[0-9]/, 'Senha deve conter ao menos um número'),
    confirmPassword: z.string().min(1, 'Confirme a nova senha'),
  })
  .refine(data => data.password === data.confirmPassword, {
    message: 'As senhas não coincidem',
    path: ['confirmPassword'],
  })

type FormData = z.infer<typeof schema>

function ResetPasswordForm() {
  const router = useRouter()
  const searchParams = useSearchParams()
  const token = searchParams.get('token') ?? ''
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)
  const [loading, setLoading] = useState(false)

  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  })

  const onSubmit = async (data: FormData) => {
    setError(null)
    setLoading(true)
    try {
      await authApi.resetPassword(token, data.password)
      setDone(true)
      setTimeout(() => router.replace('/login'), 2500)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Erro ao redefinir senha')
    } finally {
      setLoading(false)
    }
  }

  if (!token) {
    return (
      <p className="text-sm text-center text-red-500">
        Link inválido ou incompleto. Solicite um novo em{' '}
        <Link href="/forgot-password" className="underline">Esqueci minha senha</Link>.
      </p>
    )
  }

  if (done) {
    return (
      <p className="text-sm text-center text-slate-600">
        Senha redefinida com sucesso. Redirecionando para o login...
      </p>
    )
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <div>
        <Label htmlFor="password">Nova senha</Label>
        <Input id="password" type="password" {...register('password')} />
        {errors.password && <p className="text-sm text-red-500 mt-1">{errors.password.message}</p>}
      </div>
      <div>
        <Label htmlFor="confirmPassword">Confirmar nova senha</Label>
        <Input id="confirmPassword" type="password" {...register('confirmPassword')} />
        {errors.confirmPassword && (
          <p className="text-sm text-red-500 mt-1">{errors.confirmPassword.message}</p>
        )}
      </div>
      {error && <p className="text-sm text-red-500">{error}</p>}
      <Button type="submit" className="w-full" disabled={loading}>
        {loading ? 'Salvando...' : 'Redefinir senha'}
      </Button>
    </form>
  )
}

export default function ResetPasswordPage() {
  const brand = useBrand()
  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle className="text-2xl text-center">{brand.name}</CardTitle>
          <p className="text-sm text-center text-slate-500">Redefinir senha</p>
        </CardHeader>
        <CardContent>
          <Suspense fallback={<div className="h-48 flex items-center justify-center">Carregando...</div>}>
            <ResetPasswordForm />
          </Suspense>
        </CardContent>
      </Card>
    </div>
  )
}
