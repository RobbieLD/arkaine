import Alert from '@/models/alert'
import ArkaineFile from '@/models/arkaine-file'
import AdminStatusResponse, { emptyAdminStatus, hasEmbeddedThumbnailCache, normalizeAdminStatus } from '@/models/admin-status'
import ConversionProgress, { emptyConversionProgress, normalizeConversionProgress } from '@/models/conversion-progress'
import Tag from '@/models/tag'
import ThumbnailCacheStats, { emptyThumbnailCacheStats } from '@/models/thumbnail-cache-stats'
import ThumbnailProgress, { emptyThumbnailProgress, normalizeThumbnailProgress } from '@/models/thumbnail-progress'
import { PasskeyAssertionPayload, PasskeyRequestOptions } from '@/models/profile'
import ArkaineService from '@/services/arkaine.service'
import { serverUrl } from '@/config'
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr'
import { createPinia, defineStore } from 'pinia'
import type State from './state'
import { FolderStatus, folderCacheLimit, folderCacheTtl, folderKey } from './folder-cache'

export const pinia = createPinia()

interface FolderPayload {
    path: string
    files: ArkaineFile[]
    nextFile: string
}

/**
 * De-duplicates concurrent requests for the same folder. Hover prefetching, a double
 * click and the route change itself can all ask for the same listing at once.
 */
const inFlightFolders = new Map<string, Promise<{ files: ArkaineFile[], nextFile: string }>>()
const inFlightPages = new Set<string>()

let updatesConnection: HubConnection | null = null
let updatesConnectionTransition: Promise<void> = Promise.resolve()

const createInitialState = (): State => ({
    isAuthenticated: false,
    isAdmin: false,
    username: '',
    folders: {},
    folderOrder: [],
    currentPath: '',
    folderStatus: 'idle',
    alert: undefined,
    adminStatus: emptyAdminStatus(),
    thumbnailProgress: emptyThumbnailProgress(),
    conversionProgress: emptyConversionProgress(),
    conversionPaths: [],
    thumbnailCache: emptyThumbnailCacheStats()
})

const errorMessage = (error: unknown): string => {
    if (typeof error === 'string') {
        return error
    }

    if (error && typeof error === 'object' && 'message' in error) {
        const message = (error as { message: unknown }).message
        return typeof message === 'string' ? message : 'Something went wrong.'
    }

    return 'Something went wrong.'
}

const fetchFolder = (path: string): Promise<{ files: ArkaineFile[], nextFile: string }> => {
    const existing = inFlightFolders.get(path)

    if (existing) {
        return existing
    }

    const request = new ArkaineService()
        .Files(path, '')
        .then(response => {
            // The favourites pseudo collection only exists at the root of the library.
            if (!path) {
                response.files.unshift(ArkaineFile.Favourite)
            }

            return response
        })
        .finally(() => {
            inFlightFolders.delete(path)
        })

    inFlightFolders.set(path, request)
    return request
}

const isFresh = (state: State, path: string): boolean => {
    const entry = state.folders[path]
    return !!entry && (Date.now() - entry.fetchedAt) < folderCacheTtl
}

const touch = (state: State, path: string): void => {
    const index = state.folderOrder.indexOf(path)

    if (index >= 0) {
        state.folderOrder.splice(index, 1)
    }

    state.folderOrder.push(path)
}

const evict = (state: State): void => {
    let excess = state.folderOrder.length - folderCacheLimit

    if (excess <= 0) {
        return
    }

    const kept: string[] = []

    for (const path of state.folderOrder) {
        if (excess > 0 && path !== state.currentPath) {
            delete state.folders[path]
            excess--
            continue
        }

        kept.push(path)
    }

    state.folderOrder = kept
}

