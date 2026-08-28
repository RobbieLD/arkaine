export default interface ThumbnailProgress {
    generated: number
    failed: number
    scanned: number
    finished: boolean
    cancelled: boolean
    currentFile: string
}

export const emptyThumbnailProgress = (): ThumbnailProgress => ({
    generated: 0,
    failed: 0,
    scanned: 0,
    finished: false,
    cancelled: false,
    currentFile: ''
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

const readBoolean = (record: Record<string, unknown> | null, keys: string[]): boolean => {
    if (!record) {
        return false
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

    return false
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

export const normalizeThumbnailProgress = (value: unknown): ThumbnailProgress => {
    const record = asRecord(value)

    return {
        generated: readNumber(record, ['generated', 'Generated']),
        failed: readNumber(record, ['failed', 'Failed']),
        scanned: readNumber(record, ['scanned', 'Scanned']),
        finished: readBoolean(record, ['finished', 'Finished']),
        cancelled: readBoolean(record, ['cancelled', 'Cancelled']),
        currentFile: readString(record, ['currentFile', 'CurrentFile'])
    }
}
