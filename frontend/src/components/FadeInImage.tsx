import { useState, type ComponentProps } from 'react'
import { cn } from '@/lib/utils'

interface FadeInImageProps extends ComponentProps<'img'> {
  /** The opacity once loaded. A dimmed image (out of stock) ends lower than 1. */
  loadedClassName?: string
}

/**
 * An image that fades in once it has loaded, instead of appearing in pieces over its placeholder.
 * The surface behind it shows meanwhile. Without motion the image simply appears.
 */
export function FadeInImage({ className, loadedClassName = 'opacity-100', onLoad, ...props }: FadeInImageProps) {
  const [loaded, setLoaded] = useState(false)

  return (
    <img
      {...props}
      // An image from the cache may finish loading before React listens for it, so it is checked on mount too.
      ref={(image) => {
        if (image?.complete && image.naturalWidth > 0) setLoaded(true)
      }}
      onLoad={(event) => {
        setLoaded(true)
        onLoad?.(event)
      }}
      className={cn(
        'transition-opacity duration-500 motion-reduce:transition-none',
        loaded ? loadedClassName : 'opacity-0',
        className,
      )}
    />
  )
}
