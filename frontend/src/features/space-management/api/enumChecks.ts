/**
 * The app's enum objects (`OwnOverlapPolicy.Warn` and the like) against the API's enums, both
 * ways: a value the backend dropped fails the `satisfies` next to each object; one it added
 * fails here, naming the enum. Types only — nothing in this file reaches the bundle.
 */
import type { Check, NamesEvery } from '@/lib/api/schemaTypes'
import type * as SpaceManagement from './spaceManagementApi'

export type EnumObjectsAreComplete = [
  Check<NamesEvery<typeof SpaceManagement.IconKey, SpaceManagement.IconKey>>,
  Check<NamesEvery<typeof SpaceManagement.OwnOverlapPolicy, SpaceManagement.OwnOverlapPolicy>>,
  Check<NamesEvery<typeof SpaceManagement.OverrideScope, SpaceManagement.OverrideScope>>,
  Check<NamesEvery<typeof SpaceManagement.OverrideEffect, SpaceManagement.OverrideEffect>>,
  Check<NamesEvery<typeof SpaceManagement.ReasonCategory, SpaceManagement.ReasonCategory>>,
]
