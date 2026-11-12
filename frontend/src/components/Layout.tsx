import { Link, Outlet } from 'react-router'
import { Button } from '@/components/ui/button'
import { useAuthStore, useSession } from '@/features/auth/authStore'

export function Layout() {
  const session = useSession()
  const clearSession = useAuthStore((state) => state.clearSession)

  return (
    <div className="flex min-h-screen flex-col">
      <header className="border-b bg-background">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-4">
          <Link to="/" className="font-heading text-xl font-semibold tracking-tight">
            Furniture Store
          </Link>
          <nav aria-label="Account" className="flex items-center gap-1">
            {session ? (
              <>
                <Button asChild variant="ghost">
                  <Link to="/account">{session.user.firstName}</Link>
                </Button>
                <Button variant="ghost" onClick={clearSession}>
                  Sign out
                </Button>
              </>
            ) : (
              <>
                <Button asChild variant="ghost">
                  <Link to="/login">Sign in</Link>
                </Button>
                <Button asChild>
                  <Link to="/register">Register</Link>
                </Button>
              </>
            )}
          </nav>
        </div>
      </header>
      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-8">
        <Outlet />
      </main>
    </div>
  )
}
