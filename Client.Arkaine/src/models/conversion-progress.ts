export interface ConversionFileProgress {
    phase: string
    percent: number | null
    elapsedSeconds: number
    mediaTimeSeconds: number | null
    durationSeconds: number | null
    speed: number | null
    frame: number | null
    bytesCompleted: number | null
    bytesTotal: number | null
    lastUpdatedUtc: string
}

export default interface ConversionProgress {
    path: string
    converted: number
    skipped: number
    failed: number
    scanned: number
    finished: boolean
    cancelled: boolean
    currentFile: string
    currentFileProgress: ConversionFileProgress | null
}

export const emptyConversionProgress = (): ConversionProgress => ({
    path: '',
    converted: 0,
    skipped: 0,
    failed: 0,
    scanned: 0,
    finished: false,
    cancelled: false,
    currentFile: '',
    currentFileProgress: null
})

const asRecord = (value: unknown): Record<string, unknown> | null => {
    return typeof value === 'object' && value !== null
        ? value as Record<string, unknown>
        : null
}

const readNumber = (record: Record<string, unknown> | null, keys: string[]): number => {
    if (!record) {
        return 0
    }

    for (const key of keys) {
        const value = record[key]

        if (typeof value === 'number' && Number.isFinite(value)) {
            return value
        }

        if (typeof value === 'string' && value.trim() !== '') {
            const parsed = Number(value)
            if (Number.isFinite(parsed)) {
                return parsed
            }
        }
    }

    return 0
}

const readNullableNumber = (
    record: Record<string, unknown> | null,
    keys: string[]
): number | null => {
    if (!record) {
        return null
    }

    for (const key of keys) {
        const value = record[key]

        if (typeof value === 'number' && Number.isFinite(value)) {
            return value
        }

        if (typeof value === 'string' && value.trim() !== '') {
            const parsed = Number(value)
            if (Number.isFinite(parsed)) {
                return parsed
            }
        }
    }

    return null
}

const readBoolean = (
    record: Record<string, unknown> | null,
    keys: string[],
    fallback = false
): boolean => {
    if (!record) {
        return fallback
    }

    for (const key of keys) {
        const value = record[key]

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
    }

    return fallback
}

const readString = (record: Record<string, unknown> | null, keys: string[]): string => {
    if (!record) {
        return ''
    }

    for (const key of keys) {
        const value = record[key]

        if (typeof value === 'string') {
            return value
        }
    }

    return ''
}

const normalizeFileProgress = (
    record: Record<string, unknown> | null
): ConversionFileProgress | null => {
    if (!record) {
        return null
    }

    return {
        phase: readString(record, ['phase', 'Phase']) || 'preparing',
        percent: readNullableNumber(record, ['percent', 'Percent']),
        elapsedSeconds: readNumber(record, ['elapsedSeconds', 'ElapsedSeconds']),
        mediaTimeSeconds: readNullableNumber(record, ['mediaTimeSeconds', 'MediaTimeSeconds']),
        durationSeconds: readNullableNumber(record, ['durationSeconds', 'DurationSeconds']),
        speed: readNullableNumber(record, ['speed', 'Speed']),
        frame: readNullableNumber(record, ['frame', 'Frame']),
        bytesCompleted: readNullableNumber(record, ['bytesCompleted', 'BytesCompleted']),
        bytesTotal: readNullableNumber(record, ['bytesTotal', 'BytesTotal']),
        lastUpdatedUtc: readString(record, ['lastUpdatedUtc', 'LastUpdatedUtc'])
    }
}

export const normalizeConversionProgress = (value: unknown): ConversionProgress => {
    const record = asRecord(value)
    const currentFileProgress = normalizeFileProgress(
        asRecord(record?.currentFileProgress ?? record?.CurrentFileProgress)
    )

    return {
        path: readString(record, ['path', 'Path', 'conversionPath', 'ConversionPath']),
        converted: readNumber(record, ['converted', 'Converted', 'generated', 'Generated']),
        skipped: readNumber(record, ['skipped', 'Skipped']),
        failed: readNumber(record, ['failed', 'Failed']),
        scanned: readNumber(record, ['scanned', 'Scanned']),
        finished: readBoolean(record, ['finished', 'Finished']),
        cancelled: readBoolean(record, ['cancelled', 'Cancelled']),
        currentFile: readString(record, ['currentFile', 'CurrentFile']),
        currentFileProgress
    }
}
