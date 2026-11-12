import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link, useLocation } from 'react-router'
import { z } from 'zod'
import { login } from '@/api/auth'
import { FormField } from '@/components/FormField'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { useAuthStore } from '@/features/auth/authStore'
import { showApiError } from '@/lib/formErrors'

const schema = z.object({
  email: z.string().trim().min(1, 'Enter your email address.'),
  password: z.string().min(1, 'Enter your password.'),
})

type LoginForm = z.infer<typeof schema>

export function LoginPage() {
  const location = useLocation()
  const setSession = useAuthStore((state) => state.setSession)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({ resolver: zodResolver(schema) })

  const onSubmit = handleSubmit(async (values) => {
    try {
      // Saving the session is enough: the guard around this page then sends the user on.
      setSession(await login(values))
    } catch (error) {
      showApiError(error, setError, ['email', 'password'])
    }
  })

  return (
    <Card className="mx-auto mt-4 w-full max-w-md [--card-spacing:--spacing(7)]">
      <CardHeader>
        <CardTitle>
          <h1 className="text-2xl font-semibold">Welcome back</h1>
        </CardTitle>
        <CardDescription>Sign in to see your orders and check out.</CardDescription>
      </CardHeader>
      <form onSubmit={onSubmit} noValidate>
        <CardContent className="grid gap-4">
          {errors.root && (
            <Alert variant="destructive">
              <AlertDescription>{errors.root.message}</AlertDescription>
            </Alert>
          )}
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
            autoComplete="current-password"
            error={errors.password?.message}
            {...register('password')}
          />
        </CardContent>
        <CardFooter className="mt-7 flex-col items-stretch gap-4">
          <Button type="submit" size="lg" className="h-10 text-base" disabled={isSubmitting}>
            {isSubmitting ? 'Signing in…' : 'Sign in'}
          </Button>
          <p className="text-center text-sm text-muted-foreground">
            New here?{' '}
            <Link
              to="/register"
              state={location.state}
              className="text-foreground underline underline-offset-4"
            >
              Create an account
            </Link>
          </p>
        </CardFooter>
      </form>
    </Card>
  )
}
