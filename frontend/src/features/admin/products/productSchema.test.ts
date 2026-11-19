import { describe, expect, it } from 'vitest'
import { productSchema, type ProductFormInput } from './productSchema'

const valid: ProductFormInput = {
  categoryId: '01a1b5a3-c92f-7823-8e6e-234168a834dd',
  name: 'Oak chair',
  description: '',
  price: '1499,90',
  stockQuantity: '3',
  widthCm: '',
  depthCm: '',
  heightCm: '',
  weightKg: '',
  material: '',
  color: '',
  isActive: true,
}

function parse(changes: Partial<ProductFormInput>) {
  return productSchema.safeParse({ ...valid, ...changes })
}

/** The first message for one field, or undefined when the field is valid. */
function errorFor(field: keyof ProductFormInput, value: string) {
  const result = parse({ [field]: value })
  return result.success ? undefined : result.error.issues.find((issue) => issue.path[0] === field)?.message
}

describe('price', () => {
  // People in Türkiye write a decimal comma. Both marks must give the same number.
  it.each([
    ['1499,90', 1499.9],
    ['1499.90', 1499.9],
    ['1499', 1499],
    ['0', 0],
    [' 25,5 ', 25.5],
    ['99999999,99', 99999999.99],
  ])('reads %j as %d', (text, number) => {
    const result = parse({ price: text })
    expect(result.success && result.data.price).toBe(number)
  })

  it('is required', () => {
    expect(errorFor('price', '')).toBe('Enter a value.')
  })

  // The database column is numeric(10,2).
  it.each(['1499,999', '123456789', '1.499,90', '-5', 'abc', '12,'])('rejects %j', (text) => {
    expect(errorFor('price', text)).toMatch(/^Enter a number with at most 8 digits and 2 after/)
  })
})

describe('measurements', () => {
  it('are optional and become null when empty', () => {
    const result = parse({})
    expect(result.success && [result.data.widthCm, result.data.weightKg]).toEqual([null, null])
  })

  it('read a decimal comma', () => {
    const result = parse({ widthCm: '45,5', weightKg: '12,25' })
    expect(result.success && [result.data.widthCm, result.data.weightKg]).toEqual([45.5, 12.25])
  })

  it('must be greater than zero', () => {
    expect(errorFor('heightCm', '0')).toBe('Enter a value greater than 0.')
    expect(errorFor('weightKg', '0,00')).toBe('Enter a value greater than 0.')
  })

  // Centimetres are numeric(6,1) and kilograms numeric(6,2) in the database.
  it('follow the precision of their columns', () => {
    expect(errorFor('widthCm', '12345,5')).toBeUndefined()
    expect(errorFor('widthCm', '123456')).toMatch(/at most 5 digits and 1 after/)
    expect(errorFor('widthCm', '45,55')).toMatch(/at most 5 digits and 1 after/)
    expect(errorFor('weightKg', '1234,56')).toBeUndefined()
    expect(errorFor('weightKg', '12345')).toMatch(/at most 4 digits and 2 after/)
  })
})

describe('starting stock', () => {
  it('reads a whole number', () => {
    const result = parse({ stockQuantity: '1000000' })
    expect(result.success && result.data.stockQuantity).toBe(1_000_000)
  })

  it.each(['', '-1', '2,5', 'ten'])('rejects %j', (text) => {
    expect(errorFor('stockQuantity', text)).toBe('Enter a whole number.')
  })

  it('stops at one million, like the API', () => {
    expect(errorFor('stockQuantity', '1000001')).toBe('Enter at most 1,000,000.')
  })
})

describe('name and category', () => {
  it('trims the name', () => {
    const result = parse({ name: '  Oak chair  ' })
    expect(result.success && result.data.name).toBe('Oak chair')
  })

  // The slug is made from the name, so a name of only symbols would give an empty address.
  it('needs a letter or a digit in the name', () => {
    expect(errorFor('name', '!!! ---')).toBe('The name needs at least one letter or digit.')
    expect(errorFor('name', 'Çay masası')).toBeUndefined()
  })

  it('needs a category', () => {
    expect(errorFor('categoryId', '')).toBe('Choose a category.')
  })

  it('limits the text fields like the API', () => {
    expect(errorFor('name', 'a'.repeat(201))).toBe('Use at most 200 characters.')
    expect(errorFor('material', 'a'.repeat(101))).toBe('Use at most 100 characters.')
    expect(errorFor('color', 'a'.repeat(51))).toBe('Use at most 50 characters.')
  })
})
