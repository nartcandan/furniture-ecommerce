import { zodResolver } from '@hookform/resolvers/zod'
import { useId, type ReactNode } from 'react'
import { Controller, useForm } from 'react-hook-form'
import type { Product, ProductInput } from '@/api/products'
import { FormError } from '@/components/FormError'
import { FormField } from '@/components/FormField'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { useAdminCategories } from '@/features/admin/categories/useAdminCategories'
import { showApiError } from '@/lib/formErrors'
import { productSchema, type ProductFormInput, type ProductFormOutput } from './productSchema'

const fields = [
  'categoryId',
  'name',
  'description',
  'price',
  'stockQuantity',
  'widthCm',
  'depthCm',
  'heightCm',
  'weightKg',
  'material',
  'color',
  'isActive',
] as const

/** What the form hands over: the product fields, the starting stock and the active flag. */
export interface ProductFormValues {
  product: ProductInput
  stockQuantity: number
  isActive: boolean
}

interface ProductFormProps {
  /** The product to change. Without it the form is for a new product and asks for the starting stock. */
  product?: Product
  submitLabel: string
  /** Saves the values. A rejected promise shows the API's error in the form. */
  onSubmit: (values: ProductFormValues) => Promise<unknown>
  onCancel?: () => void
}

// Saved numbers are shown with a decimal comma, the way they are usually typed in.
const toText = (value: number | null | undefined) =>
  value == null ? '' : String(value).replace('.', ',')

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <fieldset className="grid gap-4 rounded-xl border bg-card p-5">
      <legend className="sr-only">{title}</legend>
      <h2 aria-hidden className="font-sans text-sm font-semibold tracking-normal">
        {title}
      </h2>
      {children}
    </fieldset>
  )
}

export function ProductForm({ product, submitLabel, onSubmit, onCancel }: ProductFormProps) {
  const categories = useAdminCategories()
  const categoryId = useId()
  const descriptionId = useId()
  const activeId = useId()
  const {
    register,
    control,
    handleSubmit,
    setError,
    getValues,
    reset,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<ProductFormInput, unknown, ProductFormOutput>({
    resolver: zodResolver(productSchema),
    defaultValues: {
      categoryId: product?.categoryId ?? '',
      name: product?.name ?? '',
      description: product?.description ?? '',
      price: toText(product?.price),
      // A product's stock changes only through the stock box, so this field is for new products.
      stockQuantity: product ? '0' : '',
      widthCm: toText(product?.widthCm),
      depthCm: toText(product?.depthCm),
      heightCm: toText(product?.heightCm),
      weightKg: toText(product?.weightKg),
      material: product?.material ?? '',
      color: product?.color ?? '',
      isActive: product?.isActive ?? true,
    },
  })

  const submit = handleSubmit(async (values) => {
    try {
      await onSubmit({
        product: {
          categoryId: values.categoryId,
          name: values.name,
          description: values.description || null,
          // The schema requires a price, so it is never null here.
          price: values.price ?? 0,
          widthCm: values.widthCm,
          depthCm: values.depthCm,
          heightCm: values.heightCm,
          weightKg: values.weightKg,
          material: values.material || null,
          color: values.color || null,
        },
        stockQuantity: values.stockQuantity,
        isActive: values.isActive,
      })
      // The saved values become the new starting point, so "Save" waits for the next change.
      reset(getValues())
    } catch (error) {
      showApiError(error, setError, fields)
    }
  })

  return (
    <form onSubmit={submit} noValidate className="grid gap-5">
      <FormError message={errors.root?.message} />

      <Section title="Details">
        <FormField label="Name" error={errors.name?.message} {...register('name')} />

        <div className="grid gap-1.5">
          <Label htmlFor={categoryId}>Category</Label>
          <Controller
            control={control}
            name="categoryId"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger
                  id={categoryId}
                  ref={field.ref}
                  onBlur={field.onBlur}
                  aria-invalid={errors.categoryId ? true : undefined}
                  className="w-full bg-background data-[size=default]:h-10"
                >
                  <SelectValue placeholder={categories.isPending ? 'Loading…' : 'Choose…'} />
                </SelectTrigger>
                <SelectContent position="popper" className="max-h-72">
                  {categories.data?.map((category) => (
                    <SelectItem key={category.id} value={category.id}>
                      {category.name}
                      {!category.isActive && (
                        <span className="text-muted-foreground">(inactive)</span>
                      )}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
          {errors.categoryId && (
            <p className="text-sm text-destructive">{errors.categoryId.message}</p>
          )}
        </div>

        <div className="grid gap-1.5">
          <Label htmlFor={descriptionId}>
            Description <span className="font-normal text-muted-foreground">(optional)</span>
          </Label>
          <Textarea
            id={descriptionId}
            rows={5}
            aria-invalid={errors.description ? true : undefined}
            className="bg-background"
            {...register('description')}
          />
          {errors.description && (
            <p className="text-sm text-destructive">{errors.description.message}</p>
          )}
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <FormField
            label="Price (₺)"
            inputMode="decimal"
            placeholder="1499,90"
            error={errors.price?.message}
            {...register('price')}
          />
          {!product && (
            <FormField
              label="Starting stock"
              inputMode="numeric"
              placeholder="0"
              error={errors.stockQuantity?.message}
              {...register('stockQuantity')}
            />
          )}
        </div>
      </Section>

      <Section title="Measurements (optional)">
        <div className="grid gap-4 sm:grid-cols-4">
          <FormField label="Width (cm)" inputMode="decimal" error={errors.widthCm?.message} {...register('widthCm')} />
          <FormField label="Depth (cm)" inputMode="decimal" error={errors.depthCm?.message} {...register('depthCm')} />
          <FormField label="Height (cm)" inputMode="decimal" error={errors.heightCm?.message} {...register('heightCm')} />
          <FormField label="Weight (kg)" inputMode="decimal" error={errors.weightKg?.message} {...register('weightKg')} />
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField label="Material" placeholder="Oak, linen…" error={errors.material?.message} {...register('material')} />
          <FormField label="Colour" placeholder="Natural, grey…" error={errors.color?.message} {...register('color')} />
        </div>
      </Section>

      {product && (
        <div className="flex items-start justify-between gap-4 rounded-xl border bg-card p-5">
          <div>
            <Label htmlFor={activeId}>Active</Label>
            <p className="mt-1 text-xs text-muted-foreground">
              An inactive product is hidden from the store and cannot be ordered. Past orders keep it.
            </p>
          </div>
          <Controller
            control={control}
            name="isActive"
            render={({ field }) => (
              <Switch id={activeId} ref={field.ref} checked={field.value} onCheckedChange={field.onChange} />
            )}
          />
        </div>
      )}

      <div className="flex justify-end gap-2">
        {onCancel && (
          <Button type="button" variant="ghost" disabled={isSubmitting} onClick={onCancel}>
            Cancel
          </Button>
        )}
        <Button type="submit" disabled={isSubmitting || (product !== undefined && !isDirty)}>
          {isSubmitting ? 'Saving…' : submitLabel}
        </Button>
      </div>
    </form>
  )
}
