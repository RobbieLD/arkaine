import ThumbnailCacheStats, { emptyThumbnailCacheStats } from '@/models/thumbnail-cache-stats'
import type ConversionProgress from '@/models/conversion-progress'
import { emptyConversionProgress, normalizeConversionProgress } from '@/models/conversion-progress'
import type ThumbnailProgress from '@/models/thumbnail-progress'
import { emptyThumbnailProgress, normalizeThumbnailProgress } from '@/models/thumbnail-progress'

export interface ThumbnailJobStatus {
    totalThumbnails: number
    thumbnailDir: string
    thumbnailExtensions: string
    thumbnailPageSize: number
    thumbnailWidth: number
    isRunning: boolean
    report: ThumbnailProgress
}

export interface ConversionJobStatus {
    isRunning: boolean
    ffmpegAvailable: boolean | null
    ffmpegPath: string
    conversionTempDir: string
    conversionPageSize: number
    imageExtensions: string
    videoExtensions: string
    report: ConversionProgress
}

export default interface AdminStatusResponse {
    thumbnails: ThumbnailJobStatus
    conversion: ConversionJobStatus
    thumbnailCache: ThumbnailCacheStats
}

interface LegacyAdminStatusResponse {
    totalThumbnails?: unknown
    thumbnailDir?: unknown
    thumbnailExtensions?: unknown
    thumbnailPageSize?: unknown
    thumbnailWidth?: unknown
    isRunning?: unknown
    ffmpegAvailable?: unknown
    ffmpegPath?: unknown
    conversionTempDir?: unknown
    conversionPageSize?: unknown
    imageExtensions?: unknown
    videoExtensions?: unknown
    thumbnailCache?: unknown
    cache?: unknown
    thumbnailCacheStats?: unknown
    ffmpeg?: unknown
    Ffmpeg?: unknown
    conversionRunning?: unknown
    isConversionRunning?: unknown
    thumbnails?: unknown
    thumbnail?: unknown
    thumbnailJob?: unknown
    conversion?: unknown
    convert?: unknown
    conversionJob?: unknown
}

const emptyThumbnailJobStatus = (): ThumbnailJobStatus => ({
    totalThumbnails: 0,
    thumbnailDir: '',
    thumbnailExtensions: '',
    thumbnailPageSize: 0,
    thumbnailWidth: 0,
    isRunning: false,
    report: emptyThumbnailProgress()
})

const emptyConversionJobStatus = (): ConversionJobStatus => ({
    isRunning: false,
    ffmpegAvailable: null,
    ffmpegPath: '',
    conversionTempDir: '',
    conversionPageSize: 0,
    imageExtensions: '',
    videoExtensions: '',
    report: emptyConversionProgress()
})

export const emptyAdminStatus = (): AdminStatusResponse => ({
    thumbnails: emptyThumbnailJobStatus(),
    conversion: emptyConversionJobStatus(),
    thumbnailCache: emptyThumbnailCacheStats()
})

const asRecord = (value: unknown): Record<string, unknown> | null => {
    return typeof value === 'object' && value !== null
        ? value as Record<string, unknown>
        : null
}

const readValue = (record: Record<string, unknown> | null, keys: string[]): unknown => {
    if (!record) {
        return undefined
    }

    for (const key of keys) {
        if (key in record) {
            return record[key]
        }
    }

    return undefined
}

const readString = (record: Record<string, unknown> | null, keys: string[], fallback = ''): string => {
    const value = readValue(record, keys)
    return typeof value === 'string' ? value : fallback
}

const readNumber = (record: Record<string, unknown> | null, keys: string[], fallback = 0): number => {
    const value = readValue(record, keys)

    if (typeof value === 'number' && Number.isFinite(value)) {
        return value
    }

    if (typeof value === 'string' && value.trim() !== '') {
        const parsed = Number(value)
        return Number.isFinite(parsed) ? parsed : fallback
    }

    return fallback
}

const readBoolean = (
    record: Record<string, unknown> | null,
    keys: string[],
    fallback: boolean | null = false
): boolean | null => {
    const value = readValue(record, keys)

    if (typeof value === 'boolean') {
        return value
    }

    if (typeof value === 'string') {
        if (value.toLowerCase() === 'true') {
            return true
        }

        if (value.toLowerCase() === 'false') {
            return false
        }
    }

    return fallback
}

const readNestedRecord = (record: Record<string, unknown> | null, keys: string[]): Record<string, unknown> | null => {
    return asRecord(readValue(record, keys))
}

const readNullableString = (record: Record<string, unknown> | null, keys: string[]): string | null => {
    const value = readValue(record, keys)
    return typeof value === 'string' ? value : null
}

const hasAnyValue = (record: Record<string, unknown> | null, keys: string[]): boolean => {
    if (!record) {
        return false
    }

    return keys.some(key => typeof record[key] !== 'undefined')
}

