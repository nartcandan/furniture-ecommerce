import { ArrowUpRight, ChevronLeft } from 'lucide-react'
import { Link, useParams } from 'react-router'
import { toast } from 'sonner'
import { ApiError } from '@/api/client'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { QueryError } from '@/components/QueryError'
import { useAdminCategories } from '@/features/admin/categories/useAdminCategories'
import { ProductForm } from '@/features/admin/products/ProductForm'
import { ProductImages } from '@/features/admin/products/ProductImages'
import { StockPanel } from '@/features/admin/products/StockPanel'
import { useAdminProduct, useUpdateProduct } from '@/features/admin/products/useAdminProducts'

function BackLink() {
  return (
    <Link
      to="/admin/products"
      className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
    >
      <ChevronLeft aria-hidden className="size-4" />
      Products
    </Link>
  )
}

export function AdminProductPage() {
  const { id = '' } = useParams()
  const product = useAdminProduct(id)
  const update = useUpdateProduct(id)
  const categories = useAdminCategories()

  if (product.isPending) {
    return (
      <div className="grid gap-4" aria-label="Loading product">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-64" />
      </div>
    )
  }

  if (product.isError) {
    // A wrong id is 400, an unknown one 404. Both mean there is nothing to show.
    const missing = product.error instanceof ApiError && [400, 404].includes(product.error.status)
    return (
      <div>
        <BackLink />
        {missing ? (
          <Alert variant="destructive" className="mt-4">
            <AlertTitle>This product does not exist</AlertTitle>
            <AlertDescription>It may have been typed wrong.</AlertDescription>
          </Alert>
        ) : (
          <QueryError title="The product could not be loaded" query={product} className="mt-4" />
        )}
      </div>
    )
  }

  const data = product.data
  const category = categories.data?.find((item) => item.id === data.categoryId)
  // The store shows a product only when the product and its category are both active.
  const inStore = data.isActive && category?.isActive !== false

  return (
    <div>
      <BackLink />
      <div className="mt-2 flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold break-words">{data.name}</h1>
          <div className="mt-2 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
            {!data.isActive ? (
              <Badge variant="outline" className="text-muted-foreground">
                Inactive
              </Badge>
            ) : inStore ? (
              <Badge variant="secondary">Active</Badge>
            ) : (
              <Badge variant="outline" className="text-highlight">
                Hidden: category inactive
              </Badge>
            )}
            <span>{data.categoryName}</span>
          </div>
        </div>
        {inStore && (
          <Button asChild variant="outline">
            <Link to={`/products/${data.slug}`} target="_blank" rel="noreferrer">
              View in store
              <ArrowUpRight aria-hidden />
            </Link>
          </Button>
        )}
      </div>

      {/* On narrow screens the stock comes right after the images, not below the long form. */}
      <div className="mt-6 grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_18rem]">
        <ProductImages product={data} />
        <div className="lg:sticky lg:top-20 lg:col-start-2 lg:row-span-2 lg:row-start-1">
          <StockPanel product={data} />
        </div>
        <div className="lg:col-start-1">
          <ProductForm
            // A new form when another product opens, so the last one's values do not stay.
            key={data.id}
            product={data}
            submitLabel="Save changes"
            onSubmit={async ({ product: input, isActive }) => {
              const saved = await update.mutateAsync({ ...input, isActive })
              toast.success(`“${saved.name}” was saved`)
            }}
          />
        </div>
      </div>
    </div>
  )
}
