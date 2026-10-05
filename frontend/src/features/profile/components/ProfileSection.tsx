import type { ReactNode } from 'react'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'

/**
 * One card on My profile: a title strip, the boxes, and a strip of buttons along the bottom —
 * the same shape for Personal information and Password.
 */
export function ProfileSection({ title, description, footer, children }: { title: string; description: string; footer: ReactNode; children: ReactNode }) {
  return (
    <Card className="gap-0 overflow-hidden rounded-2xl py-0 shadow-md">
      <CardHeader className="gap-1 border-b px-6 py-5 [.border-b]:pb-5">
        <CardTitle className="text-[17px]">{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent className="p-6">{children}</CardContent>
      <CardFooter className="justify-end gap-2 border-t bg-muted/40 px-6 py-3.5 [.border-t]:pt-3.5">{footer}</CardFooter>
    </Card>
  )
}
