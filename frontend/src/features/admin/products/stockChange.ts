/** Reads `+5`, `5` or `-2`. Anything else, and zero, is `null`. */
export function readStockChange(text: string): number | null {
  const match = /^([+-]?)(\d{1,7})$/.exec(text.trim())
  if (!match) return null
  const change = Number(match[2]) * (match[1] === '-' ? -1 : 1)
  return change === 0 || Math.abs(change) > 1_000_000 ? null : change
}
