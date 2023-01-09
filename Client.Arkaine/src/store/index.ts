import Album from '@/models/album'
import Alert from '@/models/alert'
import ArkaineFile from '@/models/arkaine-file'
import Rating from '@/models/rating'
import ArkaineService from '@/services/arkaine.service'
import { InjectionKey } from 'vue'
import { createStore, Store } from 'vuex'
import State from './state'

export const storeKey: InjectionKey<Store<State>> = Symbol('store')

export const store = createStore<State>({
  state: {
    isAuthenticated: false,
    username: '',
    albums: [],
    filesRoot: new ArkaineFile('root', '', '', ''),
    path: ''
  },
  getters: {
    getFilesList: (state): ArkaineFile[] => {
        if (!state.path) {
            return state.filesRoot.children
        }

        const filter = (file: ArkaineFile, path: string[]): ArkaineFile[] => {
            const dir = path.shift()
            for (const child of file.children) {
                if (child.fileName === dir) {
                    return filter(child, path)
                }
            }

            return file.children
        }

        return filter(state.filesRoot, state.path.split('/').filter(Boolean))
    }
  },
  mutations: {
    setAuthenticated: (state: State, authed: boolean): void => {
        state.isAuthenticated = authed
        
    },

    setAlbums: (state: State, albums: Album[]): void => {
        state.albums = albums
    },

    setFiles: (state: State, root: ArkaineFile): void => {
        state.filesRoot = root
    },

    setPath: (state: State, path: string): void => {
        state.path = path
    },

    setAlert: (state: State, alert?: Alert): void => {
        state.alert = alert
    },

    setUsername: (state: State, username: string): void => {
        state.username = username
    }
  },
  actions: {
    checkLogin: async ({ commit, dispatch }) : Promise<void> => {
        const service = new ArkaineService()
        const username = await service.LoggedIn()
        commit('setAuthenticated', true)
        commit('setUsername', username)
        await dispatch('loadAlbums')
    },

    logout: async ({ commit }): Promise<void> => {
        const service = new ArkaineService()
        await service.Logout()
        commit('setUsername', '')
        commit('setAuthenticated', false)
    },

    login: async (_, payload: { username: string, password: string}): Promise<void> => {
        const service = new ArkaineService()
        await service.Login(payload.username, payload.password)
    },

    twoFactorAuth: async ({ commit, dispatch }, payload: { username: string, code: string}): Promise<void> => {
        const service = new ArkaineService()
        const username = await service.TwoFactorAuth(payload.username, payload.code)
        commit('setAuthenticated', true)
        commit('setUsername', username)
        await dispatch('loadAlbums')
    },

    loadAlbums: async ({ commit }): Promise<void> => {
        try {
            const service = new ArkaineService()
            const albums = await service.Albums()
            commit('setAlbums', albums)
            commit('setAlert', null)
        }
        catch (e) {
            commit('setAlert', {
                isError: true,
                message: e
            })
        }

    },

    loadFiles: async ({ commit }, album: Album): Promise<void> => {
        try {
            const service = new ArkaineService()
            const filesRoot = await service.Files(album.bucketId, album.bucketName)
            commit('setFiles', filesRoot)
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

    saveRating: async (_, rating: Rating): Promise<void> => {
        const service = new ArkaineService()
        await service.SaveRating(rating)
    }
  },
  modules: {
  }
})
