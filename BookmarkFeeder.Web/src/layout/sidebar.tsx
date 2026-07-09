import { NavLink } from 'react-router-dom'
import {
  Bookmark,
  FolderTree,
  LayoutDashboard,
  Settings,
  Tags,
} from 'lucide-react'
import { cn } from '@/lib/utils'

const nav = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, end: true },
  { to: '/bookmarks', label: 'Bookmarks', icon: Bookmark, end: false },
  { to: '/tags', label: 'Tags', icon: Tags, end: false },
  { to: '/categories', label: 'Categories', icon: FolderTree, end: false },
  { to: '/settings', label: 'Settings', icon: Settings, end: false },
]

export function Sidebar() {
  return (
    <aside className="flex w-60 shrink-0 flex-col border-r bg-sidebar text-sidebar-foreground">
      <div className="flex h-14 items-center gap-2 border-b px-4">
        <Bookmark className="h-5 w-5 text-primary" />
        <span className="font-semibold">BookmarkFeeder</span>
      </div>
      <nav className="flex-1 space-y-1 p-2">
        {nav.map(({ to, label, icon: Icon, end }) => (
          <NavLink
            key={to}
            to={to}
            end={end}
            className={({ isActive }) =>
              cn(
                'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                isActive
                  ? 'bg-sidebar-accent text-sidebar-accent-foreground'
                  : 'text-muted-foreground hover:bg-sidebar-accent/50 hover:text-sidebar-accent-foreground',
              )
            }
          >
            <Icon className="h-4 w-4" />
            {label}
          </NavLink>
        ))}
      </nav>
    </aside>
  )
}
