/** My profile's query keys. See lib/api/queryKeys. */
export const profileKeys = {
  profile: {
    me: () => ['profile', 'me'] as const,
    picture: () => ['profile', 'picture'] as const,
    passwordRules: () => ['profile', 'password-rules'] as const,
  },
}
