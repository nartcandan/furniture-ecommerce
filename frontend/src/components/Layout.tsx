import type { ReactNode } from 'react'
import { Outlet, ScrollRestoration, useLocation } from 'react-router'
import { SiteFooter } from '@/components/SiteFooter'
import { SiteHeader } from '@/components/SiteHeader'

/** The store's page frame. `children` replaces the routed page, for pages shown outside the store's routes. */
export function Layout({ children }: { children?: ReactNode }) {
  const { pathname } = useLocation()

  return (
    <div className="flex min-h-screen flex-col">
      <SiteHeader />
      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-10">
        {/* A new page fades in. The key is the path, so filters and pages of the same list do not fade. */}
        <div key={pathname} className="motion-safe:animate-in motion-safe:fade-in motion-safe:duration-300">
          {children ?? <Outlet />}
        </div>
      </main>
      <SiteFooter />
      {/* A new page opens at the top. Going back returns to where the user was. */}
      <ScrollRestoration />
    </div>
  )
}
