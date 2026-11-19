import { ChevronLeft } from 'lucide-react'
import { Link, useNavigate } from 'react-router'
import { toast } from 'sonner'
import { ProductForm } from '@/features/admin/products/ProductForm'
import { useCreateProduct } from '@/features/admin/products/useAdminProducts'

export function AdminNewProductPage() {
  const create = useCreateProduct()
  const navigate = useNavigate()

  return (
    <div className="max-w-3xl">
      <Link
        to="/admin/products"
        className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
      >
        <ChevronLeft aria-hidden className="size-4" />
        Products
      </Link>
      <h1 className="mt-2 text-2xl font-semibold">New product</h1>
      <p className="mt-1 text-sm text-muted-foreground">
        A new product is active and shows in the store right away. Images are added on the next page.
      </p>

      <div className="mt-6">
        <ProductForm
          submitLabel="Create product"
          onCancel={() => navigate('/admin/products')}
          onSubmit={async ({ product, stockQuantity }) => {
            const created = await create.mutateAsync({ ...product, stockQuantity })
            toast.success(`“${created.name}” was created. Now add its images.`)
            // The product's own page is where images and later stock changes are handled.
            navigate(`/admin/products/${created.id}`, { replace: true })
          }}
        />
      </div>
    </div>
  )
}
