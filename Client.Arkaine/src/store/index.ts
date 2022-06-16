import ArkaineService from '@/services/arkaine.service'
import { InjectionKey } from 'vue'
import { createStore, Store } from 'vuex'
import State from './state'

export const storeKey: InjectionKey<Store<State>> = Symbol('store')

export const store = createStore<State>({
  state: {
    isAuthenticated: false,
    albums: [],
    files: []
  },
  getters: {
  },
  mutations: {
    setAuthenticated: (state, authed): void => {
        state.isAuthenticated = authed
    },

    setAlbums: (state, albums): void => {
        state.albums = albums
    },

    setFiles: (state, files): void => {
        state.files = files
    }
  },
  actions: {
    login: async ({ commit }, payload: { username: string, password: string}): Promise<void> => {
        const service = new ArkaineService()
        await service.Login(payload.username, payload.password)
        commit('setAuthenticated', true)
    },

    loadAlbums: async ({ commit }, accountId: string): Promise<void> => {
        const service = new ArkaineService()
        const albums = await service.Albums(accountId)
        commit('setAlbums', albums)
    },

    loadFiles: async ({ commit }, bucketId: string): Promise<void> => {
        const service = new ArkaineService()
        const files = await service.Files(bucketId)
        commit('setFiles', files)
    }
  },
  modules: {
  }
})
