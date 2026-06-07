import { PlatformSidebar } from '@/components/platform/PlatformSidebar'

export default function PlatformDashboardLayout({
  children,
}: {
  children: React.ReactNode
}) {
  return (
    <div className="flex min-h-screen">
      <PlatformSidebar />
      <main className="flex-1 bg-slate-50 p-8">{children}</main>
    </div>
  )
}
