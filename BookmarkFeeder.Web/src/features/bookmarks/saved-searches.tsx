import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Bookmark, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import {
  useCreateSavedSearch,
  useDeleteSavedSearch,
  useSavedSearches,
} from '@/api/searches'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover'
import { Separator } from '@/components/ui/separator'

interface SavedSearchesProps {
  /** The current filter string to store, e.g. "q=graphql&tags=dotnet". */
  currentQuery: string
}

/** Save the current query + filters, and re-apply a saved one. */
export function SavedSearches({ currentQuery }: SavedSearchesProps) {
  const navigate = useNavigate()
  const searches = useSavedSearches()
  const create = useCreateSavedSearch()
  const remove = useDeleteSavedSearch()
  const [name, setName] = useState('')

  function apply(query: string) {
    // The stored string is the URL's own params, so applying it restores the view.
    navigate(`/bookmarks?${query}`)
  }

  function save() {
    create.mutate(
      { name: name.trim(), query: currentQuery },
      {
        onSuccess: () => {
          setName('')
          toast.success('Search saved')
        },
        onError: (err) =>
          toast.error(err instanceof Error ? err.message : 'Could not save the search'),
      },
    )
  }

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button variant="outline" className="gap-2">
          <Bookmark className="h-4 w-4" />
          Saved
          {searches.data && searches.data.length > 0 && (
            <span className="rounded bg-primary px-1.5 text-xs text-primary-foreground">
              {searches.data.length}
            </span>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-72 space-y-3">
        <div className="max-h-56 space-y-1 overflow-y-auto">
          {searches.data?.length ? (
            searches.data.map((search) => (
              <div key={search.id} className="flex items-center gap-1">
                <button
                  type="button"
                  onClick={() => apply(search.query)}
                  className="min-w-0 flex-1 truncate rounded px-2 py-1.5 text-left text-sm hover:bg-accent"
                  title={search.query}
                >
                  {search.name}
                </button>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-7 w-7 shrink-0"
                  aria-label={`Delete ${search.name}`}
                  onClick={() => remove.mutate(search.id)}
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </Button>
              </div>
            ))
          ) : (
            <p className="px-2 py-1.5 text-sm text-muted-foreground">No saved searches yet</p>
          )}
        </div>

        <Separator />

        <div className="space-y-2">
          <Label htmlFor="saved-search-name">Name</Label>
          <Input
            id="saved-search-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="e.g. GraphQL reading"
          />
          <Button
            className="w-full"
            onClick={save}
            disabled={!name.trim() || !currentQuery || create.isPending}
          >
            Save
          </Button>
          {!currentQuery && (
            <p className="text-xs text-muted-foreground">
              Search or filter something first — there's nothing to save yet.
            </p>
          )}
        </div>
      </PopoverContent>
    </Popover>
  )
}
