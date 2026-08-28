import { createRouter, createWebHashHistory, RouteRecordRaw } from 'vue-router'
import { store } from '@/store'
import LoginView from '@/views/LoginView.vue'
import FilesView from '@/views/FilesView.vue'
import AdminView from '@/views/AdminView.vue'
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
        path: '/admin',
        name: 'Admin',
        component: AdminView,
        meta: {
            requiresAuth: true,
            requiresAdmin: true
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
    }
]

const router = createRouter({
    history: createWebHashHistory(),
    routes
})

router.beforeEach((to, _from, next) => {
    const requiresAuth = to.matched.some(r => r.meta?.requiresAuth)

    if (requiresAuth && !store.isAuthenticated) {
        next({
            name: 'Login',
            query: {
                redirect: to.fullPath
            }
        })
        return
    }

    const requiresAdmin = to.matched.some(r => r.meta?.requiresAdmin)
    if (requiresAdmin && !store.isAdmin) {
        store.setAlert({
            isError: true,
            message: 'You do not have access to the admin area.'
        })
        next({ name: 'Files' })
        return
    }

    next()
})

export default router
