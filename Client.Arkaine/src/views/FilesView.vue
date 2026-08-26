<template>
    <section class="gallery">
        <div ref="grid" class="masonry" :aria-busy="showSkeletons">
            <template v-if="showSkeletons">
                <skeleton-card
                    v-for="n of skeletonRatios.length"
                    :key="`skeleton-${n}`"
                    :ratio="skeletonRatios[n - 1]"
                />
            </template>
            <template v-else>
                <media-card
                    v-for="file of files"
                    :key="file.rawFileName || file.name"
                    :file="file"
                    :to="folderLink(file)"
                    @favourite="fav(file)"
                    @prefetch="prefetch(file)"
                />
            </template>
        </div>

        <div v-if="isEmpty" class="empty-state">
            <app-icon name="folder" />
            <p>This folder is empty.</p>
        </div>

        <div ref="sentinel" class="gallery__sentinel" aria-hidden="true"></div>

        <p v-if="loadingMore" class="gallery__status muted" role="status">Loading more…</p>
    </section>
</template>
<script lang="ts">
    import { storeKey } from '@/store'
    import { computed, defineComponent, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
    import { useRoute } from 'vue-router'
    import { useStore } from 'vuex'
    import ArkaineFile from '@/models/arkaine-file'
    import AppIcon from '@/components/AppIcon.vue'
    import MediaCard from '@/components/MediaCard.vue'
    import SkeletonCard from '@/components/SkeletonCard.vue'
    import useMasonry from '@/composables/useMasonry'

    const skeletonRatios = [0.75, 1.3, 0.62, 1, 0.8, 1.45, 0.7, 1.1, 0.95, 0.66, 1.25, 0.85]

    export default defineComponent({
        name: 'FilesView',
        components: {
            AppIcon,
            MediaCard,
            SkeletonCard
        },
        setup() {
            const store = useStore(storeKey)
            const route = useRoute()

            const grid = ref<HTMLElement>()
            const sentinel = ref<HTMLElement>()
            const loadingMore = ref(false)

            const files = computed<ArkaineFile[]>(() => store.getters['files'])
            const hasMoreFiles = computed<boolean>(() => store.getters['hasMoreFiles'])
            const showSkeletons = computed<boolean>(() => store.getters['isLoadingFolder'])
            const isEmpty = computed(() => !showSkeletons.value && files.value.length === 0)

            const { refresh } = useMasonry(grid)

            const folderLink = (file: ArkaineFile): string => {
                if (!file.isDirectory) {
                    return ''
                }

                // Favourites is a pseudo folder that lives at a fixed root-level path.
                return file.rawFileName ? `/${file.rawFileName}` : `/${file.name}/`
            }

            const prefetch = (file: ArkaineFile) => {
                if (file.isDirectory) {
                    store.dispatch('prefetchFolder', file.rawFileName || `${file.name}/`)
                }
            }

            const fav = async (file: ArkaineFile) => {
                if (!file.isFavourite) {
                    await store.dispatch('addToFavourite', file).catch(() => undefined)
                }
            }

            const loadMore = async () => {
                if (loadingMore.value || !hasMoreFiles.value || showSkeletons.value) {
                    return
                }

                loadingMore.value = true

                try {
                    await store.dispatch('loadMoreFiles', route.params.path)
                }
                catch {
                    // The store has already surfaced the failure as an alert.
                }
                finally {
                    loadingMore.value = false
                }
            }

            watch(() => route.params.path, path => {
                // Navigation is never awaited here: the store swaps the current path
                // synchronously so cached content or skeletons appear immediately.
                store.dispatch('loadFiles', path).catch(() => undefined)
                window.scrollTo({ top: 0 })
            }, { immediate: true })

            // Card list changes need the shared observer re-pointed at the new children.
            watch([files, showSkeletons], async () => {
                await nextTick()
                refresh()
            })

            let observer: IntersectionObserver | undefined

            onMounted(() => {
                observer = new IntersectionObserver(entries => {
                    if (entries.some(entry => entry.isIntersecting)) {
                        loadMore()
                    }
                }, { rootMargin: '600px 0px' })

                if (sentinel.value) {
                    observer.observe(sentinel.value)
                }
            })

            onBeforeUnmount(() => {
                observer?.disconnect()
                observer = undefined
            })

            return {
                fav,
                files,
                folderLink,
                grid,
                isEmpty,
                loadingMore,
                prefetch,
                sentinel,
                showSkeletons,
                skeletonRatios
            }
        },
    })
</script>
<style lang="scss" scoped>
    /*
     * CSS-grid masonry. `grid-auto-rows` is a small fixed unit and the row gap is zero;
     * useMasonry gives every card a `grid-row-end: span N` from its measured height, so
     * cards size to their own content instead of being stretched to match their row.
     */
    .masonry {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(15rem, 1fr));
        grid-auto-rows: 8px;
        column-gap: var(--space-4);
        row-gap: 0;
        align-items: start;
    }

    .masonry > * {
        margin-bottom: var(--space-4);
    }

    /* Single column: spans are removed by the composable, so cards just stack. */
    .masonry--stacked {
        grid-template-columns: 1fr;
        grid-auto-rows: auto;
    }

    .gallery__sentinel {
        width: 100%;
        height: 1px;
    }

    .gallery__status {
        padding: var(--space-4);
        font-size: var(--text-sm);
        text-align: center;
    }

    .empty-state {
        --icon-size: 2rem;
    }
</style>
