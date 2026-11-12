import { apiRequest } from './client'

export interface AddressInput {
  /** The user's own name for the address, for example "Home". */
  title: string
  recipientFirstName: string
  recipientLastName: string
  /** Digits only, with an optional leading "+". */
  phoneNumber: string
  city: string
  district: string
  addressLine: string
  postalCode: string | null
}

export interface Address extends AddressInput {
  id: string
}

export function getAddresses(): Promise<Address[]> {
  return apiRequest<Address[]>('/api/addresses')
}

export function createAddress(input: AddressInput): Promise<Address> {
  return apiRequest<Address>('/api/addresses', { method: 'POST', body: input })
}

export function updateAddress(id: string, input: AddressInput): Promise<Address> {
  return apiRequest<Address>(`/api/addresses/${id}`, { method: 'PUT', body: input })
}

export function deleteAddress(id: string): Promise<void> {
  return apiRequest<void>(`/api/addresses/${id}`, { method: 'DELETE' })
}
