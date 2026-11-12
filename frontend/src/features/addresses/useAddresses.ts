import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createAddress,
  deleteAddress,
  getAddresses,
  updateAddress,
  type AddressInput,
} from '@/api/addresses'

const addressesKey = ['addresses'] as const

export function useAddresses() {
  return useQuery({ queryKey: addressesKey, queryFn: getAddresses })
}

/** Creates an address, or updates the one with the given id. */
export function useSaveAddress(id?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: AddressInput) => (id ? updateAddress(id, input) : createAddress(input)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: addressesKey }),
  })
}

export function useDeleteAddress() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: deleteAddress,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: addressesKey }),
  })
}
