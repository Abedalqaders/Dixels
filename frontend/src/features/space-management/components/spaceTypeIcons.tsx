// Icons specific to the space-management hierarchy tree — not reused by any
// other feature, so they stay local instead of in the shared icon library.

export const ICONS = {
  building: (
    <svg className="ic" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><path d="M3 17V7l7-4 7 4v10z" /></svg>
  ),
  floor: (
    <svg className="ic" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round"><path d="M3.5 6h13M3.5 10h13M3.5 14h13" /></svg>
  ),
  'meeting-room': (
    <svg className="ic" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><circle cx="7.2" cy="6.8" r="2.2" /><path d="M2.8 15.3c.3-2.6 2-4 4.4-4s4 1.4 4.4 4" /><circle cx="14.3" cy="7.6" r="1.7" /><path d="M12.2 11.4c.9-.4 1.9-.4 2.7.2.9.6 1.5 1.8 1.7 3.2" /></svg>
  ),
  'focus-pod': (
    <svg className="ic" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><path d="M4.5 17V9.5a5.5 5.5 0 0 1 11 0V17" /><path d="M3 17h14" /></svg>
  ),
  desk: (
    <svg className="ic" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><rect x="5" y="3.5" width="10" height="7" rx="1" /><path d="M8 14h4M10 10.5V14" /><path d="M3 17h14" /></svg>
  ),
  // Fallback for a custom space type — a plain box, so an admin-added type still renders an
  // intentional icon instead of silently reusing another type's icon.
  generic: (
    <svg className="ic" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><rect x="3.5" y="3.5" width="13" height="13" rx="1.5" /></svg>
  ),
}

/** Maps the backend's IconKey enum ordinal (MeetingRoom=0, FocusPod=1, Desk=2, Generic=3)
 * to this map's keys. */
export function iconKeyToIconName(iconKey: number): keyof typeof ICONS {
  switch (iconKey) {
    case 0:
      return 'meeting-room'
    case 1:
      return 'focus-pod'
    case 2:
      return 'desk'
    default:
      return 'generic'
  }
}
