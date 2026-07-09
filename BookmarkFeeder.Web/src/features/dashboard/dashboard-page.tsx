import { Bookmark, BookOpen, FolderTree, Tags } from 'lucide-react'
import { useBookmarks } from '@/api/bookmarks'
import { useTags } from '@/api/tags'
import { useCategories } from '@/api/categories'
import { flattenCategories } from '@/api/categories'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'

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

export function DashboardPage() {
  const all = useBookmarks({ pageSize: 1 })
  const unread = useBookmarks({ pageSize: 1, isRead: false })
  const tags = useTags()
  const categories = useCategories()

  const categoryCount = categories.data
    ? flattenCategories(categories.data).length
    : undefined

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Dashboard</h1>
        <p className="text-muted-foreground">Your bookmark collection at a glance.</p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard
          label="Bookmarks"
          value={all.data?.pagination.totalItems}
          loading={all.isLoading}
          icon={Bookmark}
        />
        <StatCard
          label="Unread"
          value={unread.data?.pagination.totalItems}
          loading={unread.isLoading}
          icon={BookOpen}
        />
        <StatCard label="Tags" value={tags.data?.length} loading={tags.isLoading} icon={Tags} />
        <StatCard
          label="Categories"
          value={categoryCount}
          loading={categories.isLoading}
          icon={FolderTree}
        />
      </div>
    </div>
  )
}
