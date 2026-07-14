import { cn } from '@/lib/utils'
import type { BookmarkFacets, FacetItem } from '@/types/models'

interface FacetPanelProps {
  facets: BookmarkFacets
  selectedTags: string[]
  selectedCategory: string | undefined
  onToggleTag: (name: string) => void
  onSelectCategory: (id: string) => void
}

/** How the current results break down, and a way to narrow to one of the buckets. */
export function FacetPanel({
  facets,
  selectedTags,
  selectedCategory,
  onToggleTag,
  onSelectCategory,
}: FacetPanelProps) {
  const hasFacets = facets.tags.length > 0 || facets.categories.length > 0
  if (!hasFacets) return null

  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-2 rounded-md border bg-muted/30 px-3 py-2">
      {facets.tags.length > 0 && (
        <FacetGroup
          label="Tags"
          items={facets.tags}
          isActive={(item) => selectedTags.includes(item.name)}
          onSelect={(item) => onToggleTag(item.name)}
        />
      )}
      {facets.categories.length > 0 && (
        <FacetGroup
          label="Categories"
          items={facets.categories}
          isActive={(item) => selectedCategory === item.id}
          onSelect={(item) => onSelectCategory(item.id)}
        />
      )}
    </div>
  )
}

interface FacetGroupProps {
  label: string
  items: FacetItem[]
  isActive: (item: FacetItem) => boolean
  onSelect: (item: FacetItem) => void
}

function FacetGroup({ label, items, isActive, onSelect }: FacetGroupProps) {
  return (
    <div className="flex flex-wrap items-center gap-1.5">
      <span className="text-xs font-medium text-muted-foreground">{label}</span>
      {items.map((item) => {
        const active = isActive(item)
        return (
          <button
            key={item.id}
            type="button"
            aria-pressed={active}
            onClick={() => onSelect(item)}
            className={cn(
              'inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs transition-colors',
              active
                ? 'border-primary bg-primary text-primary-foreground'
                : 'hover:bg-accent',
            )}
          >
            {item.name}
            <span className={cn('tabular-nums', !active && 'text-muted-foreground')}>
              {item.count}
            </span>
          </button>
        )
      })}
    </div>
  )
}
