import { FolderTree } from 'lucide-react'
import { Badge } from '@/components/ui/badge'

export function CategoryBadge({ name }: { name: string }) {
  return (
    <Badge variant="secondary" className="gap-1">
      <FolderTree className="h-3 w-3" />
      {name}
    </Badge>
  )
}
