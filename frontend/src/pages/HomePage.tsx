import { useQuery } from '@tanstack/react-query'
import { ArrowRight, CreditCard, RotateCcw, Truck } from 'lucide-react'
import { Link } from 'react-router'
import { getProducts } from '@/api/products'
import { HeroIllustration } from '@/components/HeroIllustration'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { ProductCard, ProductCardSkeleton } from '@/features/catalog/ProductCard'
import { categoryPath, useCategories } from '@/features/catalog/useCategories'
import { cn } from '@/lib/utils'

const promises = [
  { icon: Truck, title: 'Free delivery', text: 'On every order, anywhere in Türkiye.' },
  { icon: CreditCard, title: 'Secure card payment', text: 'You pay on iyzico’s own payment page.' },
  { icon: RotateCcw, title: 'Same-day cancellation', text: 'Cancel on the day you pay for a full refund.' },
]

// Category tiles take these surfaces in turn, so the grid does not look flat.
const tileSurfaces = ['bg-secondary', 'bg-primary/10', 'bg-highlight/15']

function Hero() {
  return (
    <section className="grid items-center gap-10 lg:grid-cols-2">
      <div>
        <p className="text-sm font-medium tracking-wide text-highlight uppercase">
          Furniture &amp; decoration
        </p>
        <h1 className="mt-3 text-4xl leading-[1.1] font-semibold sm:text-5xl">
          A home that feels like yours.
        </h1>
        <p className="mt-5 max-w-md text-lg text-muted-foreground">
          Simple, well-made pieces for every room, from the sofa you sink into to the lamp you read
          by.
        </p>
        <div className="mt-8 flex flex-wrap gap-3">
          <Button asChild size="lg" className="h-11 px-6 text-base">
            <Link to="/products">
              Shop all products
              <ArrowRight aria-hidden data-icon="inline-end" />
            </Link>
          </Button>
          <Button asChild size="lg" variant="outline" className="h-11 px-6 text-base">
            <a href="#categories">Browse by room</a>
          </Button>
        </div>
      </div>
      <HeroIllustration className="w-full" />
    </section>
  )
}

function Promises() {
  return (
    <section aria-label="Why shop with us" className="mt-12 grid gap-4 sm:grid-cols-3">
      {promises.map(({ icon: Icon, title, text }) => (
        <div key={title} className="flex items-start gap-3 rounded-xl border bg-card p-4">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary">
            <Icon aria-hidden className="size-5" />
          </span>
          <div>
            <h2 className="font-sans text-sm font-semibold tracking-normal">{title}</h2>
            <p className="mt-0.5 text-sm text-muted-foreground">{text}</p>
          </div>
        </div>
      ))}
    </section>
  )
}

function Categories() {
  const categories = useCategories()

  return (
    <section id="categories" className="mt-16 scroll-mt-32">
      <h2 className="text-3xl font-semibold">Shop by room</h2>
      <p className="mt-2 text-muted-foreground">Pick a category to see what is in stock.</p>

      {categories.isPending && (
        <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3" aria-label="Loading categories">
          {[0, 1, 2].map((index) => (
            <Skeleton key={index} className="h-36 rounded-2xl" />
          ))}
        </div>
      )}

      {categories.isError && (
        <Alert variant="destructive" className="mt-6">
          <AlertTitle>The categories could not be loaded</AlertTitle>
          <AlertDescription>{categories.error.message}</AlertDescription>
        </Alert>
      )}

      {categories.data?.length === 0 && (
        <p className="mt-6 text-muted-foreground">No categories yet.</p>
      )}

      {categories.data && categories.data.length > 0 && (
        <ul className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {categories.data.map((category, index) => (
            <li key={category.id}>
              <Link
                to={categoryPath(category.slug)}
                className={cn(
                  'group flex h-full min-h-36 flex-col justify-between rounded-2xl p-6 transition-shadow outline-none hover:shadow-md focus-visible:ring-3 focus-visible:ring-ring/50',
                  tileSurfaces[index % tileSurfaces.length],
                )}
              >
                <div>
                  <h3 className="text-xl font-semibold">{category.name}</h3>
                  {category.description && (
                    <p className="mt-1 line-clamp-2 text-sm text-muted-foreground">
                      {category.description}
                    </p>
                  )}
                </div>
                <span className="mt-4 flex items-center gap-1.5 text-sm font-medium text-primary">
                  Shop now
                  <ArrowRight
                    aria-hidden
                    className="size-4 transition-transform group-hover:translate-x-1"
                  />
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

const NEW_ARRIVALS = 4

function NewArrivals() {
  const products = useQuery({
    queryKey: ['products', 'new-arrivals'],
    queryFn: () => getProducts({ sort: 'newest', pageSize: NEW_ARRIVALS }),
  })

  // The section is an extra. Without products, or if they fail to load, the page is fine without it.
  if (products.isError || products.data?.items.length === 0) {
    return null
  }

  return (
    <section className="mt-16">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <h2 className="text-3xl font-semibold">New arrivals</h2>
        <Link
          to="/products"
          className="flex items-center gap-1.5 text-sm font-medium text-primary hover:underline hover:underline-offset-4"
        >
          See all products
          <ArrowRight aria-hidden className="size-4" />
        </Link>
      </div>
      <ul className="mt-6 grid grid-cols-2 gap-x-5 gap-y-8 lg:grid-cols-4">
        {products.isPending
          ? Array.from({ length: NEW_ARRIVALS }, (_, index) => (
              <li key={index}>
                <ProductCardSkeleton />
              </li>
            ))
          : products.data.items.map((product) => (
              <li key={product.id}>
                <ProductCard product={product} />
              </li>
            ))}
      </ul>
    </section>
  )
}

export function HomePage() {
  return (
    <>
      <Hero />
      <Promises />
      <NewArrivals />
      <Categories />
    </>
  )
}
