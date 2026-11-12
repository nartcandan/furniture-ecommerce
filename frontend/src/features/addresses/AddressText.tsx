interface AddressTextProps {
  address: {
    recipientFirstName: string
    recipientLastName: string
    phoneNumber: string
    city: string
    district: string
    addressLine: string
    postalCode: string | null
  }
}

/** A delivery address, as written on a parcel. Used for saved addresses and for the copy kept in an order. */
export function AddressText({ address }: AddressTextProps) {
  return (
    <address className="text-sm leading-relaxed text-muted-foreground not-italic">
      <span className="block font-medium text-foreground">
        {address.recipientFirstName} {address.recipientLastName}
      </span>
      <span className="block whitespace-pre-line">{address.addressLine}</span>
      <span className="block">
        {address.postalCode && `${address.postalCode} `}
        {address.district} / {address.city}
      </span>
      <span className="block">{address.phoneNumber}</span>
    </address>
  )
}
