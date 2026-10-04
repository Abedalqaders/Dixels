/** Where the backend is. Shared by the API client and the localization loader. */
export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:44334'

// A production bundle must say where its API is; silently talking to localhost is a
// deployment mistake that would otherwise only show up as "Couldn't reach the server".
if (import.meta.env.PROD && !import.meta.env.VITE_API_BASE_URL) {
  throw new Error('VITE_API_BASE_URL must be set for a production build (see .env.example).')
}
