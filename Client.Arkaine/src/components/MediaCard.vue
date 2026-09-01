<template>
    <article class="card media-card">
        <!-- Folder -->
        <router-link
            v-if="file.isDirectory"
            :to="to"
            class="media-card__media"
            :class="showFolderPlaceholder
                ? 'media-card__media--placeholder'
                : ['media-card__media--frame', { 'media-card__media--auto': !ratioStyle }]"
            :style="ratioStyle"
            @mouseenter="$emit('prefetch')"
            @focus="$emit('prefetch')"
            @touchstart.passive="$emit('prefetch')"
        >
            <template v-if="!showFolderPlaceholder">
                <img
                    class="media-card__image"
                    :src="file.preview || file.thumb"
                    :alt="''"
                    loading="lazy"
                    decoding="async"
                    @error="onImageError"
                />
                <span class="media-card__badge">
                    <app-icon name="folder" />
                </span>
            </template>
        </router-link>

        <!-- Image -->
        <a
            v-else-if="file.isImage"
            :href="file.url"
            target="_blank"
            rel="noopener"
            class="media-card__media media-card__media--frame"
            :class="{ 'media-card__media--auto': !ratioStyle }"
            :style="ratioStyle"
        >
            <img
                class="media-card__image"
                :src="file.preview || file.url"
                :alt="file.name"
                loading="lazy"
                decoding="async"
                @error="onImageError"
            />
            <span class="media-card__badge media-card__badge--hover">
                <app-icon name="external" />
            </span>
        </a>

        <!-- Video -->
        <div v-else-if="file.isVideo" class="media-card__media">
            <video-player :file="file"></video-player>
        </div>

        <!-- Audio -->
        <div v-else-if="file.isAudio" class="media-card__media">
            <audio-player :file="file"></audio-player>
        </div>

        <!-- Anything else -->
        <a
            v-else
            :href="file.url"
            target="_blank"
            rel="noopener"
            class="media-card__media media-card__media--plain"
        >
            <app-icon name="file" />
            <span>Open file</span>
            <app-icon name="external" />
        </a>

        <footer class="media-card__footer">
            <div class="media-card__meta">
                <router-link
                    v-if="file.isDirectory"
                    :to="to"
                    class="media-card__name truncate"
                    :title="file.name"
                >{{ file.name }}</router-link>
                <a
                    v-else
                    :href="file.url"
                    :download="file.name"
                    class="media-card__name truncate"
                    :title="`Download ${file.name}`"
                >{{ file.name }}</a>
                <span class="media-card__sub truncate">{{ subtitle }}</span>
            </div>
            <button
                v-if="file.isImage"
                type="button"
                class="btn btn--ghost btn--icon btn--sm media-card__fav"
                :class="{ 'media-card__fav--on': file.isFavourite }"
                :aria-pressed="file.isFavourite"
                :aria-label="file.isFavourite ? 'In favourites' : 'Add to favourites'"
                @click.stop.prevent="$emit('favourite')"
            >
                <app-icon :name="file.isFavourite ? 'heartFilled' : 'heart'" />
            </button>
        </footer>
    </article>
</template>
<script lang="ts">
    import ArkaineFile from '@/models/arkaine-file'
    import { computed, defineComponent, PropType, ref } from 'vue'
    import AppIcon from './AppIcon.vue'
    import AudioPlayer from './AudioPlayer.vue'
    import VideoPlayer from './VideoPLayer.vue'

    export default defineComponent({
        name: 'MediaCard',
        components: {
            AppIcon,
            AudioPlayer,
            VideoPlayer
        },
        emits: ['favourite', 'prefetch'],
        props: {
            file: {
                type: Object as PropType<ArkaineFile>,
                required: true
            },
            to: {
                type: String,
                default: ''
            }
        },
        setup(props) {
            const failed = ref(false)

            /*
             * The server reports the thumbnail's intrinsic size, so the box can be
             * reserved at the correct aspect ratio before a single byte of image
             * arrives - no layout shift, and masonry measures once instead of twice.
             * Without dimensions the image is left to size itself naturally.
             */
            const ratioStyle = computed(() => {
                if (failed.value || !props.file.previewWidth || !props.file.previewHeight) {
                    return undefined
                }

                return {
                    '--media-ratio': `${props.file.previewWidth} / ${props.file.previewHeight}`
                }
            })

            const subtitle = computed(() => {
                if (!props.file.isDirectory) {
                    return props.file.size || 'Size unavailable'
                }

                return 'Folder'
            })

            const onImageError = (event: Event) => {
                failed.value = true

                // Folders fall back to a drawn placeholder rather than another image.
                if (props.file.isDirectory) {
                    return
                }

                const image = event.target as HTMLImageElement

                if (image.dataset.fallbackApplied) {
                    return
                }

                image.dataset.fallbackApplied = 'true'
                image.src = '/icon.png'
            }

            const showFolderPlaceholder = computed(() => props.file.isDirectory && failed.value)

            return {
                onImageError,
                ratioStyle,
                showFolderPlaceholder,
                subtitle
            }
        }
    })
