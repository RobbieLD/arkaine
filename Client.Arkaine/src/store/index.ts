import Alert from '@/models/alert'
import ArkaineService from '@/services/arkaine.service'
import { InjectionKey } from 'vue'
import { createStore, Store } from 'vuex'
import State from './state'
import ArkaineFile from '@/models/arkaine-file'

export const storeKey: InjectionKey<Store<State>> = Symbol('store')

export const store = createStore<State>({
  state: {
    isAuthenticated: false,
    username: '',
    files: [],
    nextFile: ''
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
    }
  },
  actions: {
    checkLogin: async ({ commit }) : Promise<boolean> => {
        const service = new ArkaineService()
        const username = await service.LoggedIn()
        commit('setAuthenticated', true)
        commit('setUsername', username)
        return true
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

    twoFactorAuth: async (_, payload: { username: string, code: string}): Promise<void> => {
        const service = new ArkaineService()
        await service.TwoFactorAuth(payload.username, payload.code)
    },

    loadFiles: async ({ commit }, path: string): Promise<void> => {
        try {
            const service = new ArkaineService()
            const response = await service.Files(path, '')
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
