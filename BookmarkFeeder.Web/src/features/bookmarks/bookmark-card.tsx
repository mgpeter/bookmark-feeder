import { useState } from 'react'
import {
  Check,
  Circle,
  ExternalLink,
  MoreVertical,
  Pencil,
  Trash2,
} from 'lucide-react'
import { toast } from 'sonner'
import type { Bookmark } from '@/types/models'
import { useDeleteBookmark, useMarkRead } from '@/api/bookmarks'
import { FaviconAvatar } from '@/components/favicon-avatar'
import { HighlightedText } from '@/components/highlighted-text'
import { TagChip } from '@/components/tag-chip'
import { CategoryBadge } from '@/components/category-badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
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
import { cn } from '@/lib/utils'
import { hostname, relativeDate } from '@/lib/format'
import type { ViewMode } from './use-bookmark-query'

interface Props {
  bookmark: Bookmark
  view: ViewMode
  onEdit?: (bookmark: Bookmark) => void
  /** The active search term, so matches can be marked in the title/description. */
  searchTerm?: string
}

export function BookmarkCard({ bookmark, view, onEdit, searchTerm }: Props) {
  const markRead = useMarkRead()
  const del = useDeleteBookmark()
  const [confirmOpen, setConfirmOpen] = useState(false)
  const category = bookmark.categories[0]

  function toggleRead() {
    markRead.mutate({ id: bookmark.id, isRead: !bookmark.isRead })
  }

  function remove() {
    del.mutate(bookmark.id, {
      onSuccess: () => toast.success('Bookmark deleted'),
      onError: (e) => toast.error(e instanceof Error ? e.message : 'Delete failed'),
    })
  }

  const menu = (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" className="h-8 w-8 shrink-0">
          <MoreVertical className="h-4 w-4" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem asChild>
          <a href={bookmark.url} target="_blank" rel="noreferrer">
            <ExternalLink className="mr-2 h-4 w-4" />
            Open
          </a>
        </DropdownMenuItem>
        <DropdownMenuItem onClick={toggleRead}>
          {bookmark.isRead ? (
            <Circle className="mr-2 h-4 w-4" />
          ) : (
            <Check className="mr-2 h-4 w-4" />
          )}
          Mark {bookmark.isRead ? 'unread' : 'read'}
        </DropdownMenuItem>
        {onEdit && (
          <DropdownMenuItem onClick={() => onEdit(bookmark)}>
            <Pencil className="mr-2 h-4 w-4" />
            Edit
          </DropdownMenuItem>
        )}
        <DropdownMenuSeparator />
        <DropdownMenuItem
          className="text-destructive focus:text-destructive"
          onClick={() => setConfirmOpen(true)}
        >
          <Trash2 className="mr-2 h-4 w-4" />
          Delete
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )

  const meta = (
    <div className="flex flex-wrap items-center gap-1.5">
      {bookmark.tags.map((tag) => (
        <TagChip key={tag.id} name={tag.name} color={tag.color} />
      ))}
      {category && <CategoryBadge name={category.name} />}
    </div>
  )

  const confirmDialog = (
    <AlertDialog open={confirmOpen} onOpenChange={setConfirmOpen}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete bookmark?</AlertDialogTitle>
          <AlertDialogDescription>
            “{bookmark.title}” will be removed from your collection.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction onClick={remove}>Delete</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )

  if (view === 'list') {
    return (
      <Card className={cn('flex flex-row items-center gap-3 p-3', bookmark.isRead && 'opacity-70')}>
        <FaviconAvatar url={bookmark.url} faviconUrl={bookmark.faviconUrl} />
        <div className="min-w-0 flex-1">
          <a
            href={bookmark.url}
            target="_blank"
            rel="noreferrer"
            className="truncate font-medium hover:underline"
          >
            <HighlightedText text={bookmark.title} term={searchTerm} />
          </a>
          <div className="truncate text-xs text-muted-foreground">{hostname(bookmark.url)}</div>
        </div>
        <div className="hidden md:block">{meta}</div>
        <span className="shrink-0 text-xs text-muted-foreground">
          {relativeDate(bookmark.dateAdded)}
        </span>
        {menu}
        {confirmDialog}
      </Card>
    )
  }

  return (
    <Card className={cn('flex flex-col gap-3 p-4', bookmark.isRead && 'opacity-70')}>
      <div className="flex items-start gap-3">
        <FaviconAvatar url={bookmark.url} faviconUrl={bookmark.faviconUrl} />
        <div className="min-w-0 flex-1">
          <a
            href={bookmark.url}
            target="_blank"
            rel="noreferrer"
            className="line-clamp-2 font-medium leading-tight hover:underline"
          >
            <HighlightedText text={bookmark.title} term={searchTerm} />
          </a>
          <div className="mt-0.5 truncate text-xs text-muted-foreground">
            {hostname(bookmark.url)}
          </div>
        </div>
        {menu}
      </div>
      {bookmark.description && (
        <p className="line-clamp-2 text-sm text-muted-foreground">
          <HighlightedText text={bookmark.description} term={searchTerm} />
        </p>
      )}
      <div className="mt-auto flex items-end justify-between gap-2">
        {meta}
        <span className="shrink-0 text-xs text-muted-foreground">
          {relativeDate(bookmark.dateAdded)}
        </span>
      </div>
      {confirmDialog}
    </Card>
  )
}
