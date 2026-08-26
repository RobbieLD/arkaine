<template>
    <header class="site-header">
        <nav class="navigation" aria-label="Primary navigation" @keydown.esc="closeMenu">
            <div class="navigation__identity">
                <router-link to="/" class="brand" aria-label="Arkaine home">
                    <img class="brand__mark" src="/icon.png" alt="" aria-hidden="true">
                    <span class="brand__name">Arkaine</span>
                </router-link>

                <div v-if="crumbs.length > 1" class="breadcrumbs" aria-label="Breadcrumb">
                    <template v-for="(crumb, index) in crumbs" :key="crumb.url">
                        <span v-if="index > 0" class="breadcrumbs__separator" aria-hidden="true">/</span>
                        <router-link
                            v-if="index < crumbs.length - 1"
                            :to="crumb.url"
                            class="breadcrumbs__link"
                        >
                            {{ crumb.title }}
                        </router-link>
                        <span v-else class="breadcrumbs__current" aria-current="page">{{ crumb.title }}</span>
                    </template>
                </div>

                <button
                    type="button"
                    class="navigation__toggle"
                    :aria-expanded="menuOpen"
                    aria-controls="primary-navigation"
                    :aria-label="menuOpen ? 'Close navigation menu' : 'Open navigation menu'"
                    @click="toggleMenu"
                >
                    <span class="navigation__toggle-icon" aria-hidden="true">
                        <span></span>
                        <span></span>
                        <span></span>
                    </span>
                </button>
            </div>

            <div id="primary-navigation" class="navigation__links" :class="{ 'navigation__links--open': menuOpen }">
                <router-link
                    to="/"
                    class="nav-link"
                    exact-active-class="nav-link--active"
                    @click="closeMenu"
                >
                    Home
                </router-link>
                <router-link
                    to="/profile"
                    class="nav-link"
                    active-class="nav-link--active"
                    @click="closeMenu"
                >
                    Profile
                </router-link>
                <router-link
                    v-if="admin"
                    to="/settings"
                    class="nav-link"
                    active-class="nav-link--active"
                    @click="closeMenu"
                >
                    Settings
                </router-link>
                <span v-if="username" class="navigation__user">{{ username }}</span>
                <button type="button" class="logout-button" @click="logout">Log out</button>
            </div>
        </nav>
    </header>
</template>
<script lang='ts'>
    import { storeKey } from '@/store'
    import { computed, defineComponent, ref } from 'vue'
    import { useRouter } from 'vue-router'
    import type { RouteLocationNormalizedLoaded } from 'vue-router'
    import { useStore } from 'vuex'

    export default defineComponent({
        name: 'NavBar',
        components: {},
        props: {},
        setup() {
            const store = useStore(storeKey)
            const admin = computed(() => store.state.isAdmin)
            const username = computed(() => store.state.username)
            const crumbs = ref<{ url: string, title: string }[]>([])
            const menuOpen = ref(false)

            const router = useRouter()

            const updateCrumbs = (to: RouteLocationNormalizedLoaded) => {
                crumbs.value = [{
                    title: 'Library',
                    url: '/'
                }]

                if (to.name !== 'Files' || !to.params.path) {
                    return
                }

                let path = '/'
                for (const crumb of to.params.path.toString().split('/').filter(c => c)) {
                    path += crumb + '/'
                    crumbs.value.push({
                        title: crumb,
                        url: path
                    })
                }
            }

            updateCrumbs(router.currentRoute.value)
            router.afterEach((to) => {
                updateCrumbs(to)
                menuOpen.value = false
            })

            const toggleMenu = () => {
                menuOpen.value = !menuOpen.value
            }

            const closeMenu = () => {
                menuOpen.value = false
            }
            
            const logout = async () => {
                closeMenu()
                await store.dispatch('logout')
                router.push('/login')
            }

            return {
                admin,
                closeMenu,
                crumbs,
                logout,
                menuOpen,
                toggleMenu,
                username
            }
        },
    })
