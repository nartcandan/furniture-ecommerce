import { ChevronLeft, PackageCheck, Truck } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router'
import { toast } from 'sonner'
import { ApiError } from '@/api/client'
import type { AdminOrder } from '@/api/orders'
import { FormError } from '@/components/FormError'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Skeleton } from '@/components/ui/skeleton'
import { QueryError } from '@/components/QueryError'
import { AddressText } from '@/features/addresses/AddressText'
import { useAdminOrder, useDeliverOrder, useShipOrder } from '@/features/admin/orders/useAdminOrders'
import { OrderStatusBadge } from '@/features/orders/orderStatus'
import { formatDateTime, formatPrice, orderNumber } from '@/lib/format'

function BackLink() {
  return (
    <Link
      to="/admin/orders"
      className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
    >
      <ChevronLeft aria-hidden className="size-4" />
      Orders
    </Link>
  )
}

type Step = 'ship' | 'deliver'

const steps: Record<Step, { button: string; title: string; description: string; done: string }> = {
  ship: {
    button: 'Mark as shipped',
    title: 'Mark this order as shipped?',
    description:
      'The customer can no longer cancel it, and the panel cannot take this step back. Do this once the parcel has left.',
    done: 'The order is marked as shipped',
  },
  deliver: {
    button: 'Mark as delivered',
    title: 'Mark this order as delivered?',
    description: 'This closes the order. The panel cannot take this step back.',
    done: 'The order is marked as delivered',
  },
}

/** The next step for the admin, if the order has one. Every other status changes on its own. */
function nextStep(order: AdminOrder): Step | null {
  if (order.status === 'Paid') return 'ship'
  if (order.status === 'Shipped') return 'deliver'
  return null
}

/** What the admin should know about an order they cannot move on. */
function statusNote(order: AdminOrder): string | null {
  switch (order.status) {
    case 'PendingPayment':
      return 'The customer has not paid yet. Ship it once it is paid.'
    case 'Cancelling':
      return 'The customer is cancelling and the refund is being confirmed with iyzico. Do not ship it.'
    case 'Cancelled':
    case 'PaymentFailed':
      return order.paymentError ?? null
    case 'Delivered':
      return 'This order is complete.'
    default:
      return null
  }
}

