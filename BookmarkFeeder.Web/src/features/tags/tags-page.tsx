import { useEffect, useState } from 'react'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import type { Tag } from '@/types/models'
import { useCreateTag, useDeleteTag, useTags, useUpdateTag } from '@/api/tags'
import { ApiError } from '@/lib/api-client'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
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

type DialogState = { mode: 'new' } | { mode: 'edit'; tag: Tag } | null

export function TagsPage() {
  const tags = useTags()
  const [dialog, setDialog] = useState<DialogState>(null)
  const [confirm, setConfirm] = useState<Tag | null>(null)
  const del = useDeleteTag()

  function remove(tag: Tag) {
    del.mutate(tag.id, {
      onSuccess: () => toast.success(`Deleted “${tag.name}”`),
      onError: (e) => toast.error(e instanceof Error ? e.message : 'Delete failed'),
    })
    setConfirm(null)
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Tags</h1>
          <p className="text-muted-foreground">{tags.data?.length ?? 0} tags</p>
        </div>
        <Button onClick={() => setDialog({ mode: 'new' })}>
          <Plus className="mr-1 h-4 w-4" />
          New tag
        </Button>
      </div>

      <Card>
        {tags.isLoading ? (
          <div className="space-y-2 p-4">
            {Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-8 w-full" />
            ))}
          </div>
        ) : tags.data && tags.data.length > 0 ? (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead className="w-32 text-right">Bookmarks</TableHead>
                <TableHead className="w-24" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {tags.data.map((tag) => (
                <TableRow key={tag.id}>
                  <TableCell>
                    <span className="inline-flex items-center gap-2">
                      <span
                        className="h-3 w-3 rounded-full border"
                        style={{ backgroundColor: tag.color ?? 'transparent' }}
                      />
                      {tag.name}
                    </span>
                  </TableCell>
                  <TableCell className="text-right text-muted-foreground">
                    {tag.bookmarkCount}
                  </TableCell>
                  <TableCell>
                    <div className="flex justify-end gap-1">
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8"
                        onClick={() => setDialog({ mode: 'edit', tag })}
                      >
                        <Pencil className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8 text-destructive"
                        onClick={() => setConfirm(tag)}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        ) : (
          <div className="p-10 text-center text-muted-foreground">No tags yet.</div>
        )}
      </Card>

      <TagFormDialog state={dialog} onClose={() => setDialog(null)} />

      <AlertDialog open={confirm !== null} onOpenChange={(o) => !o && setConfirm(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete tag?</AlertDialogTitle>
            <AlertDialogDescription>
              “{confirm?.name}” will be permanently deleted and removed from{' '}
              {confirm?.bookmarkCount ?? 0} bookmark(s). This cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={() => confirm && remove(confirm)}>
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}

function TagFormDialog({ state, onClose }: { state: DialogState; onClose: () => void }) {
  const create = useCreateTag()
  const update = useUpdateTag()
  const editing = state?.mode === 'edit' ? state.tag : null

  const [name, setName] = useState('')
  const [color, setColor] = useState<string | null>(null)

  useEffect(() => {
    if (!state) return
    setName(editing?.name ?? '')
    setColor(editing?.color ?? null)
  }, [state, editing])

  function save() {
    const trimmed = name.trim()
    if (!trimmed) return
    const onError = (e: unknown) =>
      toast.error(
        e instanceof ApiError && e.status === 409
          ? 'A tag with this name already exists.'
          : e instanceof Error
            ? e.message
            : 'Save failed',
      )

    if (editing) {
      update.mutate(
        { id: editing.id, body: { name: trimmed, color } },
        { onSuccess: () => (toast.success('Tag updated'), onClose()), onError },
      )
    } else {
      create.mutate(
        { name: trimmed, color },
        { onSuccess: () => (toast.success('Tag created'), onClose()), onError },
      )
    }
  }

  return (
    <Dialog open={state !== null} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-sm">
        <DialogHeader>
          <DialogTitle>{editing ? 'Edit tag' : 'New tag'}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="tag-name">Name</Label>
            <Input
              id="tag-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && save()}
              autoFocus
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="tag-color">Color</Label>
            <div className="flex items-center gap-2">
              <input
                id="tag-color"
                type="color"
                value={color ?? '#888888'}
                onChange={(e) => setColor(e.target.value)}
                className="h-9 w-12 cursor-pointer rounded border bg-transparent"
              />
              {color && (
                <Button type="button" variant="ghost" size="sm" onClick={() => setColor(null)}>
                  Clear
                </Button>
              )}
            </div>
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={save} disabled={!name.trim()}>
            {editing ? 'Save' : 'Create'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
