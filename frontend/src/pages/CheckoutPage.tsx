import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Lock, Plus } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, Navigate } from 'react-router'
import type { Address } from '@/api/addresses'
import type { Cart } from '@/api/cart'
import { createOrder, getOrder, getOrders } from '@/api/orders'
import { FormError } from '@/components/FormError'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Skeleton } from '@/components/ui/skeleton'
import { AddressForm } from '@/features/addresses/AddressForm'
import { AddressText } from '@/features/addresses/AddressText'
import { useAddresses } from '@/features/addresses/useAddresses'
import { useSession } from '@/features/auth/authStore'
import { useCart } from '@/features/cart/useCart'
import { UnpaidOrderActions } from '@/features/orders/UnpaidOrderActions'
import { formatDateTime, formatPrice, orderNumber } from '@/lib/format'
import { cn } from '@/lib/utils'

function AddressChoice({
  addresses,
  selectedId,
  onSelect,
}: {
  addresses: Address[]
  selectedId: string | undefined
  onSelect: (id: string) => void
}) {
  return (
    <RadioGroup value={selectedId} onValueChange={onSelect} className="grid gap-3 sm:grid-cols-2">
      {addresses.map((address) => (
        <label
          key={address.id}
          className={cn(
            'flex cursor-pointer gap-3 rounded-2xl border bg-card p-4 transition-colors',
            address.id === selectedId ? 'border-primary ring-1 ring-primary' : 'hover:border-input',
          )}
        >
          <RadioGroupItem value={address.id} className="mt-0.5" />
          <span className="min-w-0">
            <span className="block font-medium">{address.title}</span>
            <span className="mt-1 block">
              <AddressText address={address} />
            </span>
          </span>
        </label>
      ))}
    </RadioGroup>
  )
}

function Summary({ cart }: { cart: Cart }) {
  return (
    <>
      <h2 className="text-xl font-semibold">Order summary</h2>
      <ul className="mt-4 grid gap-3 text-sm">
        {cart.items.map((item) => (
          <li key={item.productId} className="flex justify-between gap-4">
            <span className="min-w-0">
              <span className="font-medium">{item.name}</span>
              <span className="text-muted-foreground"> × {item.quantity}</span>
            </span>
            <span className="shrink-0">{formatPrice(item.lineTotal)}</span>
          </li>
        ))}
      </ul>
      <dl className="mt-4 grid gap-3 border-t border-foreground/10 pt-4 text-sm">
        <div className="flex justify-between gap-4">
          <dt className="text-muted-foreground">Delivery</dt>
          <dd>Free</dd>
        </div>
        <div className="flex items-baseline justify-between gap-4 text-base font-semibold">
          <dt>Total</dt>
          <dd className="text-xl">{formatPrice(cart.totalAmount)}</dd>
        </div>
      </dl>
    </>
  )
}

/**
 * A customer can have one order waiting for payment at a time. While there is one, checkout offers to
 * finish or cancel it, because a new order would be refused.
 */
function UnpaidOrderNotice({ orderId }: { orderId: string }) {
  const order = useQuery({ queryKey: ['order', orderId], queryFn: () => getOrder(orderId) })

  return (
    <div className="mt-6 rounded-xl border border-highlight/30 bg-card p-4">
      <h3 className="font-sans text-base font-semibold tracking-normal">
        You have an order waiting for payment
      </h3>
      {order.isPending && <Skeleton className="mt-3 h-16" />}
      {order.isError && <p className="mt-2 text-sm text-destructive">{order.error.message}</p>}
      {order.data && (
        <>
          <p className="mt-1 text-sm text-muted-foreground">
            <Link to={`/orders/${order.data.id}`} className="font-medium text-foreground underline underline-offset-4">
              Order #{orderNumber(order.data.id)}
            </Link>{' '}
            for {formatPrice(order.data.totalAmount)}, placed on {formatDateTime(order.data.createdAt)}.{' '}
            {order.data.paymentPageUrl
              ? 'Finish paying for it, or cancel it to place a new order.'
              : 'Its payment page has expired. Cancel it to place a new order.'}
          </p>
          <UnpaidOrderActions order={order.data} className="mt-4" />
        </>
      )}
    </div>
  )
}

