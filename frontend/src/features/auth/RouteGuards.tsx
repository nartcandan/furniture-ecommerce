import { Navigate, Outlet, useLocation } from 'react-router'
import { useSession } from './authStore'

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
