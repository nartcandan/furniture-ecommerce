import { describe, expect, it } from 'vitest'
import { formatDateTime, formatPrice, orderNumber } from './format'

describe('formatPrice', () => {
  // Turkish lira, written the way shoppers in Türkiye read it.
  it.each([
    [1500.9, '₺1.500,90'],
    [0, '₺0,00'],
    [99999999.99, '₺99.999.999,99'],
  ])('writes %d as %j', (amount, text) => {
    expect(formatPrice(amount)).toBe(text)
  })
})

describe('formatDateTime', () => {
  // The API sends UTC. Türkiye is UTC+3 all year, and the cancellation day follows Türkiye time.
  it('shows Türkiye time', () => {
    expect(formatDateTime('2026-11-19T21:30:00Z')).toBe('20 Nov 2026, 00:30')
  })
})

describe('orderNumber', () => {
  it('is the last eight characters of the id, in capitals', () => {
    expect(orderNumber('01a1d0f1-8a85-7037-a35a-4208799965ee')).toBe('799965EE')
  })
})
