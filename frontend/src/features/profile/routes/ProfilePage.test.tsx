// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, Link, RouterProvider } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { ApiError, changeMyPassword, getMyProfile, updateMyProfile } from '@/features/profile/api/profileApi'
import type { ProfileDto } from '@/features/profile/api/profileApi'
import { TestProviders } from '@/test/providers'
import { getPasswordRules } from '@/features/profile/passwordRules'
import type { PasswordRules } from '@/features/profile/passwordRules'
import { ProfilePage } from './ProfilePage'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))
vi.mock('@/features/profile/passwordRules', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/features/profile/passwordRules')>()),
  getPasswordRules: vi.fn(),
}))

// ABP's defaults: 6 characters, an uppercase and a lowercase letter, a digit and a symbol.
const DEFAULT_RULES: PasswordRules = {
  requiredLength: 6,
  requiredUniqueChars: 1,
  requireDigit: true,
  requireLowercase: true,
  requireUppercase: true,
  requireNonAlphanumeric: true,
}

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
function renderPage(path = '/profile') {
  const router = createMemoryRouter(
    [
      {
        path: '/profile',
        element: (
          <>
            <Link to="/my-calendar">Elsewhere</Link>
            <ProfilePage section="profile" />
          </>
        ),
      },
      { path: '/profile/security', element: <ProfilePage section="security" /> },
      { path: '/my-calendar', element: <p>My calendar page</p> },
    ],
    { initialEntries: [path] },
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
  vi.mocked(getPasswordRules).mockResolvedValue(DEFAULT_RULES)
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

describe('ProfilePage — tabs', () => {
  it('keeps the details on Profile and the password on Security, each at its own address', async () => {
    renderPage()

    expect(await screen.findByLabelText('First name')).toBeInTheDocument()
    expect(screen.queryByLabelText('New password')).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Profile' })).toHaveAttribute('aria-current', 'page')

    await userEvent.click(screen.getByRole('link', { name: 'Security' }))

    expect(await screen.findByLabelText('New password', { selector: 'input' })).toBeInTheDocument()
    expect(screen.queryByLabelText('First name')).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Security' })).toHaveAttribute('aria-current', 'page')
  })
})

describe('ProfilePage — password', () => {
  const box = (label: string) => screen.getByLabelText(label, { selector: 'input' })
  const rule = (name: string) => screen.getByText(name).closest('li')!

  async function fillIn(current: string, next: string, confirm: string) {
    if (current) await userEvent.type(await screen.findByLabelText('Current password', { selector: 'input' }), current)
    await userEvent.type(await screen.findByLabelText('New password', { selector: 'input' }), next)
    await userEvent.type(box('Confirm new password'), confirm)
  }

  it('changes the password and empties the boxes', async () => {
    vi.mocked(changeMyPassword).mockResolvedValue(undefined)
    renderPage('/profile/security')

    await fillIn('Old1!pass', 'New1!pass', 'New1!pass')
    await userEvent.click(changePassword())

    expect(changeMyPassword).toHaveBeenCalledWith('t', { currentPassword: 'Old1!pass', newPassword: 'New1!pass' })
    expect(await screen.findByText('Password changed.')).toBeInTheDocument()
    expect(box('New password')).toHaveValue('')
  })

  it('ticks off each rule as the new password meets it', async () => {
    renderPage('/profile/security')

    await userEvent.type(await screen.findByLabelText('New password', { selector: 'input' }), 'abc')

    expect(rule('A lowercase letter (a–z)')).toHaveTextContent('done')
    expect(rule('At least 6 characters')).toHaveTextContent('not yet')
    expect(rule('An uppercase letter (A–Z)')).toHaveTextContent('not yet')

    await userEvent.type(box('New password'), 'D1!xyz')

    for (const name of ['At least 6 characters', 'An uppercase letter (A–Z)', 'A number (0–9)', 'A symbol, such as ! @ # $']) {
      expect(rule(name)).toHaveTextContent('done')
    }
  })

  it('says what a box is missing once you leave it, not while typing, and drops it once fixed', async () => {
    renderPage('/profile/security')

    const current = await screen.findByLabelText('Current password', { selector: 'input' })
    await userEvent.click(current)
    expect(current).not.toHaveAttribute('aria-invalid')
    await userEvent.tab()

    expect(current).toHaveAccessibleDescription('Enter your current password.')
    expect(current).toHaveAttribute('aria-invalid', 'true')

    await userEvent.type(current, 'Old1!pass')
    expect(current).not.toHaveAttribute('aria-invalid')
  })

  it('flags two different new passwords on leaving the second box, and clears it when they match', async () => {
    renderPage('/profile/security')

    await fillIn('Old1!pass', 'New1!pass', 'New1!pasz')
    await userEvent.tab()
    expect(box('Confirm new password')).toHaveAccessibleDescription('The two new passwords aren’t the same.')

    await userEvent.type(box('Confirm new password'), '{Backspace}s')
    expect(box('Confirm new password')).not.toHaveAttribute('aria-invalid')
  })

  it("doesn't send a new password that misses a rule, and shows which in red", async () => {
    renderPage('/profile/security')

    await fillIn('Old1!pass', 'newpass', 'newpass')
    await userEvent.click(changePassword())

    expect(changeMyPassword).not.toHaveBeenCalled()
    expect(box('New password')).toHaveAccessibleDescription(expect.stringContaining('doesn’t meet every rule below yet'))
    expect(box('New password')).toHaveFocus()
    expect(rule('An uppercase letter (A–Z)')).toHaveClass('text-destructive')
  })

  it("puts the server's reason under the box it's about, until that box is changed", async () => {
    vi.mocked(changeMyPassword)
      .mockRejectedValueOnce(new ApiError(400, { error: { code: 'Dixels:Users:WrongCurrentPassword', message: WRONG_PASSWORD } }))
      .mockRejectedValueOnce(new ApiError(400, { error: { message: 'You used this password recently.' } }))
    renderPage('/profile/security')

    await fillIn('wrong', 'New1!pass', 'New1!pass')
    await userEvent.click(changePassword())
    expect(await screen.findByText(WRONG_PASSWORD)).toBeInTheDocument()
    expect(box('Current password')).toHaveAccessibleDescription(WRONG_PASSWORD)

    await userEvent.type(box('Current password'), 'x')
    expect(screen.queryByText(WRONG_PASSWORD)).not.toBeInTheDocument()

    await userEvent.click(changePassword())
    expect(await screen.findByText('You used this password recently.')).toBeInTheDocument()
    expect(box('New password')).toHaveAccessibleDescription(expect.stringContaining('You used this password recently.'))
  })

  it('shows what was typed with the eye button', async () => {
    renderPage('/profile/security')

    const current = await screen.findByLabelText('Current password', { selector: 'input' })
    expect(current).toHaveAttribute('type', 'password')
    await userEvent.click(screen.getAllByRole('button', { name: 'Show password' })[0]!)

    expect(current).toHaveAttribute('type', 'text')
    expect(screen.getAllByRole('button', { name: 'Hide password' })).toHaveLength(1)
  })

  it('sets a first password without asking for a current one', async () => {
    vi.mocked(getMyProfile).mockResolvedValue({ ...SARA, hasPassword: false })
    vi.mocked(changeMyPassword).mockResolvedValue(undefined)
    renderPage('/profile/security')

    await fillIn('', 'New1!pass', 'New1!pass')
    expect(screen.queryByLabelText('Current password', { selector: 'input' })).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Set password' }))

    expect(changeMyPassword).toHaveBeenCalledWith('t', { currentPassword: undefined, newPassword: 'New1!pass' })
  })
})
