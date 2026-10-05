// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useAuth } from 'react-oidc-context'
import { ApiError, getMyPicture, removeMyPicture, setMyPicture } from '@/features/profile/api/profileApi'
import { shrinkPicture } from '@/features/profile/shrinkPicture'
import { TestProviders } from '@/test/providers'
import { ProfilePhoto } from './ProfilePhoto'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))
// jsdom has no canvas: the shrinking itself is the browser's job.
vi.mock('@/features/profile/shrinkPicture', () => ({ PICTURE_SIZE: 256, shrinkPicture: vi.fn() }))

const SHRUNK = new Blob(['shrunk'], { type: 'image/jpeg' })
const photo = (type = 'image/png') => new File(['big-photo'], 'me.png', { type })

function renderPhoto() {
  render(
    <TestProviders>
      <ProfilePhoto name="Sara Haddad" token="t" />
    </TestProviders>,
  )
}

const camera = () => screen.getByRole('button', { name: 'Change photo' })
const input = () => screen.getByTestId('photo-input') as HTMLInputElement
// fireEvent, not userEvent.upload: the input's accept="image/*" would quietly drop the
// wrong-type file this test wants to send.
const pick = (file: File) => fireEvent.change(input(), { target: { files: [file] } })
const picture = () => document.querySelector('.av img')

describe('ProfilePhoto', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue({ user: { access_token: 't', profile: {} } } as unknown as ReturnType<typeof useAuth>)
    vi.mocked(getMyPicture).mockResolvedValue(null)
    vi.mocked(setMyPicture).mockReset().mockResolvedValue(undefined)
    vi.mocked(removeMyPicture).mockReset().mockResolvedValue(undefined)
    vi.mocked(shrinkPicture).mockReset().mockResolvedValue(SHRUNK)
    // jsdom has no object URLs either.
    URL.createObjectURL = vi.fn(() => 'blob:picture')
    URL.revokeObjectURL = vi.fn()
  })
  afterEach(() => vi.restoreAllMocks())

  it('shows the initials until there is a picture', async () => {
    renderPhoto()

    expect(await screen.findByText('SH')).toBeInTheDocument()
    expect(picture()).toBeNull()
    // Nothing to enlarge.
    expect(screen.queryByRole('button', { name: 'View photo' })).toBeNull()
  })

  it('opens the picture larger when it is clicked', async () => {
    vi.mocked(getMyPicture).mockResolvedValue(SHRUNK)
    renderPhoto()

    await userEvent.click(await screen.findByRole('button', { name: 'View photo' }))

    const dialog = await screen.findByRole('dialog', { name: 'Sara Haddad' })
    expect(within(dialog).getByRole('img', { name: 'Sara Haddad' })).toHaveAttribute('src', 'blob:picture')
  })

  it('opens the file picker straight away when there is no picture', async () => {
    renderPhoto()
    const click = vi.spyOn(input(), 'click')

    await userEvent.click(camera())

    expect(click).toHaveBeenCalled()
  })

  it('shrinks the picked photo, uploads that, and shows it', async () => {
    renderPhoto()

    pick(photo())

    expect(await screen.findByText('Photo updated.')).toBeInTheDocument()
    expect(shrinkPicture).toHaveBeenCalledWith(expect.objectContaining({ name: 'me.png' }))
    expect(setMyPicture).toHaveBeenCalledWith('t', SHRUNK)
    expect(picture()).toHaveAttribute('src', 'blob:picture')
  })

  it('turns away a file that is not a picture, without sending it', async () => {
    renderPhoto()

    pick(photo('application/pdf'))

    expect(await screen.findByText('Pick a picture file, such as a JPEG or PNG.')).toBeInTheDocument()
    expect(shrinkPicture).not.toHaveBeenCalled()
    expect(setMyPicture).not.toHaveBeenCalled()
  })

  it("says so when the browser can't open the picture", async () => {
    vi.mocked(shrinkPicture).mockRejectedValue(new Error('broken'))
    renderPhoto()

    pick(photo())

    expect(await screen.findByText('That picture couldn’t be opened. Try another one.')).toBeInTheDocument()
    expect(setMyPicture).not.toHaveBeenCalled()
  })

  it("shows the server's reason when it refuses the picture", async () => {
    vi.mocked(setMyPicture).mockRejectedValue(new ApiError(400, { error: { message: 'That picture is too big.' } }))
    renderPhoto()

    pick(photo())

    expect(await screen.findByText('That picture is too big.')).toBeInTheDocument()
    expect(picture()).toBeNull()
  })

  it('offers a new picture or removing it once there is one, and removes it', async () => {
    vi.mocked(getMyPicture).mockResolvedValue(new Blob(['stored'], { type: 'image/jpeg' }))
    renderPhoto()
    await vi.waitFor(() => expect(picture()).not.toBeNull())

    await userEvent.click(camera())
    expect(screen.getByRole('menuitem', { name: 'Upload a new photo' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('menuitem', { name: 'Remove photo' }))

    expect(removeMyPicture).toHaveBeenCalledWith('t')
    expect(await screen.findByText('Photo removed.')).toBeInTheDocument()
    expect(picture()).toBeNull()
    expect(screen.getByText('SH')).toBeInTheDocument()
  })
})
