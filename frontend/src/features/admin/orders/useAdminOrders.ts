import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  deliverOrder,
  getAdminOrder,
  getAdminOrders,
  shipOrder,
  type AdminOrder,
  type AdminOrderQuery,
} from '@/api/orders'

const orderKey = (id: string) => ['admin', 'order', id] as const

export function useAdminOrders(query: AdminOrderQuery) {
  return useQuery({
    queryKey: ['admin', 'orders', query],
    queryFn: () => getAdminOrders(query),
    placeholderData: keepPreviousData,
  })
}

export function useAdminOrder(id: string) {
  return useQuery({
    queryKey: orderKey(id),
    queryFn: () => getAdminOrder(id),
    // While the customer pays or a refund is being confirmed, the status changes without the admin doing anything.
    refetchInterval: (query) =>
      ['PendingPayment', 'Cancelling'].includes(query.state.data?.status ?? '') ? 5000 : false,
  })
}

/** Ships or delivers. On a 409 the order changed meanwhile, so it is read again to show its real status. */
function useOrderTransition(id: string, transition: (id: string) => Promise<AdminOrder>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => transition(id),
    onSuccess: (order) => queryClient.setQueryData(orderKey(id), order),
    onError: () => queryClient.invalidateQueries({ queryKey: orderKey(id) }),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['admin', 'orders'] }),
  })
}

export const useShipOrder = (id: string) => useOrderTransition(id, shipOrder)
export const useDeliverOrder = (id: string) => useOrderTransition(id, deliverOrder)
