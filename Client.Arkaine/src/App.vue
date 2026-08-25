<template>
    <div class="app-shell">
        <nav-bar v-if="authenticated"></nav-bar>
        <div v-if="alert" class="alert" :class="{ error: alert.isError, info: !alert.isError }" role="alert">
            {{ alert.message }}
        </div>
        <main class="app-content">
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
    import { useStore } from 'vuex'
    import { storeKey } from './store'
    import NavBar from './components/NavBar.vue'
    import { version } from './config'

    export default defineComponent({
        name: 'App',
        components: {
            NavBar
        },
        setup() {
            const store = useStore(storeKey)
            const router = useRouter()
            const alert = computed(() => store.state.alert)
            const authenticated = computed(() => store.state.isAuthenticated)

            onMounted(async () => {
                try {
                    const loggedIn = await store.dispatch('checkLogin')

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
    :root,
    :host {
        --app-surface: var(--pico-card-background-color);
        --app-surface-raised: var(--pico-card-sectioning-background-color);
        --app-border: var(--pico-muted-border-color);
        --app-muted: var(--pico-muted-color);
        --app-header-background: #eef4f8;
        --app-danger: #9f1d35;
        --app-danger-background: #fff1f3;
        --app-success: #176b4d;
        --app-success-background: #edfff5;
    }

    [data-theme='dark'] {
        --app-header-background: #192635;
        --app-danger: #ffb4ab;
        --app-danger-background: #3b1d20;
        --app-success: #7de2b5;
        --app-success-background: #14352a;
    }

    @media only screen and (prefers-color-scheme: dark) {
        :root:not([data-theme]) {
            --app-header-background: #192635;
            --app-danger: #ffb4ab;
            --app-danger-background: #3b1d20;
            --app-success: #7de2b5;
            --app-success-background: #14352a;
        }
    }

    html {
        background: var(--pico-background-color);
    }

    body {
        min-width: 320px;
        margin: 0;
        padding: 0;
    }

    #app,
    .app-shell {
        min-height: 100vh;
    }

    .app-content {
        width: calc(100% - 2rem);
        max-width: 80rem;
        margin: 0 auto;
        padding: clamp(1.25rem, 3vw, 2.5rem) 0 3rem;
    }

    .app-footer {
        width: 100%;
        max-width: none;
        margin: 0;
        padding: 1rem;
        color: var(--app-muted);
        border-top: 1px solid var(--app-border);
        background: var(--app-header-background);
        font-size: 0.75rem;
        text-align: center;
    }

    .error {
        color: var(--app-danger);
        border: 1px solid color-mix(in srgb, var(--app-danger) 45%, transparent);
        background: var(--app-danger-background);
    }

    .info {
        color: var(--pico-primary-hover);
        border: 1px solid var(--pico-primary);
        background: var(--pico-primary-focus);
    }

    .alert {
        width: calc(100% - 2rem);
        max-width: 80rem;
        margin: 1rem auto 0;
        padding: 0.85rem 1rem;
        border-radius: var(--pico-border-radius);
    }

    .content {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(min(100%, 20rem), 1fr));
        gap: 1rem;
        align-items: stretch;
    }
</style>
