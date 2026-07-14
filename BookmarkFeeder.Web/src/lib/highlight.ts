export interface HighlightSegment {
  text: string
  /** True when this segment matched the search term and should be marked. */
  match: boolean
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

/**
 * The parts of a websearch query worth highlighting. Mirrors what
 * websearch_to_tsquery does with the operators, so we only mark what could
 * actually have caused the match.
 */
function searchWords(term: string): string[] {
  const words: string[] = []
  // Either a "quoted phrase" or a bare token.
  const tokens = /"([^"]+)"|(\S+)/g
  let token: RegExpExecArray | null

  while ((token = tokens.exec(term)) !== null) {
    const [, phrase, bare] = token
    if (phrase) {
      words.push(phrase.trim())
      continue
    }
    if (!bare) continue
    // A negated term cannot be why a result matched, so marking it would be a lie.
    if (bare.startsWith('-')) continue
    // Operators, not search words.
    if (/^(or|and)$/i.test(bare)) continue
    words.push(bare)
  }

  // Longest first so a phrase wins over its own constituent words.
  return words.filter(Boolean).sort((a, b) => b.length - a.length)
}

/**
 * Splits text into matched and unmatched segments for the given search term.
 * Highlighting is done here rather than server-side (no ts_headline), so the
 * marking is approximate: it marks the terms, not the exact lexemes Postgres matched.
 */
export function highlightSegments(
  text: string,
  term: string | null | undefined,
): HighlightSegment[] {
  const words = term ? searchWords(term.trim()) : []
  if (words.length === 0 || !text) return [{ text, match: false }]

  // Escaped: search terms are raw user input, and "C++" or "(approx)" would
  // otherwise be an invalid or wildly wrong pattern.
  const pattern = new RegExp(`(${words.map(escapeRegExp).join('|')})`, 'gi')
  const matchable = new Set(words.map((w) => w.toLowerCase()))

  return text
    .split(pattern)
    .filter((part) => part !== '')
    .map((part) => ({ text: part, match: matchable.has(part.toLowerCase()) }))
}
