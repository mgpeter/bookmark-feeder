import { describe, expect, it } from 'vitest'
import { splitTagInput, tagNameProblem } from './tag-names'

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
