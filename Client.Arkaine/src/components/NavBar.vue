<template>
    <header class="site-header">
        <nav class="navigation" aria-label="Primary navigation">
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
            </div>

            <div class="navigation__links">
                <router-link to="/" class="nav-link" exact-active-class="nav-link--active">Home</router-link>
                <router-link to="/profile" class="nav-link" active-class="nav-link--active">Profile</router-link>
                <router-link v-if="admin" to="/settings" class="nav-link" active-class="nav-link--active">
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
            router.afterEach(updateCrumbs)
            
            const logout = async () => {
                await store.dispatch('logout')
                router.push('/login')
            }

            return {
                admin,
                crumbs,
                logout,
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
            align-items: flex-start;
            flex-direction: column;
            gap: 0.6rem;
            padding: 0.85rem 0;
        }

        .navigation__identity,
        .navigation__links {
            width: 100%;
        }

        .navigation__links {
            justify-content: flex-start;
            overflow-x: auto;
        }

        .navigation__user {
            margin-left: auto;
        }
    }

    @media only screen and (max-width: 460px) {
        .navigation__identity {
            align-items: flex-start;
            flex-direction: column;
            gap: 0.45rem;
        }

        .breadcrumbs {
            width: 100%;
        }

        .navigation__user {
            display: none;
        }
    }
</style>
