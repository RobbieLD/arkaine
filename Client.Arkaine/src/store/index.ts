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

    loadFiles: async ({ commit }, payload: { bucketId: string, bucketName: string }): Promise<void> => {
        const service = new ArkaineService()
        const files = await service.Files(payload.bucketId, payload.bucketName)
        commit('setFiles', files)
    }
  },
  modules: {
  }
})
