import { cn } from "@/lib/utils"

// shadcn's Skeleton, on bg-muted rather than bg-accent: our accent is the brand tint, which
// reads as "selected" instead of "not here yet".
function Skeleton({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="skeleton"
      className={cn("animate-pulse rounded-md bg-muted", className)}
      {...props}
    />
  )
}

export { Skeleton }
