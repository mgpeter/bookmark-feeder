import { NavLink } from 'react-router-dom'
import { useAuth0 } from '@auth0/auth0-react'
import {
  Bookmark,
  FolderTree,
  LayoutDashboard,
  LogOut,
  Settings,
  Tags,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

const nav = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, end: true },
  { to: '/bookmarks', label: 'Bookmarks', icon: Bookmark, end: false },
  { to: '/tags', label: 'Tags', icon: Tags, end: false },
  { to: '/categories', label: 'Categories', icon: FolderTree, end: false },
  { to: '/settings', label: 'Settings', icon: Settings, end: false },
]

export function Sidebar() {
  const { user, logout } = useAuth0()

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

      {/*
        Identity needs a permanent home now that Settings is no longer the screen you are forced
        through before the app works.
      */}
      <div className="border-t p-3">
        <div className="flex items-center gap-2">
          {user?.picture ? (
            <img src={user.picture} alt="" className="size-7 shrink-0 rounded-full" />
          ) : (
            <div className="size-7 shrink-0 rounded-full bg-muted" />
          )}
          <div className="min-w-0 flex-1">
            <p className="truncate text-xs font-medium">{user?.name ?? 'Signed in'}</p>
            <p className="truncate text-xs text-muted-foreground">{user?.email}</p>
          </div>
          <Button
            variant="ghost"
            size="icon"
            title="Sign out"
            onClick={() => void logout({ logoutParams: { returnTo: window.location.origin } })}
          >
            <LogOut className="size-4" />
          </Button>
        </div>
      </div>
    </aside>
  )
}
