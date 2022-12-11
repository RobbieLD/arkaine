<template>
    <article class="album" @click="open">
        <!-- <h1>{{ name }}</h1> -->
        <img :src="background" @error="imageLoadErrorHandler" class="album__background" />
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

            const imageLoadErrorHandler = (e: any) => {
                e.target.src = 'folder.png'
            }

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
                open,
                imageLoadErrorHandler
            }
        },
    })
</script>
<style lang='scss' scoped>
    .album {
        padding: 0em;
        height: fit-content;
        max-width: 20em;
        width: 20%;
        margin: 0;

        &__name {
            text-align: center;
        }

        &__background {
            border-top-left-radius: var(--border-radius);
            border-top-right-radius: var(--border-radius);
        }

        &:hover {
            background-color: var(--primary-focus);
            cursor: pointer;
        }
    }
</style>
