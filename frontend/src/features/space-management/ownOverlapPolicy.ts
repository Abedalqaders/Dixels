import { OwnOverlapPolicy } from '@/features/space-management/api/spaceManagementApi'

// What each choice means to the people booking — shown under the picker.
export const OWN_OVERLAP_OPTIONS: { value: OwnOverlapPolicy; label: string; hint: string }[] = [
  {
    value: OwnOverlapPolicy.Allow,
    label: 'Allow',
    hint: 'People can hold several bookings at once (a desk and a meeting room) without being told.',
  },
  {
    value: OwnOverlapPolicy.Warn,
    label: 'Allow with a warning',
    hint: 'People can hold several bookings at once, but are told about the clash before they book.',
  },
  {
    value: OwnOverlapPolicy.Block,
    label: 'One booking at a time',
    hint: 'A second booking that overlaps one someone already has is refused.',
  },
]
