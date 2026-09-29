// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { Dialog, DialogContent, DialogDescription, DialogTitle } from './dialog'
import { AlertDialog, AlertDialogContent, AlertDialogDescription, AlertDialogTitle } from './alert-dialog'

// A tall dialog (the booking form with a repeat rule and its per-date preview) must scroll
// inside itself on a phone instead of running off the bottom of the screen.
describe('dialogs on small screens', () => {
  it('a dialog is capped at the viewport height and scrolls inside', () => {
    render(
      <Dialog open>
        <DialogContent>
          <DialogTitle>Book a room</DialogTitle>
          <DialogDescription>Details</DialogDescription>
        </DialogContent>
      </Dialog>,
    )
    expect(screen.getByRole('dialog')).toHaveClass('max-h-[calc(100dvh-2rem)]', 'overflow-y-auto')
  })

  it('so is an alert dialog', () => {
    render(
      <AlertDialog open>
        <AlertDialogContent>
          <AlertDialogTitle>Delete?</AlertDialogTitle>
          <AlertDialogDescription>It can be restored later.</AlertDialogDescription>
        </AlertDialogContent>
      </AlertDialog>,
    )
    expect(screen.getByRole('alertdialog')).toHaveClass('max-h-[calc(100dvh-2rem)]', 'overflow-y-auto')
  })
})
