<template>
    <article class="album" @click="open">
        <!-- <h1>{{ name }}</h1> -->
        <img :src="background" class="album__background" />
        <div class="album__name">{{ name }}</div>
    </article>
</template>
<script lang='ts'>
    import Album from '@/models/album'
    import { storeKey } from '@/store'
    import { defineComponent, PropType } from 'vue'
    import { useRouter } from 'vue-router'
    import { useStore } from 'vuex'
    
    export default defineComponent({
        name: 'AlbumBucket',
        components: {},
        props: {
            Album: {
                type: Object as PropType<Album>,
                required: true
            }
        },
        setup(props) {
            const store = useStore(storeKey)
            const router = useRouter()

            const open = async () => {
                try {
                    await store.dispatch('loadFiles', props.Album )
                    await router.push('/files/')
                }
                catch (e) {
                    const resp = String(e)
                    if (resp.startsWith('/')) {
                        router.push(resp)
                    }
                }
            }

            return {
                name: props.Album.bucketName,
                background: `${process.env?.VUE_APP_ARKAINE_SERVER}/stream/${props.Album.bucketName}/thumb.jpg`,
                open
            }
        },
    })
</script>
<style lang='scss' scoped>
    .album {
        padding: 0.5em;
        height: fit-content;

        &__name {
            text-align: center;
        }

        &__background {
            max-width: 20em;
        }

        &:hover {
            background-color: var(--primary-focus);
            cursor: pointer;
        }
    }
</style>
