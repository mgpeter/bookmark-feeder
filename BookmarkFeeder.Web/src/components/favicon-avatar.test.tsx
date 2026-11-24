import { describe, expect, it } from 'vitest'
import { render } from '@testing-library/react'
import { FaviconAvatar } from './favicon-avatar'

const img = (container: HTMLElement) => container.querySelector('img')

describe('FaviconAvatar', () => {
  it("requests an http(s) site's own /favicon.ico", () => {
    const { container } = render(<FaviconAvatar url="https://example.com/a/b" />)
    expect(img(container)).toHaveAttribute('src', 'https://example.com/favicon.ico')
  })

  it.each(['javascript:alert(1)', 'chrome://settings', 'file:///tmp/x.html', 'not a url'])(
    'shows the monogram without any request for %s',
    (url) => {
      const { container } = render(<FaviconAvatar url={url} />)
      // Previously "null/favicon.ico", fetched relative to this app.
      expect(img(container)).toBeNull()
    },
  )

  it('uses a stored favicon URL as given', () => {
    const { container } = render(
      <FaviconAvatar url="https://example.com" faviconUrl="https://cdn.example.com/icon.png" />,
    )
    expect(img(container)).toHaveAttribute('src', 'https://cdn.example.com/icon.png')
  })
})
