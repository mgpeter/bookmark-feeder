import { describe, expect, it } from 'vitest'
import { decodeTagList, encodeTagList, splitTagInput, tagNameProblem } from './tag-names'

describe('tagNameProblem', () => {
  it('rejects a comma, which the tag filter would read as two tags', () => {
    expect(tagNameProblem('a,b')).toMatch(/commas/i)
  })

  it('accepts an ordinary name', () => {
    expect(tagNameProblem('dotnet core')).toBeNull()
  })
})

describe('splitTagInput', () => {
  it('splits on commas and drops blanks', () => {
    expect(splitTagInput(' react, dotnet ,, ')).toEqual(['react', 'dotnet'])
  })
})

describe('encodeTagList / decodeTagList', () => {
  it('leaves plain names unchanged, so existing URLs keep working', () => {
    expect(encodeTagList(['react', 'dev tools'])).toBe('react,dev tools')
    expect(decodeTagList('react,dev tools')).toEqual(['react', 'dev tools'])
  })

  it('round-trips names containing commas and backslashes', () => {
    const names = ['a,b', 'c\\d', 'plain']
    expect(decodeTagList(encodeTagList(names))).toEqual(names)
  })

  it('treats a missing value as no tags', () => {
    expect(decodeTagList(null)).toEqual([])
    expect(decodeTagList('')).toEqual([])
  })
})
