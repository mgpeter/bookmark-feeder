import { describe, expect, it } from 'vitest'
import { hostname, origin, safeHref } from './format'

describe('hostname', () => {
  it('strips the www prefix', () => {
    expect(hostname('https://www.example.com/path')).toBe('example.com')
  })

  it('returns the input unchanged for a non-URL', () => {
    expect(hostname('not a url')).toBe('not a url')
  })
})

describe('origin', () => {
  it('returns the origin for a valid URL', () => {
    expect(origin('https://example.com/a/b?c=d')).toBe('https://example.com')
  })

  it('returns an empty string for a non-URL', () => {
    expect(origin('nope')).toBe('')
  })
})

describe('safeHref', () => {
  it.each(['https://react.dev/learn', 'http://intranet.example/x'])('links %s', (url) => {
    expect(safeHref(url)).toBe(url)
  })

  it.each([
    'javascript:alert(document.cookie)',
    'JavaScript:alert(1)',
    ' javascript:alert(1)',
    'data:text/html,<script>alert(1)</script>',
    'chrome://settings',
    'file:///etc/passwd',
    'not a url',
  ])('refuses %s', (url) => {
    expect(safeHref(url)).toBeUndefined()
  })
})
