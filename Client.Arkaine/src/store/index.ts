import Alert from '@/models/alert'
import ArkaineService from '@/services/arkaine.service'
import { InjectionKey } from 'vue'
import { createStore, Store } from 'vuex'
import State from './state'
import ArkaineFile from '@/models/arkaine-file'
import Settings from '@/models/settings'
import Progress from '@/models/progress'
import { HubConnectionBuilder } from '@microsoft/signalr'

export const storeKey: InjectionKey<Store<State>> = Symbol('store')

export const store = createStore<State>({
  state: {
    isAuthenticated: false,
    isAdmin: false,
    username: '',
    files: [],
    nextFile: '',
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
    }
  },
  getters: {
    hasMoreFiles: (state): boolean => {
        return state.nextFile !== null && state.nextFile !== ''
    },

    orderedFiles: (state): ArkaineFile[] => {
        return state.files.sort((a,b) => (Number(b.isDirectory) - Number(a.isDirectory)))
    }
  },
  mutations: {
    setAuthenticated: (state: State, authed: boolean): void => {
        state.isAuthenticated = authed
    },

    addTag: (state: State, request: { name: string, file: string, time: number }): void => {
        const file = state.files.find(f => f.rawFileName === request.file)
        if (file) {
            file.tags.push({
                name: request.name,
                timestamp: request.time
            })
        }
    },

    setSettings: (state: State, settings: Settings): void => {
        state.settings = settings
    },

    setRunning: (state: State, running: boolean): void => {
        state.settings.isRunning = running
    },

    setIsAdmin: (state: State, isAdmin: boolean): void => {
        state.isAdmin = isAdmin
    },

    setFiles: (state: State, files: ArkaineFile[]): void => {
        state.files = files
    },

    appendFiles: (state: State, files: ArkaineFile[]): void => {
        state.files.push(...files)
    },

    setAlert: (state: State, alert?: Alert): void => {
        state.alert = alert
    },

    setUsername: (state: State, username: string): void => {
        state.username = username
    },

    setNextFile: (state: State, fileName: string): void => {
        state.nextFile = fileName
    },

    setProgress: (state: State, progress: Progress): void => {
        state.progress = progress
    }
  },
  actions: {
    checkLogin: async ({ commit }) : Promise<boolean> => {
        const service = new ArkaineService()
        const response = await service.LoggedIn()
        commit('setAuthenticated', true)
        commit('setUsername', response.username)
        commit('setIsAdmin', response.isAdmin)
        return true
    },

    subscribeToUpdates: async ({ commit }): Promise<void> => {
        // TODO: don't make a new connection if there is already one there

        const connection = new HubConnectionBuilder()
            .withUrl(process.env?.VUE_APP_ARKAINE_SERVER + '/updates')
            .build()

        connection.on('update', (data: Progress) => {
            commit('setProgress', data)
            commit('setRunning', !data.finished)
        })

        await connection.start()
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

    logout: async ({ commit }): Promise<void> => {
        const service = new ArkaineService()
        await service.Logout()
        commit('setUsername', '')
        commit('setAuthenticated', false)
    },

    login: async (_, payload: { username: string, password: string, remember: boolean }): Promise<void> => {
        const service = new ArkaineService()
        await service.Login(payload.username, payload.password, payload.remember)
    },

    twoFactorAuth: async (_, payload: { username: string, code: string, remember: boolean}): Promise<void> => {
        const service = new ArkaineService()
        await service.TwoFactorAuth(payload.username, payload.code, payload.remember)
    },

    loadFiles: async ({ commit }, path: string): Promise<void> => {
        try {
            const service = new ArkaineService()
            const response = await service.Files(path, '')

            // only add the favourites collection if we're a the top
            if (!path) {
                response.files.unshift(ArkaineFile.Favourite)
            }

            commit('setFiles', response.files)
            commit('setNextFile', response.nextFile)
            commit('setAlert', null)
        }
        catch (e) {
            commit('setAlert', {
                isError: true,
                message: e
            })

            throw e
        }
    },

    addTag: async ({ commit }, request : { name: string, file: string, time: string }): Promise<void> => {
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
            
            await service.AddTag(request.name, request.file, seconds)
            commit('addTag', {
                name: request.name,
                file: request.file,
                time: seconds
            })
        }
        catch (e) {
            commit('setAlert', {
                isError: true,
                message: e
            })

            throw e
        }
    },

    addToFavourite: async ({ commit }, file: ArkaineFile): Promise<void> => {
        try {
            const service = new ArkaineService()
            await service.AddToFavourites(file)
        }
        catch (e) {
            commit('setAlert', {
                isError: true,
                message: e
            })

            throw e
        }
    },

    loadMoreFiles: async ({ commit, state }, path: string): Promise<void> => {
        try {
            const service = new ArkaineService()
            const response = await service.Files(path, state.nextFile)
            commit('appendFiles', response.files)
            commit('setNextFile', response.nextFile)
            commit('setAlert', null)
        }
        catch (e) {
            commit('setAlert', {
                isError: true,
                message: e
            })

            throw e
        }
    }
  },
  modules: {
  }
})
