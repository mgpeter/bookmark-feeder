import { useState } from 'react'
import { ChevronLeft, ChevronRight, Plus } from 'lucide-react'
import { useBookmarks } from '@/api/bookmarks'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { BookmarkCard } from './bookmark-card'
import { BookmarkFilters } from './bookmark-filters'
import { BookmarkEditDialog, type EditTarget } from './bookmark-edit-dialog'
import { useBookmarkQuery } from './use-bookmark-query'

export function BookmarkListPage() {
  const { query, patch, view } = useBookmarkQuery()
  const { data, isLoading, isError, error, isPlaceholderData } = useBookmarks(query)
  const [dialogTarget, setDialogTarget] = useState<EditTarget>(null)

  const pagination = data?.pagination
  const page = pagination?.page ?? query.page ?? 1
  const totalPages = pagination?.totalPages ?? 1

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Bookmarks</h1>
          <p className="text-muted-foreground">
            {pagination ? `${pagination.totalItems} total` : 'Loading…'}
          </p>
        </div>
        <Button onClick={() => setDialogTarget('new')}>
          <Plus className="mr-1 h-4 w-4" />
          Add bookmark
        </Button>
      </div>

      <BookmarkFilters />

      {isError && (
        <Card className="p-6 text-sm text-destructive">
          {error instanceof Error ? error.message : 'Failed to load bookmarks.'}
        </Card>
      )}

      {isLoading ? (
        <div className={gridClass(view)}>
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-32 w-full" />
          ))}
        </div>
      ) : data && data.data.length > 0 ? (
        <div className={cn(gridClass(view), isPlaceholderData && 'opacity-60')}>
          {data.data.map((bookmark) => (
            <BookmarkCard
              key={bookmark.id}
              bookmark={bookmark}
              view={view}
              onEdit={setDialogTarget}
            />
          ))}
        </div>
      ) : (
        !isError && (
          <Card className="p-10 text-center text-muted-foreground">
            No bookmarks match your filters.
          </Card>
        )
      )}

      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-4 pt-2">
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1}
            onClick={() => patch({ page: String(page - 1) })}
          >
            <ChevronLeft className="mr-1 h-4 w-4" />
            Previous
          </Button>
          <span className="text-sm text-muted-foreground">
            Page {page} of {totalPages}
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={page >= totalPages}
            onClick={() => patch({ page: String(page + 1) })}
          >
            Next
            <ChevronRight className="ml-1 h-4 w-4" />
          </Button>
        </div>
      )}

      <BookmarkEditDialog target={dialogTarget} onClose={() => setDialogTarget(null)} />
    </div>
  )
}

function gridClass(view: 'grid' | 'list'): string {
  return view === 'grid'
    ? 'grid gap-4 sm:grid-cols-2 lg:grid-cols-3'
    : 'flex flex-col gap-2'
}
