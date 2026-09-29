/**
 * A `matchMedia` for tests: answers width and pointer queries for a pretend viewport, so a
 * page can be rendered "on a phone" or "on a tablet". jsdom has no matchMedia at all, and
 * `useMediaQuery` then reports its fallback — fine for most tests, useless for layout ones.
 *
 *   const restore = stubMatchMedia(800)            // a tablet
 *   ...
 *   restore()
 */
export function stubMatchMedia(widthPx: number, options: { pointer?: 'fine' | 'coarse' } = {}): () => void {
  const target = globalThis as { matchMedia?: typeof matchMedia }
  const original = target.matchMedia

  function evaluate(query: string): boolean {
    // Every "(feature: value)" pair must hold; anything unknown is treated as false.
    const conditions = [...query.matchAll(/\((min-width|max-width|pointer|hover):\s*([^)]+)\)/g)]
    if (conditions.length === 0) return false
    return conditions.every(([, feature, value]) => {
      switch (feature) {
        case 'min-width':
          return widthPx >= Number.parseInt(value!, 10)
        case 'max-width':
          return widthPx <= Number.parseInt(value!, 10)
        case 'pointer':
          return (options.pointer ?? 'fine') === value!.trim()
        case 'hover':
          return (options.pointer ?? 'fine') === 'fine' ? value!.trim() === 'hover' : value!.trim() === 'none'
        default:
          return false
      }
    })
  }

  target.matchMedia = (query: string) =>
    ({
      matches: evaluate(query),
      media: query,
      onchange: null,
      addEventListener: () => {},
      removeEventListener: () => {},
      addListener: () => {},
      removeListener: () => {},
      dispatchEvent: () => false,
    }) as MediaQueryList

  return () => {
    target.matchMedia = original
  }
}
