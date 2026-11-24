/**
 * The bookmark list filters by tags as ONE comma-joined URL param (tags=a,b), the same shape the
 * API's `tags` query takes. A tag whose name contains a comma would split into two names that do
 * not exist, so it could never be filtered on. The API accepts such names, so the UI refuses to
 * create them.
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
