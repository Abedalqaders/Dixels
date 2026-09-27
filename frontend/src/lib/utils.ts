import { clsx } from 'clsx'
import type { ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

/** Joins class names and lets a later Tailwind class override an earlier conflicting one — the shadcn/ui helper. */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}
