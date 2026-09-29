/**
 * The app's three layout breakpoints, the same ones Tailwind's `sm:` / `md:` / `lg:`
 * prefixes use, so a component's classes and a hook's media query always agree:
 *
 *   < sm   phone            one column, drawer navigation, stacked explorer
 *   sm–md  large phone      two-column forms
 *   md–lg  tablet           explorer beside the list, calendar Week view, still a drawer
 *   ≥ lg   desktop          docked sidebar, calendar side panel
 *
 * CSS can't read these (media queries can't use custom properties), so the stylesheets
 * repeat the numbers — `breakpoints.test.ts` checks they never drift from this file.
 */
export const BREAKPOINTS = { sm: 640, md: 768, lg: 1024 } as const

export type Breakpoint = keyof typeof BREAKPOINTS

/** `(min-width: 768px)` — for useMediaQuery, matching Tailwind's `md:` exactly. */
export function up(breakpoint: Breakpoint): string {
  return `(min-width: ${BREAKPOINTS[breakpoint]}px)`
}

/** `(max-width: 767px)` — the complement of `up`, matching CSS `@media (max-width: …)`. */
export function below(breakpoint: Breakpoint): string {
  return `(max-width: ${BREAKPOINTS[breakpoint] - 1}px)`
}
