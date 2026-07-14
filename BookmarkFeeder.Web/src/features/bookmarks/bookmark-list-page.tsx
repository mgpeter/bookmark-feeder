import { useState } from 'react'
import { CheckCheck, ChevronLeft, ChevronRight, Plus } from 'lucide-react'
import { toast } from 'sonner'
import { useBookmarks, useMarkAllRead } from '@/api/bookmarks'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { hasNarrowingFilters } from '@/lib/bookmark-filters'
import { cn } from '@/lib/utils'
import { BookmarkCard } from './bookmark-card'
import { BookmarkFilters } from './bookmark-filters'
import { BookmarkEditDialog, type EditTarget } from './bookmark-edit-dialog'
import { FacetPanel } from './facet-panel'
import { MarkAllReadDialog } from './mark-all-read-dialog'
import { SavedSearches } from './saved-searches'
import { useBookmarkQuery } from './use-bookmark-query'

export function BookmarkListPage() {
  const { query, params, patch, view } = useBookmarkQuery()
  const { data, isLoading, isError, error, isPlaceholderData } = useBookmarks(query)
  const [dialogTarget, setDialogTarget] = useState<EditTarget>(null)
  const [confirmMarkAll, setConfirmMarkAll] = useState(false)
  const markAllRead = useMarkAllRead()

  const pagination = data?.pagination
  const page = pagination?.page ?? query.page ?? 1
  const totalPages = pagination?.totalPages ?? 1
  const totalItems = pagination?.totalItems ?? 0

  function markAll() {
    setConfirmMarkAll(false)
    markAllRead.mutate(
      { query, isRead: true },
      {
        onSuccess: ({ updated }) =>
          toast.success(
            updated === 0
              ? 'Every matching bookmark was already read'
              : `Marked ${updated.toLocaleString()} bookmark${updated === 1 ? '' : 's'} as read`,
          ),
        onError: (err) =>
          toast.error(err instanceof Error ? err.message : 'Could not mark bookmarks as read'),
      },
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Bookmarks</h1>
          <p className="text-muted-foreground">
            {pagination ? `${pagination.totalItems} total` : 'Loading…'}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <SavedSearches currentQuery={params.toString()} />
          <Button
            variant="outline"
            onClick={() => setConfirmMarkAll(true)}
            disabled={isLoading || totalItems === 0 || markAllRead.isPending}
          >
            <CheckCheck className="mr-1 h-4 w-4" />
            Mark all as read
          </Button>
          <Button onClick={() => setDialogTarget('new')}>
            <Plus className="mr-1 h-4 w-4" />
            Add bookmark
          </Button>
        </div>
      </div>

      <BookmarkFilters />

      {data?.facets && (
        <FacetPanel
          facets={data.facets}
          selectedTags={query.tags ? query.tags.split(',') : []}
          selectedCategory={query.categories ?? undefined}
          onToggleTag={(name) => {
            const selected = new Set(query.tags ? query.tags.split(',') : [])
            if (selected.has(name)) selected.delete(name)
            else selected.add(name)
            patch({ tags: selected.size ? [...selected].join(',') : null })
          }}
          onSelectCategory={(id) =>
            patch({ categories: query.categories === id ? null : id })
          }
        />
      )}

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
              searchTerm={query.search}
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

      <MarkAllReadDialog
        open={confirmMarkAll}
        count={totalItems}
        isFiltered={hasNarrowingFilters(query)}
        onConfirm={markAll}
        onOpenChange={setConfirmMarkAll}
      />
    </div>
  )
}

function gridClass(view: 'grid' | 'list'): string {
  return view === 'grid'
    ? 'grid gap-4 sm:grid-cols-2 lg:grid-cols-3'
    : 'flex flex-col gap-2'
}
