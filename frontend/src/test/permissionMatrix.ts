/**
 * One row of the permission map (see features/auth/permissionMatrix.test.tsx): for one page
 * and its menu item, which grants must open it and which must not. Each feature keeps its
 * rows in features/<feature>/permissionMatrix.ts — test data only, never imported by the app.
 */
export interface PermissionMatrixRow {
  /** A page path, exactly as its module's `pages` declares it. */
  route: string
  /** The sidebar link that leads there, if there is one. */
  link?: string
  /** Grant sets that must open it — each is a real role someone could be given. */
  allows: string[][]
  /** Grant sets that must not: one grant short of what the page's API needs. */
  denies: string[][]
}

/** Every feature's rows, found by file name — a new module's rows join without editing anything. */
export const matrixRows: PermissionMatrixRow[] = Object.values(
  import.meta.glob<{ rows: PermissionMatrixRow[] }>('/src/features/*/permissionMatrix.ts', { eager: true }),
).flatMap((file) => file.rows)
