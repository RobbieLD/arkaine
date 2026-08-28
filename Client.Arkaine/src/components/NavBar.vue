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
                    class="btn btn--ghost btn--icon navigation__toggle"
                    :aria-expanded="menuOpen"
                    aria-controls="primary-navigation"
                    :aria-label="menuOpen ? 'Close navigation menu' : 'Open navigation menu'"
                    @click="toggleMenu"
                >
                    <app-icon :name="menuOpen ? 'close' : 'menu'" />
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
                    to="/admin"
                    class="nav-link"
                    active-class="nav-link--active"
                    @click="closeMenu"
                >
                    Admin
                </router-link>
                <span v-if="username" class="navigation__user truncate">{{ username }}</span>
                <button type="button" class="btn btn--sm navigation__logout" @click="logout">
                    <app-icon name="logout" />
                    Log out
                </button>
            </div>
        </nav>
    </header>
</template>
<script lang="ts">
    import { useAppStore } from '@/store'
    import { computed, defineComponent, ref } from 'vue'
    import { useRouter } from 'vue-router'
    import type { RouteLocationNormalizedLoaded } from 'vue-router'
    import AppIcon from './AppIcon.vue'

    export default defineComponent({
        name: 'NavBar',
        components: {
            AppIcon
        },
        props: {},
        setup() {
            const store = useAppStore()
            const admin = computed(() => store.isAdmin)
            const username = computed(() => store.username)
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
                await store.logout()
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
<style lang="scss" scoped>
    .site-header {
        position: sticky;
        top: 0;
        z-index: var(--z-header);
        border-bottom: 1px solid var(--border);
        background: color-mix(in srgb, var(--canvas) 85%, transparent);
        backdrop-filter: blur(12px);
    }

    .navigation {
        display: flex;
        width: 100%;
        max-width: var(--page-width);
        min-height: 3.75rem;
        align-items: center;
        justify-content: space-between;
        gap: var(--space-5);
        margin: 0 auto;
        padding: var(--space-2) var(--page-gutter);
    }

    .navigation__identity,
    .navigation__links,
    .breadcrumbs {
        display: flex;
        align-items: center;
    }

    .navigation__identity {
        min-width: 0;
        gap: var(--space-4);
    }

    .brand {
        display: inline-flex;
        flex: 0 0 auto;
        align-items: center;
        gap: var(--space-2);
        color: var(--text);
        font-weight: 700;
        letter-spacing: -0.02em;
        text-decoration: none;
    }

    .brand__mark {
        display: block;
        width: 1.75rem;
        height: 1.75rem;
        flex: 0 0 auto;
        border-radius: var(--radius-sm);
        object-fit: cover;
    }

    .breadcrumbs {
        min-width: 0;
        flex: 1 1 auto;
        gap: var(--space-2);
        color: var(--text-muted);
        font-size: var(--text-sm);
        white-space: nowrap;
        overflow: hidden;
    }

    .breadcrumbs__link,
    .breadcrumbs__current {
        overflow: hidden;
        text-overflow: ellipsis;
    }

    .breadcrumbs__link {
        color: var(--text-muted);
        text-decoration: none;
    }

    .breadcrumbs__link:hover,
    .breadcrumbs__link:focus-visible {
        color: var(--accent);
    }

    .breadcrumbs__current {
        color: var(--text);
        font-weight: 600;
    }

    .breadcrumbs__separator {
        color: var(--text-subtle);
    }

    .navigation__toggle {
        display: none;
    }

    .navigation__links {
        flex: 0 0 auto;
        gap: var(--space-1);
    }

    .nav-link {
        display: inline-flex;
        min-height: var(--control-height-sm);
        align-items: center;
        padding: 0 var(--space-3);
        color: var(--text-muted);
        border-radius: var(--radius-md);
        font-size: var(--text-sm);
        font-weight: 600;
        text-decoration: none;
        transition:
            color var(--duration-fast) var(--ease),
            background-color var(--duration-fast) var(--ease);
    }

    .nav-link:hover,
    .nav-link:focus-visible {
        color: var(--text);
        background: var(--surface-hover);
    }

    .nav-link--active {
        color: var(--accent);
        background: var(--accent-soft);
    }

    .navigation__user {
        max-width: 10rem;
        margin-left: var(--space-2);
        padding-left: var(--space-3);
        color: var(--text-subtle);
        border-left: 1px solid var(--border);
        font-size: var(--text-xs);
    }

    .navigation__logout {
        margin-left: var(--space-2);
        --icon-size: 1rem;
    }

    @media only screen and (max-width: 760px) {
        .navigation {
            position: relative;
            gap: var(--space-2);
        }

        .navigation__identity {
            width: 100%;
            gap: var(--space-3);
        }

        .navigation__toggle {
            display: inline-grid;
            margin-left: auto;
        }

        .navigation__links {
            position: absolute;
            top: calc(100% + var(--space-1));
            right: var(--page-gutter);
            display: none;
            width: min(15rem, calc(100vw - 2rem));
            flex-direction: column;
            align-items: stretch;
            gap: var(--space-1);
            padding: var(--space-2);
            border: 1px solid var(--border-strong);
            border-radius: var(--radius-lg);
            background: var(--surface);
            box-shadow: var(--shadow-lg);
        }

        .navigation__links--open {
            display: flex;
        }

        .navigation__links .nav-link,
        .navigation__links .navigation__logout {
            width: 100%;
            justify-content: flex-start;
            margin-left: 0;
        }

        .navigation__user {
            max-width: none;
            margin: var(--space-1) 0 0;
            padding: var(--space-2) var(--space-3) var(--space-1);
            border-top: 1px solid var(--border);
            border-left: 0;
        }
    }

    @media only screen and (max-width: 460px) {
        .navigation__user {
            display: none;
        }
    }
</style>
