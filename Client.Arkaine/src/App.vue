<template>
    <div class="app-shell">
        <nav-bar v-if="authenticated"></nav-bar>
        <main class="app-content">
            <div
                v-if="alert"
                class="alert app-alert"
                :class="alert.isError ? 'alert--error' : 'alert--success'"
                :role="alert.isError ? 'alert' : 'status'"
            >
                {{ alert.message }}
            </div>
            <router-view />
        </main>
        <footer v-if="authenticated" class="app-footer">
            Version {{ version }}
        </footer>
    </div>
</template>

<script lang="ts">
    import { computed, defineComponent, onMounted } from 'vue'
    import { useRouter } from 'vue-router'
    import { useAppStore } from './store'
    import NavBar from './components/NavBar.vue'
    import { version } from './config'

    export default defineComponent({
        name: 'App',
        components: {
            NavBar
        },
        setup() {
            const store = useAppStore()
            const router = useRouter()
            const alert = computed(() => store.alert)
            const authenticated = computed(() => store.isAuthenticated)

            onMounted(async () => {
                try {
                    const loggedIn = await store.checkLogin()

                    if (loggedIn && router.currentRoute.value.name === 'Login') {
                        const redirect = router.currentRoute.value.query.redirect
                        const destination = typeof redirect === 'string' &&
                            redirect.startsWith('/') &&
                            !redirect.startsWith('//')
                            ? redirect
                            : '/'
                        await router.push(destination)
                    }
                }
                catch
                {
                    await router.push('/login')
                }
            })

            return {
                alert,
                authenticated,
                version
            }
        },
    })
</script>

<style lang="scss">
    .app-shell {
        display: flex;
        min-height: 100vh;
        flex-direction: column;
    }

    .app-content {
        width: 100%;
        max-width: var(--page-width);
        flex: 1 0 auto;
        margin: 0 auto;
        padding: var(--space-5) var(--page-gutter) var(--space-7);
    }

    .app-alert {
        margin-bottom: var(--space-5);
    }

    .app-footer {
        flex: 0 0 auto;
        padding: var(--space-4);
        color: var(--text-subtle);
        border-top: 1px solid var(--border);
        background: var(--surface-sunken);
        font-size: var(--text-xs);
        text-align: center;
    }
</style>
