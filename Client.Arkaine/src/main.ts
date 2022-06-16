import { createApp } from 'vue'
import App from './App.vue'
import router from './router'
import { store, storeKey } from './store'

createApp(App)
    .use(store, storeKey)
    .use(router)
    .mount('#app')
