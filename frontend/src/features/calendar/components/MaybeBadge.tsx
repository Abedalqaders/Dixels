/** The small amber "?" on an invite I said Maybe to, beside its people icon. */
export function MaybeBadge() {
  return (
    <span
      className="grid size-3.5 flex-none place-items-center rounded-full bg-[var(--state-expired-soft)] text-[9px] leading-none font-extrabold text-[var(--state-expired-ink)]"
      aria-hidden="true"
    >
      ?
    </span>
  )
}
