import { createApp } from 'vue'
import App from './App.vue'
import router from './router'
import { store, storeKey } from './store'

import '@picocss/pico/css/pico.css'
import 'feather-icons/dist/icons'

createApp(App)
    .use(store, storeKey)
    .use(router)
    .mount('#app')