function StepAction({ order }: { order: AdminOrder }) {
  const ship = useShipOrder(order.id)
  const deliver = useDeliverOrder(order.id)
  const step = nextStep(order)
  // The dialog keeps the step it was opened for. If the order moves on meanwhile (shipped from another tab,
  // or cancelled by the customer), the dialog must not quietly turn into the next step's question.
  const [openStep, setOpenStep] = useState<Step | null>(null)
  const shown = openStep ?? step
  const mutation = shown === 'deliver' ? deliver : ship
  const stale = openStep !== null && openStep !== step

  function close() {
    if (mutation.isPending) return
    mutation.reset()
    setOpenStep(null)
  }

  if (!shown) return null
  const text = steps[shown]

  return (
    <>
      {step && (
        <Button onClick={() => setOpenStep(step)}>
          {step === 'ship' ? <Truck aria-hidden /> : <PackageCheck aria-hidden />}
          {steps[step].button}
        </Button>
      )}
      <Dialog open={openStep !== null} onOpenChange={(next) => !next && close()}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{text.title}</DialogTitle>
            <DialogDescription>{text.description}</DialogDescription>
          </DialogHeader>
          {/* A 409 means the order changed meanwhile, for example the customer cancelled it. */}
          <FormError
            message={
              mutation.error?.message ??
              (stale ? 'This order changed meanwhile. Close this and check its status.' : undefined)
            }
          />
          <DialogFooter>
            <Button variant="ghost" disabled={mutation.isPending} onClick={close}>
              {stale ? 'Close' : 'Not yet'}
            </Button>
            {!stale && (
              <Button
                disabled={mutation.isPending}
                onClick={() =>
                  mutation.mutate(undefined, {
                    onSuccess: () => {
                      toast.success(text.done)
                      mutation.reset()
                      setOpenStep(null)
                    },
                  })
                }
              >
                {mutation.isPending ? 'Saving…' : text.button}
              </Button>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}

function Detail({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex justify-between gap-4 py-1.5 text-sm">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="text-right break-all">{children}</dd>
    </div>
  )
}

export function AdminOrderPage() {
  const { id = '' } = useParams()
  const order = useAdminOrder(id)

  if (order.isPending) {
    return (
      <div className="grid gap-4" aria-label="Loading order">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-64" />
      </div>
    )
  }

  if (order.isError) {
    const missing = order.error instanceof ApiError && [400, 404].includes(order.error.status)
    return (
      <div>
        <BackLink />
        {missing ? (
          <Alert variant="destructive" className="mt-4">
            <AlertTitle>This order does not exist</AlertTitle>
            <AlertDescription>It may have been typed wrong.</AlertDescription>
          </Alert>
        ) : (
          <QueryError title="The order could not be loaded" query={order} className="mt-4" />
        )}
      </div>
    )
  }

  const data = order.data
  const note = statusNote(data)

  return (
    <div>
      <BackLink />
      <div className="mt-2 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-mono text-2xl font-semibold">#{orderNumber(data.id)}</h1>
          <div className="mt-2 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
            <OrderStatusBadge status={data.status} />
            <span>Placed {formatDateTime(data.createdAt)}</span>
          </div>
        </div>
        <StepAction order={data} />
      </div>

      {note && (
        <p className="mt-4 rounded-lg bg-secondary px-4 py-3 text-sm text-secondary-foreground">{note}</p>
      )}

      <div className="mt-6 grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <section aria-labelledby="items-title" className="rounded-xl border bg-card">
          <h2 id="items-title" className="px-5 pt-5 font-sans text-sm font-semibold tracking-normal">
            Items
          </h2>
          <ul className="mt-2 divide-y">
            {data.items.map((item) => (
              <li key={item.productId} className="flex items-baseline justify-between gap-4 px-5 py-3 text-sm">
                <div className="min-w-0">
                  <Link to={`/admin/products/${item.productId}`} className="font-medium hover:underline">
                    {item.productName}
                  </Link>
                  <p className="text-muted-foreground">
                    {item.quantity} × {formatPrice(item.unitPrice)}
                  </p>
                </div>
                <p className="shrink-0 font-medium tabular-nums">{formatPrice(item.lineTotal)}</p>
              </li>
            ))}
          </ul>
          <div className="flex justify-between border-t px-5 py-4 font-semibold">
            <span>Total</span>
            <span className="tabular-nums">{formatPrice(data.totalAmount)}</span>
          </div>
        </section>

        <div className="grid gap-5">
          <section aria-labelledby="address-title" className="rounded-xl border bg-card p-5">
            <h2 id="address-title" className="mb-3 font-sans text-sm font-semibold tracking-normal">
              Delivery address
            </h2>
            <AddressText address={data} />
          </section>

          <section aria-labelledby="payment-title" className="rounded-xl border bg-card p-5">
            <h2 id="payment-title" className="mb-2 font-sans text-sm font-semibold tracking-normal">
              Payment
            </h2>
            <dl className="divide-y">
              <Detail label="Paid at">{data.paidAt ? formatDateTime(data.paidAt) : '—'}</Detail>
              <Detail label="iyzico payment">
                <span className="font-mono">{data.paymentId ?? '—'}</span>
              </Detail>
              <Detail label="Last change">{data.updatedAt ? formatDateTime(data.updatedAt) : '—'}</Detail>
              <Detail label="Customer id">
                <span className="font-mono text-xs">{data.userId}</span>
              </Detail>
            </dl>
          </section>
        </div>
      </div>
    </div>
  )
}
