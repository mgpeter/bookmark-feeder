import { describe, expect, it } from 'vitest'
import { highlightSegments } from './highlight'

/** Compact view of the segments: matched parts wrapped in [brackets]. */
function render(text: string, term?: string): string {
  return highlightSegments(text, term)
    .map((s) => (s.match ? `[${s.text}]` : s.text))
    .join('')
}

describe('highlightSegments', () => {
  it('marks the matching term', () => {
    expect(render('Learn GraphQL basics', 'graphql')).toBe('Learn [GraphQL] basics')
  })

  it('matches case-insensitively but preserves the original casing', () => {
    expect(render('GRAPHQL and graphql', 'GraphQL')).toBe('[GRAPHQL] and [graphql]')
  })

  it('marks every word of a multi-word search', () => {
    expect(render('React hooks guide', 'react hooks')).toBe('[React] [hooks] guide')
  })

  it.each([
    ['C++ tutorial', 'c++', '[C++] tutorial'],
    ['Use a.b.c syntax', 'a.b.c', 'Use [a.b.c] syntax'],
    ['Costs $5 (approx)', '(approx)', 'Costs $5 [(approx)]'],
    ['Match [brackets] here', '[brackets]', 'Match [[brackets]] here'],
  ])(
    // A naive implementation builds a RegExp from raw input and throws on these.
    'handles regex-unsafe term %j',
    (text, term, expected) => {
      expect(render(text, term)).toBe(expected)
    },
  )

  it('does not mark a negated term — it cannot be why the result matched', () => {
    expect(render('alpha beta', 'alpha -beta')).toBe('[alpha] beta')
  })

  it('ignores the OR operator rather than marking the word "or"', () => {
    expect(render('redux or mobx', 'redux OR mobx')).toBe('[redux] or [mobx]')
  })

  it('marks a quoted phrase as a whole', () => {
    expect(render('the react hooks guide', '"react hooks"')).toBe('the [react hooks] guide')
  })

  it.each([undefined, '', '   '])('returns the text untouched for term %j', (term) => {
    expect(render('Nothing to mark', term)).toBe('Nothing to mark')
  })

  it('returns the text untouched when nothing matches', () => {
    expect(render('Nothing to mark', 'absent')).toBe('Nothing to mark')
  })

  it('handles empty text', () => {
    expect(highlightSegments('', 'x')).toEqual([{ text: '', match: false }])
  })
})
