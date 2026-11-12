import { useId, type ComponentProps } from 'react'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

interface FormFieldProps extends ComponentProps<typeof Input> {
  label: string
  error?: string
}

/** A labelled input with its error message underneath. */
export function FormField({ label, error, id, ...inputProps }: FormFieldProps) {
  const generatedId = useId()
  const inputId = id ?? generatedId
  const errorId = `${inputId}-error`

  return (
    <div className="grid gap-1.5">
      <Label htmlFor={inputId}>{label}</Label>
      <Input
        id={inputId}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        className="h-10 bg-background"
        {...inputProps}
      />
      {error && (
        <p id={errorId} className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}
