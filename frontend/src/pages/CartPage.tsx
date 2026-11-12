import { ShoppingBag, Trash2, TriangleAlert } from 'lucide-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router'
import type { Cart, CartItem } from '@/api/cart'
import { FormError } from '@/components/FormError'
import { QuantityStepper } from '@/components/QuantityStepper'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useCart, useRemoveFromCart, useSetCartQuantity } from '@/features/cart/useCart'
import { ImagePlaceholder } from '@/features/catalog/ProductCard'
import { formatPrice } from '@/lib/format'
import { cn } from '@/lib/utils'

function Warning({ children }: { children: ReactNode }) {
  return (
    <p className="mt-2 flex items-start gap-1.5 text-sm font-medium text-destructive">
      <TriangleAlert aria-hidden className="mt-0.5 size-4 shrink-0" />
      <span>{children}</span>
    </p>
  )
}

function CartLine({ item }: { item: CartItem }) {
  const setQuantity = useSetCartQuantity()
  const remove = useRemoveFromCart()
  const isBusy = setQuantity.isPending || remove.isPending
  const error = setQuantity.error ?? remove.error

  const changeQuantity = (quantity: number) =>
    setQuantity.mutate({ productId: item.productId, quantity })

  return (
    <li className={cn('flex gap-4 py-6 transition-opacity sm:gap-6', isBusy && 'opacity-60')}>
      <Link
        to={`/products/${item.slug}`}
        className="size-24 shrink-0 overflow-hidden rounded-xl bg-secondary sm:size-32"
        tabIndex={-1}
        aria-hidden
      >
        {item.imageUrl ? (
          <img src={item.imageUrl} alt="" className="size-full object-cover" />
        ) : (
          <ImagePlaceholder className="size-full" />
        )}
      </Link>

      <div className="flex min-w-0 flex-1 flex-col">
        <div className="flex items-start justify-between gap-4">
          <div className="min-w-0">
            <h2 className="font-sans text-base font-medium tracking-normal">
              <Link to={`/products/${item.slug}`} className="hover:underline hover:underline-offset-4">
                {item.name}
              </Link>
            </h2>
            <p className="mt-1 text-sm text-muted-foreground">{formatPrice(item.unitPrice)} each</p>
          </div>
          <p className="shrink-0 font-semibold">{formatPrice(item.lineTotal)}</p>
        </div>

        {!item.isAvailable && (
          <Warning>This product is no longer sold. Remove it to continue.</Warning>
        )}
        {item.isAvailable && item.hasInsufficientStock && (
          <Warning>
            {item.stockQuantity === 0
              ? 'This product is out of stock. Remove it to continue.'
              : `Only ${item.stockQuantity} left in stock. `}
            {item.stockQuantity > 0 && (
              <button
                type="button"
                onClick={() => changeQuantity(item.stockQuantity)}
                disabled={isBusy}
                className="underline underline-offset-4"
              >
                Change to {item.stockQuantity}
              </button>
            )}
          </Warning>
        )}
        {error && (
          <div className="mt-2">
            <FormError message={error.message} />
          </div>
        )}

        <div className="mt-auto flex items-center justify-between gap-4 pt-4">
          {item.isAvailable && item.stockQuantity > 0 ? (
            <QuantityStepper
              value={item.quantity}
              max={item.stockQuantity}
              onChange={changeQuantity}
              disabled={isBusy}
              label={item.name}
            />
          ) : (
            <span />
          )}
          <Button
            variant="ghost"
            size="lg"
            onClick={() => remove.mutate(item.productId)}
            disabled={isBusy}
            className="text-muted-foreground"
          >
            <Trash2 aria-hidden />
            Remove
            <span className="sr-only">{item.name}</span>
          </Button>
        </div>
      </div>
    </li>
  )
}

function Summary({ cart }: { cart: Cart }) {
  return (
    <aside aria-labelledby="summary-title" className="h-fit rounded-2xl bg-secondary p-6 lg:sticky lg:top-36">
      <h2 id="summary-title" className="text-xl font-semibold">
        Order summary
      </h2>
      <dl className="mt-5 grid gap-3 text-sm">
        <div className="flex justify-between gap-4">
          <dt className="text-muted-foreground">
            {cart.totalQuantity} {cart.totalQuantity === 1 ? 'item' : 'items'}
          </dt>
          <dd>{formatPrice(cart.totalAmount)}</dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-muted-foreground">Delivery</dt>
          <dd>Free</dd>
        </div>
        <div className="flex items-baseline justify-between gap-4 border-t border-foreground/10 pt-4 text-base font-semibold">
          <dt>Total</dt>
          <dd className="text-xl">{formatPrice(cart.totalAmount)}</dd>
        </div>
      </dl>

      {cart.canCheckout ? (
        <Button asChild size="lg" className="mt-6 h-12 w-full text-base">
          <Link to="/checkout">Checkout</Link>
        </Button>
      ) : (
        <>
          <Button size="lg" disabled className="mt-6 h-12 w-full text-base">
            Checkout
          </Button>
          <p className="mt-3 text-sm text-muted-foreground">
            Fix the items marked above to continue.
          </p>
        </>
      )}
    </aside>
  )
}

function EmptyCart() {
  return (
    <div className="flex flex-col items-center py-16 text-center">
      <span className="flex size-16 items-center justify-center rounded-full bg-secondary text-muted-foreground">
        <ShoppingBag aria-hidden className="size-7" strokeWidth={1.5} />
      </span>
      <h2 className="mt-5 text-2xl font-semibold">Your cart is empty</h2>
      <p className="mt-2 text-muted-foreground">Find something you like and it will wait for you here.</p>
      <Button asChild size="lg" className="mt-8 h-11 px-6 text-base">
        <Link to="/products">Browse products</Link>
      </Button>
    </div>
  )
}

export function CartPage() {
  const cart = useCart()

  return (
    <section>
      <h1 className="text-3xl font-semibold sm:text-4xl">Your cart</h1>

      {cart.isPending && (
        <div className="mt-8 grid gap-4" aria-label="Loading cart">
          <Skeleton className="h-32 rounded-2xl" />
          <Skeleton className="h-32 rounded-2xl" />
        </div>
      )}

      {cart.isError && (
        <Alert variant="destructive" className="mt-8">
          <AlertTitle>Your cart could not be loaded</AlertTitle>
          <AlertDescription>{cart.error.message}</AlertDescription>
        </Alert>
      )}

      {cart.data && cart.data.items.length === 0 && <EmptyCart />}

      {cart.data && cart.data.items.length > 0 && (
        <div className="mt-4 grid gap-10 lg:grid-cols-[1fr_22rem]">
          <ul className="divide-y border-b">
            {cart.data.items.map((item) => (
              <CartLine key={item.productId} item={item} />
            ))}
          </ul>
          <Summary cart={cart.data} />
        </div>
      )}
    </section>
  )
}
