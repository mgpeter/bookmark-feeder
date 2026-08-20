import { createBrowserRouter } from 'react-router-dom'
import { Shell } from '@/layout/shell'
import { RequireAuth } from '@/auth/require-auth'
import { DashboardPage } from '@/features/dashboard/dashboard-page'
import { BookmarkListPage } from '@/features/bookmarks/bookmark-list-page'
import { TagsPage } from '@/features/tags/tags-page'
import { CategoriesPage } from '@/features/categories/categories-page'
import { SettingsPage } from '@/features/settings/settings-page'

export const router = createBrowserRouter([
  {
    element: <Shell />,
    children: [
      {
        path: '/',
        element: (
          <RequireAuth>
            <DashboardPage />
          </RequireAuth>
        ),
      },
      {
        path: '/bookmarks',
        element: (
          <RequireAuth>
            <BookmarkListPage />
          </RequireAuth>
        ),
      },
      {
        path: '/tags',
        element: (
          <RequireAuth>
            <TagsPage />
          </RequireAuth>
        ),
      },
      {
        path: '/categories',
        element: (
          <RequireAuth>
            <CategoriesPage />
          </RequireAuth>
        ),
      },
      {
        path: '/settings',
        element: (
          <RequireAuth>
            <SettingsPage />
          </RequireAuth>
        ),
      },
    ],
  },
])
