import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { X } from 'lucide-react'
import type { Bookmark } from '@/types/models'
import { useCreateBookmark, useUpdateBookmark } from '@/api/bookmarks'
import { useCategories, flattenCategories } from '@/api/categories'
import { ApiError } from '@/lib/api-client'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

const NONE = '__none__'

const schema = z.object({
  url: z.string().url('Enter a valid URL'),
  title: z.string().min(1, 'Title is required').max(500),
  description: z.string().max(2000),
  categoryId: z.string(),
  isRead: z.boolean(),
})
type FormValues = z.infer<typeof schema>

/** null closes the dialog; 'new' = create; a Bookmark = edit. */
export type EditTarget = Bookmark | 'new' | null

interface Props {
  target: EditTarget
  onClose: () => void
}

export function BookmarkEditDialog({ target, onClose }: Props) {
  const isOpen = target !== null
  const editing = target && target !== 'new' ? target : null
  const create = useCreateBookmark()
  const update = useUpdateBookmark()
  const categories = useCategories()
  const flat = categories.data ? flattenCategories(categories.data) : []

  const [tags, setTags] = useState<string[]>([])
  const [tagInput, setTagInput] = useState('')

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { url: '', title: '', description: '', categoryId: NONE, isRead: false },
  })

  useEffect(() => {
    if (!isOpen) return
    if (editing) {
      reset({
        url: editing.url,
        title: editing.title,
        description: editing.description ?? '',
        categoryId: editing.categories[0]?.id ?? NONE,
        isRead: editing.isRead,
      })
      setTags(editing.tags.map((t) => t.name))
    } else {
      reset({ url: '', title: '', description: '', categoryId: NONE, isRead: false })
      setTags([])
    }
    setTagInput('')
  }, [isOpen, editing, reset])

  function addTag() {
    const name = tagInput.trim()
    if (name && !tags.includes(name)) setTags([...tags, name])
    setTagInput('')
  }

  function onSubmit(values: FormValues) {
    const categoryId = values.categoryId === NONE ? null : values.categoryId
    const description = values.description || null

    if (editing) {
      update.mutate(
        {
          id: editing.id,
          body: { title: values.title, description, categoryId, isRead: values.isRead, tags },
        },
        {
          onSuccess: () => {
            toast.success('Bookmark updated')
            onClose()
          },
          onError: (e) => toast.error(errorMessage(e)),
        },
      )
    } else {
      create.mutate(
        { url: values.url, title: values.title, description, categoryId, tags },
        {
          onSuccess: () => {
            toast.success('Bookmark added')
            onClose()
          },
          onError: (e) => toast.error(errorMessage(e)),
        },
      )
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{editing ? 'Edit bookmark' : 'Add bookmark'}</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          {!editing && (
            <div className="space-y-1.5">
              <Label htmlFor="url">URL</Label>
              <Input id="url" {...register('url')} placeholder="https://…" />
              {errors.url && <p className="text-xs text-destructive">{errors.url.message}</p>}
            </div>
          )}

          <div className="space-y-1.5">
            <Label htmlFor="title">Title</Label>
            <Input id="title" {...register('title')} />
            {errors.title && <p className="text-xs text-destructive">{errors.title.message}</p>}
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="description">Description</Label>
            <Textarea id="description" rows={3} {...register('description')} />
          </div>

          <div className="space-y-1.5">
            <Label>Category</Label>
            <Select value={watch('categoryId')} onValueChange={(v) => setValue('categoryId', v)}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>No category</SelectItem>
                {flat.map(({ category, depth }) => (
                  <SelectItem key={category.id} value={category.id}>
                    {' '.repeat(depth * 2)}
                    {category.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="tag-input">Tags</Label>
            {tags.length > 0 && (
              <div className="flex flex-wrap gap-1.5">
                {tags.map((t) => (
                  <span
                    key={t}
                    className="inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs"
                  >
                    {t}
                    <button
                      type="button"
                      aria-label={`Remove ${t}`}
                      onClick={() => setTags(tags.filter((x) => x !== t))}
                    >
                      <X className="h-3 w-3" />
                    </button>
                  </span>
                ))}
              </div>
            )}
            <Input
              id="tag-input"
              value={tagInput}
              onChange={(e) => setTagInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault()
                  addTag()
                }
              }}
              onBlur={addTag}
              placeholder="Type a tag and press Enter"
            />
          </div>

          {editing && (
            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={watch('isRead')}
                onCheckedChange={(c) => setValue('isRead', Boolean(c))}
              />
              Mark as read
            </label>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {editing ? 'Save' : 'Add'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function errorMessage(e: unknown): string {
  if (e instanceof ApiError && e.status === 409) {
    return 'A bookmark with this URL already exists.'
  }
  return e instanceof Error ? e.message : 'Something went wrong'
}
