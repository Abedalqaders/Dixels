import { useRef, useState } from 'react'
import type { ChangeEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useQueryClient } from '@tanstack/react-query'
import { CameraIcon, ImageUpIcon, TrashIcon } from 'lucide-react'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { UserAvatar } from '@/components/UserAvatar'
import { useToast } from '@/components/Toast'
import { queryKeys } from '@/lib/api/queryKeys'
import { ApiError, removeMyPicture, setMyPicture } from '@/features/profile/api/profileApi'
import { useMyPictureUrl } from '@/features/profile/hooks/useMyPicture'
import { PICTURE_SIZE, shrinkPicture } from '@/features/profile/shrinkPicture'

/**
 * My profile's big avatar with its camera button. Without a picture the button opens the file
 * picker straight away; with one it offers a new one or removing it. The picture is cropped
 * and shrunk here before it's sent (shrinkPicture), and the saved one is put straight into the
 * shared cache, so the sidebar shows it at once. Clicking the picture opens it larger.
 */
export function ProfilePhoto({ name, token }: { name: string; token: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const pictureUrl = useMyPictureUrl()
  const fileInput = useRef<HTMLInputElement>(null)
  const [busy, setBusy] = useState(false)
  const [viewing, setViewing] = useState(false)

  const pickFile = () => fileInput.current?.click()

  async function handleFile(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    // Let the same file be picked again after a failure.
    e.target.value = ''
    if (!file) return
    if (!file.type.startsWith('image/')) {
      showToast(t('Profile:PhotoNotImage'), 'error')
      return
    }

    setBusy(true)
    try {
      let picture: Blob
      try {
        picture = await shrinkPicture(file)
      } catch {
        showToast(t('Profile:PhotoUnreadable'), 'error')
        return
      }
      await setMyPicture(token, picture)
      queryClient.setQueryData(queryKeys.profile.picture(), picture)
      showToast(t('Profile:PhotoUpdated'))
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : t('Error:Generic'), 'error')
    } finally {
      setBusy(false)
    }
  }

  async function handleRemove() {
    setBusy(true)
    try {
      await removeMyPicture(token)
      queryClient.setQueryData(queryKeys.profile.picture(), null)
      showToast(t('Profile:PhotoRemoved'))
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : t('Error:Generic'), 'error')
    } finally {
      setBusy(false)
    }
  }

  // With a picture the menu opens on click (it wraps this button); without one, the picker does.
  const cameraButton = (
    <button
      type="button"
      className="absolute -end-0.5 -bottom-0.5 flex size-8 cursor-pointer items-center justify-center rounded-full border-[3px] border-card bg-foreground text-background disabled:cursor-wait disabled:opacity-60"
      aria-label={t('Profile:ChangePhoto')}
      title={t('Profile:ChangePhoto')}
      disabled={busy}
      onClick={pictureUrl ? undefined : pickFile}
    >
      <CameraIcon className="size-3.5" aria-hidden />
    </button>
  )

  return (
    <div className="relative flex-none" aria-busy={busy}>
      {pictureUrl ? (
        <button
          type="button"
          className="block cursor-zoom-in rounded-full focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none"
          aria-label={t('Profile:ViewPhoto')}
          onClick={() => setViewing(true)}
        >
          <UserAvatar name={name} pictureUrl={pictureUrl} className={`lg${busy ? ' opacity-60' : ''}`} />
        </button>
      ) : (
        <UserAvatar name={name} pictureUrl={pictureUrl} className={`lg${busy ? ' opacity-60' : ''}`} />
      )}
      {pictureUrl ? (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>{cameraButton}</DropdownMenuTrigger>
          <DropdownMenuContent align="start">
            <DropdownMenuItem onSelect={pickFile}>
              <ImageUpIcon aria-hidden />
              {t('Profile:UploadPhoto')}
            </DropdownMenuItem>
            <DropdownMenuItem variant="destructive" onSelect={() => void handleRemove()}>
              <TrashIcon aria-hidden />
              {t('Profile:RemovePhoto')}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      ) : (
        cameraButton
      )}
      {/* Shown at the size it's stored at (PICTURE_SIZE): any bigger and it would only blur. */}
      <Dialog open={viewing && Boolean(pictureUrl)} onOpenChange={setViewing}>
        <DialogContent className="w-auto gap-3 p-4 sm:max-w-none">
          <DialogHeader className="pe-6">
            <DialogTitle className="text-base">{name}</DialogTitle>
          </DialogHeader>
          {pictureUrl && (
            <img src={pictureUrl} alt={name} width={PICTURE_SIZE} height={PICTURE_SIZE} className="max-w-full rounded-lg" />
          )}
        </DialogContent>
      </Dialog>
      <input ref={fileInput} type="file" accept="image/*" hidden onChange={(e) => void handleFile(e)} data-testid="photo-input" />
    </div>
  )
}
