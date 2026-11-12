import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link, useLocation } from 'react-router'
import { z } from 'zod'
import { register as registerAccount } from '@/api/auth'
import { ApiError } from '@/api/client'
import { FormField } from '@/components/FormField'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { useAuthStore } from '@/features/auth/authStore'
import { showApiError } from '@/lib/formErrors'

// The same rules as the API, so most mistakes are caught before a request is sent.
const schema = z
  .object({
    firstName: z.string().trim().min(1, 'Enter your first name.').max(100, 'Use at most 100 characters.'),
    lastName: z.string().trim().min(1, 'Enter your last name.').max(100, 'Use at most 100 characters.'),
    email: z
      .string()
      .trim()
      .min(1, 'Enter your email address.')
      .max(256, 'Use at most 256 characters.')
      .regex(/^[^\s@]+@[^\s@]+$/, 'Enter a valid email address.'),
    password: z
      .string()
      .min(8, 'Use at least 8 characters.')
      .max(128, 'Use at most 128 characters.')
      .regex(/[A-Z]/, 'Include an uppercase letter (A-Z).')
      .regex(/[a-z]/, 'Include a lowercase letter (a-z).')
      .regex(/[0-9]/, 'Include a digit.'),
    confirmPassword: z.string(),
  })
  .refine((values) => values.password === values.confirmPassword, {
    path: ['confirmPassword'],
    message: 'The passwords do not match.',
  })

type RegisterForm = z.infer<typeof schema>

export function RegisterPage() {
  const location = useLocation()
  const setSession = useAuthStore((state) => state.setSession)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RegisterForm>({ resolver: zodResolver(schema) })

  const onSubmit = handleSubmit(async ({ firstName, lastName, email, password }) => {
    try {
      // Saving the session is enough: the guard around this page then sends the user on.
      setSession(await registerAccount({ firstName, lastName, email, password }))
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        setError('email', { type: 'server', message: error.message })
        return
      }
      showApiError(error, setError, ['firstName', 'lastName', 'email', 'password'])
    }
  })

  return (
    <Card className="mx-auto mt-4 w-full max-w-md [--card-spacing:--spacing(7)]">
      <CardHeader>
        <CardTitle>
          <h1 className="text-2xl font-semibold">Create an account</h1>
        </CardTitle>
        <CardDescription>It takes a minute, and you can order right away.</CardDescription>
      </CardHeader>
      <form onSubmit={onSubmit} noValidate>
        <CardContent className="grid gap-4">
          {errors.root && (
            <Alert variant="destructive">
              <AlertDescription>{errors.root.message}</AlertDescription>
            </Alert>
          )}
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField
              label="First name"
              autoComplete="given-name"
              error={errors.firstName?.message}
              {...register('firstName')}
            />
            <FormField
              label="Last name"
              autoComplete="family-name"
              error={errors.lastName?.message}
              {...register('lastName')}
            />
          </div>
          <FormField
            label="Email"
            type="email"
            autoComplete="email"
            error={errors.email?.message}
            {...register('email')}
          />
          <FormField
            label="Password"
            type="password"
            autoComplete="new-password"
            error={errors.password?.message}
            {...register('password')}
          />
          <FormField
            label="Confirm password"
            type="password"
            autoComplete="new-password"
            error={errors.confirmPassword?.message}
            {...register('confirmPassword')}
          />
        </CardContent>
        <CardFooter className="mt-7 flex-col items-stretch gap-4">
          <Button type="submit" size="lg" className="h-10 text-base" disabled={isSubmitting}>
            {isSubmitting ? 'Creating account…' : 'Create account'}
          </Button>
          <p className="text-center text-sm text-muted-foreground">
            Already have an account?{' '}
            <Link
              to="/login"
              state={location.state}
              className="text-foreground underline underline-offset-4"
            >
              Sign in
            </Link>
          </p>
        </CardFooter>
      </form>
    </Card>
  )
}
