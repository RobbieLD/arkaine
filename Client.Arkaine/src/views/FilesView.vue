<template>
    <div class="content">
        <article class="item" v-for="(file, index) of files" :key="index">
            <div class="item__body">
                <!-- Folder -->
                <div class="folder item__media" v-if="file.isDirectory">
                    <router-link :to="$route.path + file.name + '/'" class="media-link">
                        <img :src="file.preview || file.thumb" @error="imageLoadErrorHandler" />
                    </router-link>
                </div>

                <!-- Image File -->
                <a v-else-if="file.isImage" class="image item__media" :href="file.url" target="_blank">
                    <img :src="file.preview || file.url" />
                </a>

                <!-- Video File -->
                <div v-else-if="file.isVideo" class="item__media item__media--player">
                    <video-player :file="file" ></video-player>
                </div>

                <!-- Audio File -->
                <div v-else-if="file.isAudio" class="item__media item__media--player">
                    <audio-player :file="file" ></audio-player>
                </div>

                <!-- Other file types -->
                <div v-else class="item__media item__media--other">
                    <a :href="file.url" target="_blank" class="file-link">{{ file.name }}</a>
                </div>
            </div>

            <footer class="item__footer">
                <div class="item__details">
                    <strong class="item__name" :title="file.name">
                        {{ file.name }}{{ file.isDirectory ? '/' : '' }}
                    </strong>
                    <small class="item__size">
                        <template v-if="file.isDirectory">
                            {{ file.childCount === undefined ? 'Item count unavailable' : `${file.childCount} ${file.childCount === 1 ? 'item' : 'items'}` }}
                        </template>
                        <template v-else>{{ file.size || 'Size unavailable' }}</template>
                    </small>
                </div>
                <button
                    v-if="file.isImage"
                    type="button"
                    class="favourite"
                    :class="file.isFavourite ? 'favourite--confirmed' : ''"
                    :aria-label="file.isFavourite ? 'Favourite' : 'Add to favourites'"
                    @click.stop="fav(file)"
                >
                    {{ file.isFavourite ? '♥' : '♡' }}
                </button>
            </footer>
        </article>
    </div>
</template>
<script lang='ts'>
    import { storeKey } from '@/store'
    import { computed, defineComponent, onMounted } from 'vue'
    import { onBeforeRouteUpdate, useRoute } from 'vue-router'
    import { useStore } from 'vuex'
    import AudioPlayer from '@/components/AudioPlayer.vue'
    import VideoPlayer from '@/components/VideoPLayer.vue'
    import ArkaineFile from '@/models/arkaine-file'

    export default defineComponent({
        name: 'FilesView',
        components: {
            AudioPlayer,
            VideoPlayer
        },
        setup() {
            const store = useStore(storeKey)
            const route = useRoute()
            const files = computed(() => store.getters['orderedFiles'])
            const hasMoreFiles = computed(() => store.getters['hasMoreFiles'])
            let loadFiles = true

            const imageLoadErrorHandler = (e: Event) => {
                (e.target as HTMLImageElement).src = '/folder.png'
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
                if (!file.isFavourite) {
                    await store.dispatch('addToFavourite', file)
                    file.isFavourite = true
                }
            }

            window.onscroll = async () => {
                if (loadFiles && hasMoreFiles.value && ((window.innerHeight + window.scrollY) >= document.body.offsetHeight - 5)) {
                    loadFiles = false
                    await store.dispatch('loadMoreFiles', route.params.path)

                    // Allow loading again in 5 seconds
                    setTimeout(() => {
                        loadFiles = true
                    }, 5000)
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
    height: 100%;
    min-width: 0;
    display: flex;
    flex-direction: column;
    margin: 0;
    padding: 0;
    border: 1px solid var(--app-border);
    border-radius: 0.75rem;
    overflow: hidden;
    background: var(--app-surface);
    box-shadow: var(--pico-card-box-shadow);
}

.item__body {
    display: flex;
    flex: 1;
    min-width: 0;
}

.item__media {
    width: 100%;
    min-width: 0;
    flex: 1 1 auto;
}

.item__media--player {
    display: flex;
    align-items: stretch;
}

.item__media--player :deep(.player),
.item__media--player :deep(.video-player) {
    width: 100%;
}

.item__media--other {
    display: flex;
    align-items: center;
    padding-bottom: 1rem;
}

.item__footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
    min-width: 0;
    margin: 0;
    padding: 0.85rem 1rem 1rem;
    border-top: 1px solid var(--app-border);
}

.item__details {
    display: grid;
    min-width: 0;
    gap: 0.2rem;
}

.item__name {
    overflow: hidden;
    color: var(--pico-color);
    text-overflow: ellipsis;
    white-space: nowrap;
}

.item__size {
    overflow: hidden;
    color: var(--app-muted);
    text-overflow: ellipsis;
    white-space: nowrap;
}

.favourite {
    display: grid;
    flex: 0 0 auto;
    width: 2rem;
    height: 2rem;
    margin: 0;
    padding: 0;
    place-items: center;
    color: var(--app-muted);
    border: 0;
    border-radius: 0;
    background: transparent;
    box-shadow: none;
    font-size: 1.65rem;
    line-height: 1;

    &--confirmed {
        color: #c2185b;
    }

    &:hover,
    &:focus-visible {
        color: #c2185b;
        border: 0;
        background: transparent;
        box-shadow: none;
    }
}

.folder {
    display: flex;
}

.image {
    display: flex;
    overflow: hidden;
    align-items: center;
    justify-content: center;
    background: var(--app-surface-raised);
}

.media-link {
    display: flex;
    width: 100%;
    height: 100%;
    align-items: center;
    justify-content: center;
    overflow: hidden;
}

.media-link:hover,
.media-link:focus-visible {
    background: var(--pico-primary-focus);
}

.image img,
.folder img {
    display: block;
    width: 100%;
    max-height: 60vh;
    object-fit: contain;
}

.file-link {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}
</style>
