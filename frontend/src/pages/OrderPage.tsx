import { useQuery } from '@tanstack/react-query'
import { CircleAlert, CircleCheck, Clock, PackageCheck, Truck, type LucideIcon } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import { ApiError } from '@/api/client'
import { getOrder, type Order, type OrderStatus } from '@/api/orders'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { AddressText } from '@/features/addresses/AddressText'
import { CancelOrderDialog } from '@/features/orders/CancelOrderDialog'
import { OrderStatusBadge } from '@/features/orders/orderStatus'
import { UnpaidOrderActions } from '@/features/orders/UnpaidOrderActions'
import { formatDateTime, formatPrice, orderNumber } from '@/lib/format'
import { cn } from '@/lib/utils'
import { NotFoundPage } from '@/pages/NotFoundPage'

// These two statuses end by themselves: a payment arrives or times out, a refund is confirmed.
const unsettled: OrderStatus[] = ['PendingPayment', 'Cancelling']

type Tone = 'good' | 'waiting' | 'bad' | 'neutral'

const toneClasses: Record<Tone, string> = {
  good: 'border-primary/20 bg-primary/8 text-primary',
  waiting: 'border-highlight/30 bg-highlight/10 text-highlight',
  bad: 'border-destructive/25 bg-destructive/8 text-destructive',
  neutral: 'border-border bg-muted text-foreground',
}

function Banner({
  tone,
  icon: Icon,
  title,
  children,
}: {
  tone: Tone
  icon: LucideIcon
  title: string
  children: ReactNode
}) {
  return (
    <div role="status" className={cn('flex gap-3.5 rounded-2xl border p-5', toneClasses[tone])}>
      <Icon aria-hidden className="mt-0.5 size-6 shrink-0" />
      <div className="min-w-0 flex-1">
        <h2 className="font-sans text-base font-semibold tracking-normal">{title}</h2>
        <div className="mt-1 text-sm text-foreground/80">{children}</div>
      </div>
    </div>
  )
}

/** A note about a payment that arrived after the order had been closed. It comes from the address of the page. */
function latePaymentNote(latePayment: string | null): string | null {
  if (latePayment === 'refunded') {
    return 'A payment arrived after this order was closed. It was refunded in full.'
  }
  if (latePayment === 'refund-failed') {
    return 'A payment arrived after this order was closed and could not be refunded automatically. Please contact us.'
  }
  return null
}

/** What happened to the order, said from its real status. The address only adds a note about a late payment. */
function StatusBanner({ order, latePayment }: { order: Order; latePayment: string | null }) {
  const lateNote = latePaymentNote(latePayment)

  switch (order.status) {
    case 'PendingPayment':
      return (
        <Banner tone="waiting" icon={Clock} title="Waiting for your payment">
          <p>
            {order.paymentPageUrl
              ? 'Your order is confirmed once the payment is made. The payment page stays open for about half an hour after the order was placed.'
              : 'The payment page has expired. If a payment was made, this page shows it shortly. Otherwise the order is closed within a few minutes and you can order again.'}
          </p>
          <UnpaidOrderActions order={order} className="mt-4" />
        </Banner>
      )
    case 'Paid':
      return (
        <Banner tone="good" icon={CircleCheck} title="Thank you, your order is confirmed">
          We received your payment and are getting your order ready.
        </Banner>
      )
    case 'Shipped':
      return (
        <Banner tone="good" icon={Truck} title="Your order is on its way">
          It has been handed to the carrier.
        </Banner>
      )
    case 'Delivered':
      return (
        <Banner tone="good" icon={PackageCheck} title="Your order was delivered">
          We hope you enjoy it.
        </Banner>
      )
    case 'Cancelling':
      return (
        <Banner tone="waiting" icon={Clock} title="Your cancellation is being processed">
          We are confirming the refund with the payment provider. This page updates by itself.
        </Banner>
      )
    case 'PaymentFailed':
      return (
        <Banner tone="bad" icon={CircleAlert} title="The payment did not go through">
          {lateNote ?? 'This order was not charged.'} Your items are still in{' '}
          <Link to="/cart" className="font-medium underline underline-offset-4">
            your cart
          </Link>
          , so you can try again.
        </Banner>
      )
    case 'Cancelled':
      return (
        <Banner tone="neutral" icon={CircleAlert} title="This order was cancelled">
          {lateNote ??
            (order.paidAt
              ? 'Your payment was refunded in full.'
              : 'It was not paid, and nothing was charged.')}
        </Banner>
      )
  }
}