const normalizeThumbnailStatus = (
    source: Record<string, unknown> | null
): ThumbnailJobStatus => ({
    totalThumbnails: readNumber(source, ['totalThumbnails', 'TotalThumbnails']),
    thumbnailDir: readString(source, ['thumbnailDir', 'ThumbnailDir']),
    thumbnailExtensions: readString(source, ['thumbnailExtensions', 'ThumbnailExtensions']),
    thumbnailPageSize: readNumber(source, ['thumbnailPageSize', 'ThumbnailPageSize']),
    thumbnailWidth: readNumber(source, ['thumbnailWidth', 'ThumbnailWidth']),
    isRunning: readBoolean(
        source,
        ['isRunning', 'IsRunning', 'running', 'Running', 'thumbnailRunning', 'ThumbnailRunning', 'isThumbnailRunning', 'IsThumbnailRunning'],
        false
    ) ?? false,
    report: normalizeThumbnailProgress(readValue(source, ['report', 'Report']))
})

const normalizeConversionStatus = (
    source: Record<string, unknown> | null,
    allowGenericRunning: boolean,
    ffmpegSource: Record<string, unknown> | null
): ConversionJobStatus => ({
    isRunning: readBoolean(
        source,
        allowGenericRunning
            ? ['isRunning', 'IsRunning', 'running', 'Running']
            : ['conversionRunning', 'ConversionRunning', 'isConversionRunning', 'IsConversionRunning'],
        false
    ) ?? false,
    ffmpegAvailable: readBoolean(
        source,
        ['ffmpegAvailable', 'FfmpegAvailable'],
        readBoolean(ffmpegSource, ['isAvailable', 'IsAvailable'], null)
    ),
    ffmpegPath: readString(
        source,
        ['ffmpegPath', 'FfmpegPath'],
        readString(ffmpegSource, ['executablePath', 'ExecutablePath'])
    ),
    conversionTempDir: readString(source, ['conversionTempDir', 'ConversionTempDir']),
    conversionPageSize: readNumber(source, ['conversionPageSize', 'ConversionPageSize']),
    imageExtensions: readString(source, ['imageExtensions', 'ImageExtensions']),
    videoExtensions: readString(source, ['videoExtensions', 'VideoExtensions']),
    report: normalizeConversionProgress(readValue(source, ['report', 'Report']))
})

const normalizeThumbnailCache = (value: unknown): ThumbnailCacheStats => {
    const source = asRecord(value)

    return {
        entries: readNumber(source, ['entries', 'Entries']),
        capacity: readNumber(source, ['capacity', 'Capacity']),
        hits: readNumber(source, ['hits', 'Hits']),
        misses: readNumber(source, ['misses', 'Misses']),
        stale: readNumber(source, ['stale', 'Stale']),
        unreadable: readNumber(source, ['unreadable', 'Unreadable']),
        notFound: readNumber(source, ['notFound', 'NotFound']),
        resets: readNumber(source, ['resets', 'Resets']),
        startedUtc: readString(source, ['startedUtc', 'StartedUtc']),
        lastResetUtc: readNullableString(source, ['lastResetUtc', 'LastResetUtc'])
    }
}

export const hasEmbeddedThumbnailCache = (value: unknown): boolean => {
    const root = asRecord(value)
    return typeof readValue(root, ['thumbnailCache', 'cache', 'thumbnailCacheStats']) !== 'undefined'
}

export const normalizeAdminStatus = (value: LegacyAdminStatusResponse | unknown): AdminStatusResponse => {
    const root = asRecord(value)
    const nestedThumbnails = readNestedRecord(root, ['thumbnails', 'thumbnail', 'thumbnailJob'])
    const nestedConversion = readNestedRecord(root, ['conversion', 'convert', 'conversionJob'])
    const nestedThumbnailCache = readValue(root, ['thumbnailCache', 'cache', 'thumbnailCacheStats'])
    const nestedFfmpeg = readNestedRecord(root, ['ffmpeg', 'Ffmpeg'])

    const thumbnailSource = nestedThumbnails ?? root
    const conversionSource = nestedConversion ?? root

    const hasThumbnailFields = hasAnyValue(root, [
        'totalThumbnails',
        'TotalThumbnails',
        'thumbnailDir',
        'ThumbnailDir'
    ])
    const hasConversionFields = hasAnyValue(root, [
        'ffmpegAvailable',
        'FfmpegAvailable',
        'ffmpegPath',
        'FfmpegPath',
        'conversionTempDir',
        'ConversionTempDir',
        'conversionPageSize',
        'ConversionPageSize',
        'imageExtensions',
        'ImageExtensions',
        'videoExtensions',
        'VideoExtensions',
        'conversionRunning',
        'ConversionRunning',
        'isConversionRunning',
        'IsConversionRunning',
        'ffmpeg',
        'Ffmpeg'
    ])

    return {
        thumbnails: hasThumbnailFields || nestedThumbnails
            ? normalizeThumbnailStatus(thumbnailSource)
            : emptyThumbnailJobStatus(),
        conversion: hasConversionFields || nestedConversion
            ? normalizeConversionStatus(conversionSource, !!nestedConversion, nestedFfmpeg)
            : emptyConversionJobStatus(),
        thumbnailCache: nestedThumbnailCache
            ? normalizeThumbnailCache(nestedThumbnailCache)
            : emptyThumbnailCacheStats()
    }
}
