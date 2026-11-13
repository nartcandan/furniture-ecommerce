import { useMutation, useQueryClient } from '@tanstack/react-query'
import { cancelOrder } from '@/api/orders'

/**
 * Cancels an order: one that waits for payment is closed at once, a paid one is refunded.
 * Either way the order list changes, and an unpaid order no longer blocks a new one.
 */
export function useCancelOrder(orderId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => cancelOrder(orderId),
    onSuccess: (order) => {
      queryClient.setQueryData(['order', orderId], order)
      void queryClient.invalidateQueries({ queryKey: ['orders'] })
    },
    // The order may have changed in the meantime: it was paid, shipped or closed.
    onError: () => {
      void queryClient.invalidateQueries({ queryKey: ['order', orderId] })
      void queryClient.invalidateQueries({ queryKey: ['orders'] })
    },
  })
}