/** Offered for a paid order on the day of payment. An unpaid order has its own actions in the banner. */
function CancelPaidOrder({ order }: { order: Order }) {
  const [isConfirming, setIsConfirming] = useState(false)

  return (
    <div className="mt-8 rounded-2xl border bg-card p-5">
      <h2 className="font-sans text-base font-semibold tracking-normal">Changed your mind?</h2>
      <p className="mt-1 text-sm text-muted-foreground">
        You can cancel this order until the end of the day you paid. The payment is refunded in full.
      </p>
      <Button variant="outline" size="lg" className="mt-4 h-10" onClick={() => setIsConfirming(true)}>
        Cancel order
      </Button>
      <CancelOrderDialog order={order} open={isConfirming} onOpenChange={setIsConfirming} />
    </div>
  )
}

export function OrderPage() {
  const { id = '' } = useParams()
  const [searchParams] = useSearchParams()
  const order = useQuery({
    queryKey: ['order', id],
    queryFn: () => getOrder(id),
    retry: (failureCount, error) =>
      !(error instanceof ApiError && [400, 404].includes(error.status)) && failureCount < 1,
    refetchInterval: (query) =>
      query.state.data && unsettled.includes(query.state.data.status) ? 5000 : false,
  })

  if (order.isPending) {
    return (
      <div className="grid gap-4" aria-label="Loading order">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-24 rounded-2xl" />
        <Skeleton className="h-48 rounded-2xl" />
      </div>
    )
  }

  if (order.isError) {
    // Someone else's order answers 404 too, and an id that is not a valid one answers 400.
    if (order.error instanceof ApiError && [400, 404].includes(order.error.status)) {
      return <NotFoundPage />
    }
    return (
      <Alert variant="destructive">
        <AlertTitle>The order could not be loaded</AlertTitle>
        <AlertDescription>{order.error.message}</AlertDescription>
      </Alert>
    )
  }

  const { data } = order

  return (
    <article>
      <Link to="/orders" className="text-sm text-muted-foreground hover:text-foreground">
        ← My orders
      </Link>
      <div className="mt-3 flex flex-wrap items-center gap-3">
        <h1 className="text-3xl font-semibold sm:text-4xl">Order #{orderNumber(data.id)}</h1>
        <OrderStatusBadge status={data.status} />
      </div>
      <p className="mt-2 text-sm text-muted-foreground">Placed on {formatDateTime(data.createdAt)}</p>

      <div className="mt-6">
        <StatusBanner order={data} latePayment={searchParams.get('payment')} />
      </div>

      <div className="mt-8 grid gap-10 lg:grid-cols-[1fr_22rem]">
        <section aria-labelledby="items-title">
          <h2 id="items-title" className="text-xl font-semibold">
            Items
          </h2>
          <ul className="mt-3 divide-y border-y">
            {data.items.map((item) => (
              <li key={item.productId} className="flex justify-between gap-4 py-4">
                <div className="min-w-0">
                  <p className="font-medium">{item.productName}</p>
                  <p className="mt-0.5 text-sm text-muted-foreground">
                    {item.quantity} × {formatPrice(item.unitPrice)}
                  </p>
                </div>
                <p className="shrink-0 font-semibold">{formatPrice(item.lineTotal)}</p>
              </li>
            ))}
          </ul>
          <dl className="mt-4 grid gap-2 text-sm">
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Delivery</dt>
              <dd>Free</dd>
            </div>
            <div className="flex items-baseline justify-between gap-4 text-base font-semibold">
              <dt>Total</dt>
              <dd className="text-xl">{formatPrice(data.totalAmount)}</dd>
            </div>
          </dl>

          {data.status === 'Paid' && data.canCancel && <CancelPaidOrder order={data} />}
        </section>

        <aside className="grid h-fit gap-6 rounded-2xl bg-secondary p-6">
          <div>
            <h2 className="font-sans text-sm font-semibold tracking-normal">Delivery address</h2>
            <div className="mt-2">
              <AddressText address={data} />
            </div>
          </div>
          <div>
            <h2 className="font-sans text-sm font-semibold tracking-normal">Payment</h2>
            <p className="mt-2 text-sm text-muted-foreground">
              {data.paidAt ? `Paid by card on ${formatDateTime(data.paidAt)}` : 'Not paid'}
            </p>
          </div>
        </aside>
      </div>
    </article>
  )
}
