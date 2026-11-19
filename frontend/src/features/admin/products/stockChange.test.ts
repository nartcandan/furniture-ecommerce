import { describe, expect, it } from 'vitest'
import { readStockChange } from './stockChange'

describe('readStockChange', () => {
  it.each([
    ['10', 10],
    ['+10', 10],
    ['-2', -2],
    [' 7 ', 7],
    ['1000000', 1_000_000],
    ['-1000000', -1_000_000],
  ])('reads %j as %d', (text, change) => {
    expect(readStockChange(text)).toBe(change)
  })

  // Zero changes nothing, and the API rejects it.
  it.each(['0', '+0', '-0', '', '   ', 'ten', '2,5', '1.5', '--2', '+-2', '1000001', '- 2'])(
    'rejects %j',
    (text) => {
      expect(readStockChange(text)).toBeNull()
    },
  )
})
