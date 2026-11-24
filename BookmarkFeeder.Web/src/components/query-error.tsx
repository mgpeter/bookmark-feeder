import { AlertCircle, RotateCw } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

interface Props {
  /** What failed to load, e.g. "tags". */
  what: string
  error?: unknown
  onRetry: () => void
  className?: string
}

/**
 * A failed load, said plainly. Without it a failed query falls through to the empty state, and
 * "No tags yet" or a count of 0 is a lie the user may act on.
 */
export function QueryError({ what, error, onRetry, className }: Props) {
  return (
    <div
      role="alert"
      className={cn('flex flex-col items-center gap-3 p-6 text-center text-sm', className)}
    >
      <p className="flex items-center gap-2 text-destructive">
        <AlertCircle className="h-4 w-4 shrink-0" />
        Could not load {what}.
      </p>
      {error instanceof Error && error.message && (
        <p className="text-xs text-muted-foreground">{error.message}</p>
      )}
      <Button variant="outline" size="sm" onClick={onRetry}>
        <RotateCw className="mr-1 h-4 w-4" />
        Retry
      </Button>
    </div>
  )
}
