import { QueryClient } from '@tanstack/react-query'
import { useAuthStore } from '@/features/auth/authStore'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, refetchOnWindowFocus: false },
  },
})

// Cached answers belong to the user who asked. They are dropped when the user changes or signs out.
useAuthStore.subscribe((state, previous) => {
  if (state.session?.user.id !== previous.session?.user.id) {
    queryClient.clear()
  }
})
