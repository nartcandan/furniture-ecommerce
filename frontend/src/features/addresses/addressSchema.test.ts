import { describe, expect, it } from 'vitest'
import { addressSchema, type AddressFormInput } from './addressSchema'

const valid: AddressFormInput = {
  title: 'Home',
  recipientFirstName: 'Ayşe',
  recipientLastName: 'Yılmaz',
  phoneNumber: '05321234567',
  city: 'İstanbul',
  district: 'Kadıköy',
  addressLine: 'Moda Cad. No: 10',
  postalCode: '',
}

function parse(changes: Partial<AddressFormInput>) {
  return addressSchema.safeParse({ ...valid, ...changes })
}

function errorFor(changes: Partial<AddressFormInput>, field: keyof AddressFormInput) {
  const result = parse(changes)
  return result.success ? undefined : result.error.issues.find((issue) => issue.path[0] === field)?.message
}

describe('phone number', () => {
  // People write numbers with spaces, dashes and brackets. Only the digits and a leading + are sent.
  it.each([
    ['0532 123 45 67', '05321234567'],
    ['+90 (532) 123-45-67', '+905321234567'],
    ['5321234567', '5321234567'],
  ])('sends %j as %j', (text, sent) => {
    const result = parse({ phoneNumber: text })
    expect(result.success && result.data.phoneNumber).toBe(sent)
  })

  it.each(['123', '0532 123 45 67 89 01 2', '0532-ABC-4567', '90+5321234567'])('rejects %j', (text) => {
    expect(errorFor({ phoneNumber: text }, 'phoneNumber')).toBe(
      'Enter 10 to 15 digits, for example 0532 123 45 67.',
    )
  })
})

describe('postal code', () => {
  it('may be empty', () => {
    expect(parse({ postalCode: '' }).success).toBe(true)
  })

  it('has five digits when given', () => {
    expect(parse({ postalCode: '34710' }).success).toBe(true)
    expect(errorFor({ postalCode: '3471' }, 'postalCode')).toBe('A postal code has five digits.')
    expect(errorFor({ postalCode: '3471a' }, 'postalCode')).toBe('A postal code has five digits.')
  })
})

describe('province and district', () => {
  it('needs both', () => {
    expect(errorFor({ city: '' }, 'city')).toBe('Choose a province.')
    expect(errorFor({ district: '' }, 'district')).toBe('Choose a district.')
  })

  it('needs a district of the chosen province', () => {
    expect(errorFor({ city: 'Ankara', district: 'Kadıköy' }, 'district')).toBe(
      'Choose a district of this province.',
    )
  })

  it('accepts a district of the chosen province', () => {
    expect(parse({ city: 'Ankara', district: 'Çankaya' }).success).toBe(true)
  })
})

describe('text fields', () => {
  it('are trimmed and required', () => {
    expect(errorFor({ title: '   ' }, 'title')).toBe('Give this address a name, for example Home.')
    const result = parse({ addressLine: '  Moda Cad. No: 10  ' })
    expect(result.success && result.data.addressLine).toBe('Moda Cad. No: 10')
  })

  it('are limited like the API', () => {
    expect(errorFor({ title: 'a'.repeat(51) }, 'title')).toBe('Use at most 50 characters.')
    expect(errorFor({ addressLine: 'a'.repeat(501) }, 'addressLine')).toBe('Use at most 500 characters.')
  })
})