</script>
<style lang='scss' scoped>
    .navigation {
        width: calc(100% - 2rem);
        max-width: 80rem;
        min-height: 4.5rem;
        margin: 0 auto;
        padding: 0.75rem 0;
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 1.25rem;
    }

    .site-header {
        border-bottom: 1px solid var(--app-border);
        background: var(--app-header-background);
    }

    .navigation__identity,
    .navigation__links,
    .breadcrumbs {
        display: flex;
        align-items: center;
    }

    .navigation__identity {
        min-width: 0;
        gap: 1rem;
    }

    .brand {
        display: inline-flex;
        align-items: center;
        flex: 0 0 auto;
        gap: 0.55rem;
        color: var(--pico-color);
        font-weight: 700;
        text-decoration: none;
    }

    .brand__mark {
        display: block;
        width: 2rem;
        height: 2rem;
        flex: 0 0 auto;
        border-radius: 0.65rem;
        object-fit: cover;
        box-shadow: 0 0.25rem 0.75rem var(--pico-primary-focus);
    }

    .brand__name {
        letter-spacing: -0.02em;
    }

    .breadcrumbs {
        min-width: 0;
        flex: 1 1 auto;
        gap: 0.55rem;
        color: var(--app-muted);
        font-size: 0.9rem;
        white-space: nowrap;
        overflow: hidden;
    }

    .breadcrumbs__link,
    .breadcrumbs__current {
        overflow: hidden;
        text-overflow: ellipsis;
    }

    .breadcrumbs__link {
        color: var(--app-muted);
        text-decoration: none;
    }

    .breadcrumbs__link:hover,
    .breadcrumbs__link:focus-visible {
        color: var(--pico-primary-hover);
    }

    .breadcrumbs__current {
        color: var(--pico-color);
        font-weight: 600;
    }

    .breadcrumbs__separator {
        color: var(--app-border);
    }

    .navigation__toggle {
        display: none;
    }

    .navigation__links {
        flex: 0 0 auto;
        gap: 0.25rem;
    }

    .nav-link,
    .logout-button {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        min-height: 2.25rem;
        margin: 0;
        padding: 0.45rem 0.7rem;
        border-radius: var(--pico-border-radius);
        font-size: 0.9rem;
        font-weight: 600;
        line-height: 1;
        text-decoration: none;
    }

    .nav-link {
        color: var(--app-muted);
    }

    .nav-link:hover,
    .nav-link:focus-visible,
    .nav-link--active {
        color: var(--pico-primary-hover);
        background: var(--pico-primary-focus);
    }

    .navigation__user {
        max-width: 10rem;
        margin-left: 0.5rem;
        padding-left: 0.75rem;
        overflow: hidden;
        color: var(--app-muted);
        border-left: 1px solid var(--app-border);
        font-size: 0.85rem;
        text-overflow: ellipsis;
        white-space: nowrap;
    }

    .logout-button {
        color: var(--pico-primary-hover);
        border: 1px solid var(--app-border);
        background: transparent;
        box-shadow: none;
    }

    .logout-button:hover,
    .logout-button:focus-visible {
        border-color: var(--pico-primary);
        background: var(--pico-primary-focus);
        box-shadow: none;
    }

    @media only screen and (max-width: 760px) {
        .navigation {
            position: relative;
            align-items: center;
            gap: 0.6rem;
            padding: 0.85rem 0;
        }

        .navigation__identity {
            width: 100%;
            gap: 0.75rem;
        }

        .navigation__toggle {
            display: grid;
            flex: 0 0 auto;
            width: 2.5rem;
            height: 2.5rem;
            margin: 0 0 0 auto;
            padding: 0.55rem;
            place-items: center;
            color: var(--pico-color);
            border: 1px solid var(--app-border);
            border-radius: var(--pico-border-radius);
            background: transparent;
            box-shadow: none;
        }

        .navigation__toggle:hover,
        .navigation__toggle:focus-visible {
            color: var(--pico-primary-hover);
            border-color: var(--pico-primary);
            background: var(--pico-primary-focus);
            box-shadow: none;
        }

        .navigation__toggle-icon {
            display: grid;
            width: 1.15rem;
            gap: 0.2rem;
        }

        .navigation__toggle-icon span {
            display: block;
            height: 2px;
            border-radius: 2px;
            background: currentColor;
        }

        .navigation__links {
            position: absolute;
            top: calc(100% - 0.1rem);
            right: 0;
            z-index: 10;
            display: none;
            width: min(16rem, 100%);
            flex-direction: column;
            align-items: stretch;
            gap: 0.25rem;
            padding: 0.5rem;
            border: 1px solid var(--app-border);
            border-radius: var(--pico-border-radius);
            background: var(--app-surface);
            box-shadow: var(--pico-card-box-shadow);
        }

        .navigation__links--open {
            display: flex;
        }

        .navigation__links .nav-link,
        .navigation__links .logout-button {
            width: 100%;
            justify-content: flex-start;
        }

        .navigation__user {
            max-width: none;
            margin: 0.25rem 0 0;
            padding: 0.55rem 0.7rem 0.3rem;
            border-top: 1px solid var(--app-border);
            border-left: 0;
        }
    }

    @media only screen and (max-width: 460px) {
        .navigation__identity {
            gap: 0.5rem;
        }

        .breadcrumbs {
            gap: 0.4rem;
        }

        .navigation__user {
            display: none;
        }
    }
</style>
