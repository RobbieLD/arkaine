import Alert from '@/models/alert'
import type AdminStatusResponse from '@/models/admin-status'
import type ConversionProgress from '@/models/conversion-progress'
import ThumbnailCacheStats from '@/models/thumbnail-cache-stats'
import type ThumbnailProgress from '@/models/thumbnail-progress'
import FolderCacheEntry, { FolderStatus } from './folder-cache'

export default interface State {
    isAuthenticated: boolean,
    isAdmin: boolean,
    username: string,
    /** Listings keyed by normalised folder path. `folderOrder` tracks LRU order. */
    folders: Record<string, FolderCacheEntry>,
    folderOrder: string[],
    currentPath: string,
    folderStatus: FolderStatus,
    alert?: Alert,
    adminStatus: AdminStatusResponse,
    thumbnailProgress: ThumbnailProgress,
    conversionProgress: ConversionProgress,
    thumbnailCache: ThumbnailCacheStats
}
