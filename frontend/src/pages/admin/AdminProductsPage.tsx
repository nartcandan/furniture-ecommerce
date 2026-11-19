import { Package, Plus, Search } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { Pagination } from '@/components/Pagination'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { useAdminCategories } from '@/features/admin/categories/useAdminCategories'
import { useAdminProducts } from '@/features/admin/products/useAdminProducts'
import { ImagePlaceholder } from '@/features/catalog/ProductCard'
import { formatNumber, formatPrice } from '@/lib/format'
import { cn } from '@/lib/utils'

const PAGE_SIZE = 20
const ALL_CATEGORIES = 'all'

/** Below this the stock is shown as running low. The store starts saying "Only N left" at the same point. */
const LOW_STOCK = 5

export function AdminProductsPage() {
  // The filters live in the address, like in the store, so the back button and reloads keep them.
  const [params, setParams] = useSearchParams()
  const search = params.get('search')?.trim() || undefined
  const category = params.get('category')?.trim() || undefined
  const pageParam = Number(params.get('page'))
  const page = Number.isInteger(pageParam) && pageParam >= 1 ? pageParam : 1

  const categories = useAdminCategories()
  const products = useAdminProducts({ search, category, page, pageSize: PAGE_SIZE, sort: 'newest' })
  const [searchText, setSearchText] = useState(search ?? '')
  // The box follows the address when it changes from outside, for example with the back button.
  const [shownSearch, setShownSearch] = useState(search)
  if (shownSearch !== search) {
    setShownSearch(search)
    setSearchText(search ?? '')
  }
  const inactiveCategories = new Set(
    categories.data?.filter((item) => !item.isActive).map((item) => item.id),
  )

  function update(changes: Record<string, string | undefined>) {
    const next = new URLSearchParams(params)
    for (const [name, value] of Object.entries(changes)) {
      if (value) next.set(name, value)
      else next.delete(name)
    }
    // A new filter starts from the first page.
    next.delete('page')
    setParams(next)
  }

  function handleSearch(event: FormEvent) {
    event.preventDefault()
    update({ search: searchText.trim() || undefined })
  }

  function pathFor(target: number) {
    const next = new URLSearchParams(params)
    if (target > 1) next.set('page', String(target))
    else next.delete('page')
    const query = next.toString()
    return query ? `/admin/products?${query}` : '/admin/products'
  }

  const filtered = search !== undefined || category !== undefined

  return (
    <div>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Products</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Newest first. Inactive products are listed too, but hidden from the store.
          </p>
        </div>
        <Button asChild>
          <Link to="/admin/products/new">
            <Plus aria-hidden />
            New product
          </Link>
        </Button>
      </div>

      <div className="mt-6 flex flex-col gap-2 sm:flex-row">
        <form role="search" onSubmit={handleSearch} className="relative flex-1">
          <Search
            aria-hidden
            className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground"
          />
          <Input
            type="search"
            aria-label="Search products by name"
            placeholder="Search by name"
            value={searchText}
            onChange={(event) => setSearchText(event.target.value)}
            className="bg-background pl-8"
          />
        </form>
        <Select
          value={category ?? ALL_CATEGORIES}
          onValueChange={(value) => update({ category: value === ALL_CATEGORIES ? undefined : value })}
        >
          <SelectTrigger aria-label="Category" className="w-full bg-background sm:w-56">
            <SelectValue />
          </SelectTrigger>
          <SelectContent position="popper" className="max-h-72">
            <SelectItem value={ALL_CATEGORIES}>All categories</SelectItem>
            {categories.data?.map((item) => (
              <SelectItem key={item.id} value={item.slug}>
                {item.name}
                {!item.isActive && <span className="text-muted-foreground">(inactive)</span>}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {products.isPending && (
        <div className="mt-4 grid gap-2" aria-label="Loading products">
          {Array.from({ length: 6 }, (_, index) => (
            <Skeleton key={index} className="h-14" />
          ))}
        </div>
      )}

      {products.isError && (
        <Alert variant="destructive" className="mt-4">
          <AlertTitle>The products could not be loaded</AlertTitle>
          <AlertDescription>{products.error.message}</AlertDescription>
        </Alert>
      )}

      {products.data?.items.length === 0 && (
        <div className="mt-4 flex flex-col items-center rounded-xl border border-dashed px-6 py-10 text-center">
          <Package aria-hidden className="size-8 text-muted-foreground/60" strokeWidth={1.5} />
          <p className="mt-3 font-medium">{filtered ? 'No products match' : 'No products yet'}</p>
          <p className="mt-1 text-sm text-muted-foreground">
            {filtered ? 'Try another name or category.' : 'Add the first one with “New product”.'}
          </p>
        </div>
      )}

      {products.data && products.data.items.length > 0 && (
        <>
          <p className="mt-4 text-sm text-muted-foreground">
            {formatNumber(products.data.totalCount)}{' '}
            {products.data.totalCount === 1 ? 'product' : 'products'}
          </p>
          <div
            className={cn(
              'mt-2 rounded-xl border bg-card transition-opacity',
              products.isPlaceholderData && 'opacity-60',
            )}
          >
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="pl-4">Product</TableHead>
                  <TableHead className="text-right">Price</TableHead>
                  <TableHead className="text-right">Stock</TableHead>
                  <TableHead className="pr-4">Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {products.data.items.map((product) => {
                  const image = product.images[0]
                  return (
                    <TableRow key={product.id} className="relative">
                      <TableCell className="max-w-md pl-4">
                        <div className="flex items-center gap-3">
                          {image ? (
                            <img src={image.url} alt="" className="size-10 shrink-0 rounded-md object-cover" />
                          ) : (
                            <ImagePlaceholder className="size-10 shrink-0 rounded-md" />
                          )}
                          <div className="min-w-0">
                            {/* The link covers the whole row, so any part of it opens the product. */}
                            <Link
                              to={`/admin/products/${product.id}`}
                              className="block truncate font-medium after:absolute after:inset-0 hover:underline"
                            >
                              {product.name}
                            </Link>
                            <span className="block truncate text-xs text-muted-foreground">
                              {product.categoryName}
                            </span>
                          </div>
                        </div>
                      </TableCell>
                      <TableCell className="text-right tabular-nums">{formatPrice(product.price)}</TableCell>
                      <TableCell
                        className={cn(
                          'text-right tabular-nums',
                          product.stockQuantity === 0 && 'font-medium text-destructive',
                          product.stockQuantity > 0 && product.stockQuantity <= LOW_STOCK && 'font-medium text-highlight',
                        )}
                      >
                        {formatNumber(product.stockQuantity)}
                      </TableCell>
                      <TableCell className="pr-4">
                        {product.isActive && inactiveCategories.has(product.categoryId) ? (
                          // The store hides products of an inactive category.
                          <Badge variant="outline" className="text-highlight">
                            Category inactive
                          </Badge>
                        ) : product.isActive ? (
                          <Badge variant="secondary">Active</Badge>
                        ) : (
                          <Badge variant="outline" className="text-muted-foreground">
                            Inactive
                          </Badge>
                        )}
                      </TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>
          </div>
          <Pagination page={products.data.page} totalPages={products.data.totalPages} pathFor={pathFor} />
        </>
      )}
    </div>
  )
}
