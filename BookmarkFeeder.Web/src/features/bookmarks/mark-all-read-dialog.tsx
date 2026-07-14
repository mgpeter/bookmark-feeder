import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'

interface MarkAllReadDialogProps {
  open: boolean
  /** How many bookmarks match the current filters. */
  count: number
  /** Whether anything is narrowing the set — changes the warning. */
  isFiltered: boolean
  onConfirm: () => void
  onOpenChange: (open: boolean) => void
}

/**
 * Confirmation for a bulk read change. There is no undo, so the stated count and the
 * filtered/unfiltered wording are the only safeguard against sweeping the whole collection.
 */
export function MarkAllReadDialog({
  open,
  count,
  isFiltered,
  onConfirm,
  onOpenChange,
}: MarkAllReadDialogProps) {
  const formatted = count.toLocaleString()

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            {isFiltered
              ? `Mark all ${formatted} matching bookmarks as read?`
              : `Mark all ${formatted} bookmarks as read?`}
          </AlertDialogTitle>
          <AlertDialogDescription>
            {isFiltered
              ? 'This applies to every bookmark matching your current filters, across all pages — not just the ones on screen. It cannot be undone.'
              : 'This affects your entire collection. It cannot be undone.'}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm}>Mark as read</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
