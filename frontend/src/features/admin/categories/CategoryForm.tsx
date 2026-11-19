import { zodResolver } from '@hookform/resolvers/zod'
import { useId } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { z } from 'zod'
import type { Category } from '@/api/categories'
import { FormError } from '@/components/FormError'
import { FormField } from '@/components/FormField'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { Textarea } from '@/components/ui/textarea'
import { showApiError } from '@/lib/formErrors'
import { useCreateCategory, useUpdateCategory } from './useAdminCategories'

// The same rules as the API. The slug is made from the name, so the name needs a letter or a digit.
const schema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Enter a name.')
    .max(100, 'Use at most 100 characters.')
    .regex(/[\p{L}\p{N}]/u, 'The name needs at least one letter or digit.'),
  description: z.string().trim().max(500, 'Use at most 500 characters.'),
  isActive: z.boolean(),
})

type CategoryFormValues = z.infer<typeof schema>

const fields = ['name', 'description', 'isActive'] as const

interface CategoryFormProps {
  /** The category to change. Without it the form creates a new one. */
  category?: Category
  onSaved: (category: Category) => void
  onCancel: () => void
}

export function CategoryForm({ category, onSaved, onCancel }: CategoryFormProps) {
  const create = useCreateCategory()
  const update = useUpdateCategory(category?.id ?? '')
  const descriptionId = useId()
  const activeId = useId()
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<CategoryFormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: category?.name ?? '',
      description: category?.description ?? '',
      isActive: category?.isActive ?? true,
    },
  })

  const onSubmit = handleSubmit(async ({ name, description, isActive }) => {
    const input = { name, description: description || null }
    try {
      // A new category always starts active. The API does not take the field on create.
      onSaved(
        category
          ? await update.mutateAsync({ ...input, isActive })
          : await create.mutateAsync(input),
      )
    } catch (error) {
      showApiError(error, setError, fields)
    }
  })

  return (
    <form onSubmit={onSubmit} noValidate className="grid gap-4">
      <FormError message={errors.root?.message} />

      <div className="grid gap-1.5">
        <FormField label="Name" error={errors.name?.message} {...register('name')} />
        {category && (
          <p className="text-xs text-muted-foreground">
            Store address: <span className="font-mono">/products?category={category.slug}</span>. It
            does not change with the name, so existing links keep working.
          </p>
        )}
      </div>

      <div className="grid gap-1.5">
        <Label htmlFor={descriptionId}>
          Description <span className="font-normal text-muted-foreground">(optional)</span>
        </Label>
        <Textarea
          id={descriptionId}
          rows={3}
          aria-invalid={errors.description ? true : undefined}
          className="bg-background"
          {...register('description')}
        />
        {errors.description && (
          <p className="text-sm text-destructive">{errors.description.message}</p>
        )}
      </div>

      {category && (
        <div className="flex items-start justify-between gap-4 rounded-lg border p-3">
          <div>
            <Label htmlFor={activeId}>Active</Label>
            <p className="mt-1 text-xs text-muted-foreground">
              An inactive category and its products are hidden from the store and cannot be ordered.
            </p>
          </div>
          <Controller
            control={control}
            name="isActive"
            render={({ field }) => (
              <Switch
                id={activeId}
                ref={field.ref}
                checked={field.value}
                onCheckedChange={field.onChange}
              />
            )}
          />
        </div>
      )}

      <div className="flex justify-end gap-2 pt-2">
        <Button type="button" variant="ghost" disabled={isSubmitting} onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Saving…' : category ? 'Save changes' : 'Create category'}
        </Button>
      </div>
    </form>
  )
}
