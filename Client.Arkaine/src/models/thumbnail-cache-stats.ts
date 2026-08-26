export default interface ThumbnailCacheStats {
    entries: number
    capacity: number
    hits: number
    misses: number
    stale: number
    unreadable: number
    notFound: number
    resets: number
    startedUtc: string
    lastResetUtc: string | null
}

export const emptyThumbnailCacheStats = (): ThumbnailCacheStats => ({
    entries: 0,
    capacity: 0,
    hits: 0,
    misses: 0,
    stale: 0,
    unreadable: 0,
    notFound: 0,
    resets: 0,
    startedUtc: '',
    lastResetUtc: null
})
