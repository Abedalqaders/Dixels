// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { act, render, waitFor } from '@testing-library/react'
import { useAuth } from 'react-oidc-context'
import i18n from '@/i18n'
import { LanguageSync } from './LanguageSync'
import { saveMyLanguage } from './myLanguageApi'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))
vi.mock('./myLanguageApi', () => ({ saveMyLanguage: vi.fn() }))

function signedInAs(sub: string, token = `token-${sub}`) {
  vi.mocked(useAuth).mockReturnValue({ user: { profile: { sub }, access_token: token } } as unknown as ReturnType<typeof useAuth>)
}

function signedOut() {
  vi.mocked(useAuth).mockReturnValue({ user: null } as unknown as ReturnType<typeof useAuth>)
}

describe('LanguageSync', () => {
  beforeEach(async () => {
    vi.mocked(saveMyLanguage).mockReset().mockResolvedValue(undefined)
    await i18n.changeLanguage('en')
  })

  it("saves the signed-in user's language", async () => {
    signedInAs('u1')
    render(<LanguageSync />)

    await waitFor(() => expect(saveMyLanguage).toHaveBeenCalledWith('token-u1', 'en'))
  })

  it('saves it again when they switch language', async () => {
    signedInAs('u1')
    render(<LanguageSync />)
    await act(() => i18n.changeLanguage('ar'))

    await waitFor(() => expect(saveMyLanguage).toHaveBeenLastCalledWith('token-u1', 'ar'))
    expect(saveMyLanguage).toHaveBeenCalledTimes(2)
  })

  it('sends nothing more when only the token renews', async () => {
    signedInAs('u1')
    const { rerender } = render(<LanguageSync />)
    signedInAs('u1', 'renewed')
    rerender(<LanguageSync />)

    await waitFor(() => expect(saveMyLanguage).toHaveBeenCalledTimes(1))
  })

  it('tries again with the next token after a failed save', async () => {
    vi.mocked(saveMyLanguage).mockRejectedValueOnce(new Error('offline'))
    signedInAs('u1')
    const { rerender } = render(<LanguageSync />)
    await waitFor(() => expect(saveMyLanguage).toHaveBeenCalledTimes(1))

    signedInAs('u1', 'renewed')
    rerender(<LanguageSync />)

    await waitFor(() => expect(saveMyLanguage).toHaveBeenLastCalledWith('renewed', 'en'))
  })

  it('sends nothing while signed out', () => {
    signedOut()
    render(<LanguageSync />)

    expect(saveMyLanguage).not.toHaveBeenCalled()
  })
})
