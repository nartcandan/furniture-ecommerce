const priceFormat = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' })
const numberFormat = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 })

/** Prices are in Turkish lira and written the way shoppers in Türkiye read them: ₺1.500,90. */
export function formatPrice(amount: number): string {
  return priceFormat.format(amount)
}

export function formatNumber(value: number): string {
  return numberFormat.format(value)
}
