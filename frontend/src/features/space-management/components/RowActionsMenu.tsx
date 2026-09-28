import type { ReactNode } from 'react'
import { MoreHorizontalIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'

// A single three-dots button replacing what used to be 3-4 always-visible row icons —
// decluttered per row, with the same actions moved into a shadcn dropdown. Radix renders
// the menu in a portal and positions it itself, so it's never clipped by a scrolling list.

export interface RowMenuAction {
  label: string
  icon: ReactNode
  onClick: () => void
  disabled?: boolean
  destructive?: boolean
}

interface RowActionsMenuProps {
  label: string
  actions: RowMenuAction[]
}

export function RowActionsMenu({ label, actions }: RowActionsMenuProps) {
  return (
    // modal={false}: most actions open a dialog. A modal menu would still be handing focus
    // back to its trigger as that dialog opens, and the two fight over focus and pointer
    // lock — a known Radix pitfall when a menu item opens a dialog.
    <DropdownMenu modal={false}>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon-sm" title={`${label} actions`} aria-label={`${label} actions`}>
          <MoreHorizontalIcon />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="min-w-40">
        {actions.map((a) => (
          <DropdownMenuItem
            key={a.label}
            variant={a.destructive ? 'destructive' : 'default'}
            disabled={a.disabled}
            onSelect={a.onClick}
          >
            {a.icon}
            {a.label}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
