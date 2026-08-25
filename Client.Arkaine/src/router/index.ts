import { createRouter, createWebHashHistory, RouteRecordRaw } from 'vue-router'
import { store } from '@/store'
import LoginView from '@/views/LoginView.vue'
import FilesView from '@/views/FilesView.vue'
import SettingsView from '@/views/SettingsView.vue'
import ProfileView from '@/views/ProfileView.vue'

const routes: Array<RouteRecordRaw> = [
    {
        path: '/profile',
        name: 'Profile',
        component: ProfileView,
        meta: {
            requiresAuth: true
        }
    },
    {
        path: '/:path(.*)',
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
    },
    {
        path: '/settings',
        name: 'Settings',
        component: SettingsView
    }
]

const router = createRouter({
    history: createWebHashHistory(),
    routes
})

router.beforeEach((to, from, next) => {
    const requiresAuth = to.matched.some(r => r.meta?.requiresAuth)
    if (requiresAuth && !store.state.isAuthenticated) {
        next({
            name: 'Login',
            query: {
                redirect: to.fullPath
            }
        })
    } else {
        next()
    }
})

export default router
