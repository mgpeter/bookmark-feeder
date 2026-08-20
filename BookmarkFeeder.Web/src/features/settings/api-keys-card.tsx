import { useState } from 'react'
import { toast } from 'sonner'
import { Check, Copy, KeyRound, Loader2 } from 'lucide-react'
import { useApiKeys, useCreateApiKey, useRevokeApiKey } from '@/api/api-keys'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { relativeDate } from '@/lib/format'
import type { CreatedApiKey } from '@/types/models'

const EXPIRY_OPTIONS = [
  { value: 'never', label: 'Never expires', days: null },
  { value: '90', label: '90 days', days: 90 },
  { value: '365', label: '1 year', days: 365 },
]

/**
 * Mint, list and revoke the keys the browser extension authenticates with.
 *
 * The freshly minted key is held in local component state and never written to the query cache,
 * because it is the one copy that will ever exist.
 */
export function ApiKeysCard() {
  const { data: keys, isLoading } = useApiKeys()
  const create = useCreateApiKey()
  const revoke = useRevokeApiKey()

  const [name, setName] = useState('')
  const [expiry, setExpiry] = useState('never')
  const [minted, setMinted] = useState<CreatedApiKey | null>(null)
  const [copied, setCopied] = useState(false)

  async function mint() {
    const days = EXPIRY_OPTIONS.find((o) => o.value === expiry)?.days ?? null
    try {
      const created = await create.mutateAsync({ name: name.trim(), expiresInDays: days })
      setMinted(created)
      setCopied(false)
      setName('')
    } catch {
      toast.error('Could not create the key')
    }
  }

  async function copy(value: string) {
    try {
      await navigator.clipboard.writeText(value)
      setCopied(true)
      toast.success('Key copied to the clipboard')
    } catch {
      toast.error('Could not copy — select the key and copy it manually')
    }
  }

  const active = keys?.filter((k) => !k.revokedAt) ?? []
  const revoked = keys?.filter((k) => k.revokedAt) ?? []

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <KeyRound className="size-4" />
          API keys
        </CardTitle>
        <CardDescription>
          The browser extension signs in with a key rather than an Auth0 login. Create one key per
          browser, so you can revoke a single machine without disturbing the others. Keys can add
          bookmarks and read tags and categories — they cannot delete anything or manage keys.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-6">
        {minted && (
          <div className="space-y-2 rounded-md border border-primary/40 bg-primary/5 p-4">
            <p className="text-sm font-medium">Copy your key now — it will not be shown again.</p>
            <div className="flex gap-2">
              <Input readOnly value={minted.key} className="font-mono text-xs" />
              <Button variant="outline" size="icon" onClick={() => copy(minted.key)}>
                {copied ? <Check className="size-4" /> : <Copy className="size-4" />}
              </Button>
            </div>
            <p className="text-xs text-muted-foreground">
              Paste it into the extension popup alongside your server URL. We only store a hash, so
              if you lose it you will need to revoke this key and create another.
            </p>
            <Button variant="ghost" size="sm" onClick={() => setMinted(null)}>
              Done
            </Button>
          </div>
        )}

        <div className="flex flex-wrap items-end gap-2">
          <div className="min-w-48 flex-1 space-y-2">
            <Label htmlFor="keyName">Name</Label>
            <Input
              id="keyName"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Work laptop - Chrome"
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="keyExpiry">Expires</Label>
            <Select value={expiry} onValueChange={setExpiry}>
              <SelectTrigger id="keyExpiry" className="w-40">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {EXPIRY_OPTIONS.map((option) => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <Button onClick={mint} disabled={!name.trim() || create.isPending}>
            {create.isPending && <Loader2 className="size-4 animate-spin" />}
            Create key
          </Button>
        </div>

        {isLoading ? (
          <p className="text-sm text-muted-foreground">Loading keys…</p>
        ) : active.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            No keys yet. Create one to connect the browser extension.
          </p>
        ) : (
          <ul className="divide-y rounded-md border">
            {active.map((key) => (
              <li key={key.id} className="flex items-center justify-between gap-4 p-3">
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium">{key.name}</p>
                  <p className="text-xs text-muted-foreground">
                    <span className="font-mono">{key.prefix}…</span>
                    {' · created '}
                    {relativeDate(key.createdAt)}
                    {' · '}
                    {key.lastUsedAt ? `used ${relativeDate(key.lastUsedAt)}` : 'never used'}
                    {key.expiresAt && ` · expires ${new Date(key.expiresAt).toLocaleDateString()}`}
                  </p>
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={async () => {
                    await revoke.mutateAsync(key.id)
                    toast.success(`Revoked "${key.name}"`)
                  }}
                >
                  Revoke
                </Button>
              </li>
            ))}
          </ul>
        )}

        {revoked.length > 0 && (
          <details className="text-sm">
            <summary className="cursor-pointer text-muted-foreground">
              {revoked.length} revoked {revoked.length === 1 ? 'key' : 'keys'}
            </summary>
            <ul className="mt-2 space-y-1">
              {revoked.map((key) => (
                <li key={key.id} className="flex items-center gap-2 text-xs text-muted-foreground">
                  <Badge variant="outline">revoked</Badge>
                  <span>{key.name}</span>
                  <span className="font-mono">{key.prefix}…</span>
                </li>
              ))}
            </ul>
          </details>
        )}
      </CardContent>
    </Card>
  )
}
