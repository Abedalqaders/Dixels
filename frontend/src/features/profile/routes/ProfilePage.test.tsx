// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, Link, RouterProvider } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { ApiError, changeMyPassword, getMyProfile, updateMyProfile } from '@/features/profile/api/profileApi'
import type { ProfileDto } from '@/features/profile/api/profileApi'
import { TestProviders } from '@/test/providers'
import { ProfilePage } from './ProfilePage'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))

const SARA: ProfileDto = {
  userName: 'sara',
  email: 'sara@dixels.io',
  name: 'Sara',
  surname: 'Haddad',
  phoneNumber: null,
  hasPassword: true,
  concurrencyStamp: 'stamp-1',
}

const WRONG_PASSWORD = 'That isn’t your current password.'

// A data router, as in the app: the page asks before leaving with unsaved changes (useBlocker).
function renderPage() {
  const router = createMemoryRouter(
    [
      {
        path: '/profile',
        element: (
          <>
            <Link to="/my-calendar">Elsewhere</Link>
            <ProfilePage />
          </>
        ),
      },
      { path: '/my-calendar', element: <p>My calendar page</p> },
    ],
    { initialEntries: ['/profile'] },
  )
  render(
    <TestProviders>
      <RouterProvider router={router} />
    </TestProviders>,
  )
}

const save = () => screen.getByRole('button', { name: 'Save changes' })
const changePassword = () => screen.getByRole('button', { name: 'Change password' })

beforeEach(() => {
  vi.mocked(useAuth).mockReturnValue({
    user: { access_token: 't', profile: { role: 'employee', preferred_username: 'sara' } },
  } as unknown as ReturnType<typeof useAuth>)
  vi.mocked(getMyProfile).mockResolvedValue(SARA)
  vi.mocked(updateMyProfile).mockReset()
  vi.mocked(changeMyPassword).mockReset()
})

