import { ArrowLeft, ArrowRight, ImagePlus, Trash2 } from 'lucide-react'
import { useRef, useState, type ChangeEvent } from 'react'
import { toast } from 'sonner'
import type { Product, ProductImage } from '@/api/products'
import { FormError } from '@/components/FormError'
import { errorMessage } from '@/lib/formErrors'
import { Button } from '@/components/ui/button'
import { FadeInImage } from '@/components/FadeInImage'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  useDeleteProductImage,
  useRefreshProduct,
  useReorderProductImages,
  useUploadProductImage,
} from './useAdminProducts'

// The same limits as the API. They are checked here too, so a wrong file fails before it is sent.
const MAX_IMAGES = 10
const MAX_FILE_SIZE = 5 * 1024 * 1024
const ACCEPTED_TYPES = ['image/jpeg', 'image/png', 'image/webp']

function DeleteImageDialog({
  productId,
  image,
  onClose,
}: {
  productId: string
  image: ProductImage | null
  onClose: () => void
}) {
  const remove = useDeleteProductImage(productId)

  function close() {
    remove.reset()
    onClose()
  }

  return (
    <Dialog
      open={image !== null}
      onOpenChange={(open) => {
        if (!open && !remove.isPending) close()
      }}
    >
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete this image?</DialogTitle>
          <DialogDescription>The file is removed from the server. This cannot be undone.</DialogDescription>
        </DialogHeader>
        {image && <img src={image.url} alt="" className="max-h-48 rounded-lg object-contain" />}
        <FormError message={remove.error ? errorMessage(remove.error) : undefined} />
        <DialogFooter>
          <Button variant="ghost" disabled={remove.isPending} onClick={close}>
            Keep image
          </Button>
          <Button
            variant="destructive"
            disabled={remove.isPending}
            onClick={() => image && remove.mutate(image.id, { onSuccess: close })}
          >
            {remove.isPending ? 'Deleting…' : 'Delete image'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** The product's images: upload, order (the first one is the main image) and delete. */
export function ProductImages({ product }: { product: Product }) {
  const upload = useUploadProductImage(product.id)
  const reorder = useReorderProductImages(product.id)
  const refreshProduct = useRefreshProduct(product.id)
  const fileInput = useRef<HTMLInputElement>(null)
  const [progress, setProgress] = useState<{ done: number; total: number } | null>(null)
  const [errors, setErrors] = useState<string[]>([])
  const [deleting, setDeleting] = useState<ProductImage | null>(null)
  const images = product.images
  const room = MAX_IMAGES - images.length
  const busy = progress !== null || reorder.isPending

  async function handleFiles(event: ChangeEvent<HTMLInputElement>) {
    const files = [...(event.target.files ?? [])]
    // The same file can be picked again after a failure.
    event.target.value = ''
    if (files.length === 0) return

    const problems: string[] = []
    const valid = files.filter((file) => {
      if (!ACCEPTED_TYPES.includes(file.type)) {
        problems.push(`${file.name}: only JPEG, PNG and WebP images can be uploaded.`)
        return false
      }
      if (file.size > MAX_FILE_SIZE) {
        problems.push(`${file.name}: the file is larger than 5 MB.`)
        return false
      }
      return true
    })
    if (valid.length > room) {
      problems.push(
        `A product can have at most ${MAX_IMAGES} images. ${valid.length - room} of the chosen files were not uploaded.`,
      )
      valid.splice(room)
    }

    // One at a time, in the order they were chosen, so they are also stored in that order.
    let uploaded = 0
    setProgress({ done: 0, total: valid.length })
    for (const file of valid) {
      try {
        await upload.mutateAsync(file)
        uploaded++
      } catch (error) {
        problems.push(`${file.name}: ${errorMessage(error)}`)
      }
      setProgress((current) => current && { ...current, done: current.done + 1 })
    }
    if (valid.length > 0) {
      await refreshProduct()
    }
    setProgress(null)
    setErrors(problems)
    if (uploaded > 0) {
      toast.success(uploaded === 1 ? 'The image was uploaded' : `${uploaded} images were uploaded`)
    }
  }

  function move(index: number, offset: -1 | 1) {
    const ids = images.map((image) => image.id)
    const [moved] = ids.splice(index, 1)
    ids.splice(index + offset, 0, moved)
    setErrors([])
    reorder.mutate(ids, { onError: (error) => setErrors([errorMessage(error)]) })
  }

  return (
    <section aria-labelledby="images-title" className="rounded-xl border bg-card p-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 id="images-title" className="font-sans text-sm font-semibold tracking-normal">
            Images
          </h2>
          <p className="mt-1 text-xs text-muted-foreground">
            The first image is the main one, shown in lists. JPEG, PNG or WebP, at most 5 MB each,{' '}
            {MAX_IMAGES} per product.
          </p>
        </div>
        <input
          ref={fileInput}
          type="file"
          accept={ACCEPTED_TYPES.join(',')}
          multiple
          hidden
          onChange={handleFiles}
        />
        <Button variant="outline" disabled={busy || room <= 0} onClick={() => fileInput.current?.click()}>
          <ImagePlus aria-hidden />
          {progress ? `Uploading ${Math.min(progress.done + 1, progress.total)} of ${progress.total}…` : 'Add images'}
        </Button>
      </div>

      {errors.length > 0 && (
        <div className="mt-4 grid gap-2">
          {errors.map((message) => (
            <FormError key={message} message={message} />
          ))}
        </div>
      )}

      {images.length === 0 ? (
        <button
          type="button"
          disabled={busy}
          onClick={() => fileInput.current?.click()}
          className="mt-4 flex w-full flex-col items-center rounded-lg border border-dashed px-6 py-10 text-center text-sm text-muted-foreground transition-colors hover:bg-muted/50"
        >
          <ImagePlus aria-hidden className="size-8 text-muted-foreground/60" strokeWidth={1.5} />
          <span className="mt-2 font-medium text-foreground">No images yet</span>
          <span className="mt-1">The store shows a placeholder until one is added.</span>
        </button>
      ) : (
        <ol className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
          {images.map((image, index) => (
            <li key={image.id} className="group relative overflow-hidden rounded-lg border bg-muted">
              <FadeInImage
                src={image.url}
                alt={image.altText ?? `${product.name}, image ${index + 1}`}
                className="aspect-square w-full object-cover"
              />
              {index === 0 && (
                <span className="absolute top-2 left-2 rounded-full bg-background/90 px-2 py-0.5 text-xs font-medium">
                  Main
                </span>
              )}
              <div className="flex items-center justify-between gap-1 border-t bg-background p-1">
                <div className="flex">
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    aria-label={`Move image ${index + 1} earlier`}
                    disabled={busy || index === 0}
                    onClick={() => move(index, -1)}
                  >
                    <ArrowLeft aria-hidden />
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    aria-label={`Move image ${index + 1} later`}
                    disabled={busy || index === images.length - 1}
                    onClick={() => move(index, 1)}
                  >
                    <ArrowRight aria-hidden />
                  </Button>
                </div>
                <Button
                  variant="ghost"
                  size="icon-sm"
                  aria-label={`Delete image ${index + 1}`}
                  disabled={busy}
                  className="text-muted-foreground hover:text-destructive"
                  onClick={() => setDeleting(image)}
                >
                  <Trash2 aria-hidden />
                </Button>
              </div>
            </li>
          ))}
        </ol>
      )}

      <DeleteImageDialog productId={product.id} image={deleting} onClose={() => setDeleting(null)} />
    </section>
  )
}