</script>
<style lang="scss" scoped>
    .media-card {
        display: flex;
        min-width: 0;
        flex-direction: column;
        overflow: hidden;
        transition:
            border-color var(--duration-fast) var(--ease),
            transform var(--duration-fast) var(--ease);
    }

    .media-card:hover {
        border-color: var(--border-strong);
    }

    .media-card__media {
        display: block;
        width: 100%;
        min-width: 0;
    }

    /* Framed media: the box owns the aspect ratio, the image fills it exactly. */
    .media-card__media--frame {
        position: relative;
        aspect-ratio: var(--media-ratio, auto);
        background: var(--surface-sunken);
        line-height: 0;
    }

    .media-card__media--frame:focus-visible {
        outline-offset: -3px;
    }

    .media-card__image {
        display: block;
        width: 100%;
        height: 100%;
        object-fit: cover;
    }

    /* No server dimensions: let the image dictate the height instead of cropping. */
    .media-card__media--auto .media-card__image {
        height: auto;
    }

    /* Folders with no generated thumbnail get a drawn folder tab, not a stock image. */
    .media-card__media--placeholder {
        height: 3em;
        border-top: var(--folder-tab) 22px solid;
        border-right: var(--folder-tab) 22px solid;
        background: var(--folder);
    }

    .media-card__badge {
        position: absolute;
        top: var(--space-2);
        left: var(--space-2);
        display: grid;
        width: 1.75rem;
        height: 1.75rem;
        place-items: center;
        color: var(--text);
        border-radius: var(--radius-sm);
        background: rgb(6 9 14 / 65%);
        backdrop-filter: blur(4px);
        --icon-size: 1rem;
    }

    .media-card__badge--hover {
        top: auto;
        left: auto;
        right: var(--space-2);
        bottom: var(--space-2);
        opacity: 0;
        transition: opacity var(--duration-fast) var(--ease);
    }

    .media-card__media--frame:hover .media-card__badge--hover,
    .media-card__media--frame:focus-visible .media-card__badge--hover {
        opacity: 1;
    }

    .media-card__media--plain {
        display: flex;
        align-items: center;
        gap: var(--space-2);
        padding: var(--space-5) var(--space-4);
        color: var(--text-muted);
        font-size: var(--text-sm);
        font-weight: 600;
        text-decoration: none;
    }

    .media-card__media--plain span {
        flex: 1 1 auto;
    }

    .media-card__media--plain:hover {
        color: var(--accent);
    }

    .media-card__footer {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--space-3);
        min-width: 0;
        padding: var(--space-3) var(--space-3) var(--space-3) var(--space-4);
        border-top: 1px solid var(--border);
    }

    .media-card__meta {
        display: grid;
        min-width: 0;
        gap: 0.15rem;
    }

    .media-card__name {
        color: var(--text);
        font-size: var(--text-sm);
        font-weight: 600;
        text-decoration: none;
    }

    .media-card__name:hover,
    .media-card__name:focus-visible {
        color: var(--accent);
        text-decoration: underline;
    }

    .media-card__sub {
        color: var(--text-subtle);
        font-size: var(--text-xs);
    }

    .media-card__fav:hover {
        color: var(--favourite);
    }

    .media-card__fav--on {
        color: var(--favourite);
    }
</style>