export function CheckoutPage() {
  const user = useSession()?.user
  const queryClient = useQueryClient()
  const cart = useCart()
  const addresses = useAddresses()
  const orders = useQuery({ queryKey: ['orders'], queryFn: getOrders })
  const [chosenId, setChosenId] = useState<string>()
  const [isAdding, setIsAdding] = useState(false)

  const placeOrder = useMutation({
    mutationFn: createOrder,
    onSuccess: (order) => {
      // The payment page belongs to iyzico, so this leaves the app. iyzico sends the browser back to the order.
      window.location.assign(order.paymentPageUrl ?? `/orders/${order.id}`)
    },
    // A refusal usually means the stock or a price changed, and the cart shows what. It can also mean
    // that an order is already waiting for payment, which the order list shows.
    onError: () => {
      void queryClient.invalidateQueries({ queryKey: ['cart'] })
      void queryClient.invalidateQueries({ queryKey: ['orders'] })
    },
  })

  // The back button on the payment page brings this page back exactly as it was left, with the button still
  // saying that the payment page is opening. The page is put back to work and its data is read again.
  const resetPlaceOrder = placeOrder.reset
  useEffect(() => {
    function handlePageShow(event: PageTransitionEvent) {
      if (event.persisted) {
        resetPlaceOrder()
        void queryClient.invalidateQueries()
      }
    }
    window.addEventListener('pageshow', handlePageShow)
    return () => window.removeEventListener('pageshow', handlePageShow)
  }, [resetPlaceOrder, queryClient])

  if (cart.isPending || addresses.isPending) {
    return (
      <div className="grid gap-4" aria-label="Loading checkout">
        <Skeleton className="h-10 w-48" />
        <Skeleton className="h-40 rounded-2xl" />
      </div>
    )
  }

  if (cart.isError || addresses.isError) {
    return (
      <Alert variant="destructive">
        <AlertTitle>Checkout could not be loaded</AlertTitle>
        <AlertDescription>{(cart.error ?? addresses.error)?.message}</AlertDescription>
      </Alert>
    )
  }

  // While the browser is on its way to the payment page, the order is already made and the page must not react to it.
  const isLeaving = placeOrder.isSuccess
  if (cart.data.items.length === 0 && !isLeaving) {
    return <Navigate to="/cart" replace />
  }

  // The first address is chosen until the user picks another, also right after the first one is added.
  const selectedId =
    addresses.data.find((address) => address.id === chosenId)?.id ?? addresses.data[0]?.id
  const isBusy = placeOrder.isPending || isLeaving
  const unpaidOrder = isLeaving
    ? undefined
    : orders.data?.find((order) => order.status === 'PendingPayment')
  const defaultName = user && { firstName: user.firstName, lastName: user.lastName }

  function handleSaved(address: Address) {
    setChosenId(address.id)
    setIsAdding(false)
  }

  return (
    <div>
      <h1 className="text-3xl font-semibold sm:text-4xl">Checkout</h1>

      <div className="mt-8 grid gap-10 lg:grid-cols-[1fr_22rem]">
        <section aria-labelledby="address-title">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <h2 id="address-title" className="text-xl font-semibold">
              Delivery address
            </h2>
            {addresses.data.length > 0 && (
              <Button variant="outline" size="lg" className="h-10" onClick={() => setIsAdding(true)}>
                <Plus aria-hidden />
                New address
              </Button>
            )}
          </div>

          {addresses.data.length === 0 ? (
            <div className="mt-4 rounded-2xl border bg-card p-6">
              <p className="mb-5 text-sm text-muted-foreground">
                Add the address we should deliver to. It is saved for your next order.
              </p>
              <AddressForm defaultName={defaultName} onSaved={handleSaved} />
            </div>
          ) : (
            <div className="mt-4">
              <AddressChoice
                addresses={addresses.data}
                selectedId={selectedId}
                onSelect={setChosenId}
              />
            </div>
          )}

          <Dialog open={isAdding} onOpenChange={setIsAdding}>
            <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
              <DialogHeader>
                <DialogTitle>New address</DialogTitle>
                <DialogDescription>It is saved for your next order too.</DialogDescription>
              </DialogHeader>
              <AddressForm
                defaultName={defaultName}
                onSaved={handleSaved}
                onCancel={() => setIsAdding(false)}
              />
            </DialogContent>
          </Dialog>
        </section>

        <aside className="h-fit rounded-2xl bg-secondary p-6 lg:sticky lg:top-36">
          <Summary cart={cart.data} />

          {!cart.data.canCheckout && !isLeaving && (
            <p className="mt-5 text-sm font-medium text-destructive">
              Some items in your cart are no longer available.{' '}
              <Link to="/cart" className="underline underline-offset-4">
                Review your cart
              </Link>
            </p>
          )}

          {unpaidOrder ? (
            // After the order is cancelled the list is read again, and the pay button comes back.
            <UnpaidOrderNotice key={unpaidOrder.id} orderId={unpaidOrder.id} />
          ) : (
            <>
              {placeOrder.error && (
                <div className="mt-5">
                  <FormError message={placeOrder.error.message} />
                </div>
              )}

              <Button
                size="lg"
                className="mt-6 h-12 w-full text-base"
                disabled={isBusy || !selectedId || !cart.data.canCheckout}
                onClick={() => selectedId && placeOrder.mutate(selectedId)}
              >
                <Lock aria-hidden />
                {isBusy ? 'Opening the payment page…' : `Pay ${formatPrice(cart.data.totalAmount)}`}
              </Button>
              <p className="mt-3 text-center text-xs text-muted-foreground">
                You pay by card on iyzico’s secure payment page.
              </p>
            </>
          )}
        </aside>
      </div>
    </div>
  )
}