describe('ProfilePage — details', () => {
  it('shows who you are, in boxes to type in; username and email locked', async () => {
    renderPage()

    expect(await screen.findByLabelText('First name')).toHaveValue('Sara')
    expect(screen.getByLabelText('Last name')).toHaveValue('Haddad')
    expect(screen.getByLabelText('Phone number')).toHaveValue('')
    expect(screen.getByLabelText('Email')).toBeDisabled()
    expect(screen.getByLabelText('Username')).toBeDisabled()
    // Nothing changed yet: nothing to save.
    expect(save()).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled()
  })

  it('saves the name and phone typed in, sending the username and email back as they were', async () => {
    vi.mocked(updateMyProfile).mockResolvedValue({ ...SARA, name: 'Sarah', phoneNumber: '+962 79 000', concurrencyStamp: 'stamp-2' })
    renderPage()

    const first = await screen.findByLabelText('First name')
    await userEvent.clear(first)
    await userEvent.type(first, 'Sarah')
    await userEvent.type(screen.getByLabelText('Phone number'), '+962 79 000')
    await userEvent.click(save())

    expect(updateMyProfile).toHaveBeenCalledWith('t', {
      userName: 'sara',
      email: 'sara@dixels.io',
      name: 'Sarah',
      surname: 'Haddad',
      phoneNumber: '+962 79 000',
      concurrencyStamp: 'stamp-1',
    })
    expect(await screen.findByText('Profile saved.')).toBeInTheDocument()
    expect(screen.getByText('Sarah Haddad')).toBeInTheDocument()
    expect(save()).toBeDisabled()
  })

  it('puts the boxes back with Cancel', async () => {
    renderPage()

    await userEvent.type(await screen.findByLabelText('Last name'), 'X')
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(screen.getByLabelText('Last name')).toHaveValue('Haddad')
    expect(save()).toBeDisabled()
  })

  it('stops a phone number with letters in it', async () => {
    renderPage()

    await userEvent.type(await screen.findByLabelText('Phone number'), '079 abc')
    await userEvent.click(save())

    expect(screen.getByText('Use digits, spaces and + ( ) - only.')).toBeInTheDocument()
    expect(updateMyProfile).not.toHaveBeenCalled()
  })

  it("says why a save failed and keeps what's shown", async () => {
    vi.mocked(updateMyProfile).mockRejectedValue(new ApiError(409, { error: { message: 'Changed by someone else.' } }))
    renderPage()

    await userEvent.type(await screen.findByLabelText('Last name'), 'X')
    await userEvent.click(save())

    expect(await screen.findByText('Changed by someone else.')).toBeInTheDocument()
    expect(screen.getByText('Sara Haddad')).toBeInTheDocument()
  })

  it('asks before leaving with changes unsaved', async () => {
    renderPage()

    await userEvent.type(await screen.findByLabelText('Last name'), 'X')
    await userEvent.click(screen.getByRole('link', { name: 'Elsewhere' }))

    expect(screen.getByRole('alertdialog', { name: 'Discard unsaved changes?' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Discard changes' }))
    expect(await screen.findByText('My calendar page')).toBeInTheDocument()
  })
})

describe('ProfilePage — password', () => {
  it('changes the password and empties the boxes', async () => {
    vi.mocked(changeMyPassword).mockResolvedValue(undefined)
    renderPage()

    await userEvent.type(await screen.findByLabelText('Current password'), 'Old1!pass')
    await userEvent.type(screen.getByLabelText('New password'), 'New1!pass')
    await userEvent.type(screen.getByLabelText('Confirm new password'), 'New1!pass')
    await userEvent.click(changePassword())

    expect(changeMyPassword).toHaveBeenCalledWith('t', { currentPassword: 'Old1!pass', newPassword: 'New1!pass' })
    expect(await screen.findByText('Password changed.')).toBeInTheDocument()
    expect(screen.getByLabelText('New password')).toHaveValue('')
  })

  it('stops when the two new passwords differ', async () => {
    renderPage()

    await userEvent.type(await screen.findByLabelText('Current password'), 'Old1!pass')
    await userEvent.type(screen.getByLabelText('New password'), 'New1!pass')
    await userEvent.type(screen.getByLabelText('Confirm new password'), 'New1!pasz')
    await userEvent.click(changePassword())

    expect(screen.getByLabelText('Confirm new password')).toHaveAccessibleDescription('The two new passwords aren’t the same.')
    expect(changeMyPassword).not.toHaveBeenCalled()
  })

  it("puts the server's reason under the box it's about", async () => {
    vi.mocked(changeMyPassword)
      .mockRejectedValueOnce(new ApiError(400, { error: { code: 'Dixels:Users:WrongCurrentPassword', message: WRONG_PASSWORD } }))
      .mockRejectedValueOnce(new ApiError(400, { error: { message: 'Passwords must be at least 6 characters.' } }))
    renderPage()

    await userEvent.type(await screen.findByLabelText('Current password'), 'wrong')
    await userEvent.type(screen.getByLabelText('New password'), 'abc')
    await userEvent.type(screen.getByLabelText('Confirm new password'), 'abc')

    await userEvent.click(changePassword())
    expect(await screen.findByText(WRONG_PASSWORD)).toBeInTheDocument()
    expect(screen.getByLabelText('Current password')).toHaveAccessibleDescription(WRONG_PASSWORD)

    await userEvent.click(changePassword())
    expect(await screen.findByText('Passwords must be at least 6 characters.')).toBeInTheDocument()
    expect(screen.getByLabelText('New password')).toHaveAccessibleDescription('Passwords must be at least 6 characters.')
  })

  it('sets a first password without asking for a current one', async () => {
    vi.mocked(getMyProfile).mockResolvedValue({ ...SARA, hasPassword: false })
    vi.mocked(changeMyPassword).mockResolvedValue(undefined)
    renderPage()

    await userEvent.type(await screen.findByLabelText('New password'), 'New1!pass')
    expect(screen.queryByLabelText('Current password')).not.toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Confirm new password'), 'New1!pass')
    await userEvent.click(screen.getByRole('button', { name: 'Set password' }))

    expect(changeMyPassword).toHaveBeenCalledWith('t', { currentPassword: undefined, newPassword: 'New1!pass' })
  })
})
