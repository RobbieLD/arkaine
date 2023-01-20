<template>
    <div class="content">
        <article class="item" v-for="(file, index) of files" :key="index">
            
            <!-- Folder -->
            <div class="folder" v-if="file.isDirectory" @click="open(file.name)">
                <div class="headings title">
                    <h2>{{ file.name }}/</h2>
                </div>
                <img :src="file.thumb" @error="imageLoadErrorHandler" />
            </div>

            <!-- Image File -->
            <a v-else-if="file.isImage" class="image" :href="file.url" target="_blank">
                <img :src="file.url"/>
            </a>
            
            <!-- Video File -->
            <div v-else-if="file.isVideo">
                <video controls>
                    <source :src="file.url" :type="file.contentType">
                </video>
            </div>

            <!-- Audio File -->
            <div v-else-if="file.isAudio">
                <a :href="file.url" target="_blank">{{ file.name }}</a>
                <audio-player :src="file.url" class="player" :fileName="file.name"></audio-player>
            </div>

            <!-- Other file types -->
            <div v-else>
                <a :href="file.url" target="_blank">{{ file.name }}</a>
            </div>
        </article>
        <button v-if="hasMoreFiles" @click="nextPage" :class="{ show: showMoreButton }" class="np">+</button>
    </div>
</template>
<script lang='ts'>
    import { storeKey } from '@/store'
    import { computed, defineComponent, onMounted, ref } from 'vue'
    import { onBeforeRouteUpdate, useRoute, useRouter } from 'vue-router'
    import { useStore } from 'vuex'
    import AudioPlayer from '@/components/AudioPlayer.vue'

    export default defineComponent({
        name: 'FilesView',
        components: {
            AudioPlayer,
        },
        setup() {
            const router = useRouter()
            const store = useStore(storeKey)
            const route = useRoute()
            const showMoreButton = ref(false)
            const files = computed(() => store.getters['orderedFiles'])
            const hasMoreFiles = computed(() => store.getters['hasMoreFiles'])

            const imageLoadErrorHandler = (e: Event) => {
                (e.target as HTMLImageElement).src = 'folder.png'
            }

            const open = async (name: string) => {
                await router.push(route.path + name + '/')
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

            window.onscroll = () => {
                if ((window.innerHeight + window.scrollY) >= document.body.offsetHeight - 5) {
                    showMoreButton.value = true
                }
                else if (document.body.clientWidth <= 576)
                {
                    showMoreButton.value = false
                }
            }
            

            return {
                files,
                open,
                nextPage,
                showMoreButton,
                hasMoreFiles,
                imageLoadErrorHandler
            }
        },
    })
</script>
<style lang='scss' scoped>
    .item {
        cursor: pointer;
        width: 20em;
        height: fit-content;
        padding: 0.5em;
    }

    .title {
        text-align: center;
    }

    .show { 
        display: initial !important;
    }

    .np {
        font-size: 2em;
        display: none;
    }

    .player {
        padding-top: 2em;
    }

    .folder {
        display: grid;
        justify-content: center;
    }

    .audio {
        width: 100%;
        background: #f1f3f4;
    }

    .video video {
        width: 100%;
        width: -moz-available;
        width: -webkit-fill-available;
        width: fill-available;
    }

    .image {
        max-width: 30em;
        display: grid;
        justify-content: center;
    }

    .folder {
        &:hover {
            background-color: var(--primary-focus);
        }
    }

    /* Mobile */
    @media only screen and (max-width: 576px) {

        .item {
            margin: 0;
        }
        
        .image {
            max-width: initial;
        }

        .folder {
            width: 100vh
        }

        .np {
            position: fixed;
            top: 20%;
            width: min-content;
            right: 0;
            padding-left: 0.8em;
            padding-right: 0.8em;
            border-bottom-right-radius: 0;
            border-top-right-radius: 0;
        }
    }
</style>
