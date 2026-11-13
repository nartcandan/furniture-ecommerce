import { ChevronRight, Package } from 'lucide-react'
import { Link } from 'react-router'
import { AddressBook } from '@/features/addresses/AddressBook'
import { useSession } from '@/features/auth/authStore'

export function AccountPage() {
  const user = useSession()?.user

  // The route guard only renders this page for a signed-in user.
  if (!user) {
    return null
  }

  return (
    <div>
      <h1 className="text-3xl font-semibold sm:text-4xl">My account</h1>

      <div className="mt-8 grid gap-4 sm:grid-cols-2">
        <section aria-labelledby="profile-title" className="rounded-2xl bg-secondary p-6">
          <h2 id="profile-title" className="font-sans text-sm font-semibold tracking-normal">
            Profile
          </h2>
          <p className="mt-3 text-lg font-medium">
            {user.firstName} {user.lastName}
          </p>
          <p className="mt-0.5 truncate text-sm text-muted-foreground">{user.email}</p>
        </section>

        <Link
          to="/orders"
          className="group flex items-center gap-4 rounded-2xl border bg-card p-6 transition-shadow outline-none hover:shadow-sm focus-visible:ring-3 focus-visible:ring-ring/50"
        >
          <span className="flex size-12 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary">
            <Package aria-hidden className="size-6" />
          </span>
          <span className="min-w-0 flex-1">
            <span className="block text-lg font-medium">My orders</span>
            <span className="block text-sm text-muted-foreground">
              Follow, review or cancel your orders
            </span>
          </span>
          <ChevronRight
            aria-hidden
            className="size-5 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5"
          />
        </Link>
      </div>

      <div className="mt-12">
        <AddressBook />
      </div>
    </div>
  )
}
