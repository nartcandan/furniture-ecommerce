import { useSession } from '@/features/auth/authStore'

export function AccountPage() {
  const user = useSession()?.user

  // The route guard only renders this page for a signed-in user.
  if (!user) {
    return null
  }

  return (
    <section>
      <h1 className="font-heading text-2xl font-semibold">My account</h1>
      <dl className="mt-4 grid max-w-md grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm">
        <dt className="text-muted-foreground">Name</dt>
        <dd>
          {user.firstName} {user.lastName}
        </dd>
        <dt className="text-muted-foreground">Email</dt>
        <dd>{user.email}</dd>
      </dl>
    </section>
  )
}
