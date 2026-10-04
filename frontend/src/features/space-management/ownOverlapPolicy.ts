import { OwnOverlapPolicy } from '@/features/space-management/api/spaceManagementApi'
import type { TextKeys } from '@/i18n/keys'

// What each choice means to the people booking — shown under the picker. Keys, not texts,
// so the words follow the reader's language: t(option.labelKey).
export const OWN_OVERLAP_OPTIONS: { value: OwnOverlapPolicy; labelKey: keyof TextKeys; hintKey: keyof TextKeys }[] = [
  { value: OwnOverlapPolicy.Allow, labelKey: 'Rules:OverlapAllow', hintKey: 'Rules:OverlapAllowHint' },
  { value: OwnOverlapPolicy.Warn, labelKey: 'Rules:OverlapWarn', hintKey: 'Rules:OverlapWarnHint' },
  { value: OwnOverlapPolicy.Block, labelKey: 'Rules:OverlapBlock', hintKey: 'Rules:OverlapBlockHint' },
]
