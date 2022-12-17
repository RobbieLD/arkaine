<template>
    <div class="content">
        <article :class="{ image: file.isImage, folder: file.isFolder, audio: file.isAudio, video: file.isVideo }" class="item" v-for="(file, index) of files.data" :key="index">
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
                <a :href="file.url" target="_blank">{{ file.fileName }} ({{ index + 1 }}/{{ files.total }})</a>
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
                <a :href="file.url" target="_blank">{{ file.fileName }}</a>
            </div>
            <div v-if="!file.isAudio && !file.isFolder" class="caption">
                <span>
                    {{ index + 1 }}/{{ files.total }}
                </span>
                <span class="rating">
                    <rating-control v-if="!file.isFolder" icon="♡" v-model:modelValue.number="file.rating.value" @update:modelValue="saveRating(file.rating)"></rating-control>
                </span>
            </div>
        </article>
        <button v-if="(files.total > files.data.length)" @click="nextPage" :class="{ show: showMoreButton }" class="np">+</button>
    </div>
</template>
<script lang='ts'>
    import { storeKey } from '@/store'
    import { computed, defineComponent, onMounted, ref } from 'vue'
    import { onBeforeRouteUpdate, useRoute, useRouter } from 'vue-router'
    import { useStore } from 'vuex'
    import AudioPlayer from '@/components/AudioPlayer.vue'
    import Rating from '@/models/rating'
    import ArkaineFile from '@/models/arkaine-file'
    import RatingControl from '@/components/RatingControl.vue'

    export default defineComponent({
        name: 'FilesView',
        components: {
            AudioPlayer,
            RatingControl
        },
        setup() {
            const router = useRouter()
            const store = useStore(storeKey)
            const route = useRoute()
            const count = ref(0)
            const test = ref(0)
            const showMoreButton = ref(false)
            const files = computed(() => {
                const fs = store.getters['getFilesList']
                return {
                    data: fs.slice(0, count.value).sort((a: ArkaineFile, b: ArkaineFile) => (b.rating?.value || 0) - (a.rating?.value || 0)),
                    total: fs.length
                }
            })

            const open = async (name: string) => {
                await router.push(route.path + name + '/')
            }

            const saveRating =  async (r: Rating) => {
                await store.dispatch('saveRating', r)
            }

            onMounted(() => {
                store.commit('setPath', '')
                count.value = 20
            })

            onBeforeRouteUpdate((to) => {
                store.commit('setPath', to.params.path)
            })

            const nextPage = () => {
                if (count.value < files.value.total) {
                    count.value += 20
                }
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
                test,
                saveRating,
                showMoreButton
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

    .show { 
        display: initial !important;
    }

    .np {
        font-size: 2em;
        display: none;
    }

    .player {
        padding-top: 1em;
    }

    .audio {
        width: 100%;
        background: #f1f3f4;
    }

    .caption {
        display: grid;
        grid-auto-flow: column;
        align-items: center;
    }

    .rating {
        justify-self: end;
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