export const useAppStore = defineStore('app', {
    state: createInitialState,
    getters: {
        hasMoreFiles: (state): boolean => {
            return !!state.folders[state.currentPath]?.nextFile
        },

        files: (state): ArkaineFile[] => {
            const entry = state.folders[state.currentPath]

            if (!entry) {
                return []
            }

            // Copied before sorting: sorting in place mutates cached state from a getter.
            return [...entry.files].sort((a, b) => Number(b.isDirectory) - Number(a.isDirectory))
        },

        isLoadingFolder: (state): boolean => {
            return state.folderStatus === 'loading'
        },

        hasFolderError: (state): boolean => {
            return state.folderStatus === 'error'
        }
    },
    actions: {
        setAuthenticated(authed: boolean): void {
            this.isAuthenticated = authed
        },

        setTags(request: { file: string, tags: Tag[] }): void {
            // Tags belong to a file, not a folder, so every cached copy is kept in step.
            for (const entry of Object.values(this.folders)) {
                for (const file of entry.files) {
                    if (file.rawFileName === request.file) {
                        file.tags = request.tags
                    }
                }
            }
        },

        setFavourite(rawFileName: string): void {
            for (const entry of Object.values(this.folders)) {
                for (const file of entry.files) {
                    if (file.rawFileName === rawFileName) {
                        file.isFavourite = true
                    }
                }
            }
        },

        setAdminStatus(status: AdminStatusResponse): void {
            this.adminStatus = status
        },

        setThumbnailCache(stats: ThumbnailCacheStats): void {
            this.thumbnailCache = stats
        },

        setThumbnailRunning(running: boolean): void {
            this.adminStatus.thumbnails.isRunning = running
        },

        setConversionRunning(running: boolean): void {
            this.adminStatus.conversion.isRunning = running
        },

        setIsAdmin(isAdmin: boolean): void {
            this.isAdmin = isAdmin
        },

        setCurrentPath(path: string): void {
            this.currentPath = path
        },

        setFolderStatus(status: FolderStatus): void {
            this.folderStatus = status
        },

        setFolder(payload: FolderPayload): void {
            this.folders[payload.path] = {
                files: payload.files,
                nextFile: payload.nextFile,
                fetchedAt: Date.now()
            }

            touch(this, payload.path)
            evict(this)
        },

        appendToFolder(payload: FolderPayload): void {
            const entry = this.folders[payload.path]

            if (!entry) {
                return
            }

            entry.files.push(...payload.files)
            entry.nextFile = payload.nextFile
        },

        touchFolder(path: string): void {
            if (this.folders[path]) {
                touch(this, path)
            }
        },

        invalidateFolders(prefix: string): void {
            for (const path of Object.keys(this.folders)) {
                if (!path.startsWith(prefix)) {
                    continue
                }

                delete this.folders[path]
                const index = this.folderOrder.indexOf(path)

                if (index >= 0) {
                    this.folderOrder.splice(index, 1)
                }
            }
        },

        clearFolders(): void {
            this.folders = {}
            this.folderOrder = []
            this.folderStatus = 'idle'
        },

        setAlert(alert?: Alert): void {
            this.alert = alert
        },

        setUsername(username: string): void {
            this.username = username
        },

        setThumbnailProgress(progress: ThumbnailProgress): void {
            this.thumbnailProgress = progress
        },

        setConversionProgress(progress: ConversionProgress): void {
            this.conversionProgress = progress
        },

        setConversionPaths(paths: string[]): void {
            this.conversionPaths = paths
        },

        resetAdminState(): void {
            this.adminStatus = emptyAdminStatus()
            this.thumbnailProgress = emptyThumbnailProgress()
            this.conversionProgress = emptyConversionProgress()
            this.conversionPaths = []
            this.thumbnailCache = emptyThumbnailCacheStats()
        },

        applyAdminStatus(
            status: AdminStatusResponse,
            hydrateCache = false,
            hydrateProgress = true
        ): void {
            this.setAdminStatus(status)

            if (hydrateCache) {
                this.setThumbnailCache(status.thumbnailCache)
            }

            if (hydrateProgress) {
                const thumbnailReport = status.thumbnails.report
                if (thumbnailReport.finished || thumbnailReport.scanned > 0 || status.thumbnails.isRunning) {
                    this.setThumbnailProgress(thumbnailReport)
                }

                const conversionReport = status.conversion.report
                if (conversionReport.finished || conversionReport.scanned > 0 || status.conversion.isRunning) {
                    this.setConversionProgress(conversionReport)
                }
            }
        },

        async checkLogin(): Promise<boolean> {
            const service = new ArkaineService()
            const response = await service.LoggedIn()
            this.setAuthenticated(true)
            this.setUsername(response.username)
            this.setIsAdmin(response.isAdmin)
            return true
        },

        async deleteTag(request: { id: number, fileName: string }): Promise<void> {
            const service = new ArkaineService()
            const tags = await service.DeleteTag(request.id)
            this.setTags({ file: request.fileName, tags })
        },

        async subscribeToUpdates(): Promise<void> {
            const transition = updatesConnectionTransition.then(async () => {
                if (updatesConnection) {
                    return
                }

                const connection = new HubConnectionBuilder()
                    .withUrl(serverUrl + '/updates')
                    .withAutomaticReconnect()
                    .build()

                connection.on('update', (data: ThumbnailProgress | string) => {
                    // The same event also carries plain status strings from the ingest pipeline.
                    if (typeof data === 'string') {
                        return
                    }

                    const progress = normalizeThumbnailProgress(data)
                    this.setThumbnailProgress(progress)
                    this.setThumbnailRunning(!progress.finished)
                })

                connection.on('convert', (data: ConversionProgress | string) => {
                    if (typeof data === 'string') {
                        return
                    }

                    const progress = normalizeConversionProgress(data)
                    this.setConversionProgress(progress)
                    this.setConversionRunning(!progress.finished)
                })

                connection.onreconnected(async () => {
                    try {
                        await Promise.all([
                            this.loadAdminStatus(),
                            this.loadThumbnailCacheStats()
                        ])
                    }
                    catch (e) {
                        this.setAlert({
                            isError: true,
                            message: errorMessage(e)
                        })
                    }
                })

                connection.onclose(() => {
                    if (updatesConnection === connection) {
                        updatesConnection = null
                    }
                })

                updatesConnection = connection

                try {
                    await connection.start()
                }
                catch (e) {
                    if (updatesConnection === connection) {
                        updatesConnection = null
                    }
                    throw e
                }
            })

            updatesConnectionTransition = transition.catch(() => undefined)
            await transition
        },

        async unsubscribeFromUpdates(): Promise<void> {
            const transition = updatesConnectionTransition.then(async () => {
                const connection = updatesConnection
                if (!connection) {
                    return
                }

                try {
                    await connection.stop()
                }
                finally {
                    if (updatesConnection === connection) {
                        updatesConnection = null
                    }
                }
            })

            updatesConnectionTransition = transition.catch(() => undefined)
            await transition
        },

        async startThumbnails(): Promise<void> {
            const service = new ArkaineService()
            const response = await service.StartThumbnails()
            this.setThumbnailProgress(emptyThumbnailProgress())
            this.setThumbnailRunning(true)
            if (response) {
                this.applyAdminStatus(normalizeAdminStatus(response), hasEmbeddedThumbnailCache(response))
            }
            else {
                await this.loadAdminStatus()
            }
        },

        async stopThumbnails(): Promise<void> {
            const service = new ArkaineService()
            const response = await service.StopThumbnails()
            this.setThumbnailProgress({
                ...this.thumbnailProgress,
                finished: true,
                cancelled: true,
                currentFile: ''
            })
            this.setThumbnailRunning(false)
            if (response) {
                this.applyAdminStatus(normalizeAdminStatus(response), hasEmbeddedThumbnailCache(response), false)
            }
            else {
                await this.loadAdminStatus()
            }
        },

        async startConversion(path: string, deleteConvertedFiles: boolean): Promise<void> {
            const service = new ArkaineService()
            const response = await service.StartConversion(path, deleteConvertedFiles)
            this.setConversionProgress(emptyConversionProgress())
            this.setConversionRunning(true)
            if (response) {
                this.applyAdminStatus(normalizeAdminStatus(response), hasEmbeddedThumbnailCache(response))
            }
            else {
                await this.loadAdminStatus()
            }
        },

        async loadConversionPaths(): Promise<void> {
            const service = new ArkaineService()
            const paths = await service.GetConversionPaths()
            this.setConversionPaths(paths)
        },

        async stopConversion(): Promise<void> {
            const service = new ArkaineService()
            const response = await service.StopConversion()
            this.setConversionProgress({
                ...this.conversionProgress,
                finished: true,
                cancelled: true,
                currentFile: ''
            })
            this.setConversionRunning(false)
            if (response) {
                this.applyAdminStatus(normalizeAdminStatus(response), hasEmbeddedThumbnailCache(response), false)
            }
            else {
                await this.loadAdminStatus()
            }
        },

        async loadAdminStatus(): Promise<void> {
            const service = new ArkaineService()
            const response = await service.GetAdminStatus()
            this.applyAdminStatus(normalizeAdminStatus(response), hasEmbeddedThumbnailCache(response))
        },

        async loadThumbnailCacheStats(): Promise<void> {
            const service = new ArkaineService()
            const response = await service.GetThumbnailCacheStats()
            this.setThumbnailCache(response)
        },

        async clearThumbnailCache(): Promise<void> {
            const service = new ArkaineService()
            const response = await service.ClearThumbnailCache()
            this.setThumbnailCache(response)
        },

        async logout(): Promise<void> {
            const service = new ArkaineService()
            try {
                await service.Logout()
            }
            finally {
                await this.unsubscribeFromUpdates()
                this.setUsername('')
                this.setAuthenticated(false)
                this.setIsAdmin(false)
                this.clearFolders()
                this.setCurrentPath('')
                this.resetAdminState()
            }
        },

        async login(payload: { username: string, password: string, remember: boolean }): Promise<boolean> {
            const service = new ArkaineService()
            return await service.Login(payload.username, payload.password, payload.remember)
        },

        async passkeyRequestOptions(username?: string): Promise<PasskeyRequestOptions> {
            const service = new ArkaineService()
            return await service.GetPasskeyRequestOptions(username)
        },

        async passkeyLogin(payload: { credential: PasskeyAssertionPayload, remember: boolean }): Promise<void> {
            const service = new ArkaineService()
            await service.PasskeyLogin(payload.credential, payload.remember)
        },

        async twoFactorAuth(payload: { code: string, remember: boolean }): Promise<void> {
            const service = new ArkaineService()
            await service.TwoFactorAuth(payload.code, payload.remember)
        },

        /**
         * Stale-while-revalidate. The current path is committed synchronously so the
         * gallery swaps to the target folder - cached content or skeletons - before any
         * network work begins. Navigation must never wait on a round trip.
         */
        async loadFiles(rawPath: unknown): Promise<void> {
            const path = folderKey(rawPath)

            this.setCurrentPath(path)
            this.setAlert(undefined)

            if (this.folders[path]) {
                this.touchFolder(path)

                if (isFresh(this, path)) {
                    this.setFolderStatus('idle')
                    return
                }

                this.setFolderStatus('revalidating')

                try {
                    const response = await fetchFolder(path)
                    this.setFolder({ path, files: response.files, nextFile: response.nextFile })
                }
                catch {
                    // A background refresh failing is not worth blanking a folder the
                    // user is already looking at, so the cached listing stays on screen.
                }
                finally {
                    if (this.currentPath === path) {
                        this.setFolderStatus('idle')
                    }
                }

                return
            }

            this.setFolderStatus('loading')

            try {
                const response = await fetchFolder(path)
                this.setFolder({ path, files: response.files, nextFile: response.nextFile })

                if (this.currentPath === path) {
                    this.setFolderStatus('idle')
                }
            }
            catch (e) {
                if (this.currentPath === path) {
                    this.setFolderStatus('error')
                    this.setAlert({ isError: true, message: errorMessage(e) })
                }

                throw e
            }
        },

        /** Warms the cache for a folder the user is about to open. Never touches the view. */
        async prefetchFolder(rawPath: unknown): Promise<void> {
            const path = folderKey(rawPath)

            if (isFresh(this, path) || inFlightFolders.has(path)) {
                return
            }

            try {
                const response = await fetchFolder(path)
                this.setFolder({ path, files: response.files, nextFile: response.nextFile })
            }
            catch {
                // Prefetching is best effort - a failure just means no warm cache.
            }
        },

        async addTag(request: { name: string, file: string, time: string }): Promise<void> {
            try {
                const service = new ArkaineService()
                let seconds = 0

                if (request.time) {
                    const parts = request.time.split(':')

                    for (let i = parts.length - 1; i >= 0; i--) {
                        const exp = Math.pow(60, (parts.length - i) - 1)
                        seconds += Number.parseInt(parts[i]) * exp
                    }
                }

                const tags = await service.AddTag(request.name, request.file, seconds)

                this.setTags({ file: request.file, tags })
            }
            catch (e) {
                this.setAlert({
                    isError: true,
                    message: errorMessage(e)
                })

                throw e
            }
        },

        async addToFavourite(file: ArkaineFile): Promise<void> {
            try {
                const service = new ArkaineService()
                await service.AddToFavourites(file)
                this.setFavourite(file.rawFileName)
                // The favourites collection has changed, so drop its cached listing.
                this.invalidateFolders('Favourites')
            }
            catch (e) {
                this.setAlert({
                    isError: true,
                    message: errorMessage(e)
                })

                throw e
            }
        },

        async loadMoreFiles(rawPath: unknown): Promise<void> {
            const path = folderKey(rawPath)
            const entry = this.folders[path]

            if (!entry?.nextFile) {
                return
            }

            const pageKey = `${path}|${entry.nextFile}`

            if (inFlightPages.has(pageKey)) {
                return
            }

            inFlightPages.add(pageKey)

            try {
                const service = new ArkaineService()
                const response = await service.Files(path, entry.nextFile)
                this.appendToFolder({ path, files: response.files, nextFile: response.nextFile })
                this.setAlert(undefined)
            }
            catch (e) {
                this.setAlert({
                    isError: true,
                    message: errorMessage(e)
                })

                throw e
            }
            finally {
                inFlightPages.delete(pageKey)
            }
        }
    }
})

export const store = useAppStore(pinia)
