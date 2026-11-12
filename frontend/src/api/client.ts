import { useAuthStore } from '@/features/auth/authStore'

const baseUrl: string = import.meta.env.VITE_API_URL ?? 'http://localhost:5068'

/** The error body the API sends (RFC 7807). `errors` is filled for validation errors. */
interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  /** Validation messages by the API's field name (for example `FirstName`). Empty for other errors. */
  readonly fieldErrors: Record<string, string[]>

  constructor(status: number, message: string, fieldErrors: Record<string, string[]> = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.fieldErrors = fieldErrors
  }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  /** Sent as JSON, or as multipart when it is a FormData. */
  body?: unknown
}

/** Sends a request to the API. The token of the signed-in user is added automatically. */
export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body } = options
  const token = useAuthStore.getState().session?.token
  const headers = new Headers()
  const isForm = body instanceof FormData

  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }
  if (body !== undefined && !isForm) {
    headers.set('Content-Type', 'application/json')
  }

  let response: Response
  try {
    response = await fetch(`${baseUrl}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, 'The server could not be reached. Please try again.')
  }

  if (response.status === 401 && token) {
    // The token expired or is no longer valid. Without a session, protected pages send the user to sign in.
    // The token is compared first, so a late answer cannot sign out a user who has just signed in again.
    if (useAuthStore.getState().session?.token === token) {
      useAuthStore.getState().clearSession()
    }
    throw new ApiError(401, 'Your session has expired. Please sign in again.')
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as ProblemDetails | null
    throw new ApiError(
      response.status,
      problem?.detail ?? problem?.title ?? 'Something went wrong. Please try again.',
      problem?.errors,
    )
  }

  if (response.status === 204) {
    return undefined as T
  }
  return (await response.json()) as T
}
