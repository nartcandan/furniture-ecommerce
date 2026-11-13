import { MapPin, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import type { Address } from '@/api/addresses'
import { FormError } from '@/components/FormError'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
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
import { useSession } from '@/features/auth/authStore'
import { AddressForm } from './AddressForm'
import { AddressText } from './AddressText'
import { useAddresses, useDeleteAddress } from './useAddresses'

/** What the form dialog is doing: nothing, adding a new address, or changing one. */
type Editing = { mode: 'closed' } | { mode: 'new' } | { mode: 'edit'; address: Address }

function DeleteAddressDialog({ address, onClose }: { address: Address | null; onClose: () => void }) {
  const remove = useDeleteAddress()

  function handleDelete() {
    if (!address) return
    remove.mutate(address.id, {
      onSuccess: () => {
        toast.success(`“${address.title}” was deleted`)
        onClose()
      },
    })
  }

  return (
    <Dialog
      open={address !== null}
      onOpenChange={(open) => {
        if (!open && !remove.isPending) {
          remove.reset()
          onClose()
        }
      }}
    >
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete this address?</DialogTitle>
          <DialogDescription>
            “{address?.title}” is removed from your account. Orders you already placed keep their
            own copy of the address.
          </DialogDescription>
        </DialogHeader>
        <FormError message={remove.error?.message} />
        <DialogFooter>
          <Button variant="ghost" size="lg" className="h-10" disabled={remove.isPending} onClick={onClose}>
            Keep address
          </Button>
          <Button
            variant="destructive"
            size="lg"
            className="h-10"
            disabled={remove.isPending}
            onClick={handleDelete}
          >
            {remove.isPending ? 'Deleting…' : 'Delete address'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** The user's saved addresses, with adding, changing and deleting. */
export function AddressBook() {
  const user = useSession()?.user
  const addresses = useAddresses()
  const [editing, setEditing] = useState<Editing>({ mode: 'closed' })
  const [deleting, setDeleting] = useState<Address | null>(null)
  const close = () => setEditing({ mode: 'closed' })

  return (
    <section aria-labelledby="addresses-title">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 id="addresses-title" className="text-2xl font-semibold">
          Addresses
        </h2>
        <Button variant="outline" size="lg" className="h-10" onClick={() => setEditing({ mode: 'new' })}>
          <Plus aria-hidden />
          New address
        </Button>
      </div>

      {addresses.isPending && (
        <div className="mt-4 grid gap-3 sm:grid-cols-2" aria-label="Loading addresses">
          <Skeleton className="h-36 rounded-2xl" />
          <Skeleton className="h-36 rounded-2xl" />
        </div>
      )}

      {addresses.isError && (
        <Alert variant="destructive" className="mt-4">
          <AlertTitle>Your addresses could not be loaded</AlertTitle>
          <AlertDescription>{addresses.error.message}</AlertDescription>
        </Alert>
      )}

      {addresses.data?.length === 0 && (
        <div className="mt-4 flex flex-col items-center rounded-2xl border border-dashed px-6 py-10 text-center">
          <MapPin aria-hidden className="size-8 text-muted-foreground/60" strokeWidth={1.5} />
          <p className="mt-3 font-medium">No saved addresses</p>
          <p className="mt-1 text-sm text-muted-foreground">
            Add one now, or when you place your first order.
          </p>
        </div>
      )}

      {addresses.data && addresses.data.length > 0 && (
        <ul className="mt-4 grid gap-3 sm:grid-cols-2">
          {addresses.data.map((address) => (
            <li key={address.id} className="flex flex-col rounded-2xl border bg-card p-5">
              <h3 className="font-sans text-base font-medium tracking-normal">{address.title}</h3>
              <div className="mt-1 flex-1">
                <AddressText address={address} />
              </div>
              <div className="mt-4 -ml-2.5 flex gap-1">
                <Button variant="ghost" size="lg" onClick={() => setEditing({ mode: 'edit', address })}>
                  <Pencil aria-hidden />
                  Edit
                  <span className="sr-only">{address.title}</span>
                </Button>
                <Button
                  variant="ghost"
                  size="lg"
                  className="text-muted-foreground"
                  onClick={() => setDeleting(address)}
                >
                  <Trash2 aria-hidden />
                  Delete
                  <span className="sr-only">{address.title}</span>
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}

      <Dialog open={editing.mode !== 'closed'} onOpenChange={(open) => !open && close()}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{editing.mode === 'edit' ? 'Edit address' : 'New address'}</DialogTitle>
            <DialogDescription>
              {editing.mode === 'edit'
                ? 'Orders you already placed are not changed.'
                : 'You can choose it at checkout.'}
            </DialogDescription>
          </DialogHeader>
          {editing.mode !== 'closed' && (
            <AddressForm
              // The key gives each address its own form, so one address's values never show in another's.
              key={editing.mode === 'edit' ? editing.address.id : 'new'}
              address={editing.mode === 'edit' ? editing.address : undefined}
              defaultName={user && { firstName: user.firstName, lastName: user.lastName }}
              onSaved={(saved) => {
                toast.success(`“${saved.title}” was saved`)
                close()
              }}
              onCancel={close}
            />
          )}
        </DialogContent>
      </Dialog>

      <DeleteAddressDialog address={deleting} onClose={() => setDeleting(null)} />
    </section>
  )
}
