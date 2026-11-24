import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import type {
  Category,
  CreateCategoryRequest,
  UpdateCategoryRequest,
} from '@/types/models'
import { bookmarksKey, categoriesKey } from './query-keys'

export { categoriesKey }

/**
 * Renaming a category changes the badge on its bookmarks; deleting one detaches or reassigns them.
 * Either way the bookmark list is stale too.
 */
function invalidateCategoriesAndBookmarks(qc: ReturnType<typeof useQueryClient>) {
  return Promise.all([
    qc.invalidateQueries({ queryKey: [categoriesKey] }),
    qc.invalidateQueries({ queryKey: [bookmarksKey] }),
  ])
}

export function useCategories() {
  return useQuery({
    queryKey: [categoriesKey, 'tree'],
    queryFn: () => api.get<Category[]>('/categories'),
  })
}

export function useCreateCategory() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateCategoryRequest) => api.post<Category>('/categories', body),
    onSuccess: () => qc.invalidateQueries({ queryKey: [categoriesKey] }),
  })
}

export function useUpdateCategory() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateCategoryRequest }) =>
      api.put<Category>(`/categories/${id}`, body),
    onSuccess: () => invalidateCategoriesAndBookmarks(qc),
  })
}

export function useDeleteCategory() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, reassignTo }: { id: string; reassignTo?: string }) =>
      api.del<void>(`/categories/${id}`, reassignTo ? { reassignTo } : undefined),
    onSuccess: () => invalidateCategoriesAndBookmarks(qc),
  })
}

/** Flattens the category tree to a list with indentation depth for pickers. */
export function flattenCategories(
  tree: Category[],
  depth = 0,
): { category: Category; depth: number }[] {
  return tree.flatMap((category) => [
    { category, depth },
    ...flattenCategories(category.children ?? [], depth + 1),
  ])
}
