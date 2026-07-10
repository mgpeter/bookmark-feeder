import { useEffect, useState } from 'react'
import { ChevronRight, FolderPlus, Pencil, Plus, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import type { Category } from '@/types/models'
import {
  flattenCategories,
  useCategories,
  useCreateCategory,
  useDeleteCategory,
  useUpdateCategory,
} from '@/api/categories'
import { ApiError } from '@/lib/api-client'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
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

const NONE = '__none__'
const DETACH = '__detach__'

type DialogState =
  | { mode: 'new'; parentId: string | null }
  | { mode: 'edit'; category: Category }
  | null

export function CategoriesPage() {
  const categories = useCategories()
  const [dialog, setDialog] = useState<DialogState>(null)
  const [confirm, setConfirm] = useState<Category | null>(null)

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Categories</h1>
          <p className="text-muted-foreground">Organize bookmarks into a hierarchy.</p>
        </div>
        <Button onClick={() => setDialog({ mode: 'new', parentId: null })}>
          <Plus className="mr-1 h-4 w-4" />
          New category
        </Button>
      </div>

      <Card className="p-2">
        {categories.isLoading ? (
          <div className="space-y-2 p-2">
            {Array.from({ length: 4 }).map((_, i) => (
              <Skeleton key={i} className="h-9 w-full" />
            ))}
          </div>
        ) : categories.data && categories.data.length > 0 ? (
          <CategoryNodes
            nodes={categories.data}
            depth={0}
            onAddChild={(parentId) => setDialog({ mode: 'new', parentId })}
            onEdit={(category) => setDialog({ mode: 'edit', category })}
            onDelete={setConfirm}
          />
        ) : (
          <div className="p-10 text-center text-muted-foreground">No categories yet.</div>
        )}
      </Card>

      <CategoryFormDialog
        state={dialog}
        allCategories={categories.data ?? []}
        onClose={() => setDialog(null)}
      />

      <DeleteCategoryDialog
        category={confirm}
        allCategories={categories.data ?? []}
        onClose={() => setConfirm(null)}
      />
    </div>
  )
}

function CategoryNodes({
  nodes,
  depth,
  onAddChild,
  onEdit,
  onDelete,
}: {
  nodes: Category[]
  depth: number
  onAddChild: (parentId: string) => void
  onEdit: (category: Category) => void
  onDelete: (category: Category) => void
}) {
  return (
    <ul>
      {nodes.map((node) => (
        <li key={node.id}>
          <div
            className="group flex items-center gap-2 rounded-md px-2 py-1.5 hover:bg-accent"
            style={{ paddingLeft: `${depth * 20 + 8}px` }}
          >
            <ChevronRight
              className={node.children.length ? 'h-4 w-4 text-muted-foreground' : 'h-4 w-4 opacity-0'}
            />
            <span className="flex-1 text-sm">{node.name}</span>
            <span className="text-xs text-muted-foreground">{node.bookmarkCount}</span>
            <div className="flex gap-0.5 opacity-0 transition-opacity group-hover:opacity-100">
              <Button
                variant="ghost"
                size="icon"
                className="h-7 w-7"
                title="Add subcategory"
                onClick={() => onAddChild(node.id)}
              >
                <FolderPlus className="h-4 w-4" />
              </Button>
              <Button
                variant="ghost"
                size="icon"
                className="h-7 w-7"
                title="Edit"
                onClick={() => onEdit(node)}
              >
                <Pencil className="h-4 w-4" />
              </Button>
              <Button
                variant="ghost"
                size="icon"
                className="h-7 w-7 text-destructive"
                title="Delete"
                onClick={() => onDelete(node)}
              >
                <Trash2 className="h-4 w-4" />
              </Button>
            </div>
          </div>
          {node.children.length > 0 && (
            <CategoryNodes
              nodes={node.children}
              depth={depth + 1}
              onAddChild={onAddChild}
              onEdit={onEdit}
              onDelete={onDelete}
            />
          )}
        </li>
      ))}
    </ul>
  )
}

function CategoryFormDialog({
  state,
  allCategories,
  onClose,
}: {
  state: DialogState
  allCategories: Category[]
  onClose: () => void
}) {
  const create = useCreateCategory()
  const update = useUpdateCategory()
  const editing = state?.mode === 'edit' ? state.category : null

  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [parentId, setParentId] = useState<string>(NONE)

  useEffect(() => {
    if (!state) return
    if (state.mode === 'edit') {
      setName(state.category.name)
      setDescription(state.category.description ?? '')
      setParentId(state.category.parentCategoryId ?? NONE)
    } else {
      setName('')
      setDescription('')
      setParentId(state.parentId ?? NONE)
    }
  }, [state])

  const flat = flattenCategories(allCategories)
  // In edit mode, prevent selecting self as parent (API also guards cycles).
  const parentOptions = editing ? flat.filter((f) => f.category.id !== editing.id) : flat

  function save() {
    const trimmed = name.trim()
    if (!trimmed) return
    const body = {
      name: trimmed,
      description: description.trim() || null,
      parentCategoryId: parentId === NONE ? null : parentId,
    }
    const onError = (e: unknown) =>
      toast.error(
        e instanceof ApiError && e.status === 400
          ? 'Invalid parent (would create a cycle).'
          : e instanceof Error
            ? e.message
            : 'Save failed',
      )

    if (editing) {
      update.mutate(
        { id: editing.id, body },
        { onSuccess: () => (toast.success('Category updated'), onClose()), onError },
      )
    } else {
      create.mutate(body, {
        onSuccess: () => (toast.success('Category created'), onClose()),
        onError,
      })
    }
  }

  return (
    <Dialog open={state !== null} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{editing ? 'Edit category' : 'New category'}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="cat-name">Name</Label>
            <Input
              id="cat-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              autoFocus
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="cat-desc">Description</Label>
            <Textarea
              id="cat-desc"
              rows={2}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label>Parent</Label>
            <Select value={parentId} onValueChange={setParentId}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>None (top level)</SelectItem>
                {parentOptions.map(({ category, depth }) => (
                  <SelectItem key={category.id} value={category.id}>
                    {' '.repeat(depth * 2)}
                    {category.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
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

function DeleteCategoryDialog({
  category,
  allCategories,
  onClose,
}: {
  category: Category | null
  allCategories: Category[]
  onClose: () => void
}) {
  const del = useDeleteCategory()
  const [reassign, setReassign] = useState<string>(DETACH)

  useEffect(() => {
    setReassign(DETACH)
  }, [category])

  const options = flattenCategories(allCategories).filter((f) => f.category.id !== category?.id)

  function remove() {
    if (!category) return
    del.mutate(
      { id: category.id, reassignTo: reassign === DETACH ? undefined : reassign },
      {
        onSuccess: () => (toast.success(`Deleted “${category.name}”`), onClose()),
        onError: (e) => toast.error(e instanceof Error ? e.message : 'Delete failed'),
      },
    )
  }

  return (
    <AlertDialog open={category !== null} onOpenChange={(o) => !o && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete category?</AlertDialogTitle>
          <AlertDialogDescription>
            “{category?.name}” will be deleted. Choose what happens to its{' '}
            {category?.bookmarkCount ?? 0} bookmark(s); subcategories move up to the parent.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <div className="space-y-1.5">
          <Label>Reassign bookmarks to</Label>
          <Select value={reassign} onValueChange={setReassign}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={DETACH}>No category (detach)</SelectItem>
              {options.map(({ category: c, depth }) => (
                <SelectItem key={c.id} value={c.id}>
                  {' '.repeat(depth * 2)}
                  {c.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction onClick={remove}>Delete</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
