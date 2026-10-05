// @vitest-environment jsdom
import { afterEach, describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { TopBar } from './TopBar'
import { stubMatchMedia } from '@/test/matchMedia'

let restore = () => {}
afterEach(() => restore())

const toggle = () => screen.queryByRole('button', { name: /Switch to (dark|light) theme/ })
const language = () => screen.queryByRole('button', { name: /^Language:/ })

describe('TopBar', () => {
  it('puts the language and theme switches at the end of the bar on a wide screen', () => {
    restore = stubMatchMedia(1280)
    render(<TopBar>Level 3</TopBar>)

    expect(screen.getByText('Level 3')).toBeInTheDocument()
    expect(language()).toBeInTheDocument()
    expect(toggle()).toBeInTheDocument()
  })

  it('leaves the switches to the menu band below lg, keeping what the page put in the bar', () => {
    restore = stubMatchMedia(800)
    render(<TopBar>Level 3</TopBar>)

    expect(screen.getByText('Level 3')).toBeInTheDocument()
    expect(language()).not.toBeInTheDocument()
    expect(toggle()).not.toBeInTheDocument()
  })

  it('draws nothing below lg when the page has nothing of its own for it', () => {
    restore = stubMatchMedia(800)
    const { container } = render(<TopBar />)

    expect(container).toBeEmptyDOMElement()
  })

  it('shows the breadcrumbs, on a phone too: links up the way, the page itself last and not a link', () => {
    restore = stubMatchMedia(390)
    render(
      <MemoryRouter>
        <TopBar crumbs={[{ label: 'Space management' }, { label: 'Hierarchy', to: '/admin/buildings' }, { label: 'Riverside HQ' }]} />
      </MemoryRouter>,
    )

    const trail = screen.getByRole('navigation', { name: 'Breadcrumb' })
    expect(trail).toHaveTextContent('Space managementHierarchyRiverside HQ')
    expect(screen.getByRole('link', { name: 'Hierarchy' })).toHaveAttribute('href', '/admin/buildings')
    expect(screen.queryByRole('link', { name: 'Space management' })).not.toBeInTheDocument()
    expect(screen.getByText('Riverside HQ')).toHaveAttribute('aria-current', 'page')
  })

  it('holds the place of a name still loading', () => {
    restore = stubMatchMedia(1280)
    render(
      <MemoryRouter>
        <TopBar crumbs={[{ label: 'Hierarchy', to: '/admin/buildings' }, {}]} />
      </MemoryRouter>,
    )

    expect(screen.getByRole('status', { name: 'Loading…' })).toBeInTheDocument()
  })
})
