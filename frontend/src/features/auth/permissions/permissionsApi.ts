import { request } from '@/lib/api/httpClient'
import { Permissions } from './permissionNames'

/** The one slice of ABP's application configuration we read: what the signed-in user may do. */
interface ApplicationConfigurationDto {
  auth: {
    /** Every permission the backend defines, keyed by name. */
    policies: Record<string, boolean>
    /** Every permission granted to the caller (through their roles or directly), keyed by name. */
    grantedPolicies: Record<string, boolean>
  }
}

/** The names in Permissions (permissionNames.ts) that the backend doesn't define. */
export function unknownPermissionNames(definedPolicies: Record<string, boolean>): string[] {
  const used = Object.values(Permissions).flatMap((group) => Object.values(group))
  return used.filter((name) => !(name in definedPolicies))
}

/**
 * The signed-in user's granted permissions, from ABP's own /api/abp/application-configuration.
 * Any signed-in user may call it and it only ever answers for the caller — unlike the
 * permission-management API, which is for editing grants and needs admin rights.
 * Localization resources are skipped: they're most of the payload and we don't use them.
 */
export async function getGrantedPolicies(token: string): Promise<Record<string, boolean>> {
  const config = await request<ApplicationConfigurationDto>(
    '/api/abp/application-configuration?includeLocalizationResources=false',
    token,
  )
  // permissionNames.ts is a hand-kept copy of DixelsPermissions.cs. A rename on one side
  // silently hides pages on the other, so in development say so the moment the names arrive.
  if (import.meta.env.DEV) {
    const unknown = unknownPermissionNames(config.auth.policies ?? {})
    if (unknown.length > 0) {
      console.warn(`Permissions unknown to the backend (check permissionNames.ts against DixelsPermissions.cs): ${unknown.join(', ')}`)
    }
  }
  return config.auth.grantedPolicies ?? {}
}
