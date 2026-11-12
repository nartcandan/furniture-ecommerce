import { useQuery } from '@tanstack/react-query'
import { ChevronRight, Package } from 'lucide-react'
import { Link, useSearchParams } from 'react-router'
import { getOrders } from '@/api/orders'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { OrderStatusBadge } from '@/features/orders/orderStatus'
import { formatDateTime, formatPrice, orderNumber } from '@/lib/format'

export function OrdersPage() {
  const [searchParams] = useSearchParams()
  const orders = useQuery({ queryKey: ['orders'], queryFn: getOrders })

  return (
    <section>
      <h1 className="text-3xl font-semibold sm:text-4xl">My orders</h1>

      {/* The payment provider sends the browser here when it cannot say which order a payment was for. */}
      {searchParams.get('payment') === 'unknown' && (
        <Alert className="mt-6">
          <AlertTitle>We could not match that payment to an order</AlertTitle>
          <AlertDescription>
            Check the status of your latest order below. If it is still waiting, the payment did not
            reach us.
          </AlertDescription>
        </Alert>
      )}

      {orders.isPending && (
        <div className="mt-8 grid gap-3" aria-label="Loading orders">
          <Skeleton className="h-20 rounded-2xl" />
          <Skeleton className="h-20 rounded-2xl" />
        </div>
      )}

      {orders.isError && (
        <Alert variant="destructive" className="mt-8">
          <AlertTitle>Your orders could not be loaded</AlertTitle>
          <AlertDescription>{orders.error.message}</AlertDescription>
        </Alert>
      )}

      {orders.data?.length === 0 && (
        <div className="flex flex-col items-center py-16 text-center">
          <span className="flex size-16 items-center justify-center rounded-full bg-secondary text-muted-foreground">
            <Package aria-hidden className="size-7" strokeWidth={1.5} />
          </span>
          <h2 className="mt-5 text-2xl font-semibold">No orders yet</h2>
          <p className="mt-2 text-muted-foreground">Your orders will be listed here.</p>
          <Button asChild size="lg" className="mt-8 h-11 px-6 text-base">
            <Link to="/products">Browse products</Link>
          </Button>
        </div>
      )}

      {orders.data && orders.data.length > 0 && (
        <ul className="mt-8 grid gap-3">
          {orders.data.map((order) => (
            <li key={order.id}>
              <Link
                to={`/orders/${order.id}`}
                className="group flex items-center gap-4 rounded-2xl border bg-card p-5 transition-shadow outline-none hover:shadow-sm focus-visible:ring-3 focus-visible:ring-ring/50"
              >
                <div className="grid min-w-0 flex-1 gap-x-6 gap-y-1 sm:grid-cols-[1fr_auto_auto] sm:items-center">
                  <div className="min-w-0">
                    <p className="font-medium">Order #{orderNumber(order.id)}</p>
                    <p className="mt-0.5 text-sm text-muted-foreground">
                      {formatDateTime(order.createdAt)} · {order.totalQuantity}{' '}
                      {order.totalQuantity === 1 ? 'item' : 'items'}
                    </p>
                  </div>
                  <div>
                    <OrderStatusBadge status={order.status} />
                  </div>
                  <p className="font-semibold sm:w-32 sm:text-right">{formatPrice(order.totalAmount)}</p>
                </div>
                <ChevronRight
                  aria-hidden
                  className="size-5 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5"
                />
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
