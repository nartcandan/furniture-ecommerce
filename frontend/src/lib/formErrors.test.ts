import { describe, expect, it, vi } from 'vitest'
import { ApiError } from '@/api/client'
import { errorMessage, showApiError } from './formErrors'

// The API client reads the session store, which keeps the session in localStorage.
vi.mock('@/features/auth/authStore', () => ({ useAuthStore: { getState: () => ({ session: null }) } }))

describe('errorMessage', () => {
  // A validation error's title only says "Validation failed", which tells the user nothing.
  it('uses the field messages of a validation error', () => {
    const error = new ApiError(400, 'Validation failed', {
      File: ['The file must be a JPEG, PNG or WebP image.'],
    })
    expect(errorMessage(error)).toBe('The file must be a JPEG, PNG or WebP image.')
  })

  it('joins several field messages', () => {
    const error = new ApiError(400, 'Validation failed', { Change: ['First.', 'Second.'], Other: ['Third.'] })
    expect(errorMessage(error)).toBe('First. Second. Third.')
  })

  it("uses the error's own message otherwise", () => {
    expect(errorMessage(new ApiError(409, 'Only 3 in stock.'))).toBe('Only 3 in stock.')
  })

  it('does not show the text of an unexpected error', () => {
    expect(errorMessage(new TypeError('x is undefined'))).toBe('Something went wrong. Please try again.')
  })
})

describe('showApiError', () => {
  const fields = ['firstName', 'email'] as const

  it('puts a field message under its field, matching the API name', () => {
    const setError = vi.fn()
    showApiError(new ApiError(400, 'Validation failed', { FirstName: ['Too long.', 'Has digits.'] }), setError, fields)

    expect(setError).toHaveBeenCalledOnce()
    expect(setError).toHaveBeenCalledWith('firstName', { type: 'server', message: 'Too long. Has digits.' })
  })

  it('shows the message above the form when no field of the form matches', () => {
    const setError = vi.fn()
    showApiError(new ApiError(400, 'Validation failed', { Password: ['Too short.'] }), setError, fields)

    expect(setError).toHaveBeenCalledWith('root', { type: 'server', message: 'Validation failed' })
  })

  it('shows other errors above the form', () => {
    const setError = vi.fn()
    showApiError(new ApiError(409, 'This email is already registered.'), setError, fields)

    expect(setError).toHaveBeenCalledWith('root', { type: 'server', message: 'This email is already registered.' })
  })

  it('hides the text of an unexpected error', () => {
    const setError = vi.fn()
    showApiError(new Error('boom'), setError, fields)

    expect(setError).toHaveBeenCalledWith('root', {
      type: 'server',
      message: 'Something went wrong. Please try again.',
    })
  })
})
