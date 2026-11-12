import { Outlet } from 'react-router'
import { SiteFooter } from '@/components/SiteFooter'
import { SiteHeader } from '@/components/SiteHeader'

export function Layout() {
  return (
    <div className="flex min-h-screen flex-col">
      <SiteHeader />
      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-10">
        <Outlet />
      </main>
      <SiteFooter />
    </div>
  )
}
