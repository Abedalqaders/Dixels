import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { Sidebar } from '../../../components/Sidebar'
import { useAsync } from '../../../hooks/useAsync'
import { useUnsavedChangesWarning } from '../hooks/useUnsavedChangesWarning'
import { Toast, useToast } from '../../../components/Toast'
import { EffectiveValueStrip } from '../components/EffectiveValueStrip'
import type { EffectiveItem, EffectiveSource } from '../components/EffectiveValueStrip'
import { BuildingLevelFields } from '../components/BuildingLevelFields'
import type { BuildingDraft } from '../components/BuildingLevelFields'
import { FloorLevelFields } from '../components/FloorLevelFields'
import type { FloorDraft } from '../components/FloorLevelFields'
import { SpaceLevelFields } from '../components/SpaceLevelFields'
import type { SpaceDraft } from '../components/SpaceLevelFields'
import { ClosuresList } from '../components/ClosuresList'
import { ResetToParentButton } from '../components/ResetToParentButton'
import { buildingDraftEquals, floorDraftEquals, spaceDraftEquals } from '../components/draftEquality'
import { minutesToHours } from '../components/DurationPicker'
import { OperatingDays } from '../domain/operatingDays'
import { OperatingWindow } from '../domain/operatingWindow'
import { isCurrentlyClosed } from '../domain/constraintResolver'
import type { OverrideWindow } from '../domain/constraintResolver'
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
  updateFloorConstraints,
  updateSpaceConstraints,
  OverrideScope,
  OverrideEffect,
} from '../api/spaceManagementApi'
import type {
  AvailabilityOverrideDto,
  CreateAvailabilityOverrideDto,
  OperatingWindowDto,
} from '../api/spaceManagementApi'
import '../../../styles/tokens.css'
import '../../../styles/base.css'
import '../../../styles/admin.css'
import '../../../styles/login.css'

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
  const { toast, showToast } = useToast()
  const [warnings, setWarnings] = useState<string[]>([])

  const level = params.level as Level
  const id = params.id ?? ''
  const validLevel = level === 'building' || level === 'floor' || level === 'space'

  const { status, data, error, refetch } = useAsync(async (): Promise<PageData> => {
    if (level === 'building') {
      const building = await getBuilding(token, id)
      const days = operatingDaysFromApi(building.days)
      const hours = operatingWindowFromApi(building.hours)
      const [ownOverridesResult] = await Promise.all([getOverrides(token, OverrideScope.Building, id)])
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
          { label: 'Minimum lead time', value: `${building.minLeadMinutes} min`, source: 'Building' },
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
            minLeadMinutes: building.minLeadMinutes,
          },
        },
      }
    }

    if (level === 'floor') {
      const floor = await getFloor(token, id)
      const [building, resolved, ownOverridesResult, buildingOverridesResult] = await Promise.all([
        getBuilding(token, floor.buildingId),
        getFloorResolvedConstraints(token, id),
        getOverrides(token, OverrideScope.Floor, id),
        getOverrides(token, OverrideScope.Building, floor.buildingId),
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
      getOverrides(token, OverrideScope.Space, id),
      getOverrides(token, OverrideScope.Floor, space.floorId),
      getOverrides(token, OverrideScope.Building, floor.buildingId),
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
  }, [level, id, token])

  const [buildingDraft, setBuildingDraft] = useState<BuildingDraft | null>(null)
  const [floorDraft, setFloorDraft] = useState<FloorDraft | null>(null)
  const [spaceDraft, setSpaceDraft] = useState<SpaceDraft | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (!data) return
    setBuildingDraft(data.building?.draft ?? null)
    setFloorDraft(data.floor?.draft ?? null)
    setSpaceDraft(data.space?.draft ?? null)
    setWarnings([])
  }, [data])

  const isDirty =
    (!!data?.building && !!buildingDraft && !buildingDraftEquals(buildingDraft, data.building.draft)) ||
    (!!data?.floor && !!floorDraft && !floorDraftEquals(floorDraft, data.floor.draft)) ||
    (!!data?.space && !!spaceDraft && !spaceDraftEquals(spaceDraft, data.space.draft))

  useUnsavedChangesWarning(isDirty)

  function handleSaveError(err: unknown) {
    if (err instanceof ApiError && err.status === 409) {
      showToast("Someone else changed this since you loaded it — reloading the latest version.", 'error')
      refetch()
      return
    }
    showToast(err instanceof ApiError ? err.message : 'Something went wrong — please try again.', 'error')
  }

  async function handleSave() {
    if (!data) return
    setSaving(true)
    try {
      if (data.level === 'building' && buildingDraft) {
        const result = await updateBuildingConstraints(token, id, {
          days: operatingDaysToApi(buildingDraft.days),
          hours: operatingWindowToApi(buildingDraft.hours),
          maxDurationMinutes: buildingDraft.maxDurationMinutes,
          maxHorizonDays: buildingDraft.maxHorizonDays,
          minLeadMinutes: buildingDraft.minLeadMinutes,
          concurrencyStamp: data.concurrencyStamp,
        })
        setWarnings(result.warnings)
      } else if (data.level === 'floor' && floorDraft) {
        const result = await updateFloorConstraints(token, id, {
          days: floorDraft.days ? operatingDaysToApi(floorDraft.days) : null,
          hours: floorDraft.hours ? operatingWindowToApi(floorDraft.hours) : null,
          maxDurationMinutes: floorDraft.maxDurationMinutes,
          concurrencyStamp: data.concurrencyStamp,
        })
        setWarnings(result.warnings)
      } else if (data.level === 'space' && spaceDraft) {
        const result = await updateSpaceConstraints(token, id, {
          days: spaceDraft.days ? operatingDaysToApi(spaceDraft.days) : null,
          hours: spaceDraft.hours ? operatingWindowToApi(spaceDraft.hours) : null,
          maxDurationMinutes: spaceDraft.maxDurationMinutes,
          minAttendees: spaceDraft.minAttendees,
          concurrencyStamp: data.concurrencyStamp,
        })
        setWarnings(result.warnings)
      }
      showToast('Constraints saved.')
      refetch()
    } catch (err) {
      handleSaveError(err)
    } finally {
      setSaving(false)
    }
  }

  function handleDiscard() {
    if (!data) return
    setBuildingDraft(data.building?.draft ?? null)
    setFloorDraft(data.floor?.draft ?? null)
    setSpaceDraft(data.space?.draft ?? null)
    setWarnings([])
    showToast('Changes discarded')
  }

  function handleResetToParent() {
    if (floorDraft) setFloorDraft({ days: null, hours: null, maxDurationMinutes: null })
    if (spaceDraft) setSpaceDraft({ days: null, hours: null, maxDurationMinutes: null, minAttendees: null })
  }

  async function handleCreateOverride(input: CreateAvailabilityOverrideDto) {
    try {
      await createOverride(token, input)
      showToast('Closure added.')
      refetch()
    } catch (err) {
      handleSaveError(err)
    }
  }

  async function handleDeleteOverride(overrideId: string) {
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
    if (isDirty && !window.confirm('Discard unsaved changes and go back to Space management?')) {
      return
    }
    navigate('/admin/buildings#hierarchy')
  }

  return (
    <div className="app">
      <Sidebar />
      <div className="main">
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
          {validLevel && status === 'loading' && <p className="treeempty">Loading constraints…</p>}
          {validLevel && status === 'error' && <p className="treeempty">Couldn't load constraints: {error.message}</p>}

          {validLevel && status === 'success' && data && (
            <>
              <EffectiveValueStrip items={data.effectiveItems} />

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

              <ClosuresList
                scope={data.scope}
                scopeId={id}
                ownOverrides={data.ownOverrides}
                ancestorOverrides={data.ancestorOverrides}
                isCurrentlyClosed={data.isCurrentlyClosedNow}
                onCreate={handleCreateOverride}
                onDelete={handleDeleteOverride}
              />

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
                <button type="button" className="btn sec" onClick={handleBack}>
                  ← Back to Space management
                </button>
                <div className="btngroup">
                  {data.level !== 'building' && <ResetToParentButton onReset={handleResetToParent} disabled={saving} />}
                  <button className="btn sec" onClick={handleDiscard} disabled={saving}>
                    Discard
                  </button>
                  <button className="btn" onClick={handleSave} disabled={saving}>
                    {saving ? 'Saving…' : 'Save constraints'}
                  </button>
                </div>
              </div>
            </>
          )}
        </div>
      </div>
      <Toast toast={toast} />
    </div>
  )
}
