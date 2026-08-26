import Alert from '@/models/alert'
import ArkaineService from '@/services/arkaine.service'
import { InjectionKey } from 'vue'
import { createStore, Store } from 'vuex'
import State from './state'
import ArkaineFile from '@/models/arkaine-file'
import Settings from '@/models/settings'
import Progress from '@/models/progress'
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr'
import Tag from '@/models/tag'
import ThumbnailCacheStats, { emptyThumbnailCacheStats } from '@/models/thumbnail-cache-stats'
import { PasskeyAssertionPayload, PasskeyRequestOptions } from '@/models/profile'
import { serverUrl } from '@/config'
import { FolderStatus, folderCacheLimit, folderCacheTtl, folderKey } from './folder-cache'

export const storeKey: InjectionKey<Store<State>> = Symbol('store')

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

export const store = createStore<State>({
    state: {
        isAuthenticated: false,
        isAdmin: false,
        username: '',
        folders: {},
        folderOrder: [],
        currentPath: '',
        folderStatus: 'idle',
        settings: {
            totalThumbnails: 0,
            thumbnailDir: '',
            thumbnailExtensions: '',
            thumbnailPageSize: 0,
            thumbnailWidth: 0,
            isRunning: false,
            badThumbnails: 0
        },
        progress: {
            failed: 0,
            generated: 0,
            scanned: 0,
            finished: false
        },
        thumbnailCache: emptyThumbnailCacheStats()
    },
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
    mutations: {
        setAuthenticated: (state: State, authed: boolean): void => {
            state.isAuthenticated = authed
        },

        setTags: (state: State, request: { file: string, tags: Tag[] }): void => {
            // Tags belong to a file, not a folder, so every cached copy is kept in step.
            for (const entry of Object.values(state.folders)) {
                for (const file of entry.files) {
                    if (file.rawFileName === request.file) {
                        file.tags = request.tags
                    }
                }
            }
        },

        setFavourite: (state: State, rawFileName: string): void => {
            for (const entry of Object.values(state.folders)) {
                for (const file of entry.files) {
                    if (file.rawFileName === rawFileName) {
                        file.isFavourite = true
                    }
                }
            }
        },

        setSettings: (state: State, settings: Settings): void => {
            state.settings = settings
        },

        setThumbnailCache: (state: State, stats: ThumbnailCacheStats): void => {
            state.thumbnailCache = stats
        },

        setRunning: (state: State, running: boolean): void => {
            state.settings.isRunning = running
        },

        setIsAdmin: (state: State, isAdmin: boolean): void => {
            state.isAdmin = isAdmin
        },

        setCurrentPath: (state: State, path: string): void => {
            state.currentPath = path
        },

        setFolderStatus: (state: State, status: FolderStatus): void => {
            state.folderStatus = status
        },

        setFolder: (state: State, payload: FolderPayload): void => {
            state.folders[payload.path] = {
                files: payload.files,
                nextFile: payload.nextFile,
                fetchedAt: Date.now()
            }

            touch(state, payload.path)
            evict(state)
        },

        appendToFolder: (state: State, payload: FolderPayload): void => {
            const entry = state.folders[payload.path]

            if (!entry) {
                return
            }

            entry.files.push(...payload.files)
            entry.nextFile = payload.nextFile
        },

        touchFolder: (state: State, path: string): void => {
            if (state.folders[path]) {
                touch(state, path)
            }
        },

        invalidateFolders: (state: State, prefix: string): void => {
            for (const path of Object.keys(state.folders)) {
                if (!path.startsWith(prefix)) {
                    continue
                }

                delete state.folders[path]
                const index = state.folderOrder.indexOf(path)

                if (index >= 0) {
                    state.folderOrder.splice(index, 1)
                }
            }
        },

        clearFolders: (state: State): void => {
            state.folders = {}
            state.folderOrder = []
            state.folderStatus = 'idle'
        },

        setAlert: (state: State, alert?: Alert): void => {
            state.alert = alert
        },

        setUsername: (state: State, username: string): void => {
            state.username = username
        },

        setProgress: (state: State, progress: Progress): void => {
            state.progress = progress
        },
    },
    actions: {
        checkLogin: async ({ commit }): Promise<boolean> => {
            const service = new ArkaineService()
            const response = await service.LoggedIn()
            commit('setAuthenticated', true)
            commit('setUsername', response.username)
            commit('setIsAdmin', response.isAdmin)
            return true
        },

        deleteTag: async ({ commit }, request: { id: number, fileName: string }): Promise<void> => {
            const service = new ArkaineService()
            const tags = await service.DeleteTag(request.id)
            commit('setTags', { file: request.fileName, tags })
        },

        subscribeToUpdates: async ({ commit }): Promise<void> => {
            if (updatesConnection) {
                return
            }

            const connection = new HubConnectionBuilder()
                .withUrl(serverUrl + '/updates')
                .withAutomaticReconnect()
                .build()

            connection.on('update', (data: Progress | string) => {
                // The same event also carries plain status strings from the ingest pipeline.
                if (typeof data !== 'object' || data === null) {
                    return
                }

                commit('setProgress', data)
                commit('setRunning', !data.finished)
            })

            connection.onclose(() => {
                updatesConnection = null
            })

            updatesConnection = connection

            try {
                await connection.start()
            }
            catch (e) {
                updatesConnection = null
                throw e
            }
        },

        unsubscribeFromUpdates: async (): Promise<void> => {
            const connection = updatesConnection
            updatesConnection = null
            await connection?.stop()
        },

        startGeneration: async ({ commit }): Promise<void> => {
            const service = new ArkaineService()
            await service.Start()
            commit('setRunning', true)
        },

        cancelGeneration: async ({ commit }): Promise<void> => {
            const service = new ArkaineService()
            await service.Stop()
            commit('setRunning', false)
        },

        loadSettings: async ({ commit }): Promise<void> => {
            const service = new ArkaineService()
            const response = await service.GetSettings()
            commit('setSettings', response)
        },

        loadThumbnailCacheStats: async ({ commit }): Promise<void> => {
            const service = new ArkaineService()
            const response = await service.GetThumbnailCacheStats()
            commit('setThumbnailCache', response)
        },

        clearThumbnailCache: async ({ commit }): Promise<void> => {
            const service = new ArkaineService()
            const response = await service.ClearThumbnailCache()
            commit('setThumbnailCache', response)
        },

        logout: async ({ commit, dispatch }): Promise<void> => {
            const service = new ArkaineService()
            await service.Logout()
            await dispatch('unsubscribeFromUpdates')
            commit('setUsername', '')
            commit('setAuthenticated', false)
            commit('clearFolders')
            commit('setCurrentPath', '')
        },

        login: async (_, payload: { username: string, password: string, remember: boolean }): Promise<boolean> => {
            const service = new ArkaineService()
            return await service.Login(payload.username, payload.password, payload.remember)
        },

        passkeyRequestOptions: async (_, username?: string): Promise<PasskeyRequestOptions> => {
            const service = new ArkaineService()
            return await service.GetPasskeyRequestOptions(username)
        },

        passkeyLogin: async (_, payload: { credential: PasskeyAssertionPayload, remember: boolean }): Promise<void> => {
            const service = new ArkaineService()
            await service.PasskeyLogin(payload.credential, payload.remember)
        },

        twoFactorAuth: async (_, payload: { code: string, remember: boolean }): Promise<void> => {
            const service = new ArkaineService()
            await service.TwoFactorAuth(payload.code, payload.remember)
        },

        /**
         * Stale-while-revalidate. The current path is committed synchronously so the
         * gallery swaps to the target folder - cached content or skeletons - before any
         * network work begins. Navigation must never wait on a round trip.
         */
        loadFiles: async ({ commit, state }, rawPath: unknown): Promise<void> => {
            const path = folderKey(rawPath)

            commit('setCurrentPath', path)
            commit('setAlert', undefined)

            if (state.folders[path]) {
                commit('touchFolder', path)

                if (isFresh(state, path)) {
                    commit('setFolderStatus', 'idle')
                    return
                }

                commit('setFolderStatus', 'revalidating')

                try {
                    const response = await fetchFolder(path)
                    commit('setFolder', { path, files: response.files, nextFile: response.nextFile })
                }
                catch {
                    // A background refresh failing is not worth blanking a folder the
                    // user is already looking at, so the cached listing stays on screen.
                }
                finally {
                    if (state.currentPath === path) {
                        commit('setFolderStatus', 'idle')
                    }
                }

                return
            }

            commit('setFolderStatus', 'loading')

            try {
                const response = await fetchFolder(path)
                commit('setFolder', { path, files: response.files, nextFile: response.nextFile })

                if (state.currentPath === path) {
                    commit('setFolderStatus', 'idle')
                }
            }
            catch (e) {
                if (state.currentPath === path) {
                    commit('setFolderStatus', 'error')
                    commit('setAlert', { isError: true, message: errorMessage(e) })
                }

                throw e
            }
        },

        /** Warms the cache for a folder the user is about to open. Never touches the view. */
        prefetchFolder: async ({ commit, state }, rawPath: unknown): Promise<void> => {
            const path = folderKey(rawPath)

            if (isFresh(state, path) || inFlightFolders.has(path)) {
                return
            }

            try {
                const response = await fetchFolder(path)
                commit('setFolder', { path, files: response.files, nextFile: response.nextFile })
            }
            catch {
                // Prefetching is best effort - a failure just means no warm cache.
            }
        },

        addTag: async ({ commit }, request: { name: string, file: string, time: string }): Promise<void> => {
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

                commit('setTags', { file: request.file, tags })
            }
            catch (e) {
                commit('setAlert', {
                    isError: true,
                    message: errorMessage(e)
                })

                throw e
            }
        },

        addToFavourite: async ({ commit }, file: ArkaineFile): Promise<void> => {
            try {
                const service = new ArkaineService()
                await service.AddToFavourites(file)
                commit('setFavourite', file.rawFileName)
                // The favourites collection has changed, so drop its cached listing.
                commit('invalidateFolders', 'Favourites')
            }
            catch (e) {
                commit('setAlert', {
                    isError: true,
                    message: errorMessage(e)
                })

                throw e
            }
        },

        loadMoreFiles: async ({ commit, state }, rawPath: unknown): Promise<void> => {
            const path = folderKey(rawPath)
            const entry = state.folders[path]

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
                commit('appendToFolder', { path, files: response.files, nextFile: response.nextFile })
                commit('setAlert', undefined)
            }
            catch (e) {
                commit('setAlert', {
                    isError: true,
                    message: errorMessage(e)
                })

                throw e
            }
            finally {
                inFlightPages.delete(pageKey)
            }
        }
    },
    modules: {
    }
})
