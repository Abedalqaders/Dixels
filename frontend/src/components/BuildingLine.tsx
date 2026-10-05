import { MapPinIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { TextSkeleton } from '@/components/LoadingSkeletons'

/**
 * The line under a booking page's title saying which building it's about: a pin, the
 * building's name, then details such as its timezone. Plain text — there's only ever one
 * building, so nothing here is picked or clicked.
 */
export function BuildingLine({ name, details, loading }: { name?: string; details?: string; loading?: boolean }) {
  const { t } = useTranslation()
  return (
    <p className="buildingline">
      <MapPinIcon aria-hidden="true" />
      {loading ? (
        <TextSkeleton label={t('Common:LoadingBuilding')} />
      ) : (
        <>
          <b>{name}</b>
          {details && <span>· {details}</span>}
        </>
      )}
    </p>
  )
}
