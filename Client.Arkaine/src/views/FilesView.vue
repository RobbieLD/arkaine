<template>
    <div class="content">
        <article class="item" v-for="(file, index) of files" :key="index">

            <span v-if="file.isImage" class="favourite" :class="file.isFavourite ? 'favourite--confirmed' : ''" @click="fav(file)">♡</span>
            
            <!-- Folder -->
            <div class="folder" v-if="file.isDirectory">
                <router-link :to="$route.path + file.name + '/'">
                <div class="headings title">
                    <h2>{{ file.name }}/</h2>
                </div>
                <img :src="file.preview || file.thumb" @error="imageLoadErrorHandler" />
                </router-link>
            </div>

            <!-- Image File -->
            <a v-else-if="file.isImage" class="image" :href="file.url" target="_blank">
                <img :src="file.preview || file.url"/>
            </a>
            
            <!-- Video File -->
            <div v-else-if="file.isVideo">
                <div class="caption">{{ file.name }}</div>
                <video controls class="video">
                    <source :src="file.url" :type="file.contentType">
                </video>
            </div>

            <!-- Audio File -->
            <div v-else-if="file.isAudio" class="audio">
                <a :href="file.url" target="_blank">{{ file.name }}</a>
                <audio-player :src="file.url" class="player" :fileName="file.name"></audio-player>
            </div>

            <!-- Other file types -->
            <div v-else>
                <a :href="file.url" target="_blank">{{ file.name }}</a>
            </div>
        </article>
    </div>
</template>
<script lang='ts'>
    import { storeKey } from '@/store'
    import { computed, defineComponent, onMounted, ref } from 'vue'
    import { onBeforeRouteUpdate, useRoute } from 'vue-router'
    import { useStore } from 'vuex'
    import AudioPlayer from '@/components/AudioPlayer.vue'
    import ArkaineFile from '@/models/arkaine-file'

    export default defineComponent({
        name: 'FilesView',
        components: {
            AudioPlayer,
        },
        setup() {
            const store = useStore(storeKey)
            const route = useRoute()
            const files = computed(() => store.getters['orderedFiles'])
            const hasMoreFiles = computed(() => store.getters['hasMoreFiles'])

            const imageLoadErrorHandler = (e: Event) => {
                (e.target as HTMLImageElement).src = 'folder.png'
            }

            onMounted(async () => {
                await store.dispatch('loadFiles', '')
            })

            onBeforeRouteUpdate(async (to) => {
                await store.dispatch('loadFiles', to.params.path)
            })

            const nextPage = async () => {
                await store.dispatch('loadMoreFiles', route.params.path)
            }

            const fav = async (file: ArkaineFile) => {
                if (!file.isFavourite)
                {
                    await store.dispatch('addToFavourite', file)
                    file.isFavourite = true
                }
            }

            window.onscroll = async () => {
                if (hasMoreFiles.value && ((window.innerHeight + window.scrollY) >= document.body.offsetHeight - 5)) {
                    await store.dispatch('loadMoreFiles', route.params.path)
                }
            }
            

            return {
                files,
                fav,
                nextPage,
                hasMoreFiles,
                imageLoadErrorHandler
            }
        },
    })
</script>
<style lang='scss' scoped>
    .item {
        cursor: pointer;
        height: fit-content;
        padding: 0.5em;
        display: grid;
        margin: 0;
    }

    .favourite {
        position: absolute;
        font-size: 2em;
        margin-right: 0.3em;
        justify-self: end;
        color: white;

        &--confirmed {
            color: rgb(192, 16, 69);
        }
    }

    .caption {
        margin-bottom: 0.5em;
        font-size: 0.8em;
    }

    .title {
        text-align: center;
    }

    .show { 
        display: initial !important;
    }

    .player {
        padding-top: 2em;
    }

    .folder {
        display: grid;
        justify-content: center;
    }

    .audio {
        width: 90vw;
    }

    .video {
        max-width: min(90vw, 30em);
    }

    .image {
        max-width: 300px;
        display: grid;
        justify-content: center;
    }

    .folder {
        max-width: 300px;
        &:hover {
            background-color: var(--primary-focus);
        }
    }

    /* Mobile */
    @media only screen and (max-width: 400px) {

        .item {
            margin: 0;
        }        
    }
</style>
