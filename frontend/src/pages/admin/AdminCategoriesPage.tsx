import { FolderTree, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import type { Category } from '@/api/categories'
import { FormError } from '@/components/FormError'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Skeleton } from '@/components/ui/skeleton'
import { QueryError } from '@/components/QueryError'
import { EmptyState } from '@/components/EmptyState'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { CategoryForm } from '@/features/admin/categories/CategoryForm'
import {
  useAdminCategories,
  useDeleteCategory,
} from '@/features/admin/categories/useAdminCategories'

/** What the form dialog is doing: nothing, adding a new category, or changing one. */
type Editing = { mode: 'closed' } | { mode: 'new' } | { mode: 'edit'; category: Category }

function DeleteCategoryDialog({
  category,
  onClose,
}: {
  category: Category | null
  onClose: () => void
}) {
  const remove = useDeleteCategory()

  function close() {
    remove.reset()
    onClose()
  }

  function handleDelete() {
    if (!category) return
    remove.mutate(category.id, {
      onSuccess: () => {
        toast.success(`“${category.name}” was deleted`)
        close()
      },
    })
  }

  return (
    <Dialog
      open={category !== null}
      onOpenChange={(open) => {
        if (!open && !remove.isPending) close()
      }}
    >
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete “{category?.name}”?</DialogTitle>
          <DialogDescription>
            Only a category that never held a product can be deleted. Otherwise make it inactive:
            it is hidden from the store and its products can no longer be ordered.
          </DialogDescription>
        </DialogHeader>
        <FormError message={remove.error?.message} />
        <DialogFooter>
          <Button variant="ghost" disabled={remove.isPending} onClick={close}>
            Keep category
          </Button>
          <Button variant="destructive" disabled={remove.isPending} onClick={handleDelete}>
            {remove.isPending ? 'Deleting…' : 'Delete category'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export function AdminCategoriesPage() {
  const categories = useAdminCategories()
  const [editing, setEditing] = useState<Editing>({ mode: 'closed' })
  const [deleting, setDeleting] = useState<Category | null>(null)
  const close = () => setEditing({ mode: 'closed' })

  return (
    <div>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Categories</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Inactive categories are hidden from the store.
          </p>
        </div>
        <Button onClick={() => setEditing({ mode: 'new' })}>
          <Plus aria-hidden />
          New category
        </Button>
      </div>

      {categories.isPending && (
        <div className="mt-6 grid gap-2" aria-label="Loading categories">
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} className="h-10" />
          ))}
        </div>
      )}

      {categories.isError && (
        <QueryError title="The categories could not be loaded" query={categories} className="mt-6" />
      )}

      {categories.data?.length === 0 && (
        <EmptyState
          icon={FolderTree}
          title="No categories yet"
          description="Products need a category, so start here."
          className="mt-6"
        />
      )}

      {categories.data && categories.data.length > 0 && (
        <div className="mt-6 rounded-xl border bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="pl-4">Name</TableHead>
                <TableHead>Slug</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="pr-4 text-right">
                  <span className="sr-only">Actions</span>
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {categories.data.map((category) => (
                <TableRow key={category.id}>
                  <TableCell className="max-w-xs pl-4">
                    <span className="block truncate font-medium">{category.name}</span>
                    {category.description && (
                      <span className="block truncate text-xs text-muted-foreground">
                        {category.description}
                      </span>
                    )}
                  </TableCell>
                  <TableCell className="font-mono text-xs text-muted-foreground">
                    {category.slug}
                  </TableCell>
                  <TableCell>
                    {category.isActive ? (
                      <Badge variant="secondary">Active</Badge>
                    ) : (
                      <Badge variant="outline" className="text-muted-foreground">
                        Inactive
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell className="pr-4 text-right whitespace-nowrap">
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label={`Edit ${category.name}`}
                      onClick={() => setEditing({ mode: 'edit', category })}
                    >
                      <Pencil aria-hidden />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label={`Delete ${category.name}`}
                      className="text-muted-foreground hover:text-destructive"
                      onClick={() => setDeleting(category)}
                    >
                      <Trash2 aria-hidden />
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <Dialog open={editing.mode !== 'closed'} onOpenChange={(open) => !open && close()}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editing.mode === 'edit' ? 'Edit category' : 'New category'}</DialogTitle>
            <DialogDescription>
              {editing.mode === 'edit'
                ? 'Changes show in the store right away.'
                : 'A new category is active and shows in the store right away.'}
            </DialogDescription>
          </DialogHeader>
          {editing.mode !== 'closed' && (
            <CategoryForm
              // A fresh form for each category, so the values of the last one do not stay.
              key={editing.mode === 'edit' ? editing.category.id : 'new'}
              category={editing.mode === 'edit' ? editing.category : undefined}
              onCancel={close}
              onSaved={(category) => {
                toast.success(
                  `“${category.name}” was ${editing.mode === 'edit' ? 'saved' : 'created'}`,
                )
                close()
              }}
            />
          )}
        </DialogContent>
      </Dialog>

      <DeleteCategoryDialog category={deleting} onClose={() => setDeleting(null)} />
    </div>
  )
}
