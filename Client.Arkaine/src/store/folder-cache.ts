import ArkaineFile from '@/models/arkaine-file'

export default interface FolderCacheEntry {
    files: ArkaineFile[]
    nextFile: string
    fetchedAt: number
}

export type FolderStatus = 'idle' | 'loading' | 'revalidating' | 'error'

/** How long a cached listing is trusted without a background revalidation. */
export const folderCacheTtl = 60_000

/** Upper bound on cached folders. Entries hold metadata only, never image bytes. */
export const folderCacheLimit = 50

/**
 * Normalises a route param into a single canonical cache key so that '', '/',
 * 'photos' and 'photos/' cannot each occupy their own slot.
 */
export const folderKey = (path: unknown): string => {
    const raw = Array.isArray(path) ? path.join('/') : (path ?? '')
    const trimmed = raw.toString().replace(/^\/+/, '')

    if (!trimmed) {
        return ''
    }

    return trimmed.endsWith('/') ? trimmed : `${trimmed}/`
}
