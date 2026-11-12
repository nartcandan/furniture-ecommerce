const priceFormat = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' })
const numberFormat = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 })

/** Prices are in Turkish lira and written the way shoppers in Türkiye read them: ₺1.500,90. */
export function formatPrice(amount: number): string {
  return priceFormat.format(amount)
}

export function formatNumber(value: number): string {
  return numberFormat.format(value)
}

const dateTimeFormat = new Intl.DateTimeFormat('en-GB', {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: 'Europe/Istanbul',
})

/** Dates from the API are UTC. They are shown in Türkiye time, which is also what the cancellation day follows. */
export function formatDateTime(isoDate: string): string {
  return dateTimeFormat.format(new Date(isoDate))
}

/** A short order number people can read out. The full id stays in the address. */
export function orderNumber(id: string): string {
  return id.replaceAll('-', '').slice(-8).toUpperCase()
}
