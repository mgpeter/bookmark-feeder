import { describe, expect, it } from 'vitest'
import { hostname, origin } from './format'

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
