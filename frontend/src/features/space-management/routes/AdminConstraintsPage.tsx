import { useState } from 'react'
import { useBlocker, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { Sidebar } from '@/components/Sidebar'
import { useApiQuery } from '@/hooks/useApiQuery'
import { queryKeys } from '@/lib/api/queryKeys'
import { useBookingImpactPrompt } from '@/features/space-management/hooks/useBookingImpactPrompt'
import { ConfirmDialog, useConfirm } from '@/components/ConfirmDialog'
import { useUnsavedChangesWarning } from '@/features/space-management/hooks/useUnsavedChangesWarning'
import { useToast } from '@/components/Toast'
import { EffectiveValueStrip } from '@/features/space-management/components/EffectiveValueStrip'
import type { EffectiveItem, EffectiveSource } from '@/features/space-management/components/EffectiveValueStrip'
import { BuildingLevelFields } from '@/features/space-management/components/BuildingLevelFields'
import { OWN_OVERLAP_OPTIONS } from '@/features/space-management/ownOverlapPolicy'
import type { BuildingDraft } from '@/features/space-management/components/BuildingLevelFields'
import { FloorLevelFields } from '@/features/space-management/components/FloorLevelFields'
import type { FloorDraft } from '@/features/space-management/components/FloorLevelFields'
import { SpaceLevelFields } from '@/features/space-management/components/SpaceLevelFields'
import type { SpaceDraft } from '@/features/space-management/components/SpaceLevelFields'
import { ClosuresList } from '@/features/space-management/components/ClosuresList'
import { hierarchyPermissions, Permissions } from '@/features/auth/permissions/permissionNames'
import { usePermission } from '@/features/auth/permissions/usePermission'
import { ResetToParentButton } from '@/features/space-management/components/ResetToParentButton'
import { buildingDraftEquals, floorDraftEquals, spaceDraftEquals } from '@/features/space-management/components/draftEquality'
import { minutesToHours } from '@/features/space-management/components/DurationPicker'
import { OperatingDays } from '@/features/space-management/operatingDays'
import { OperatingWindow } from '@/features/space-management/operatingWindow'
import { isCurrentlyClosed } from '@/features/space-management/constraintResolver'
import type { OverrideWindow } from '@/features/space-management/constraintResolver'
import {
  ApiError,
  getBuilding,
  getFloor,
  getFloorResolvedConstraints,
  getSpace,
  getSpaceResolvedConstraints,
  getOverrides,
  createOverride,
  deleteOverride,
  updateBuildingConstraints,
  getBuildingConstraintsImpact,
  getFloorConstraintsImpact,
  getSpaceConstraintsImpact,
  getOverrideImpact,
  updateFloorConstraints,
  updateSpaceConstraints,
  OverrideScope,
  OverrideEffect,
} from '@/features/space-management/api/spaceManagementApi'
import type {
  AvailabilityOverrideDto,
  CreateAvailabilityOverrideDto,
  OperatingWindowDto,
} from '@/features/space-management/api/spaceManagementApi'
import '@/styles/tokens.css'
import '@/styles/base.css'
import '@/styles/admin.css'
import { FormSkeleton } from '@/components/LoadingSkeletons'
import { TopBar } from '@/components/TopBar'

type Level = 'building' | 'floor' | 'space'

interface AncestorClosure {
  override: AvailabilityOverrideDto
  levelLabel: string
}

interface PageData {
  level: Level
  concurrencyStamp: string
  effectiveItems: EffectiveItem[]
  ownOverrides: AvailabilityOverrideDto[]
  ancestorOverrides: AncestorClosure[]
  scope: OverrideScope
  isCurrentlyClosedNow: boolean
  breadcrumb: string
  building?: { name: string; draft: BuildingDraft }
  floor?: {
    name: string
    buildingName: string
    draft: FloorDraft
    parentDays: OperatingDays
    parentHours: OperatingWindow
    parentMaxDurationMinutes: number
  }
  space?: {
    name: string
    capacity: number
    draft: SpaceDraft
    parentDays: OperatingDays
    parentDaysSource: 'Building' | 'Floor'
    parentHours: OperatingWindow
    parentHoursSource: 'Building' | 'Floor'
    parentMaxDurationMinutes: number
    parentMaxDurationSource: 'Building' | 'Floor'
  }
}

function operatingDaysFromApi(days: number[]): OperatingDays {
  return new OperatingDays(days.reduce((mask, d) => mask | (1 << d), 0))
}

function operatingDaysToApi(days: OperatingDays): number[] {
  const result: number[] = []
  for (let i = 0; i < 7; i++) if ((days.mask & (1 << i)) !== 0) result.push(i)
  return result
}

function operatingWindowFromApi(w: OperatingWindowDto): OperatingWindow {
  return w.isOpen24Hours ? OperatingWindow.FullDay : new OperatingWindow(w.open, w.close)
}

function operatingWindowToApi(w: OperatingWindow): OperatingWindowDto {
  return { isOpen24Hours: w.isOpen24Hours, open: w.open, close: w.close }
}

function describeDays(days: OperatingDays): string {
  const names = days.toDayNames()
  if (names.length === 7) return 'Every day'
  if (names.length === 0) return 'None'
  return names.map((n) => n.slice(0, 3)).join(', ')
}

function describeHours(hours: OperatingWindow): string {
  return hours.isOpen24Hours ? '24 hours' : `${hours.open} – ${hours.close}`
}

function toOverrideWindows(overrides: AvailabilityOverrideDto[]): OverrideWindow[] {
  return overrides.map((o) => ({
    startsAt: o.startsAt,
    endsAt: o.endsAt,
    effect: o.effect === OverrideEffect.Closed ? 'Closed' : 'Open',
  }))
}

export function AdminConstraintsPage() {
  const params = useParams<{ level: string; id: string }>()
  const navigate = useNavigate()
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const { ask: askImpact, prompt: impactPrompt } = useBookingImpactPrompt()
  const { confirm, dialog: confirmDialog } = useConfirm()
  const { showToast } = useToast()
  const [warnings, setWarnings] = useState<string[]>([])

  const level = params.level as Level
  const id = params.id ?? ''
  const validLevel = level === 'building' || level === 'floor' || level === 'space'

  // What this user may do here, from their ABP grants: change the rules only with the level's
  // Edit (otherwise the page is read-only), and see, add or remove closures only with the
  // Overrides permissions.
  const canEditRules = usePermission(hierarchyPermissions(validLevel ? level : 'building').Edit)
  const canViewClosures = usePermission(Permissions.Overrides.Default)
  const canAddClosure = usePermission(Permissions.Overrides.Create)
  const canDeleteClosure = usePermission(Permissions.Overrides.Delete)
  // Without Overrides.Default the closures API refuses: load none rather than fail the whole page.
  const listOverrides = (scope: OverrideScope, scopeId: string) =>
    canViewClosures ? getOverrides(token, scope, scopeId) : Promise.resolve({ items: [] as AvailabilityOverrideDto[] })

  const { status, data, error, refetch } = useApiQuery(queryKeys.hierarchy.constraints(level, id, canViewClosures), async (): Promise<PageData> => {
    if (level === 'building') {
      const building = await getBuilding(token, id)
      const days = operatingDaysFromApi(building.days)
      const hours = operatingWindowFromApi(building.hours)
      const [ownOverridesResult] = await Promise.all([listOverrides(OverrideScope.Building, id)])
      const ownOverrides = ownOverridesResult.items

      return {
        level: 'building',
        concurrencyStamp: building.concurrencyStamp,
        effectiveItems: [
          { label: 'Timezone', value: building.timezone, source: 'Building' },
          { label: 'Operating days', value: describeDays(days), source: 'Building' },
          { label: 'Operating hours', value: describeHours(hours), source: 'Building' },
          { label: 'Maximum duration', value: `${minutesToHours(building.maxDurationMinutes)}h`, source: 'Building' },
          { label: 'Booking horizon', value: `${building.maxHorizonDays} days`, source: 'Building' },
          { label: 'Recurring bookings horizon', value: `${building.maxSeriesHorizonDays} days`, source: 'Building' },
          { label: 'Minimum lead time', value: `${building.minLeadMinutes} min`, source: 'Building' },
          {
            label: 'Overlapping bookings',
            value: OWN_OVERLAP_OPTIONS.find((o) => o.value === building.ownOverlapPolicy)?.label ?? '',
            source: 'Building',
          },
        ],
        ownOverrides,
        ancestorOverrides: [],
        scope: OverrideScope.Building,
        isCurrentlyClosedNow: isCurrentlyClosed(days, hours, toOverrideWindows(ownOverrides), new Date()),
        breadcrumb: "Building level — only this level's own rules are set here.",
        building: {
          name: building.name,
          draft: {
            days,
            hours,
            maxDurationMinutes: building.maxDurationMinutes,
            maxHorizonDays: building.maxHorizonDays,
            maxSeriesHorizonDays: building.maxSeriesHorizonDays,
            minLeadMinutes: building.minLeadMinutes,
            ownOverlapPolicy: building.ownOverlapPolicy,
          },
        },
      }
    }

    if (level === 'floor') {
      const floor = await getFloor(token, id)
      const [building, resolved, ownOverridesResult, buildingOverridesResult] = await Promise.all([
        getBuilding(token, floor.buildingId),
        getFloorResolvedConstraints(token, id),
        listOverrides(OverrideScope.Floor, id),
        listOverrides(OverrideScope.Building, floor.buildingId),
      ])

      const parentDays = operatingDaysFromApi(building.days)
      const parentHours = operatingWindowFromApi(building.hours)
      const resolvedDays = operatingDaysFromApi(resolved.days.value)
      const resolvedHours = operatingWindowFromApi(resolved.hours.value)
      const ownOverrides = ownOverridesResult.items
      const ancestorOverrides: AncestorClosure[] = buildingOverridesResult.items.map((o) => ({
        override: o,
        levelLabel: 'Building',
      }))

      return {
        level: 'floor',
        concurrencyStamp: floor.concurrencyStamp,
        effectiveItems: [
          { label: 'Timezone', value: building.timezone, source: 'Building' },
          { label: 'Operating days', value: describeDays(resolvedDays), source: resolved.days.source as EffectiveSource },
          { label: 'Operating hours', value: describeHours(resolvedHours), source: resolved.hours.source as EffectiveSource },
          {
            label: 'Maximum duration',
            value: `${minutesToHours(resolved.maxDurationMinutes.value)}h`,
            source: resolved.maxDurationMinutes.source as EffectiveSource,
          },
          { label: 'Booking horizon', value: `${resolved.maxHorizonDays} days`, source: 'Building' },
          { label: 'Minimum lead time', value: `${resolved.minLeadMinutes} min`, source: 'Building' },
        ],
        ownOverrides,
        ancestorOverrides,
        scope: OverrideScope.Floor,
        isCurrentlyClosedNow: isCurrentlyClosed(
          resolvedDays,
          resolvedHours,
          toOverrideWindows([...ownOverrides, ...ancestorOverrides.map((a) => a.override)]),
          new Date(),
        ),
        breadcrumb: `Floor level · ${building.name} — only this level's own rules are set here. Anything left on Inherit follows the level above.`,
        floor: {
          name: floor.name,
          buildingName: building.name,
          draft: {
            days: floor.days ? operatingDaysFromApi(floor.days) : null,
            hours: floor.hours ? operatingWindowFromApi(floor.hours) : null,
            maxDurationMinutes: floor.maxDurationMinutes,
          },
          parentDays,
          parentHours,
          parentMaxDurationMinutes: building.maxDurationMinutes,
        },
      }
    }

    const space = await getSpace(token, id)
    const floor = await getFloor(token, space.floorId)
    // The floor's own raw fields (not a separate resolved-constraints call) are enough to
    // know both whether it overrides and what its resolved value is: when it does override,
    // its own field IS the resolved value; when it doesn't, the Building's is.
    const [resolved, ownOverridesResult, floorOverridesResult, buildingOverridesResult, building] = await Promise.all([
      getSpaceResolvedConstraints(token, id),
      listOverrides(OverrideScope.Space, id),
      listOverrides(OverrideScope.Floor, space.floorId),
      listOverrides(OverrideScope.Building, floor.buildingId),
      getBuilding(token, floor.buildingId),
    ])

    const floorOwnDays = floor.days !== null
    const floorOwnHours = floor.hours !== null
    const floorOwnMaxDuration = floor.maxDurationMinutes !== null

    const parentDays = floorOwnDays ? operatingDaysFromApi(floor.days!) : operatingDaysFromApi(building.days)
    const parentHours = floorOwnHours ? operatingWindowFromApi(floor.hours!) : operatingWindowFromApi(building.hours)
    const parentMaxDurationMinutes = floorOwnMaxDuration ? floor.maxDurationMinutes! : building.maxDurationMinutes

    const resolvedDays = operatingDaysFromApi(resolved.days.value)
    const resolvedHours = operatingWindowFromApi(resolved.hours.value)

    const ownOverrides = ownOverridesResult.items
    const ancestorOverrides: AncestorClosure[] = [
      ...floorOverridesResult.items.map((o) => ({ override: o, levelLabel: 'Floor' })),
      ...buildingOverridesResult.items.map((o) => ({ override: o, levelLabel: 'Building' })),
    ]

    return {
      level: 'space',
      concurrencyStamp: space.concurrencyStamp,
      effectiveItems: [
        { label: 'Timezone', value: building.timezone, source: 'Building' },
        { label: 'Operating days', value: describeDays(resolvedDays), source: resolved.days.source as EffectiveSource },
        { label: 'Operating hours', value: describeHours(resolvedHours), source: resolved.hours.source as EffectiveSource },
        {
          label: 'Maximum duration',
          value: `${minutesToHours(resolved.maxDurationMinutes.value)}h`,
          source: resolved.maxDurationMinutes.source as EffectiveSource,
        },
        { label: 'Booking horizon', value: `${resolved.maxHorizonDays} days`, source: 'Building' },
        { label: 'Minimum lead time', value: `${resolved.minLeadMinutes} min`, source: 'Building' },
        {
          label: 'Minimum attendees',
          value: resolved.minAttendees === null ? 'No minimum' : String(resolved.minAttendees),
          source: resolved.minAttendees === null ? 'None' : 'Space',
        },
        {
          label: 'Capacity (max)',
          value: resolved.capacity === null ? '—' : `${resolved.capacity} seat${resolved.capacity === 1 ? '' : 's'}`,
          source: 'Space',
        },
      ],
      ownOverrides,
      ancestorOverrides,
      scope: OverrideScope.Space,
      isCurrentlyClosedNow: isCurrentlyClosed(
        resolvedDays,
        resolvedHours,
        toOverrideWindows([...ownOverrides, ...ancestorOverrides.map((a) => a.override)]),
        new Date(),
      ),
      breadcrumb: `Space level · ${floor.name} · ${building.name} — only this level's own rules are set here. Anything left on Inherit follows the level above.`,
      space: {
        name: space.name,
        capacity: space.capacity,
        draft: {
          days: space.days ? operatingDaysFromApi(space.days) : null,
          hours: space.hours ? operatingWindowFromApi(space.hours) : null,
          maxDurationMinutes: space.maxDurationMinutes,
          minAttendees: space.minAttendees,
        },
        parentDays,
        parentDaysSource: floorOwnDays ? 'Floor' : 'Building',
        parentHours,
        parentHoursSource: floorOwnHours ? 'Floor' : 'Building',
        parentMaxDurationMinutes,
        parentMaxDurationSource: floorOwnMaxDuration ? 'Floor' : 'Building',
      },
    }
  })

  const [buildingDraft, setBuildingDraft] = useState<BuildingDraft | null>(null)
  const [floorDraft, setFloorDraft] = useState<FloorDraft | null>(null)
  const [spaceDraft, setSpaceDraft] = useState<SpaceDraft | null>(null)
  const [saving, setSaving] = useState(false)

  // Drafts follow the server data: seeded on first load, re-seeded only when `data` itself
  // changes (another level opened, or someone else saved and the 409 path reloaded). The
  // cache hands back the same object for an unchanged refetch, so a background refresh or
  // a silent token renew never touches what is being edited. Done during render rather
  // than in an effect so the first paint already has the drafts.
  const [seededFrom, setSeededFrom] = useState<PageData | null>(null)
  if (data && data !== seededFrom) {
    setSeededFrom(data)
    setBuildingDraft(data.building?.draft ?? null)
    setFloorDraft(data.floor?.draft ?? null)
    setSpaceDraft(data.space?.draft ?? null)
    setWarnings([])
  }

  const isDirty =
    (!!data?.building && !!buildingDraft && !buildingDraftEquals(buildingDraft, data.building.draft)) ||
    (!!data?.floor && !!floorDraft && !floorDraftEquals(floorDraft, data.floor.draft)) ||
    (!!data?.space && !!spaceDraft && !spaceDraftEquals(spaceDraft, data.space.draft))

  useUnsavedChangesWarning(isDirty)
  // In-app navigation (the sidebar, Back, the explorer) while dirty: ask first. The data
  // router holds the navigation until proceed() or reset() is called.
  const blocker = useBlocker(({ currentLocation, nextLocation }) => isDirty && currentLocation.pathname !== nextLocation.pathname)

  function handleSaveError(err: unknown) {
    if (err instanceof ApiError && err.status === 409) {
      showToast("Someone else changed this since you loaded it — reloading the latest version.", 'error')
      refetch()
      return
    }
    showToast(err instanceof ApiError ? err.message : 'Something went wrong — please try again.', 'error')
  }

  // The save for whichever level this page edits, and the matching "which bookings would
  // this break?" check — same payload, so the check is exactly what the save would do.
  function saveCalls() {
    if (!data) return null
    if (data.level === 'building' && buildingDraft) {
      const input = {
        days: operatingDaysToApi(buildingDraft.days),
        hours: operatingWindowToApi(buildingDraft.hours),
        maxDurationMinutes: buildingDraft.maxDurationMinutes,
        maxHorizonDays: buildingDraft.maxHorizonDays,
        minLeadMinutes: buildingDraft.minLeadMinutes,
        maxSeriesHorizonDays: buildingDraft.maxSeriesHorizonDays,
        ownOverlapPolicy: buildingDraft.ownOverlapPolicy,
        concurrencyStamp: data.concurrencyStamp,
      }
      return {
        impact: () => getBuildingConstraintsImpact(token, id, input),
        save: (cancelAffectedBookings: boolean) => updateBuildingConstraints(token, id, { ...input, cancelAffectedBookings }),
      }
    }
    if (data.level === 'floor' && floorDraft) {
      const input = {
        days: floorDraft.days ? operatingDaysToApi(floorDraft.days) : null,
        hours: floorDraft.hours ? operatingWindowToApi(floorDraft.hours) : null,
        maxDurationMinutes: floorDraft.maxDurationMinutes,
        concurrencyStamp: data.concurrencyStamp,
      }
      return {
        impact: () => getFloorConstraintsImpact(token, id, input),
        save: (cancelAffectedBookings: boolean) => updateFloorConstraints(token, id, { ...input, cancelAffectedBookings }),
      }
    }
    if (data.level === 'space' && spaceDraft) {
      const input = {
        days: spaceDraft.days ? operatingDaysToApi(spaceDraft.days) : null,
        hours: spaceDraft.hours ? operatingWindowToApi(spaceDraft.hours) : null,
        maxDurationMinutes: spaceDraft.maxDurationMinutes,
        minAttendees: spaceDraft.minAttendees,
        concurrencyStamp: data.concurrencyStamp,
      }
      return {
        impact: () => getSpaceConstraintsImpact(token, id, input),
        save: (cancelAffectedBookings: boolean) => updateSpaceConstraints(token, id, { ...input, cancelAffectedBookings }),
      }
    }
    return null
  }

  async function handleSave() {
    const calls = saveCalls()
    if (!calls) return
    setSaving(true)
    try {
      // Bookings the new rules would break: the admin decides before anything is saved.
      const impact = await calls.impact()
      let cancel = false
      if (impact.count > 0) {
        const choice = await askImpact({ mode: 'change', impact })
        if (!choice) return
        cancel = choice === 'cancel'
      }

      const result = await calls.save(cancel)
      setWarnings(result.warnings)
      showToast(
        result.cancelledBookings
          ? `Constraints saved · ${result.cancelledBookings} ${result.cancelledBookings === 1 ? 'booking' : 'bookings'} cancelled.`
          : 'Constraints saved.',
      )
      refetch()
    } catch (err) {
      handleSaveError(err)
    } finally {
      setSaving(false)
    }
  }

  function handleResetToParent() {
    if (floorDraft) setFloorDraft({ days: null, hours: null, maxDurationMinutes: null })
    if (spaceDraft) setSpaceDraft({ days: null, hours: null, maxDurationMinutes: null, minAttendees: null })
  }

  async function handleCreateOverride(input: CreateAvailabilityOverrideDto) {
    try {
      const impact = await getOverrideImpact(token, input)
      let cancel = false
      if (impact.count > 0) {
        const choice = await askImpact({ mode: 'closure', impact })
        if (!choice) return
        cancel = choice === 'cancel'
      }

      await createOverride(token, { ...input, cancelAffectedBookings: cancel })
      showToast(cancel ? `Closure added · ${impact.count} ${impact.count === 1 ? 'booking' : 'bookings'} cancelled.` : 'Closure added.')
      refetch()
    } catch (err) {
      handleSaveError(err)
    }
  }

  async function handleDeleteOverride(overrideId: string) {
    const yes = await confirm({
      title: 'Remove this closure?',
      description: 'The space becomes bookable again for that time.',
      confirmLabel: 'Remove closure',
      destructive: true,
    })
    if (!yes) return
    try {
      await deleteOverride(token, overrideId)
      showToast('Closure removed.')
      refetch()
    } catch (err) {
      handleSaveError(err)
    }
  }

  const displayName = data?.building?.name ?? data?.floor?.name ?? data?.space?.name ?? ''

  function handleBack() {
    // Unsaved edits are caught by the blocker above, whichever way the admin leaves.
    navigate('/admin/buildings#hierarchy')
  }

  return (
    <div className="app">
      <Sidebar />
      <div className="main">
        <TopBar />
        <div className="content constraintspage">
          <div>
            <button
              type="button"
              className="backlink"
              style={{ background: 'none', border: 'none', padding: 0, cursor: 'pointer' }}
              onClick={handleBack}
            >
              ← Space management
            </button>
            <h1 className="pagetitle">{displayName || 'Constraints'}</h1>
            <p className="lead">{data?.breadcrumb ?? ''}</p>
          </div>

          {!validLevel && <p className="treeempty">Invalid constraints level.</p>}
          {validLevel && status === 'loading' && <FormSkeleton label="Loading constraints…" />}
          {validLevel && status === 'error' && <p className="treeempty">Couldn't load constraints: {error.message}</p>}

          {validLevel && status === 'success' && data && (
            <>
              <EffectiveValueStrip items={data.effectiveItems} />

              {!canEditRules && (
                <p className="inhnote" role="status">
                  View only — your account can't change these rules. Ask an administrator if you think it should.
                </p>
              )}

              {/* display: contents — only here to disable every field inside at once. */}
              <fieldset disabled={!canEditRules} style={{ display: 'contents' }}>

              {data.level === 'building' && data.building && buildingDraft && (
                <BuildingLevelFields draft={buildingDraft} buildingName={data.building.name} onChange={setBuildingDraft} />
              )}

              {data.level === 'floor' && data.floor && floorDraft && (
                <FloorLevelFields
                  draft={floorDraft}
                  floorName={data.floor.name}
                  parentDays={data.floor.parentDays}
                  parentHours={data.floor.parentHours}
                  parentMaxDurationMinutes={data.floor.parentMaxDurationMinutes}
                  onChange={setFloorDraft}
                />
              )}

              {data.level === 'space' && data.space && spaceDraft && (
                <SpaceLevelFields
                  draft={spaceDraft}
                  spaceName={data.space.name}
                  capacity={data.space.capacity}
                  parentDays={data.space.parentDays}
                  parentDaysSource={data.space.parentDaysSource}
                  parentHours={data.space.parentHours}
                  parentHoursSource={data.space.parentHoursSource}
                  parentMaxDurationMinutes={data.space.parentMaxDurationMinutes}
                  parentMaxDurationSource={data.space.parentMaxDurationSource}
                  onChange={setSpaceDraft}
                />
              )}
              </fieldset>

              {canViewClosures && (
                <ClosuresList
                  scope={data.scope}
                  scopeId={id}
                  ownOverrides={data.ownOverrides}
                  ancestorOverrides={data.ancestorOverrides}
                  isCurrentlyClosed={data.isCurrentlyClosedNow}
                  onCreate={handleCreateOverride}
                  onDelete={handleDeleteOverride}
                  canCreate={canAddClosure}
                  canDelete={canDeleteClosure}
                />
              )}

              {warnings.length > 0 && (
                <div className="warn">
                  <div>
                    Tightening a constraint never cancels existing bookings — confirmed bookings are grandfathered.
                    <ul>
                      {warnings.map((w) => (
                        <li key={w}>{w}</li>
                      ))}
                    </ul>
                  </div>
                </div>
              )}

              <div className="saverow">
                <div className="btngroup">
                  {canEditRules && data.level !== 'building' && <ResetToParentButton onReset={handleResetToParent} disabled={saving} />}
                  <button type="button" className="btn sec" onClick={handleBack}>
                    ← Back to Space management
                  </button>
                  {canEditRules && (
                    <button className="btn" onClick={handleSave} disabled={saving}>
                      {saving ? 'Saving…' : 'Save constraints'}
                    </button>
                  )}
                </div>
              </div>
            </>
          )}
        </div>
      </div>
      {impactPrompt}
      {confirmDialog}
      {blocker.state === 'blocked' && (
        <ConfirmDialog
          title="Discard unsaved changes?"
          description="The rules you changed here haven't been saved."
          confirmLabel="Discard changes"
          cancelLabel="Keep editing"
          destructive
          onAnswer={(discard) => (discard ? blocker.proceed() : blocker.reset())}
        />
      )}
    </div>
  )
}
