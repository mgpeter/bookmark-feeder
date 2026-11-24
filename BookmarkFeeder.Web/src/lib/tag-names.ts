/**
 * Commas separate tags where the UI takes several at once (the bookmark dialog's tag input), so
 * the UI does not create names containing one. Names that already have one - from the API or an
 * import - still filter correctly: see encodeTagList.
 */
export const TAG_NAME_COMMA_MESSAGE =
  'Tag names cannot contain commas: the tag filter uses commas to separate tags.'

/** Why a tag name cannot be used, or null when it is fine. Empty names are left to the caller. */
export function tagNameProblem(name: string): string | null {
  return name.includes(',') ? TAG_NAME_COMMA_MESSAGE : null
}

/** Splits typed input on commas into trimmed, non-empty tag names. */
export function splitTagInput(input: string): string[] {
  return input
    .split(',')
    .map((part) => part.trim())
    .filter(Boolean)
}

/**
 * Encodes tag names for the `tags` filter param. Names are comma-separated, so a comma inside a
 * name - possible for tags created through the API, or before the UI refused them - is escaped as
 * `\,` and a backslash as `\\`. The API decodes the same way; plain names encode unchanged, so
 * existing URLs and saved searches keep working.
 */
export function encodeTagList(names: string[]): string {
  return names.map((n) => n.replace(/\\/g, '\\\\').replace(/,/g, '\\,')).join(',')
}

/** Inverse of {@link encodeTagList}. */
export function decodeTagList(value: string | null | undefined): string[] {
  if (!value) return []
  const names: string[] = []
  let current = ''
  for (let i = 0; i < value.length; i++) {
    const ch = value[i]
    if (ch === '\\' && i + 1 < value.length) {
      current += value[++i]
    } else if (ch === ',') {
      names.push(current)
      current = ''
    } else {
      current += ch
    }
  }
  names.push(current)
  return names.map((n) => n.trim()).filter(Boolean)
}
