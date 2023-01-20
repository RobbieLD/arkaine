<template>
    <nav>
        <ul>
            <li><strong>{{ title }}</strong></li>
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
            const title = ref('/')

            const router = useRouter()

            router.afterEach((to) => {
                title.value = to.params.path.toString() || '/'
            })
            
            const logout = async (e: Event) => {
                e.preventDefault()
                await store.dispatch('logout')
                router.push('/login')
            }

            return {
                username,
                title,
                logout
            }
        },
    })
</script>
<style lang='scss' scoped>
</style>
