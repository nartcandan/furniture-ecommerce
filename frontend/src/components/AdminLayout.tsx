import { ArrowUpRight, FolderTree, LogOut, Package, ReceiptText } from 'lucide-react'
import { Link, NavLink, Outlet, ScrollRestoration, useLocation } from 'react-router'
import { Logo } from '@/components/Logo'
import { Button } from '@/components/ui/button'
import { useAuthStore, useSession } from '@/features/auth/authStore'
import { cn } from '@/lib/utils'

const sections = [
  { to: '/admin/orders', label: 'Orders', icon: ReceiptText },
  { to: '/admin/products', label: 'Products', icon: Package },
  { to: '/admin/categories', label: 'Categories', icon: FolderTree },
]

/** The admin panel's frame: a slim header and the section list, without the store's header and footer. */
export function AdminLayout() {
  const session = useSession()
  const clearSession = useAuthStore((state) => state.clearSession)
  const { pathname } = useLocation()

  return (
    <div className="flex min-h-screen flex-col">
      <header className="sticky top-0 z-40 border-b bg-background/90 backdrop-blur">
        <div className="mx-auto flex h-14 max-w-7xl items-center justify-between gap-4 px-4">
          <div className="flex items-center gap-3">
            <Logo />
            <span className="rounded-full bg-secondary px-2.5 py-0.5 text-xs font-medium text-secondary-foreground">
              Admin
            </span>
          </div>
          <div className="flex items-center gap-1">
            <span className="mr-2 hidden max-w-56 truncate text-sm text-muted-foreground sm:block">
              {session?.user.email}
            </span>
            <Button asChild variant="ghost">
              <Link to="/">
                Store
                <ArrowUpRight aria-hidden />
              </Link>
            </Button>
            <Button variant="ghost" onClick={clearSession}>
              <LogOut aria-hidden />
              Sign out
            </Button>
          </div>
        </div>
      </header>

      <div className="mx-auto flex w-full max-w-7xl flex-1 flex-col md:flex-row">
        <nav
          aria-label="Admin sections"
          className="border-b px-4 py-3 md:w-52 md:shrink-0 md:border-r md:border-b-0 md:py-6"
        >
          <ul className="flex gap-1 overflow-x-auto md:flex-col">
            {sections.map(({ to, label, icon: Icon }) => (
              <li key={to}>
                <NavLink
                  to={to}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-2 rounded-md px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-muted hover:text-foreground',
                      isActive && 'bg-secondary font-medium text-foreground',
                    )
                  }
                >
                  <Icon aria-hidden className="size-4" />
                  {label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
        <main className="min-w-0 flex-1 px-4 py-6 md:px-8">
          <div key={pathname} className="motion-safe:animate-in motion-safe:fade-in motion-safe:duration-300">
            <Outlet />
          </div>
        </main>
      </div>
      <ScrollRestoration />
    </div>
  )
}
