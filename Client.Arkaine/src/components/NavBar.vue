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
            <!-- Make this logout -->
            <li><a @click="logout" title="Logout" href="#">{{ username }}</a></li>
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
            const username = computed(() => store.state.username)
            //const title = ref('/')
            const crumbs = ref<{ url: string, title: string }[]>([])

            const router = useRouter()

            router.afterEach((to) => {
                let path = '/'
                crumbs.value = [{
                    title: 'root',
                    url: '/'
                }]
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
                username,
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
