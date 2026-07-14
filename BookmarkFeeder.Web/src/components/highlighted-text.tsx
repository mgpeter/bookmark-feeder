import { highlightSegments } from '@/lib/highlight'

interface HighlightedTextProps {
  text: string
  /** The active search term; when absent the text renders untouched. */
  term?: string | null
}

/** Renders text with the search term's matches wrapped in <mark>. */
export function HighlightedText({ text, term }: HighlightedTextProps) {
  const segments = highlightSegments(text, term)

  return (
    <>
      {segments.map((segment, i) =>
        segment.match ? (
          <mark key={i} className="rounded-sm bg-yellow-200 text-inherit dark:bg-yellow-500/40">
            {segment.text}
          </mark>
        ) : (
          <span key={i}>{segment.text}</span>
        ),
      )}
    </>
  )
}
