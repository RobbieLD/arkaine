import Album from '@/models/album'
import ArkaineFile from '@/models/arkaine-file'
import ArkaineService from '@/services/arkaine.service'
import { InjectionKey } from 'vue'
import { createStore, Store } from 'vuex'
import State from './state'

export const storeKey: InjectionKey<Store<State>> = Symbol('store')

export const store = createStore<State>({
  state: {
    isAuthenticated: false,
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

    appendPath: (state: State, path: string): void => {
        state.path += '/' + path
    }

  },
  actions: {
    checkLogin: async ({ commit, dispatch }) : Promise<void> => {
        const service = new ArkaineService()
        await service.LoggedIn()
        commit('setAuthenticated', true)
        await dispatch('loadAlbums')
    },

    login: async ({ commit, dispatch }, payload: { username: string, password: string}): Promise<void> => {
        const service = new ArkaineService()
        await service.Login(payload.username, payload.password)
        commit('setAuthenticated', true)
        await dispatch('loadAlbums')
    },

    loadAlbums: async ({ commit }): Promise<void> => {
        const service = new ArkaineService()
        const albums = await service.Albums()
        commit('setAlbums', albums)
    },

    loadFiles: async ({ commit }, album: Album): Promise<void> => {
        const service = new ArkaineService()
        const filesRoot = await service.Files(album.bucketId, album.bucketName)
        commit('setFiles', filesRoot)
    }
  },
  modules: {
  }
})
