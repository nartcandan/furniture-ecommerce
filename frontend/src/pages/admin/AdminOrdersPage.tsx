import { ReceiptText } from 'lucide-react'
import { Link, useSearchParams } from 'react-router'
import type { OrderStatus } from '@/api/orders'
import { Pagination } from '@/components/Pagination'
import { Skeleton } from '@/components/ui/skeleton'
import { QueryError } from '@/components/QueryError'
import { EmptyState } from '@/components/EmptyState'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { useAdminOrders } from '@/features/admin/orders/useAdminOrders'
import { OrderStatusBadge } from '@/features/orders/orderStatus'
import { orderStatusLabel } from '@/features/orders/orderStatusLabels'
import { formatDateTime, formatNumber, formatPrice, orderNumber } from '@/lib/format'
import { cn } from '@/lib/utils'

const PAGE_SIZE = 20

/** The filters, in the order the admin works through them: what to ship first. */
const filters: { status?: OrderStatus; label: string }[] = [
  { label: 'All' },
  { status: 'Paid', label: 'To ship' },
  { status: 'Shipped', label: orderStatusLabel('Shipped') },
  { status: 'Delivered', label: orderStatusLabel('Delivered') },
  { status: 'PendingPayment', label: orderStatusLabel('PendingPayment') },
  { status: 'Cancelling', label: orderStatusLabel('Cancelling') },
  { status: 'Cancelled', label: orderStatusLabel('Cancelled') },
  { status: 'PaymentFailed', label: orderStatusLabel('PaymentFailed') },
]

function filterPath(status: OrderStatus | undefined, page = 1): string {
  const params = new URLSearchParams()
  if (status) params.set('status', status)
  if (page > 1) params.set('page', String(page))
  const query = params.toString()
  return query ? `/admin/orders?${query}` : '/admin/orders'
}

export function AdminOrdersPage() {
  // The filter lives in the address, so a reload or the back button keeps it.
  const [params] = useSearchParams()
  const statusParam = params.get('status')
  const status = filters.find((filter) => filter.status === statusParam)?.status
  const pageParam = Number(params.get('page'))
  const page = Number.isInteger(pageParam) && pageParam >= 1 ? pageParam : 1
  const orders = useAdminOrders({ status, page, pageSize: PAGE_SIZE })

  return (
    <div>
      <h1 className="text-2xl font-semibold">Orders</h1>
      <p className="mt-1 text-sm text-muted-foreground">
        All customers, newest first. Paid orders wait to be shipped.
      </p>

      <nav aria-label="Order status" className="mt-6 -mx-4 overflow-x-auto px-4 [scrollbar-width:none]">
        <ul className="flex gap-1.5">
          {filters.map((filter) => {
            const active = filter.status === status
            return (
              <li key={filter.label} className="shrink-0">
                <Link
                  to={filterPath(filter.status)}
                  aria-current={active ? 'page' : undefined}
                  className={cn(
                    'inline-flex h-8 items-center rounded-full border px-3 text-sm transition-colors',
                    active
                      ? 'border-primary bg-primary text-primary-foreground'
                      : 'bg-background text-muted-foreground hover:bg-muted hover:text-foreground',
                  )}
                >
                  {filter.label}
                </Link>
              </li>
            )
          })}
        </ul>
      </nav>

      {orders.isPending && (
        <div className="mt-4 grid gap-2" aria-label="Loading orders">
          {Array.from({ length: 6 }, (_, index) => (
            <Skeleton key={index} className="h-14" />
          ))}
        </div>
      )}

      {orders.isError && (
        <QueryError title="The orders could not be loaded" query={orders} className="mt-4" />
      )}

      {orders.data?.items.length === 0 && (
        <EmptyState
          icon={ReceiptText}
          title={
            status === 'Paid' ? 'Nothing to ship' : status ? 'No orders with this status' : 'No orders yet'
          }
          className="mt-4"
        />
      )}

      {orders.data && orders.data.items.length > 0 && (
        <>
          <p className="mt-4 text-sm text-muted-foreground">
            {formatNumber(orders.data.totalCount)} {orders.data.totalCount === 1 ? 'order' : 'orders'}
          </p>
          <div
            className={cn(
              'mt-2 rounded-xl border bg-card transition-opacity',
              orders.isPlaceholderData && 'opacity-60',
            )}
          >
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="pl-4">Order</TableHead>
                  <TableHead>Recipient</TableHead>
                  <TableHead className="hidden text-right sm:table-cell">Items</TableHead>
                  <TableHead className="text-right">Total</TableHead>
                  <TableHead className="pr-4">Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {orders.data.items.map((order) => (
                  <TableRow key={order.id} className="relative">
                    <TableCell className="pl-4">
                      {/* The link covers the whole row, so any part of it opens the order. */}
                      <Link
                        to={`/admin/orders/${order.id}`}
                        className="block font-mono font-medium after:absolute after:inset-0 hover:underline"
                      >
                        #{orderNumber(order.id)}
                      </Link>
                      <span className="block text-xs text-muted-foreground">
                        {formatDateTime(order.createdAt)}
                      </span>
                    </TableCell>
                    <TableCell className="max-w-48">
                      <span className="block truncate">{order.recipientName}</span>
                      <span className="block truncate text-xs text-muted-foreground">{order.city}</span>
                    </TableCell>
                    <TableCell className="hidden text-right tabular-nums sm:table-cell">
                      {formatNumber(order.totalQuantity)}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{formatPrice(order.totalAmount)}</TableCell>
                    <TableCell className="pr-4">
                      <OrderStatusBadge status={order.status} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
          <Pagination
            page={orders.data.page}
            totalPages={orders.data.totalPages}
            pathFor={(target) => filterPath(status, target)}
          />
        </>
      )}
    </div>
  )
}
