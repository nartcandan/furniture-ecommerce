import { CircleAlert } from 'lucide-react'

/** An error that belongs to the whole form, shown above its fields. */
export function FormError({ message }: { message?: string }) {
  if (!message) {
    return null
  }

  return (
    <div
      role="alert"
      className="flex animate-in items-start gap-2.5 rounded-lg border border-destructive/25 bg-destructive/8 px-3.5 py-3 text-sm font-medium text-destructive fade-in slide-in-from-top-1"
    >
      <CircleAlert aria-hidden className="mt-0.5 size-4 shrink-0" />
      <p>{message}</p>
    </div>
  )
}
