import { describe, expect, it } from 'vitest'
import { DEFAULT_SORT, productsPath, readFilters, toSearchParams } from './productFilters'

const read = (query: string) => readFilters(new URLSearchParams(query))

describe('readFilters', () => {
  it('uses the defaults for an empty address', () => {
    expect(read('')).toEqual({
      category: undefined,
      search: undefined,
      minPrice: undefined,
      maxPrice: undefined,
      sort: DEFAULT_SORT,
      page: 1,
    })
  })

  it('reads every filter', () => {
    expect(read('category=living-room&search=sofa&minPrice=100&maxPrice=2500.5&sort=price_asc&page=3')).toEqual({
      category: 'living-room',
      search: 'sofa',
      minPrice: 100,
      maxPrice: 2500.5,
      sort: 'price_asc',
      page: 3,
    })
  })

  it('drops blank text filters', () => {
    const filters = read('category=%20%20&search=%20')
    expect(filters.category).toBeUndefined()
    expect(filters.search).toBeUndefined()
  })

  it('trims text filters', () => {
    expect(read('search=%20oak%20table%20').search).toBe('oak table')
  })

  // Anyone can type an address. Values the API would reject are dropped instead of sent.
  it.each(['-5', 'abc', 'Infinity', ''])('drops the price %j', (price) => {
    const filters = read(`minPrice=${price}&maxPrice=${price}`)
    expect(filters.minPrice).toBeUndefined()
    expect(filters.maxPrice).toBeUndefined()
  })

  it('drops a maximum price below the minimum', () => {
    const filters = read('minPrice=500&maxPrice=100')
    expect(filters.minPrice).toBe(500)
    expect(filters.maxPrice).toBeUndefined()
  })

  it('keeps a maximum price equal to the minimum', () => {
    expect(read('minPrice=500&maxPrice=500').maxPrice).toBe(500)
  })

  it('falls back to the default sort for an unknown one', () => {
    expect(read('sort=cheapest').sort).toBe(DEFAULT_SORT)
  })

  it.each(['0', '-1', '1.5', 'abc', '1000001'])('falls back to the first page for %j', (page) => {
    expect(read(`page=${page}`).page).toBe(1)
  })
})

describe('toSearchParams', () => {
  it('leaves the defaults out, so a plain list keeps a plain address', () => {
    expect(toSearchParams(read('')).toString()).toBe('')
    expect(productsPath(read('sort=newest&page=1'))).toBe('/products')
  })

  it('writes back what readFilters read', () => {
    const query = 'category=bedroom&search=lamp&minPrice=10&maxPrice=99&sort=name&page=2'
    expect(toSearchParams(read(query)).toString()).toBe(query)
  })
})
