import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { SearchX } from 'lucide-react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { getProducts, type ProductSort } from '@/api/products'
import { Pagination } from '@/components/Pagination'
import { Button } from '@/components/ui/button'
import { QueryError } from '@/components/QueryError'
import { EmptyState } from '@/components/EmptyState'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { PriceFilter } from '@/features/catalog/PriceFilter'
import { ProductCard, ProductCardSkeleton } from '@/features/catalog/ProductCard'
import {
  productsPath,
  readFilters,
  toProductQuery,
  type ProductFilters,
} from '@/features/catalog/productFilters'
import { useCategories } from '@/features/catalog/useCategories'
import { cn } from '@/lib/utils'

const sortLabels: Record<ProductSort, string> = {
  newest: 'Newest first',
  price_asc: 'Price: low to high',
  price_desc: 'Price: high to low',
  name: 'Name: A to Z',
}

const gridClass = 'grid grid-cols-2 gap-x-5 gap-y-8 xl:grid-cols-3'

function CategoryFilter({ filters }: { filters: ProductFilters }) {
  const categories = useCategories()
  const linkClass = 'block rounded-md px-2.5 py-1.5 text-sm transition-colors hover:bg-muted'
  const activeClass = 'bg-muted font-medium'

  return (
    <ul className="-mx-2.5 grid gap-0.5">
      <li>
        <Link
          to={productsPath({ ...filters, category: undefined, page: 1 })}
          aria-current={filters.category ? undefined : 'true'}
          className={cn(linkClass, !filters.category && activeClass)}
        >
          All products
        </Link>
      </li>
      {categories.data?.map((category) => (
        <li key={category.id}>
          <Link
            to={productsPath({ ...filters, category: category.slug, page: 1 })}
            aria-current={filters.category === category.slug ? 'true' : undefined}
            className={cn(linkClass, filters.category === category.slug && activeClass)}
          >
            {category.name}
          </Link>
        </li>
      ))}
    </ul>
  )
}

export function ProductsPage() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const filters = readFilters(searchParams)
  const categories = useCategories()

  const products = useQuery({
    queryKey: ['products', filters],
    queryFn: () => getProducts(toProductQuery(filters)),
    // The previous page stays on screen while the next one loads, so the list does not jump.
    placeholderData: keepPreviousData,
  })

  const category = categories.data?.find((candidate) => candidate.slug === filters.category)
  const hasFilters = Boolean(
    filters.search || filters.minPrice !== undefined || filters.maxPrice !== undefined,
  )
  const title = filters.search
    ? `Results for “${filters.search}”`
    : (category?.name ?? 'All products')

  // Any change to the filters goes back to the first page.
  function applyFilters(changes: Partial<ProductFilters>) {
    void navigate(productsPath({ ...filters, ...changes, page: 1 }))
  }

  const total = products.data?.totalCount ?? 0

  return (
    <div className="grid gap-10 lg:grid-cols-[14rem_1fr]">
      <aside aria-label="Filters" className="grid content-start gap-8">
        {/* On small screens the category bar in the header does this job. */}
        <section className="hidden lg:block">
          <h2 className="font-sans text-sm font-semibold tracking-normal">Category</h2>
          <div className="mt-3">
            <CategoryFilter filters={filters} />
          </div>
        </section>
        <section>
          <h2 className="font-sans text-sm font-semibold tracking-normal">Price (₺)</h2>
          <div className="mt-3">
            <PriceFilter
              key={`${filters.minPrice}-${filters.maxPrice}`}
              minPrice={filters.minPrice}
              maxPrice={filters.maxPrice}
              onApply={(minPrice, maxPrice) => applyFilters({ minPrice, maxPrice })}
            />
          </div>
        </section>
      </aside>

      <section aria-labelledby="products-title" className="min-w-0">
        <h1 id="products-title" className="text-3xl font-semibold sm:text-4xl">
          {title}
        </h1>
        {category?.description && !filters.search && (
          <p className="mt-2 text-muted-foreground">{category.description}</p>
        )}

        <div className="mt-6 flex flex-wrap items-center justify-between gap-3 border-b pb-4">
          <p className="text-sm text-muted-foreground" aria-live="polite">
            {/* A failed list has no count. "0 products" would read as an empty shop. */}
            {products.isPending
              ? 'Loading…'
              : products.isError
                ? '\u00a0'
                : `${total} ${total === 1 ? 'product' : 'products'}`}
            {filters.search && category && ` in ${category.name}`}
            {hasFilters && (
              <>
                {' · '}
                <Link
                  to={productsPath({ sort: filters.sort, category: filters.category, page: 1 })}
                  className="text-foreground underline underline-offset-4"
                >
                  Clear filters
                </Link>
              </>
            )}
          </p>
          <Select
            value={filters.sort}
            onValueChange={(sort) => applyFilters({ sort: sort as ProductSort })}
          >
            <SelectTrigger aria-label="Sort by" className="w-52 bg-card data-[size=default]:h-10">
              <SelectValue />
            </SelectTrigger>
            <SelectContent align="end">
              {Object.entries(sortLabels).map(([value, label]) => (
                <SelectItem key={value} value={value}>
                  {label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {products.isError && (
          <QueryError title="The products could not be loaded" query={products} className="mt-6" />
        )}

        {products.isPending && (
          <div className={cn(gridClass, 'mt-8')} aria-label="Loading products">
            {Array.from({ length: 6 }, (_, index) => (
              <ProductCardSkeleton key={index} />
            ))}
          </div>
        )}

        {products.data && products.data.items.length === 0 && (
          <EmptyState
            variant="page"
            icon={SearchX}
            title="Nothing matches"
            description="Try another search, price range or category."
            action={
              <Button asChild variant="outline" size="lg" className="h-10 px-5">
                <Link to="/products">See all products</Link>
              </Button>
            }
          />
        )}

        {products.data && products.data.items.length > 0 && (
          <>
            <ul
              className={cn(
                gridClass,
                'mt-8 transition-opacity',
                products.isPlaceholderData && 'opacity-50',
              )}
            >
              {products.data.items.map((product) => (
                <li key={product.id}>
                  <ProductCard product={product} />
                </li>
              ))}
            </ul>
            <Pagination
              page={products.data.page}
              totalPages={products.data.totalPages}
              pathFor={(page) => productsPath({ ...filters, page })}
            />
          </>
        )}
      </section>
    </div>
  )
}
