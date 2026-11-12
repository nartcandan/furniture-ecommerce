import { useQuery } from '@tanstack/react-query'
import { ChevronLeft, ChevronRight, CreditCard, Truck } from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { ApiError } from '@/api/client'
import { getProduct, type Product } from '@/api/products'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Skeleton } from '@/components/ui/skeleton'
import { AddToCart } from '@/features/cart/AddToCart'
import { ImagePlaceholder } from '@/features/catalog/ProductCard'
import { categoryPath } from '@/features/catalog/useCategories'
import { formatNumber, formatPrice } from '@/lib/format'
import { cn } from '@/lib/utils'
import { NotFoundPage } from '@/pages/NotFoundPage'

const LOW_STOCK = 5

function Gallery({ product }: { product: Product }) {
  const { images } = product
  const [index, setIndex] = useState(0)

  if (images.length === 0) {
    return <ImagePlaceholder className='aspect-square rounded-3xl' />
  }

  const hasSeveral = images.length > 1
  // The arrows go round: after the last photo comes the first.
  const show = (next: number) => setIndex((next + images.length) % images.length)
  const arrowClass =
    'absolute top-1/2 flex size-10 -translate-y-1/2 items-center justify-center rounded-full bg-card/90 text-foreground shadow-sm transition outline-none hover:bg-card focus-visible:ring-3 focus-visible:ring-ring/50 md:opacity-0 md:group-hover:opacity-100 md:focus-visible:opacity-100'

  return (
    <div>
      <div
        className='group relative aspect-square overflow-hidden rounded-3xl bg-secondary'
        role='group'
        aria-roledescription='carousel'
        aria-label='Product photos'
      >
        {/* All photos sit side by side and the row slides, so changing photo is one smooth movement. */}
        <div
          className='flex size-full transition-transform duration-500 ease-out motion-reduce:transition-none'
          style={{ transform: `translateX(-${index * 100}%)` }}
        >
          {images.map((image, position) => (
            <img
              key={image.id}
              src={image.url}
              alt={image.altText ?? `${product.name}, photo ${position + 1}`}
              aria-hidden={position !== index}
              className='size-full shrink-0 object-cover'
            />
          ))}
        </div>

        {hasSeveral && (
          <>
            <button
              type='button'
              onClick={() => show(index - 1)}
              aria-label='Previous photo'
              className={cn(arrowClass, 'left-3')}
            >
              <ChevronLeft aria-hidden className='size-5' />
            </button>
            <button
              type='button'
              onClick={() => show(index + 1)}
              aria-label='Next photo'
              className={cn(arrowClass, 'right-3')}
            >
              <ChevronRight aria-hidden className='size-5' />
            </button>
            <p
              aria-live='polite'
              className='absolute right-3 bottom-3 rounded-full bg-card/90 px-2.5 py-1 text-xs font-medium tabular-nums'
            >
              {index + 1} / {images.length}
            </p>
          </>
        )}
      </div>

      {hasSeveral && (
        <ul className='mt-3 grid grid-cols-5 gap-3'>
          {images.map((image, position) => (
            <li key={image.id}>
              <button
                type='button'
                onClick={() => show(position)}
                aria-label={`Show photo ${position + 1}`}
                aria-pressed={position === index}
                className={cn(
                  'block aspect-square w-full overflow-hidden rounded-xl bg-secondary opacity-70 ring-2 ring-transparent ring-offset-2 ring-offset-background transition outline-none hover:opacity-100 focus-visible:ring-ring',
                  position === index && 'opacity-100 ring-primary',
                )}
              >
                <img src={image.url} alt='' loading='lazy' className='size-full object-cover' />
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function StockStatus({ quantity }: { quantity: number }) {
  if (quantity === 0) {
    return <p className="font-medium text-destructive">Out of stock</p>
  }
  if (quantity <= LOW_STOCK) {
    return <p className="font-medium text-highlight">Only {quantity} left in stock</p>
  }
  return <p className="font-medium text-primary">In stock</p>
}

function Details({ product }: { product: Product }) {
  const { widthCm, depthCm, heightCm, weightKg, material, color } = product
  const rows: [string, string][] = []

  if (widthCm !== null) rows.push(['Width', `${formatNumber(widthCm)} cm`])
  if (depthCm !== null) rows.push(['Depth', `${formatNumber(depthCm)} cm`])
  if (heightCm !== null) rows.push(['Height', `${formatNumber(heightCm)} cm`])
  if (weightKg !== null) rows.push(['Weight', `${formatNumber(weightKg)} kg`])
  if (material) rows.push(['Material', material])
  if (color) rows.push(['Colour', color])

  if (rows.length === 0) {
    return null
  }

  return (
    <section className="mt-8">
      <h2 className="text-xl font-semibold">Details</h2>
      <dl className="mt-3 divide-y border-y text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="flex justify-between gap-6 py-2.5">
            <dt className="text-muted-foreground">{label}</dt>
            <dd className="text-right font-medium">{value}</dd>
          </div>
        ))}
      </dl>
    </section>
  )
}

function LoadingState() {
  return (
    <div className="grid gap-10 lg:grid-cols-2" aria-label="Loading product">
      <Skeleton className="aspect-square rounded-3xl" />
      <div>
        <Skeleton className="h-4 w-1/4" />
        <Skeleton className="mt-4 h-10 w-3/4" />
        <Skeleton className="mt-6 h-8 w-1/3" />
        <Skeleton className="mt-8 h-24 w-full" />
      </div>
    </div>
  )
}

export function ProductDetailPage() {
  const { slug = '' } = useParams()
  const product = useQuery({
    queryKey: ['product', slug],
    queryFn: () => getProduct(slug),
    // A product that does not exist will not appear on a second try.
    retry: (failureCount, error) =>
      !(error instanceof ApiError && error.status === 404) && failureCount < 1,
  })

  if (product.isPending) {
    return <LoadingState />
  }

  if (product.isError) {
    if (product.error instanceof ApiError && product.error.status === 404) {
      return <NotFoundPage />
    }
    return (
      <Alert variant="destructive">
        <AlertTitle>The product could not be loaded</AlertTitle>
        <AlertDescription>{product.error.message}</AlertDescription>
      </Alert>
    )
  }

  const { data } = product

  return (
    <article>
      <nav aria-label="Breadcrumb">
        <ol className="flex flex-wrap items-center gap-1.5 text-sm text-muted-foreground">
          <li>
            <Link to="/products" className="hover:text-foreground">
              All products
            </Link>
          </li>
          <ChevronRight aria-hidden className="size-3.5" />
          <li>
            <Link to={categoryPath(data.categorySlug)} className="hover:text-foreground">
              {data.categoryName}
            </Link>
          </li>
          <ChevronRight aria-hidden className="size-3.5" />
          <li aria-current="page" className="text-foreground">
            {data.name}
          </li>
        </ol>
      </nav>

      <div className="mt-6 grid gap-10 lg:grid-cols-2 lg:gap-14">
        {/* The key starts the gallery again when the address changes to another product. */}
        <Gallery key={data.id} product={data} />

        <div>
          <p className="text-sm tracking-wide text-muted-foreground uppercase">{data.categoryName}</p>
          <h1 className="mt-2 text-4xl leading-tight font-semibold">{data.name}</h1>
          <p className="mt-4 text-3xl font-semibold">{formatPrice(data.price)}</p>
          <div className="mt-2 text-sm">
            <StockStatus quantity={data.stockQuantity} />
          </div>

          {data.description && (
            <p className="mt-6 leading-relaxed whitespace-pre-line text-muted-foreground">
              {data.description}
            </p>
          )}

          <div className="mt-8">
            {/* The key resets the quantity when the address changes to another product. */}
            <AddToCart key={data.id} product={data} />
          </div>

          <ul className="mt-6 grid gap-3 rounded-2xl bg-secondary p-5 text-sm">
            <li className="flex items-center gap-3">
              <Truck aria-hidden className="size-5 shrink-0 text-primary" />
              Free delivery anywhere in Türkiye
            </li>
            <li className="flex items-center gap-3">
              <CreditCard aria-hidden className="size-5 shrink-0 text-primary" />
              Secure card payment, with same-day cancellation
            </li>
          </ul>

          <Details product={data} />
        </div>
      </div>
    </article>
  )
}
