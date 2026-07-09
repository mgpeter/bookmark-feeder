import { cn } from '@/lib/utils'

interface Props {
  name: string
  color?: string | null
  className?: string
}

export function TagChip({ name, color, className }: Props) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs',
        className,
      )}
      style={color ? { borderColor: color, color } : undefined}
    >
      {color && (
        <span className="h-2 w-2 rounded-full" style={{ backgroundColor: color }} />
      )}
      {name}
    </span>
  )
}
