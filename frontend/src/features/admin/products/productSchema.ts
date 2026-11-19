import { z } from 'zod'

interface DecimalRules {
  /** Digits allowed after the decimal mark. */
  decimals: number
  /** Digits allowed before it. Together they match the column's precision in the database. */
  integerDigits: number
  /** Prices may be 0, measurements must be greater than 0. */
  allowZero: boolean
  required: boolean
}

/**
 * A number typed into a text box. Both `1500.90` and `1500,90` are accepted, since people in Türkiye write
 * a decimal comma. An empty optional box becomes `null`.
 */
export function decimal({ decimals, integerDigits, allowZero, required }: DecimalRules) {
  const pattern = new RegExp(`^\\d{1,${integerDigits}}([.,]\\d{1,${decimals}})?$`)
  return z
    .string()
    .trim()
    .superRefine((value, context) => {
      if (value === '') {
        if (required) context.addIssue({ code: 'custom', message: 'Enter a value.' })
        return
      }
      if (!pattern.test(value)) {
        context.addIssue({
          code: 'custom',
          message: `Enter a number with at most ${integerDigits} digits and ${decimals} after the decimal mark.`,
        })
        return
      }
      if (!allowZero && Number(value.replace(',', '.')) === 0) {
        context.addIssue({ code: 'custom', message: 'Enter a value greater than 0.' })
      }
    })
    .transform((value) => (value === '' ? null : Number(value.replace(',', '.'))))
}

const measurement = (decimals: number) =>
  decimal({ decimals, integerDigits: 6 - decimals, allowZero: false, required: false })

const optionalText = (max: number) => z.string().trim().max(max, `Use at most ${max} characters.`)

// The same rules as the API.
export const productSchema = z.object({
  categoryId: z.string().min(1, 'Choose a category.'),
  name: z
    .string()
    .trim()
    .min(1, 'Enter a name.')
    .max(200, 'Use at most 200 characters.')
    .regex(/[\p{L}\p{N}]/u, 'The name needs at least one letter or digit.'),
  description: optionalText(5000),
  price: decimal({ decimals: 2, integerDigits: 8, allowZero: true, required: true }),
  stockQuantity: z
    .string()
    .trim()
    .regex(/^\d{1,7}$/, 'Enter a whole number.')
    .transform(Number)
    .refine((value) => value <= 1_000_000, 'Enter at most 1,000,000.'),
  widthCm: measurement(1),
  depthCm: measurement(1),
  heightCm: measurement(1),
  weightKg: measurement(2),
  material: optionalText(100),
  color: optionalText(50),
  isActive: z.boolean(),
})

export type ProductFormInput = z.input<typeof productSchema>
export type ProductFormOutput = z.output<typeof productSchema>
