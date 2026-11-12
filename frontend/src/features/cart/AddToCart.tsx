import { ShoppingBag } from 'lucide-react'
import { useState } from 'react'
import { useLocation, useNavigate } from 'react-router'
import { toast } from 'sonner'
import type { Product } from '@/api/products'
import { FormError } from '@/components/FormError'
import { QuantityStepper } from '@/components/QuantityStepper'
import { Button } from '@/components/ui/button'
import { useSession } from '@/features/auth/authStore'
import { useAddToCart } from './useCart'

/** The quantity picker and the "Add to cart" button on a product page. */
export function AddToCart({ product }: { product: Product }) {
  const session = useSession()
  const navigate = useNavigate()
  const location = useLocation()
  const addToCart = useAddToCart()
  const [quantity, setQuantity] = useState(1)

  if (product.stockQuantity === 0) {
    return (
      <Button size="lg" disabled className="h-12 w-full text-base">
        Out of stock
      </Button>
    )
  }

  function handleAdd() {
    if (!session) {
      // The cart is kept on the server, so it needs an account. The user comes back to this product.
      void navigate('/login', { state: { from: location.pathname + location.search } })
      return
    }

    addToCart.mutate(
      { productId: product.id, quantity },
      {
        onSuccess: () => {
          setQuantity(1)
          toast.success(`${product.name} is in your cart`, {
            action: { label: 'View cart', onClick: () => void navigate('/cart') },
          })
        },
      },
    )
  }

  return (
    <div className="grid gap-3">
      {/* The API says how many are left when the cart already holds some of this product. */}
      <FormError message={addToCart.error?.message} />
      <div className="flex items-center gap-3">
        <QuantityStepper
          value={quantity}
          max={product.stockQuantity}
          onChange={setQuantity}
          disabled={addToCart.isPending}
          label={product.name}
          className="[&_button]:size-12"
        />
        <Button
          size="lg"
          onClick={handleAdd}
          disabled={addToCart.isPending}
          className="h-12 flex-1 text-base"
        >
          <ShoppingBag aria-hidden className="size-5" />
          {addToCart.isPending ? 'Adding…' : 'Add to cart'}
        </Button>
      </div>
    </div>
  )
}
