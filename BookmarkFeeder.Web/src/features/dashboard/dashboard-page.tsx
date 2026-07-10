import { Link } from 'react-router-dom'
import { Bookmark, BookOpen, FolderTree, Tags } from 'lucide-react'
import { useBookmarks } from '@/api/bookmarks'
import { useTags } from '@/api/tags'
import { useCategories, flattenCategories } from '@/api/categories'
import { FaviconAvatar } from '@/components/favicon-avatar'
import { TagChip } from '@/components/tag-chip'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { hostname, relativeDate } from '@/lib/format'

export function DashboardPage() {
  const all = useBookmarks({ pageSize: 1 })
  const unread = useBookmarks({ pageSize: 1, isRead: false })
  const recent = useBookmarks({ pageSize: 6, sortBy: 'dateAdded', sortOrder: 'desc' })
  const tags = useTags()
  const categories = useCategories()

  const categoryCount = categories.data
    ? flattenCategories(categories.data).length
    : undefined
  const topTags = [...(tags.data ?? [])]
    .sort((a, b) => b.bookmarkCount - a.bookmarkCount)
    .slice(0, 12)

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Dashboard</h1>
        <p className="text-muted-foreground">Your bookmark collection at a glance.</p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard label="Bookmarks" value={all.data?.pagination.totalItems} loading={all.isLoading} icon={Bookmark} />
        <StatCard label="Unread" value={unread.data?.pagination.totalItems} loading={unread.isLoading} icon={BookOpen} />
        <StatCard label="Tags" value={tags.data?.length} loading={tags.isLoading} icon={Tags} />
        <StatCard label="Categories" value={categoryCount} loading={categories.isLoading} icon={FolderTree} />
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">Recent additions</CardTitle>
          </CardHeader>
          <CardContent className="space-y-1">
            {recent.isLoading ? (
              Array.from({ length: 5 }).map((_, i) => <Skeleton key={i} className="h-10 w-full" />)
            ) : recent.data && recent.data.data.length > 0 ? (
              recent.data.data.map((b) => (
                <a
                  key={b.id}
                  href={b.url}
                  target="_blank"
                  rel="noreferrer"
                  className="flex items-center gap-3 rounded-md p-2 hover:bg-accent"
                >
                  <FaviconAvatar url={b.url} faviconUrl={b.faviconUrl} />
                  <div className="min-w-0 flex-1">
                    <div className="truncate text-sm font-medium">{b.title}</div>
                    <div className="truncate text-xs text-muted-foreground">{hostname(b.url)}</div>
                  </div>
                  <span className="shrink-0 text-xs text-muted-foreground">
                    {relativeDate(b.dateAdded)}
                  </span>
                </a>
              ))
            ) : (
              <p className="p-4 text-sm text-muted-foreground">No bookmarks yet.</p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Top tags</CardTitle>
          </CardHeader>
          <CardContent>
            {tags.isLoading ? (
              <Skeleton className="h-24 w-full" />
            ) : topTags.length > 0 ? (
              <div className="flex flex-wrap gap-1.5">
                {topTags.map((t) => (
                  <Link key={t.id} to={`/bookmarks?tags=${encodeURIComponent(t.name)}`}>
                    <TagChip name={`${t.name} (${t.bookmarkCount})`} color={t.color} />
                  </Link>
                ))}
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">No tags yet.</p>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

function StatCard({
  label,
  value,
  loading,
  icon: Icon,
}: {
  label: string
  value: number | undefined
  loading: boolean
  icon: React.ComponentType<{ className?: string }>
}) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">{label}</CardTitle>
        <Icon className="h-4 w-4 text-muted-foreground" />
      </CardHeader>
      <CardContent>
        {loading ? (
          <Skeleton className="h-8 w-16" />
        ) : (
          <div className="text-3xl font-semibold">{value ?? 0}</div>
        )}
      </CardContent>
    </Card>
  )
}
