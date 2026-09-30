// @vitest-environment jsdom
import { afterEach, describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { TopBar } from './TopBar'
import { stubMatchMedia } from '@/test/matchMedia'

let restore = () => {}
afterEach(() => restore())

const toggle = () => screen.queryByRole('button', { name: /Switch to (dark|light) theme/ })

describe('TopBar', () => {
  it('puts the theme toggle at the end of the bar on a wide screen', () => {
    restore = stubMatchMedia(1280)
    render(<TopBar>Level 3</TopBar>)

    expect(screen.getByText('Level 3')).toBeInTheDocument()
    expect(toggle()).toBeInTheDocument()
  })

  it('leaves the toggle to the menu band below lg, keeping what the page put in the bar', () => {
    restore = stubMatchMedia(800)
    render(<TopBar>Level 3</TopBar>)

    expect(screen.getByText('Level 3')).toBeInTheDocument()
    expect(toggle()).not.toBeInTheDocument()
  })

  it('draws nothing below lg when the page has nothing of its own for it', () => {
    restore = stubMatchMedia(800)
    const { container } = render(<TopBar />)

    expect(container).toBeEmptyDOMElement()
  })
})
