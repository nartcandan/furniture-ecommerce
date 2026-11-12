import { Armchair } from 'lucide-react'
import { Link } from 'react-router'
import type { Product } from '@/api/products'
import { Badge } from '@/components/ui/badge'
import { formatPrice } from '@/lib/format'
import { cn } from '@/lib/utils'

/** Shown where a product has no photo yet. */
export function ImagePlaceholder({ className }: { className?: string }) {
  return (
    <div className={cn('flex items-center justify-center bg-secondary text-muted-foreground/40', className)}>
      <Armchair aria-hidden className="size-1/4" strokeWidth={1.25} />
    </div>
  )
}

export function ProductCard({ product }: { product: Product }) {
  const image = product.images[0]
  const isOutOfStock = product.stockQuantity === 0

  return (
    <Link
      to={`/products/${product.slug}`}
      className="group block rounded-2xl outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
    >
      <div className="relative aspect-[4/5] overflow-hidden rounded-2xl bg-secondary">
        {image ? (
          <img
            src={image.url}
            alt={image.altText ?? ''}
            loading="lazy"
            className={cn(
              'size-full object-cover transition-transform duration-500 group-hover:scale-105',
              isOutOfStock && 'opacity-60',
            )}
          />
        ) : (
          <ImagePlaceholder className="size-full" />
        )}
        {isOutOfStock && (
          <Badge variant="secondary" className="absolute top-3 left-3 bg-card text-foreground">
            Out of stock
          </Badge>
        )}
      </div>
      <div className="px-1 pt-3">
        <p className="text-xs tracking-wide text-muted-foreground uppercase">{product.categoryName}</p>
        <h3 className="mt-1 font-sans text-base leading-snug font-medium tracking-normal group-hover:underline group-hover:underline-offset-4">
          {product.name}
        </h3>
        <p className="mt-1 font-semibold">{formatPrice(product.price)}</p>
      </div>
    </Link>
  )
}

export function ProductCardSkeleton() {
  return (
    <div aria-hidden>
      <div className="aspect-[4/5] animate-pulse rounded-2xl bg-muted" />
      <div className="mt-3 h-3 w-1/3 animate-pulse rounded bg-muted" />
      <div className="mt-2 h-4 w-2/3 animate-pulse rounded bg-muted" />
      <div className="mt-2 h-4 w-1/4 animate-pulse rounded bg-muted" />
    </div>
  )
}
