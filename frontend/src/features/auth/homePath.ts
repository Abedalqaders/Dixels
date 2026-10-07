/** Where "home" is: a route that waits for the user's grants, then sends them to landingFor().
 * Its own file so the permission gate can link home without importing the module registry
 * (landing.ts does, and the registry's routes use the gate). */
export const HOME_PATH = '/home'
