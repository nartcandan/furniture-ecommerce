import { Navigate, Outlet, useLocation } from 'react-router'
import { Layout } from '@/components/Layout'
import { NotFoundPage } from '@/pages/NotFoundPage'
import { useIsAdmin, useSession } from './authStore'

interface LoginRedirectState {
  from?: string
}

/** Pages for signed-in users. Others are sent to sign in and come back afterwards. */
export function RequireAuth() {
  const session = useSession()
  const location = useLocation()

  if (!session) {
    const state: LoginRedirectState = { from: location.pathname + location.search }
    return <Navigate to="/login" replace state={state} />
  }
  return <Outlet />
}

/**
 * The admin panel. A guest is sent to sign in. A customer sees the 404 page, so the panel does not show
 * that it exists. The API checks the role again on every request.
 */
export function RequireAdmin() {
  const session = useSession()
  const isAdmin = useIsAdmin()
  const location = useLocation()

  if (!session) {
    const state: LoginRedirectState = { from: location.pathname + location.search }
    return <Navigate to="/login" replace state={state} />
  }
  return isAdmin ? (
    <Outlet />
  ) : (
    <Layout>
      <NotFoundPage />
    </Layout>
  )
}

/** The sign-in and register pages. A signed-in user is sent on, which is also what happens right after signing in. */
export function GuestOnly() {
  const session = useSession()
  const location = useLocation()

  if (session) {
    const from = (location.state as LoginRedirectState | null)?.from
    return <Navigate to={from ?? '/'} replace />
  }
  return <Outlet />
}
