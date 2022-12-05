<template>
    <div class="content">
        <article :class="{ image: file.isImage, folder: file.isFolder, audio: file.isAudio, video: file.isVideo }" class="item" v-for="(file, index) of files" :key="index">
            <!-- Image File -->
            <a v-if="file.isImage" class="image" :href="file.url" target="_blank">
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
                <a :href="file.url" target="_blank">{{ file.fileName }} ({{ index + 1 }}/{{ files.length }})</a>
                <audio-player :src="file.url" class="player" :fileName="file.fileName"></audio-player>
            </div>

            <!-- Folder -->
            <div v-else-if="file.isFolder" @click="open(file.fileName)">
                <div class="headings">
                    <h2>{{ file.fileName }}</h2>
                    <h3>{{ file.children.length }} items</h3>
                </div>
            </div>

            <!-- Other file types -->
            <div v-else>
                <a :href="file.url" target="_blank">{{ files.fileName }}</a>
            </div>
            <div v-if="!file.isAudio && !file.isFolder" class="caption">{{ file.fileName }} ({{ index + 1 }}/{{ files.length }})</div>
        </article>
    </div>
</template>
<script lang='ts'>
    import { storeKey } from '@/store'
    import { computed, defineComponent, onMounted } from 'vue'
    import { onBeforeRouteUpdate, useRoute, useRouter } from 'vue-router'
    import { useStore } from 'vuex'
    import AudioPlayer from '@/components/AudioPlayer.vue'

    export default defineComponent({
        name: 'FilesView',
        components: {
            AudioPlayer
        },
        setup() {
            const router = useRouter()
            const store = useStore(storeKey)
            const route = useRoute()
            const files = computed(() => store.getters['getFilesList'])
            const open = async (name: string) => {
                await router.push(route.path + name + '/')
            }

            onMounted(() => {
                store.commit('setPath', '')
            })

            onBeforeRouteUpdate((to) => {
                store.commit('setPath', to.params.path)
            })

            return {
                files,
                open
            }
        },
    })
</script>
<style lang='scss' scoped>
    .item {
        cursor: pointer;
        min-width: 10em;
        height: fit-content;
        padding: 0.5em;
    }

    .player {
        padding-top: 1em;
    }

    .audio {
        width: 100%;
        background: #f1f3f4;
    }

    .caption {
        text-align: center;
    }

    .video video {
        width: 100%;
        width: -moz-available;
        width: -webkit-fill-available;
        width: fill-available;
    }

    .image {
        max-width: 30em;
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
    }
</style>
