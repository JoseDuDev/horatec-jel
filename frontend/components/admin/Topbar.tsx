'use client'

import { useRouter } from 'next/navigation'
import { useAuthStore } from '@/store/auth'
import { Button } from '@/components/ui/button'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'

export function Topbar() {
  const router = useRouter()
  const { user, clearAuth } = useAuthStore()

  const handleLogout = () => {
    document.cookie = 'access_token=; path=/; max-age=0'
    document.cookie = 'tenant_slug=; path=/; max-age=0'
    clearAuth()
    router.replace('/login')
  }

  return (
    <header className="h-16 border-b bg-white flex items-center justify-end px-6 gap-4">
      {user && (
        <span className="text-sm text-slate-600">{user.name}</span>
      )}
      <Avatar className="h-8 w-8">
        <AvatarFallback>{user?.name?.[0] ?? 'A'}</AvatarFallback>
      </Avatar>
      <Button variant="outline" size="sm" onClick={handleLogout}>
        Sair
      </Button>
    </header>
  )
}
