<template>
    <div class="content">
        <album-bucket v-for="(album, index) of albums" :key="index" :Album="album"></album-bucket>
    </div>
</template>

<script lang="ts">
    import { computed, defineComponent, onMounted } from 'vue'
    import AlbumBucket from '@/components/AlbumBucket.vue'
    import { useStore } from 'vuex'
    import { storeKey } from '@/store'
    
    export default defineComponent({
        name: 'AlbumsView',
        components: {
            AlbumBucket
        },
        setup() {
            const store = useStore(storeKey)
            const albums = computed(() => store.state.albums)

            onMounted(() => {
                store.commit('setTitle', 'Buckets')
            })

            return {
                albums
            }
        }
    })
</script>
<style lang="scss">
</style>
