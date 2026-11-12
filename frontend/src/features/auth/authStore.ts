import { create } from 'zustand'
import { persist } from 'zustand/middleware'

export interface AuthUser {
  id: string
  email: string
  firstName: string
  lastName: string
  roles: string[]
}

export interface Session {
  token: string
  /** ISO date. The API rejects the token from this moment on. */
  expiresAt: string
  user: AuthUser
}

interface AuthState {
  session: Session | null
  setSession: (session: Session) => void
  clearSession: () => void
}

function isExpired(session: Session): boolean {
  return new Date(session.expiresAt).getTime() <= Date.now()
}

/**
 * The signed-in user and their token. Kept in localStorage, so a page reload does not sign the user out.
 * Data from the API (products, cart, orders) does not belong here. TanStack Query holds it.
 */
export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      session: null,
      setSession: (session) => set({ session }),
      clearSession: () => set({ session: null }),
    }),
    {
      name: 'furniture-store.auth',
      partialize: (state) => ({ session: state.session }),
      // A token that expired while the tab was closed is dropped before anything uses it.
      onRehydrateStorage: () => (state) => {
        if (state?.session && isExpired(state.session)) {
          state.clearSession()
        }
      },
    },
  ),
)

export function useSession(): Session | null {
  return useAuthStore((state) => state.session)
}

export function useIsAdmin(): boolean {
  return useAuthStore((state) => state.session?.user.roles.includes('Admin') ?? false)
}
