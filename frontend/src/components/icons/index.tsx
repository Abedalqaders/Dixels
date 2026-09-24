// Icons reused across more than one feature (Sidebar nav + marketing copy on
// Home, or duplicated inline SVGs that would otherwise drift apart). Icons
// used by only a single feature stay local to that feature instead.

export function MenuIcon() {
  return (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round"><path d="M3 6h14M3 10h14M3 14h14" /></svg>
  )
}

export function CalendarIcon() {
  return (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><rect x="2.5" y="3.5" width="15" height="13" rx="2" /><path d="M2.5 7.5h15M7 2v3M13 2v3" /></svg>
  )
}

export function CalendarLinesIcon() {
  return (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><rect x="2.5" y="3.5" width="15" height="13" rx="2" /><path d="M6 8h8M6 11.5h5" /></svg>
  )
}

export function SearchIcon() {
  return (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round"><circle cx="9" cy="9" r="5.5" /><path d="M13 13l4 4" /></svg>
  )
}

export function ClockIcon() {
  return (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round"><circle cx="10" cy="10" r="7.5" /><path d="M10 5.5V10l3 2" /></svg>
  )
}

export function BuildingDoorIcon() {
  return (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><path d="M3 17V7l7-4 7 4v10z" /><path d="M8 17v-5h4v5" /></svg>
  )
}

export function FloorsIcon() {
  return (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round"><path d="M3.5 6h13M3.5 10h13M3.5 14h13" /></svg>
  )
}

export function SpacesIcon() {
  return (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><rect x="5" y="3.5" width="10" height="7" rx="1" /><path d="M8 14h4M10 10.5V14" /><path d="M3 17h14" /></svg>
  )
}

export function PeopleIcon() {
  return (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><circle cx="7.5" cy="6.5" r="2.8" /><path d="M2.5 16c.4-3.4 2.5-5.2 5-5.2s4.6 1.8 5 5.2" /><circle cx="14.5" cy="7.3" r="2.1" /><path d="M12.3 10.9c1.1-.4 2.3-.3 3.3.4 1.1.8 1.8 2.3 2 4.7" /></svg>
  )
}

export function SignOutIcon() {
  return (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><path d="M8 3.5H5a1.5 1.5 0 0 0-1.5 1.5v10A1.5 1.5 0 0 0 5 16.5h3" /><path d="M12.5 13.5 16 10l-3.5-3.5M16 10H8" /></svg>
  )
}
