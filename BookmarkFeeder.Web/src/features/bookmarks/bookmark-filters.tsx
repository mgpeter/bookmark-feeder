import { useEffect, useState } from 'react'
import { ArrowDownAZ, ArrowUpAZ, LayoutGrid, List, Search, Tag, X } from 'lucide-react'
import { useTags } from '@/api/tags'
import { useCategories, flattenCategories } from '@/api/categories'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { useBookmarkQuery } from './use-bookmark-query'

const ALL = '__all__'

export function BookmarkFilters() {
  const { query, patch, view, readFilter } = useBookmarkQuery()
  const tags = useTags()
  const categories = useCategories()

  const [search, setSearch] = useState(query.search ?? '')

  // Debounce the search box → URL param.
  useEffect(() => {
    const handle = setTimeout(() => {
      if ((query.search ?? '') !== search) patch({ q: search || null })
    }, 300)
    return () => clearTimeout(handle)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search])

  // Keep the box in sync if the URL changes elsewhere (e.g. Clear).
  useEffect(() => {
    setSearch(query.search ?? '')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query.search])

  const selectedTags = query.tags ? query.tags.split(',') : []

  function toggleTag(name: string) {
    const set = new Set(selectedTags)
    if (set.has(name)) set.delete(name)
    else set.add(name)
    patch({ tags: set.size ? [...set].join(',') : null })
  }

  const flatCategories = categories.data ? flattenCategories(categories.data) : []
  const hasFilters =
    Boolean(query.search) ||
    selectedTags.length > 0 ||
    Boolean(query.categories) ||
    readFilter !== 'all'

  return (
    <div className="flex flex-wrap items-center gap-2">
      <div className="relative min-w-56 flex-1">
        <Search className="pointer-events-none absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
        <Input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search title, URL, description…"
          className="pl-8"
        />
      </div>

      {/* Tags multi-select */}
      <Popover>
        <PopoverTrigger asChild>
          <Button variant="outline" className="gap-2">
            <Tag className="h-4 w-4" />
            Tags
            {selectedTags.length > 0 && (
              <span className="rounded bg-primary px-1.5 text-xs text-primary-foreground">
                {selectedTags.length}
              </span>
            )}
          </Button>
        </PopoverTrigger>
        <PopoverContent align="start" className="w-56 p-2">
          <div className="max-h-64 space-y-1 overflow-y-auto">
            {tags.data?.length ? (
              tags.data.map((tag) => (
                <label
                  key={tag.id}
                  className="flex cursor-pointer items-center gap-2 rounded px-2 py-1.5 text-sm hover:bg-accent"
                >
                  <Checkbox
                    checked={selectedTags.includes(tag.name)}
                    onCheckedChange={() => toggleTag(tag.name)}
                  />
                  <span className="flex-1">{tag.name}</span>
                  <span className="text-xs text-muted-foreground">{tag.bookmarkCount}</span>
                </label>
              ))
            ) : (
              <p className="px-2 py-1.5 text-sm text-muted-foreground">No tags yet</p>
            )}
          </div>
        </PopoverContent>
      </Popover>

      {/* Category filter */}
      <Select
        value={query.categories ? query.categories : ALL}
        onValueChange={(v) => patch({ categories: v === ALL ? null : v })}
      >
        <SelectTrigger className="w-44">
          <SelectValue placeholder="Category" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={ALL}>All categories</SelectItem>
          {flatCategories.map(({ category, depth }) => (
            <SelectItem key={category.id} value={category.id}>
              {' '.repeat(depth * 2)}
              {category.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      {/* Read state */}
      <Select
        value={readFilter}
        onValueChange={(v) => patch({ read: v === 'all' ? null : v })}
      >
        <SelectTrigger className="w-32">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">All</SelectItem>
          <SelectItem value="unread">Unread</SelectItem>
          <SelectItem value="read">Read</SelectItem>
        </SelectContent>
      </Select>

      {/* Sort */}
      <Select value={query.sortBy} onValueChange={(v) => patch({ sort: v })}>
        <SelectTrigger className="w-40">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="dateAdded">Date added</SelectItem>
          <SelectItem value="dateModified">Date modified</SelectItem>
          <SelectItem value="title">Title</SelectItem>
          <SelectItem value="url">URL</SelectItem>
        </SelectContent>
      </Select>
      <Button
        variant="outline"
        size="icon"
        title={query.sortOrder === 'asc' ? 'Ascending' : 'Descending'}
        onClick={() => patch({ dir: query.sortOrder === 'asc' ? 'desc' : 'asc' })}
      >
        {query.sortOrder === 'asc' ? (
          <ArrowUpAZ className="h-4 w-4" />
        ) : (
          <ArrowDownAZ className="h-4 w-4" />
        )}
      </Button>

      {/* View toggle */}
      <ToggleGroup
        type="single"
        value={view}
        onValueChange={(v) => v && patch({ view: v, page: String(query.page ?? 1) })}
        variant="outline"
      >
        <ToggleGroupItem value="grid" aria-label="Grid view">
          <LayoutGrid className="h-4 w-4" />
        </ToggleGroupItem>
        <ToggleGroupItem value="list" aria-label="List view">
          <List className="h-4 w-4" />
        </ToggleGroupItem>
      </ToggleGroup>

      {hasFilters && (
        <Button
          variant="ghost"
          size="sm"
          onClick={() =>
            patch({ q: null, tags: null, categories: null, source: null, read: null })
          }
        >
          <X className="mr-1 h-4 w-4" />
          Clear
        </Button>
      )}
    </div>
  )
}
