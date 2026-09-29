import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { ApiError, createSpaceType, updateSpaceType } from '@/features/space-management/api/spaceManagementApi'
import type { IconKey, SpaceTypeDto } from '@/features/space-management/api/spaceManagementApi'
import { ICON_OPTIONS, ICONS, iconKeyToIconName } from './spaceTypeIcons'

interface SpaceTypeFormDialogProps {
  token: string
  /** The type being edited, or null to add a new one. */
  spaceType: SpaceTypeDto | null
  onClose: () => void
  onSaved: (message: string) => void
}

/** Add or edit one space type. Mounted only while open, so its fields always start from
 * the row that opened it. Errors (e.g. a duplicate name) show inline, where the admin is
 * typing, instead of in a toast behind the dialog. */
export function SpaceTypeFormDialog({ token, spaceType, onClose, onSaved }: SpaceTypeFormDialogProps) {
  const [name, setName] = useState(spaceType?.name ?? '')
  const [iconKey, setIconKey] = useState(spaceType?.iconKey ?? 0)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const isEdit = spaceType !== null
  const trimmed = name.trim()
  const unchanged = isEdit && trimmed === spaceType.name && iconKey === spaceType.iconKey

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!trimmed || unchanged) return

    setSaving(true)
    setError(null)
    try {
      if (isEdit) {
        await updateSpaceType(token, spaceType.id, { name: trimmed, iconKey })
        onSaved('Space type updated.')
      } else {
        await createSpaceType(token, { name: trimmed, iconKey })
        onSaved('Space type added.')
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Something went wrong — please try again.')
      setSaving(false)
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && !saving && onClose()}>
      <DialogContent className="sm:max-w-md">
        <form onSubmit={handleSubmit} className="flex flex-col gap-5">
          <DialogHeader>
            <DialogTitle>{isEdit ? `Edit ${spaceType.name}` : 'Add a space type'}</DialogTitle>
            <DialogDescription>
              Shared across every building — changes show everywhere the type is used.
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-2">
            <Label htmlFor="st-name">Name</Label>
            <Input
              id="st-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Phone booth"
              autoComplete="off"
              autoFocus
              disabled={saving}
              aria-invalid={error ? true : undefined}
            />
          </div>

          <div className="flex flex-col gap-2">
            <Label id="st-icon-label">Icon</Label>
            <ToggleGroup
              type="single"
              variant="outline"
              aria-labelledby="st-icon-label"
              value={String(iconKey)}
              // Radix sends '' when the selected item is clicked again — keep the current icon.
              onValueChange={(v) => v && setIconKey(Number(v) as IconKey)}
              disabled={saving}
            >
              {ICON_OPTIONS.map((o) => (
                <ToggleGroupItem key={o.value} value={String(o.value)} aria-label={o.label} title={o.label}>
                  {ICONS[iconKeyToIconName(o.value)]}
                </ToggleGroupItem>
              ))}
            </ToggleGroup>
          </div>

          {error && (
            <p className="text-sm text-destructive" role="alert">
              {error}
            </p>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={saving}>
              Cancel
            </Button>
            <Button type="submit" disabled={saving || !trimmed || unchanged}>
              {saving ? 'Saving…' : isEdit ? 'Save' : 'Add type'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
