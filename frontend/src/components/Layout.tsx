import type { ReactNode } from 'react'
import { Outlet, ScrollRestoration } from 'react-router'
import { SiteFooter } from '@/components/SiteFooter'
import { SiteHeader } from '@/components/SiteHeader'

/** The store's page frame. `children` replaces the routed page, for pages shown outside the store's routes. */
export function Layout({ children }: { children?: ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col">
      <SiteHeader />
      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-10">
        {children ?? <Outlet />}
      </main>
      <SiteFooter />
      {/* A new page opens at the top. Going back returns to where the user was. */}
      <ScrollRestoration />
    </div>
  )
}
