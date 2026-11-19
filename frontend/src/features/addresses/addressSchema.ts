import { z } from 'zod'
import { districtsByProvince } from '@/data/districts'

const required = (message: string, max: number) =>
  z.string().trim().min(1, message).max(max, `Use at most ${max} characters.`)

// The same rules as the API. People write phone numbers with spaces, dashes and brackets, so those are
// removed before the number is checked and sent.
export const addressSchema = z
  .object({
    title: required('Give this address a name, for example Home.', 50),
    recipientFirstName: required('Enter the first name.', 100),
    recipientLastName: required('Enter the last name.', 100),
    phoneNumber: z
      .string()
      .transform((value) => value.replace(/[\s\-()]/g, ''))
      .pipe(z.string().regex(/^\+?[0-9]{10,15}$/, 'Enter 10 to 15 digits, for example 0532 123 45 67.')),
    city: z.string().min(1, 'Choose a province.'),
    district: z.string().min(1, 'Choose a district.'),
    addressLine: required('Enter the street, building and door number.', 500),
    postalCode: z
      .string()
      .trim()
      .regex(/^([0-9]{5})?$/, 'A postal code has five digits.'),
  })
  // An address saved before the district list existed may hold a district that is not in it.
  .refine((values) => districtsByProvince[values.city]?.includes(values.district) ?? true, {
    path: ['district'],
    message: 'Choose a district of this province.',
  })

export type AddressFormInput = z.input<typeof addressSchema>
export type AddressFormOutput = z.output<typeof addressSchema>
