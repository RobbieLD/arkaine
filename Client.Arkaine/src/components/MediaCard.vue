<template>
    <article class="card media-card">
        <!-- Folder -->
        <router-link
            v-if="file.isDirectory"
            :to="to"
            class="media-card__media media-card__media--frame"
            :class="{ 'media-card__media--auto': !ratioStyle }"
            :style="ratioStyle"
            @mouseenter="$emit('prefetch')"
            @focus="$emit('prefetch')"
            @touchstart.passive="$emit('prefetch')"
        >
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
        <a
            v-else-if="file.isVideo"
            :href="file.url"
            target="_blank"
            rel="noopener"
            class="media-card__media media-card__media--frame"
            :class="{ 'media-card__media--auto': !ratioStyle }"
            :style="ratioStyle"
            :aria-label="`Open ${file.name}`"
        >
            <img
                class="media-card__image"
                :src="file.preview || '/icon.png'"
                :alt="file.name"
                loading="lazy"
                decoding="async"
                @error="onImageError"
            />
            <span class="media-card__badge">
                <app-icon name="play" />
            </span>
        </a>

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
                :aria-label="file.isFavourite ? 'Remove from favourites' : 'Add to favourites'"
                @click.stop.prevent="$emit('favourite')"
            >
                <app-icon :name="file.isFavourite ? 'heartFilled' : 'heart'" />
            </button>
            <button
                v-if="canCompressFile"
                type="button"
                class="btn btn--ghost btn--icon btn--sm media-card__compress"
                :class="{
                    'media-card__compress--queueing': compressionState === 'queueing',
                    'media-card__compress--queued': compressionState === 'queued'
                }"
                :disabled="compressionState !== 'idle'"
                :aria-label="compressionState === 'queued'
                    ? `Compression queued for ${file.name}`
                    : `Queue compression for ${file.name}`"
                :title="compressionState === 'queued'
                    ? 'Compression queued'
                    : 'Queue compression'"
                @click.stop.prevent="$emit('compress')"
            >
                <app-icon :name="compressionState === 'queued' ? 'check' : 'refresh'" />
            </button>
        </footer>
    </article>
</template>
<script lang="ts">
    import ArkaineFile from '@/models/arkaine-file'
    import { computed, defineComponent, PropType, ref } from 'vue'
    import AppIcon from './AppIcon.vue'
    import AudioPlayer from './AudioPlayer.vue'

    type CompressionState = 'idle' | 'queueing' | 'queued'

    export default defineComponent({
        name: 'MediaCard',
        components: {
            AppIcon,
            AudioPlayer
        },
        emits: ['compress', 'favourite', 'prefetch'],
        props: {
            file: {
                type: Object as PropType<ArkaineFile>,
                required: true
            },
            to: {
                type: String,
                default: ''
            },
            canCompress: {
                type: Boolean,
                default: false
            },
            compressionState: {
                type: String as PropType<CompressionState>,
                default: 'idle'
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

            const canCompressFile = computed(() =>
                props.canCompress &&
                props.file.isVideo &&
                !props.file.name.replace(/\.[^/.]+$/, '').endsWith('_compressed')
            )

            const onImageError = (event: Event) => {
                const image = event.target as HTMLImageElement

                if (image.dataset.fallbackApplied) {
                    return
                }

                image.dataset.fallbackApplied = 'true'
                failed.value = true
                image.src = '/icon.png'
            }

            return {
                onImageError,
                canCompressFile,
                ratioStyle,
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

    .media-card__compress--queueing {
        animation: media-card-compress-pulse 1s ease-in-out infinite;
    }

    .media-card__compress--queued {
        color: var(--success);
    }

    @keyframes media-card-compress-pulse {
        50% {
            opacity: 0.45;
        }
    }
</style>
