import { query, request } from '@/lib/api/httpClient'
import type { ColleagueDto } from './bookingsApi'

/**
 * People in my building whose name or email contains `filter` — never me, only active
 * users. The server answers fewer than 2 characters with an empty list.
 */
export async function searchColleagues(token: string, filter: string, maxResultCount = 10): Promise<ColleagueDto[]> {
  const result = await request<{ items: ColleagueDto[] }>('/api/app/colleagues' + query({ filter, maxResultCount }), token)
  return result.items
}

/**
 * Whether bookings may invite people from outside by email — off until guests can be told
 * by email (Dixels.Bookings.ExternalGuestsEnabled, from ABP's application configuration).
 */
export async function getExternalGuestsEnabled(token: string): Promise<boolean> {
  const config = await request<{ setting: { values: Record<string, string | undefined> } }>(
    '/api/abp/application-configuration?includeLocalizationResources=false',
    token,
  )
  return config.setting.values['Dixels.Bookings.ExternalGuestsEnabled']?.toLowerCase() === 'true'
}
