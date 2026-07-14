import { useEffect, useState } from 'react'
import { toast } from 'sonner'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import { ScrollArea } from '@/components/ui/scroll-area'
import { listFolders } from '@/lib/bookmarks'
import type { BookmarkFolder, Settings } from '@/lib/types'

interface FolderPickerProps {
  selected: BookmarkFolder[]
  onChange: (patch: Partial<Settings>) => Promise<void>
}

/** Lets the user pick which bookmark folders get synced. */
export function FolderPicker({ selected, onChange }: FolderPickerProps) {
  const [folders, setFolders] = useState<BookmarkFolder[] | null>(null)

  useEffect(() => {
    void listFolders().then(setFolders)
  }, [])

  async function toggle(folder: BookmarkFolder, checked: boolean) {
    const ids = new Set(selected.map((f) => f.id))
    if (checked) ids.add(folder.id)
    else ids.delete(folder.id)

    // Rebuild from the folder list so the stored order always follows the tree.
    const next = folders!.filter((f) => ids.has(f.id))

    try {
      await onChange({ selectedFolders: next })
    } catch {
      // chrome.storage.sync has a per-item quota; a huge selection can be rejected.
      toast.error('Could not save the folder selection.')
    }
  }

  if (!folders) {
    return <p className="py-6 text-center text-sm text-muted-foreground">Loading folders…</p>
  }

  if (folders.length === 0) {
    return <p className="py-6 text-center text-sm text-muted-foreground">No bookmark folders found.</p>
  }

  return (
    <div className="space-y-2">
      <p className="text-xs text-muted-foreground">
        Bookmarks in the folders you pick — and their subfolders — are synced.
      </p>
      <ScrollArea className="h-56 rounded-md border">
        <div className="space-y-1 p-2">
          {folders.map((folder) => {
            const id = `folder-${folder.id}`
            return (
              <div
                key={folder.id}
                className="flex items-center gap-2 rounded-sm px-1 py-1 hover:bg-accent"
                style={{ paddingLeft: `${(folder.depth - 1) * 12 + 4}px` }}
              >
                <Checkbox
                  id={id}
                  checked={selected.some((f) => f.id === folder.id)}
                  onCheckedChange={(checked) => void toggle(folder, checked === true)}
                />
                <Label htmlFor={id} className="truncate text-sm font-normal">
                  {folder.title}
                </Label>
              </div>
            )
          })}
        </div>
      </ScrollArea>
    </div>
  )
}
