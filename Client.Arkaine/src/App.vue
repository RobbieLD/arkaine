<template>
    <router-view />
</template>

<script lang="ts">
    import { defineComponent, onMounted, onUnmounted } from 'vue'
    import { useRouter } from 'vue-router'
    import { useStore } from 'vuex'
    import { storeKey } from './store'

    export default defineComponent({
        name: 'App',
        components: {},
        setup() {
            const store = useStore(storeKey)
            const router = useRouter()

            const unsubscribe = store.subscribe(async (mutation) => {
                if (mutation.type == 'setAlbums') {
                    await router.push('/')
                }                
            })

            onMounted(() => {
                store.dispatch('checkLogin')
                console.log('App load')
            })

            onUnmounted(() => {
                unsubscribe()
            })
        },
    })
</script>

<style lang="scss">
    body {
        padding: 0 2em;
    }

    .content {
        display: flex;
        flex-direction: row;
        flex-wrap: wrap;
        gap: 1em;
        justify-content: center;
    }
</style>
