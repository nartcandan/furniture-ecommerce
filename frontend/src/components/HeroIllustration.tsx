/** A living-room corner drawn with the theme colours: a lamp, an armchair, a side table and a plant. */
export function HeroIllustration({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 480 400" role="img" aria-label="A cosy corner with an armchair and a lamp" className={className}>
      {/* Wall and floor */}
      <rect width="480" height="400" rx="28" className="fill-secondary" />
      <path d="M0 318h480v54a28 28 0 0 1-28 28H28a28 28 0 0 1-28-28z" className="fill-highlight/25" />
      <path d="M0 318h480" strokeWidth="2" className="stroke-foreground/10" />

      {/* Framed picture */}
      <rect x="286" y="62" width="104" height="124" rx="6" className="fill-card stroke-foreground/15" strokeWidth="2" />
      <circle cx="338" cy="110" r="20" className="fill-highlight/70" />
      <path d="M298 174l26-34 18 20 14-14 22 28z" className="fill-primary/60" />

      {/* Floor lamp */}
      <path d="M92 318V128" strokeWidth="5" strokeLinecap="round" className="stroke-foreground/70" />
      <path d="M66 318h52" strokeWidth="6" strokeLinecap="round" className="stroke-foreground/70" />
      <path d="M58 132l14-56h40l14 56z" className="fill-highlight" />

      {/* Armchair */}
      <path d="M168 318v20M300 318v20" strokeWidth="7" strokeLinecap="round" className="stroke-foreground/70" />
      <rect x="160" y="170" width="148" height="120" rx="30" className="fill-primary" />
      <rect x="138" y="232" width="46" height="90" rx="20" className="fill-primary" />
      <rect x="284" y="232" width="46" height="90" rx="20" className="fill-primary" />
      <rect x="176" y="258" width="116" height="62" rx="16" className="fill-primary" />
      <rect x="180" y="250" width="108" height="34" rx="14" className="fill-primary-foreground/20" />
      <rect x="196" y="196" width="76" height="52" rx="14" className="fill-highlight/85" />

      {/* Side table with a plant */}
      <path d="M372 266h76" strokeWidth="7" strokeLinecap="round" className="stroke-foreground/70" />
      <path d="M384 268l-8 52M436 268l8 52" strokeWidth="5" strokeLinecap="round" className="stroke-foreground/70" />
      <path d="M392 262l5-34h26l5 34z" className="fill-card stroke-foreground/15" strokeWidth="2" />
      <path
        d="M410 228c0-26-8-44-24-54 2 22 10 40 24 54zm0 0c0-30 6-52 22-66 2 26-6 48-22 66zm0 0c-4-16-14-26-30-30 6 16 16 26 30 30z"
        className="fill-primary/80"
      />
    </svg>
  )
}
