<template>
    <nav>
        <ul>
            <li><strong>{{ path }}</strong></li>
        </ul>
        <ul>
            <!-- Make this logout -->
            <li><a @click="logout" title="Logout" href="#">{{ username }}</a></li>
        </ul>
    </nav>
</template>
<script lang='ts'>
    import { storeKey } from '@/store'
    import { computed, defineComponent } from 'vue'
    import { useRoute } from 'vue-router'
    import { useStore } from 'vuex'

    export default defineComponent({
        name: 'NavBar',
        components: {},
        props: {},
        setup() {
            const store = useStore(storeKey)
            const username = computed(() => store.state.username)
            const route = useRoute()

            const logout = async (e: Event) => {
                e.preventDefault()
                store.dispatch('logout')
            }

            return {
                username,
                path: route.path,
                logout
            }
        },
    })
</script>
<style lang='scss' scoped>
</style>
