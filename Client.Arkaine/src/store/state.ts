import Alert from '@/models/alert'
import Progress from '@/models/progress'
import Settings from '@/models/settings'
import ThumbnailCacheStats from '@/models/thumbnail-cache-stats'
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
    settings: Settings,
    progress: Progress,
    thumbnailCache: ThumbnailCacheStats
}
