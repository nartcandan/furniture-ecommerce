import { zodResolver } from '@hookform/resolvers/zod'
import { useId, type ReactNode } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { z } from 'zod'
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

interface DecimalRules {
  /** Digits allowed after the decimal mark. */
  decimals: number
  /** Digits allowed before it. Together they match the column's precision in the database. */
  integerDigits: number
  /** Prices may be 0, measurements must be greater than 0. */
  allowZero: boolean
  required: boolean
}

/**
 * A number typed into a text box. Both `1500.90` and `1500,90` are accepted, since people in Türkiye write
 * a decimal comma. An empty optional box becomes `null`.
 */
function decimal({ decimals, integerDigits, allowZero, required }: DecimalRules) {
  const pattern = new RegExp(`^\\d{1,${integerDigits}}([.,]\\d{1,${decimals}})?$`)
  return z
    .string()
    .trim()
    .superRefine((value, context) => {
      if (value === '') {
        if (required) context.addIssue({ code: 'custom', message: 'Enter a value.' })
        return
      }
      if (!pattern.test(value)) {
        context.addIssue({
          code: 'custom',
          message: `Enter a number with at most ${integerDigits} digits and ${decimals} after the decimal mark.`,
        })
        return
      }
      if (!allowZero && Number(value.replace(',', '.')) === 0) {
        context.addIssue({ code: 'custom', message: 'Enter a value greater than 0.' })
      }
    })
    .transform((value) => (value === '' ? null : Number(value.replace(',', '.'))))
}

const measurement = (decimals: number) =>
  decimal({ decimals, integerDigits: 6 - decimals, allowZero: false, required: false })

const optionalText = (max: number) => z.string().trim().max(max, `Use at most ${max} characters.`)

// The same rules as the API.
const schema = z.object({
  categoryId: z.string().min(1, 'Choose a category.'),
  name: z
    .string()
    .trim()
    .min(1, 'Enter a name.')
    .max(200, 'Use at most 200 characters.')
    .regex(/[\p{L}\p{N}]/u, 'The name needs at least one letter or digit.'),
  description: optionalText(5000),
  price: decimal({ decimals: 2, integerDigits: 8, allowZero: true, required: true }),
  stockQuantity: z
    .string()
    .trim()
    .regex(/^\d{1,7}$/, 'Enter a whole number.')
    .transform(Number)
    .refine((value) => value <= 1_000_000, 'Enter at most 1,000,000.'),
  widthCm: measurement(1),
  depthCm: measurement(1),
  heightCm: measurement(1),
  weightKg: measurement(2),
  material: optionalText(100),
  color: optionalText(50),
  isActive: z.boolean(),
})

type ProductFormInput = z.input<typeof schema>
type ProductFormOutput = z.output<typeof schema>

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
    resolver: zodResolver(schema),
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
