import { zodResolver } from '@hookform/resolvers/zod'
import { useId } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { z } from 'zod'
import type { Address, AddressInput } from '@/api/addresses'
import { FormError } from '@/components/FormError'
import { FormField } from '@/components/FormField'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { provinces } from '@/data/provinces'
import { showApiError } from '@/lib/formErrors'
import { useSaveAddress } from './useAddresses'

const required = (message: string, max: number) =>
  z.string().trim().min(1, message).max(max, `Use at most ${max} characters.`)

// The same rules as the API. People write phone numbers with spaces, dashes and brackets, so those are
// removed before the number is checked and sent.
const schema = z.object({
  title: required('Give this address a name, for example Home.', 50),
  recipientFirstName: required('Enter the first name.', 100),
  recipientLastName: required('Enter the last name.', 100),
  phoneNumber: z
    .string()
    .transform((value) => value.replace(/[\s\-()]/g, ''))
    .pipe(z.string().regex(/^\+?[0-9]{10,15}$/, 'Enter 10 to 15 digits, for example 0532 123 45 67.')),
  city: z.string().min(1, 'Choose a province.'),
  district: required('Enter the district.', 50),
  addressLine: required('Enter the street, building and door number.', 500),
  postalCode: z
    .string()
    .trim()
    .regex(/^([0-9]{5})?$/, 'A postal code has five digits.'),
})

type AddressFormInput = z.input<typeof schema>
type AddressFormOutput = z.output<typeof schema>

const fields = [
  'title',
  'recipientFirstName',
  'recipientLastName',
  'phoneNumber',
  'city',
  'district',
  'addressLine',
  'postalCode',
] as const

interface AddressFormProps {
  /** The address to change. Without it the form creates a new one. */
  address?: Address
  /** Fills the recipient's name in a new address. */
  defaultName?: { firstName: string; lastName: string }
  onSaved: (address: Address) => void
  onCancel?: () => void
}

export function AddressForm({ address, defaultName, onSaved, onCancel }: AddressFormProps) {
  const save = useSaveAddress(address?.id)
  const cityId = useId()
  const addressLineId = useId()
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<AddressFormInput, unknown, AddressFormOutput>({
    resolver: zodResolver(schema),
    defaultValues: {
      title: address?.title ?? '',
      recipientFirstName: address?.recipientFirstName ?? defaultName?.firstName ?? '',
      recipientLastName: address?.recipientLastName ?? defaultName?.lastName ?? '',
      phoneNumber: address?.phoneNumber ?? '',
      city: address?.city ?? '',
      district: address?.district ?? '',
      addressLine: address?.addressLine ?? '',
      postalCode: address?.postalCode ?? '',
    },
  })

  const onSubmit = handleSubmit(async (values) => {
    const input: AddressInput = { ...values, postalCode: values.postalCode || null }
    try {
      onSaved(await save.mutateAsync(input))
    } catch (error) {
      showApiError(error, setError, fields)
    }
  })

  return (
    <form onSubmit={onSubmit} noValidate className="grid gap-4">
      <FormError message={errors.root?.message} />

      <FormField
        label="Address name"
        placeholder="Home, Office…"
        error={errors.title?.message}
        {...register('title')}
      />

      <div className="grid gap-4 sm:grid-cols-2">
        <FormField
          label="First name"
          autoComplete="given-name"
          error={errors.recipientFirstName?.message}
          {...register('recipientFirstName')}
        />
        <FormField
          label="Last name"
          autoComplete="family-name"
          error={errors.recipientLastName?.message}
          {...register('recipientLastName')}
        />
      </div>

      <FormField
        label="Phone"
        type="tel"
        autoComplete="tel"
        placeholder="0532 123 45 67"
        error={errors.phoneNumber?.message}
        {...register('phoneNumber')}
      />

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="grid gap-1.5">
          <Label htmlFor={cityId}>Province</Label>
          <Controller
            control={control}
            name="city"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger
                  id={cityId}
                  ref={field.ref}
                  onBlur={field.onBlur}
                  aria-invalid={errors.city ? true : undefined}
                  className="w-full bg-background data-[size=default]:h-10"
                >
                  <SelectValue placeholder="Choose…" />
                </SelectTrigger>
                <SelectContent className="max-h-72">
                  {provinces.map((province) => (
                    <SelectItem key={province} value={province}>
                      {province}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
          {errors.city && <p className="text-sm text-destructive">{errors.city.message}</p>}
        </div>
        <FormField label="District" error={errors.district?.message} {...register('district')} />
      </div>

      <div className="grid gap-1.5">
        <Label htmlFor={addressLineId}>Address</Label>
        <Textarea
          id={addressLineId}
          rows={3}
          autoComplete="street-address"
          placeholder="Neighbourhood, street, building and door number"
          aria-invalid={errors.addressLine ? true : undefined}
          className="bg-background"
          {...register('addressLine')}
        />
        {errors.addressLine && (
          <p className="text-sm text-destructive">{errors.addressLine.message}</p>
        )}
      </div>

      <FormField
        label="Postal code (optional)"
        inputMode="numeric"
        autoComplete="postal-code"
        maxLength={5}
        error={errors.postalCode?.message}
        className="h-10 bg-background sm:max-w-40"
        {...register('postalCode')}
      />

      <div className="mt-2 flex flex-wrap justify-end gap-2">
        {onCancel && (
          <Button type="button" variant="ghost" size="lg" className="h-10 px-4" onClick={onCancel}>
            Cancel
          </Button>
        )}
        <Button type="submit" size="lg" className="h-10 px-5" disabled={isSubmitting}>
          {isSubmitting ? 'Saving…' : 'Save address'}
        </Button>
      </div>
    </form>
  )
}
