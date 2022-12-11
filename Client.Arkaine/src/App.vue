<template>
    <div v-if="alert" class="alert" :class="{ error: alert.isError, info: !alert.isError }">{{ alert.message }}</div>
    <router-view />
</template>

<script lang="ts">
    import { computed, defineComponent, onMounted, onUnmounted } from 'vue'
    import { useRouter } from 'vue-router'
    import { useStore } from 'vuex'
    import { storeKey } from './store'

    export default defineComponent({
        name: 'App',
        components: {},
        setup() {
            const store = useStore(storeKey)
            const router = useRouter()
            const alert = computed(() => store.state.alert)

            const unsubscribe = store.subscribe(async (mutation) => {
                if (mutation.type == 'setAlbums') {
                    await router.push('/')
                }                
            })

            onMounted(async () => {
                await store.dispatch('checkLogin')
            })

            onUnmounted(() => {
                unsubscribe()
            })

            return {
                alert
            }
        },
    })
</script>

<style lang="scss">
    body {
        padding: 0 2em;
    }

    .error {
        color: darkred;
        border: red solid 2px;
        background: #ffeded;
    }

    .info {
        color: #072872;
        border: #2882ff solid 2px;
        background: #e7f1ff;
    }

    .alert {
        margin: 1em;
        padding: 1em;
    }

    .content {
        display: flex;
        flex-direction: row;
        flex-wrap: wrap;
        gap: 1em;
        justify-content: center;
    }

    @media only screen and (max-width: 576px) {
        body {
            padding: 0;
        }
    }
</style>
