import { allowedHoursRange, OperatingWindow } from '../domain/operatingWindow'

// Building has no HoursRangeInput of its own in the mock (it's the one level with no
// parent to narrow against), but Floor/Space both need this. Proactive constraint via
// native <input type="time"> min/max where the parent's range is expressible that way
// (non-wrapping, not 24h) — the mock only ever rejects reactively with a toast, this adds
// the grey-out the plan calls for. Wrapping/24h parents fall back to save-time validation
// (ensureHoursNarrowing), since native time inputs can't express a wrapping min/max.

interface HoursRangeInputProps {
  value: OperatingWindow
  parent: OperatingWindow
  disabled: boolean
  onChange: (next: OperatingWindow) => void
}

export function HoursRangeInput({ value, parent, disabled, onChange }: HoursRangeInputProps) {
  const allowed = allowedHoursRange(parent)
  const parentWraps = !allowed.isOpen24Hours && allowed.open > allowed.close

  function setOpen24Hours(checked: boolean) {
    if (checked) {
      onChange(OperatingWindow.FullDay)
    } else {
      onChange(new OperatingWindow(allowed.open, allowed.close === '00:00' ? '23:59' : allowed.close))
    }
  }

  function setOpen(open: string) {
    if (value.isOpen24Hours) return
    onChange(new OperatingWindow(open, value.close))
  }

  function setClose(close: string) {
    if (value.isOpen24Hours) return
    onChange(new OperatingWindow(value.open, close))
  }

  return (
    <>
      <label className="chk">
        <input
          type="checkbox"
          style={{ margin: 0 }}
          checked={value.isOpen24Hours}
          disabled={disabled || !allowed.isOpen24Hours}
          onChange={(e) => setOpen24Hours(e.target.checked)}
        />
        Open 24 hours
      </label>
      {!value.isOpen24Hours && (
        <div className="pair narrow">
          <input
            className="ctrl mono"
            type="time"
            value={value.open}
            disabled={disabled}
            min={!parentWraps ? allowed.open : undefined}
            max={!parentWraps ? allowed.close : undefined}
            onChange={(e) => setOpen(e.target.value)}
          />
          <span className="dash">–</span>
          <input
            className="ctrl mono"
            type="time"
            value={value.close}
            disabled={disabled}
            min={!parentWraps ? allowed.open : undefined}
            max={!parentWraps ? allowed.close : undefined}
            onChange={(e) => setClose(e.target.value)}
          />
        </div>
      )}
    </>
  )
}
