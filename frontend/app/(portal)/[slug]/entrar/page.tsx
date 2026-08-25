'use client'

import { Suspense, use, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import Link from 'next/link'
import { useRouter, useSearchParams } from 'next/navigation'
import { portalApi } from '@/lib/api/portal'
import { completePortalLogin } from '@/lib/portal-session'
import { GoogleSignInButton } from '@/components/portal/GoogleSignInButton'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'

// O backend devolve ProblemDetails com `title` = código do erro — traduzimos
// os códigos conhecidos para mensagens amigáveis.
const ERROR_MESSAGES: Record<string, string> = {
  'Auth.InvalidCredentials': 'E-mail/celular ou senha incorretos.',
  'Auth.EmailAlreadyRegistered': 'Este e-mail já está cadastrado. Tente entrar na aba "Entrar".',
  'Auth.PhoneAlreadyRegistered': 'Este celular já está cadastrado neste estabelecimento.',
  'Auth.TenantNotFound': 'Estabelecimento não encontrado.',
}

function friendlyError(err: unknown, fallback: string): string {
  const msg = err instanceof Error ? err.message : ''
  return ERROR_MESSAGES[msg] ?? (msg && !msg.startsWith('HTTP') ? msg : fallback)
}

const onlyDigits = (v: string) => v.replace(/\D/g, '')

const loginSchema = z.object({
  identifier: z.string().min(1, 'Informe seu e-mail ou celular'),
  password: z.string().min(1, 'Senha obrigatória'),
})

// Mesma política do backend (RegisterWithEmailCommandValidator):
// mín. 8 caracteres, 1 letra maiúscula e 1 número; celular 10–13 dígitos.
const registerSchema = z.object({
  name: z.string().min(1, 'Nome é obrigatório').max(150, 'Nome muito longo'),
  email: z.string().min(1, 'E-mail é obrigatório').email('E-mail inválido'),
  phone: z
    .string()
    .optional()
    .refine(
      (v) => !v || (onlyDigits(v).length >= 10 && onlyDigits(v).length <= 13),
      'Celular inválido. Informe DDD + número.'
    ),
  password: z
    .string()
    .min(8, 'A senha deve ter no mínimo 8 caracteres')
    .regex(/[A-Z]/, 'A senha deve conter ao menos uma letra maiúscula')
    .regex(/[0-9]/, 'A senha deve conter ao menos um número'),
})

type LoginData = z.infer<typeof loginSchema>
type RegisterData = z.infer<typeof registerSchema>

function EntrarContent({ slug }: { slug: string }) {
  const router = useRouter()
  const searchParams = useSearchParams()

  // Só aceitamos caminhos internos (evita open redirect via ?next=).
  const rawNext = searchParams.get('next')
  const next =
    rawNext && rawNext.startsWith('/') && !rawNext.startsWith('//') ? rawNext : `/${slug}`

  const [loginError, setLoginError] = useState<string | null>(null)
  const [registerError, setRegisterError] = useState<string | null>(null)
  const [loginLoading, setLoginLoading] = useState(false)
  const [registerLoading, setRegisterLoading] = useState(false)

  const loginForm = useForm<LoginData>({ resolver: zodResolver(loginSchema) })
  const registerForm = useForm<RegisterData>({ resolver: zodResolver(registerSchema) })

  const finishLogin = async (tokens: { accessToken: string; refreshToken: string }) => {
    await completePortalLogin(slug, tokens)
    router.replace(next)
  }

  const onLogin = async (data: LoginData) => {
    setLoginError(null)
    setLoginLoading(true)
    try {
      const tokens = await portalApi.loginWithEmail(slug, data.identifier.trim(), data.password)
      await finishLogin(tokens)
    } catch (err) {
      setLoginError(friendlyError(err, 'Não foi possível entrar. Tente novamente.'))
      setLoginLoading(false)
    }
  }

  const onRegister = async (data: RegisterData) => {
    setRegisterError(null)
    setRegisterLoading(true)
    try {
      const tokens = await portalApi.registerWithEmail(slug, {
        name: data.name.trim(),
        email: data.email.trim(),
        phone: data.phone ? onlyDigits(data.phone) : undefined,
        password: data.password,
      })
      await finishLogin(tokens)
    } catch (err) {
      setRegisterError(friendlyError(err, 'Não foi possível criar sua conta. Tente novamente.'))
      setRegisterLoading(false)
    }
  }

  const googleDivider = (
    <>
      <div className="flex items-center gap-3 my-4">
        <div className="h-px flex-1 bg-slate-200" />
        <span className="text-xs text-slate-400 uppercase">ou</span>
        <div className="h-px flex-1 bg-slate-200" />
      </div>
      <div className="flex justify-center">
        <GoogleSignInButton slug={slug} onSuccess={() => router.replace(next)} />
      </div>
    </>
  )

  return (
    <div className="max-w-md mx-auto px-4 py-12">
      <Card>
        <CardHeader>
          <CardTitle className="text-2xl text-center">Acesse sua conta</CardTitle>
        </CardHeader>
        <CardContent>
          <Tabs defaultValue="entrar">
            <TabsList className="grid w-full grid-cols-2 mb-6">
              <TabsTrigger value="entrar">Entrar</TabsTrigger>
              <TabsTrigger value="criar-conta">Criar conta</TabsTrigger>
            </TabsList>

            <TabsContent value="entrar">
              <form onSubmit={loginForm.handleSubmit(onLogin)} className="space-y-4">
                <div>
                  <Label htmlFor="identifier">E-mail ou celular</Label>
                  <Input
                    id="identifier"
                    autoComplete="username"
                    placeholder="voce@email.com ou (47) 99999-9999"
                    {...loginForm.register('identifier')}
                  />
                  {loginForm.formState.errors.identifier && (
                    <p className="text-sm text-red-500 mt-1">
                      {loginForm.formState.errors.identifier.message}
                    </p>
                  )}
                </div>
                <div>
                  <Label htmlFor="login-password">Senha</Label>
                  <Input
                    id="login-password"
                    type="password"
                    autoComplete="current-password"
                    {...loginForm.register('password')}
                  />
                  {loginForm.formState.errors.password && (
                    <p className="text-sm text-red-500 mt-1">
                      {loginForm.formState.errors.password.message}
                    </p>
                  )}
                </div>
                {loginError && <p className="text-sm text-red-500">{loginError}</p>}
                <Button type="submit" className="w-full" disabled={loginLoading}>
                  {loginLoading ? 'Entrando...' : 'Entrar'}
                </Button>
                <p className="text-sm text-center">
                  <Link href="/forgot-password" className="text-slate-500 underline">
                    Esqueci minha senha
                  </Link>
                </p>
              </form>
              {googleDivider}
            </TabsContent>

            <TabsContent value="criar-conta">
              <form onSubmit={registerForm.handleSubmit(onRegister)} className="space-y-4">
                <div>
                  <Label htmlFor="name">Nome</Label>
                  <Input id="name" autoComplete="name" {...registerForm.register('name')} />
                  {registerForm.formState.errors.name && (
                    <p className="text-sm text-red-500 mt-1">
                      {registerForm.formState.errors.name.message}
                    </p>
                  )}
                </div>
                <div>
                  <Label htmlFor="email">E-mail</Label>
                  <Input
                    id="email"
                    type="email"
                    autoComplete="email"
                    {...registerForm.register('email')}
                  />
                  {registerForm.formState.errors.email && (
                    <p className="text-sm text-red-500 mt-1">
                      {registerForm.formState.errors.email.message}
                    </p>
                  )}
                </div>
                <div>
                  <Label htmlFor="phone">
                    Celular <span className="text-slate-400 font-normal">(opcional)</span>
                  </Label>
                  <Input
                    id="phone"
                    type="tel"
                    autoComplete="tel"
                    placeholder="(47) 99999-9999"
                    {...registerForm.register('phone')}
                  />
                  {registerForm.formState.errors.phone && (
                    <p className="text-sm text-red-500 mt-1">
                      {registerForm.formState.errors.phone.message}
                    </p>
                  )}
                </div>
                <div>
                  <Label htmlFor="register-password">Senha</Label>
                  <Input
                    id="register-password"
                    type="password"
                    autoComplete="new-password"
                    {...registerForm.register('password')}
                  />
                  <p className="text-xs text-slate-500 mt-1">
                    Mínimo de 8 caracteres, com 1 letra maiúscula e 1 número.
                  </p>
                  {registerForm.formState.errors.password && (
                    <p className="text-sm text-red-500 mt-1">
                      {registerForm.formState.errors.password.message}
                    </p>
                  )}
                </div>
                {registerError && <p className="text-sm text-red-500">{registerError}</p>}
                <Button type="submit" className="w-full" disabled={registerLoading}>
                  {registerLoading ? 'Criando conta...' : 'Criar conta'}
                </Button>
              </form>
              {googleDivider}
            </TabsContent>
          </Tabs>
        </CardContent>
      </Card>
    </div>
  )
}

interface Props {
  params: Promise<{ slug: string }>
}

export default function EntrarPage({ params }: Props) {
  const { slug } = use(params)
  return (
    <Suspense
      fallback={<div className="h-64 flex items-center justify-center text-slate-500">Carregando...</div>}
    >
      <EntrarContent slug={slug} />
    </Suspense>
  )
}
