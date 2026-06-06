import { NextResponse } from 'next/server'
import type { NextRequest } from 'next/server'

export function middleware(req: NextRequest) {
  const { pathname } = req.nextUrl

  // Protect /admin/* — require access_token cookie
  if (pathname.startsWith('/admin')) {
    const token = req.cookies.get('access_token')?.value
    if (!token) {
      const loginUrl = new URL('/login', req.url)
      loginUrl.searchParams.set('redirect', pathname)
      return NextResponse.redirect(loginUrl)
    }
  }

  // Protect /platform/* (except /platform/login) — require platform_access_token cookie
  if (pathname.startsWith('/platform') && !pathname.startsWith('/platform/login')) {
    const token = req.cookies.get('platform_access_token')?.value
    if (!token) {
      return NextResponse.redirect(new URL('/platform/login', req.url))
    }
  }

  return NextResponse.next()
}

export const config = {
  matcher: ['/admin/:path*', '/platform/:path*'],
}
