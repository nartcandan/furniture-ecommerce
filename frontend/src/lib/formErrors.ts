import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'
import { ApiError } from '@/api/client'

/**
 * Shows an API error in a form. Validation messages go under their fields, anything else above the form.
 * The API names fields like `FirstName`, the forms like `firstName`.
 */
export function showApiError<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  fields: readonly Path<T>[],
): void {
  let shownUnderField = false

  if (error instanceof ApiError) {
    for (const [apiName, messages] of Object.entries(error.fieldErrors)) {
      const name = (apiName.charAt(0).toLowerCase() + apiName.slice(1)) as Path<T>
      if (fields.includes(name) && messages.length > 0) {
        setError(name, { type: 'server', message: messages.join(' ') })
        shownUnderField = true
      }
    }
  }

  if (!shownUnderField) {
    setError('root', {
      type: 'server',
      message:
        error instanceof ApiError ? error.message : 'Something went wrong. Please try again.',
    })
  }
}
