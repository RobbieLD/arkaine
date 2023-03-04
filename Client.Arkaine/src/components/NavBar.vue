<template>
    <nav class="navigation">
        <ul>
            <li>
                <span v-for="(crumb, index) in crumbs" :key="index">
                    <router-link :to="crumb.url">{{ crumb.title }}</router-link> /
                </span>
            </li>
        </ul>
        <ul>
            <li><router-link to="/">Home</router-link></li>
            <li><router-link v-if="admin" to="/settings">Settings</router-link></li>
            <li><a @click="logout" title="Logout" href="#">Logout</a></li>
        </ul>
    </nav>
</template>
<script lang='ts'>
    import { storeKey } from '@/store'
    import { computed, defineComponent, ref } from 'vue'
    import { useRouter } from 'vue-router'
    import { useStore } from 'vuex'

    export default defineComponent({
        name: 'NavBar',
        components: {},
        props: {},
        setup() {
            const store = useStore(storeKey)
            const admin = computed(() => store.state.isAdmin)
            //const title = ref('/')
            const crumbs = ref<{ url: string, title: string }[]>([])

            const router = useRouter()

            router.afterEach((to) => {
                let path = '/'
                crumbs.value = [{
                    title: 'root',
                    url: '/'
                }]

                if (!to.params.path) {
                    return
                }
                
                for (const crumb of to.params.path.toString().split('/').filter(c => c)) {
                    path += crumb + '/'
                    crumbs.value.push({
                        title: crumb,
                        url: path
                    })
                }
            })
            
            const logout = async (e: Event) => {
                e.preventDefault()
                await store.dispatch('logout')
                router.push('/login')
            }

            return {
                admin,
                crumbs,
                logout
            }
        },
    })
</script>
<style lang='scss' scoped>
@media only screen and (max-width: 576px) {
    .navigation {
        margin-left: 1em;
        margin-right: 1em;
    }
}
</style>
