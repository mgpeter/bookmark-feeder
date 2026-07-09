import { createBrowserRouter } from 'react-router-dom'
import { Shell } from '@/layout/shell'
import { RequireConfig } from '@/config/require-config'
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
          <RequireConfig>
            <DashboardPage />
          </RequireConfig>
        ),
      },
      {
        path: '/bookmarks',
        element: (
          <RequireConfig>
            <BookmarkListPage />
          </RequireConfig>
        ),
      },
      {
        path: '/tags',
        element: (
          <RequireConfig>
            <TagsPage />
          </RequireConfig>
        ),
      },
      {
        path: '/categories',
        element: (
          <RequireConfig>
            <CategoriesPage />
          </RequireConfig>
        ),
      },
      { path: '/settings', element: <SettingsPage /> },
    ],
  },
])
