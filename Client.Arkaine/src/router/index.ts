import { createRouter, createWebHashHistory, RouteRecordRaw } from 'vue-router'
import { store } from '@/store'
import AlbumsView from '@/views/AlbumsView.vue'
import LoginView from '@/views/LoginView.vue'
import FilesView from '@/views/FilesView.vue'

const routes: Array<RouteRecordRaw> = [
    {
        path: '/',
        name: 'Albums',
        component: AlbumsView,
        meta: {
            requiresAuth: true
        },
    },
    {
        path: '/files/:path(.*)',
        name: 'Files',
        component: FilesView,
        meta: {
            requiresAuth: true
        },
    },
    {
        path: '/login',
        name: 'Login',
        component: LoginView
    }
]

const router = createRouter({
    history: createWebHashHistory(),
    routes
})

router.beforeEach((to, from, next) => {
    const requiresAuth = to.matched.some(r => r.meta?.requiresAuth)
    if (requiresAuth && !store.state.isAuthenticated) {
        router.push('/login')
    } else {
        next()
    }
})

export default router
