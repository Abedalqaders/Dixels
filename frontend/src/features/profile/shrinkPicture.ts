/** The side of the square every profile picture is stored at: sharp at the largest size it
 * is shown (88 px on My profile) on a high-density screen, and only a few dozen KB. */
export const PICTURE_SIZE = 256

/**
 * A picture the person picked, made ready to upload: cropped to its centre square, shrunk to
 * PICTURE_SIZE, and saved as a JPEG (on white, so a transparent PNG doesn't turn black). A
 * phone photo of several MB comes out well under the server's 1 MB limit.
 *
 * Rejects when the browser can't open the file as a picture.
 */
export async function shrinkPicture(file: Blob): Promise<Blob> {
  const bitmap = await createImageBitmap(file)
  try {
    const side = Math.min(bitmap.width, bitmap.height)
    const canvas = document.createElement('canvas')
    canvas.width = PICTURE_SIZE
    canvas.height = PICTURE_SIZE
    const context = canvas.getContext('2d')
    if (!context) throw new Error('No 2D canvas')
    context.fillStyle = '#ffffff'
    context.fillRect(0, 0, PICTURE_SIZE, PICTURE_SIZE)
    context.imageSmoothingQuality = 'high'
    context.drawImage(bitmap, (bitmap.width - side) / 2, (bitmap.height - side) / 2, side, side, 0, 0, PICTURE_SIZE, PICTURE_SIZE)
    return await new Promise<Blob>((resolve, reject) =>
      canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('Could not encode the picture'))), 'image/jpeg', 0.9),
    )
  } finally {
    bitmap.close()
  }
}
