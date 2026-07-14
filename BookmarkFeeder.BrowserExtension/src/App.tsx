import { Bookmark, ExternalLink } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Toaster } from '@/components/ui/sonner'
import { FolderPicker } from '@/features/folder-picker'
import { SettingsPanel } from '@/features/settings-panel'
import { SyncPanel } from '@/features/sync-panel'
import { useSettings } from '@/hooks/use-settings'
import { dashboardUrl } from '@/lib/api'

export function App() {
  const { settings, update, reload } = useSettings()

  if (!settings) {
    return (
      <div className="w-[380px] p-4 text-sm text-muted-foreground">Loading…</div>
    )
  }

  function openDashboard() {
    void chrome.tabs.create({ url: dashboardUrl(settings!.serverUrl) })
  }

  return (
    <div className="flex w-[380px] flex-col gap-3 p-4">
      <header className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Bookmark className="h-5 w-5 text-primary" />
          <span className="font-semibold">BookmarkFeeder</span>
        </div>
        <Button
          variant="ghost"
          size="sm"
          onClick={openDashboard}
          disabled={!settings.serverUrl}
          title="Open the BookmarkFeeder dashboard"
        >
          Dashboard
          <ExternalLink />
        </Button>
      </header>

      {/* Land on Settings until there's somewhere to sync to. */}
      <Tabs defaultValue={settings.serverUrl ? 'sync' : 'settings'}>
        <TabsList className="w-full">
          <TabsTrigger value="sync">Sync</TabsTrigger>
          <TabsTrigger value="folders">Folders</TabsTrigger>
          <TabsTrigger value="settings">Settings</TabsTrigger>
        </TabsList>

        <TabsContent value="sync" className="mt-3">
          <SyncPanel settings={settings} onSynced={reload} />
        </TabsContent>

        <TabsContent value="folders" className="mt-3">
          <FolderPicker selected={settings.selectedFolders} onChange={update} />
        </TabsContent>

        <TabsContent value="settings" className="mt-3">
          <SettingsPanel settings={settings} onSave={update} />
        </TabsContent>
      </Tabs>

      <Toaster position="bottom-center" />
    </div>
  )
}
