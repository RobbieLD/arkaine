<template>
    <div class="page settings-page">
        <header class="page-header">
            <div>
                <p class="eyebrow">Administration</p>
                <h1 class="page-header__title">Media processing</h1>
                <p class="page-header__lead">Manage thumbnail generation and monitor the library index.</p>
            </div>
            <span class="badge" :class="{ 'badge--active': options.isRunning }" aria-live="polite">
                <span class="status-dot" aria-hidden="true"></span>
                {{ options.isRunning ? 'Running' : 'Idle' }}
            </span>
        </header>

        <section class="stats" aria-label="Thumbnail statistics">
            <article class="card stat-card">
                <span class="stat-card__label">Total thumbnails</span>
                <strong class="stat-card__value">{{ options.totalThumbnails.toLocaleString() }}</strong>
                <small class="stat-card__note">Available in the library</small>
            </article>
            <article class="card stat-card">
                <span class="stat-card__label">Needs attention</span>
                <strong class="stat-card__value stat-card__value--warning">
                    {{ options.badThumbnails.toLocaleString() }}
                </strong>
                <small class="stat-card__note">Thumbnails that could not be generated</small>
            </article>
            <article class="card stat-card">
                <span class="stat-card__label">This run</span>
                <strong class="stat-card__value">{{ progress.generated.toLocaleString() }}</strong>
                <small class="stat-card__note">{{ progressMessage }}</small>
            </article>
        </section>

        <section class="panel">
            <header class="panel__header">
                <div>
                    <h2 class="panel__title">Thumbnail generation</h2>
                    <p class="muted">Scan the media library and create missing thumbnails.</p>
                </div>
                <button
                    type="button"
                    class="btn"
                    :class="options.isRunning ? 'btn--danger' : 'btn--primary'"
                    :aria-label="options.isRunning ? 'Cancel thumbnail generation' : 'Start thumbnail generation'"
                    @click="handleStartClick"
                >
                    <app-icon :name="options.isRunning ? 'close' : 'play'" />
                    {{ options.isRunning ? 'Cancel run' : 'Start generation' }}
                </button>
            </header>

            <div v-if="options.isRunning" class="running-state" role="status">
                <progress class="progress" aria-label="Thumbnail generation is in progress"></progress>
                <span>{{ progressMessage }}</span>
            </div>

            <dl class="settings-list">
                <div>
                    <dt>Thumbnail directory</dt>
                    <dd><code>{{ options.thumbnailDir || 'Not configured' }}</code></dd>
                </div>
                <div>
                    <dt>File types</dt>
                    <dd>{{ options.thumbnailExtensions || 'All supported types' }}</dd>
                </div>
                <div>
                    <dt>Thumbnail width</dt>
                    <dd>{{ options.thumbnailWidth }} px</dd>
                </div>
                <div>
                    <dt>Page size</dt>
                    <dd>{{ options.thumbnailPageSize.toLocaleString() }} files</dd>
                </div>
                <div>
                    <dt>Scanned</dt>
                    <dd>{{ progress.scanned.toLocaleString() }}</dd>
                </div>
                <div>
                    <dt>Failed this run</dt>
                    <dd>{{ progress.failed.toLocaleString() }}</dd>
                </div>
            </dl>
        </section>
        <section class="panel">
            <header class="panel__header">
                <div>
                    <h2 class="panel__title">Thumbnail dimension cache</h2>
                    <p class="muted">
                        Image dimensions are read once from each thumbnail header and kept in memory, so
                        listing a folder does not re-open every file.
                    </p>
                </div>
                <div class="panel__actions">
                    <button
                        type="button"
                        class="btn btn--sm"
                        :disabled="cacheBusy"
                        @click="refreshCache"
                    >
                        <app-icon name="refresh" />
                        Refresh
                    </button>
                    <button
                        type="button"
                        class="btn btn--sm btn--danger"
                        :disabled="cacheBusy || cache.entries === 0"
                        @click="clearCache"
                    >
                        <app-icon name="trash" />
                        Clear
                    </button>
                </div>
            </header>

            <div class="meters">
                <div class="meter">
                    <div class="meter__head">
                        <span class="meter__label">Hit rate</span>
                        <strong class="meter__value" :style="{ color: hitRateColour }">
                            {{ cacheLookups ? hitRate.toFixed(1) + '%' : '—' }}
                        </strong>
                    </div>
                    <div class="meter__track">
                        <div
                            class="meter__fill"
                            :style="{ width: hitRate + '%', background: hitRateColour }"
                        ></div>
                    </div>
                    <small class="meter__note">
                        {{ cacheLookups.toLocaleString() }} lookups since {{ cacheSince }}
                    </small>
                </div>

                <div class="meter">
                    <div class="meter__head">
                        <span class="meter__label">Capacity used</span>
                        <strong class="meter__value">{{ usage.toFixed(1) }}%</strong>
                    </div>
                    <div class="meter__track">
                        <div class="meter__fill meter__fill--accent" :style="{ width: usage + '%' }"></div>
                    </div>
                    <small class="meter__note">
                        {{ cache.entries.toLocaleString() }} of {{ cache.capacity.toLocaleString() }} entries
                    </small>
                </div>
            </div>

            <dl class="settings-list">
                <div>
                    <dt>Hits</dt>
                    <dd>{{ cache.hits.toLocaleString() }}</dd>
                </div>
                <div>
                    <dt>Misses</dt>
                    <dd>{{ cache.misses.toLocaleString() }}</dd>
                </div>
                <div>
                    <dt>Re-read after change</dt>
                    <dd>{{ cache.stale.toLocaleString() }}</dd>
                </div>
                <div>
                    <dt>Unreadable images</dt>
                    <dd :class="{ 'value--warning': cache.unreadable > 0 }">
                        {{ cache.unreadable.toLocaleString() }}
                    </dd>
                </div>
                <div>
                    <dt>Thumbnail not found</dt>
                    <dd>{{ cache.notFound.toLocaleString() }}</dd>
                </div>
                <div>
                    <dt>Cache resets</dt>
                    <dd>{{ cache.resets.toLocaleString() }}{{ lastReset ? ` · last ${lastReset}` : '' }}</dd>
                </div>
            </dl>
        </section>
    </div>
</template>

<script lang="ts">
    import { storeKey } from '@/store'
    import { computed, defineComponent, onBeforeUnmount, onMounted, ref } from 'vue'
    import { useStore } from 'vuex'
    import AppIcon from '@/components/AppIcon.vue'

    export default defineComponent({
        name: 'SettingsView',
        components: {
            AppIcon
        },
        setup() {
            const store = useStore(storeKey)
            const options = computed(() => store.state.settings)
            const progress = computed(() => store.state.progress)
            const cache = computed(() => store.state.thumbnailCache)
            const cacheBusy = ref(false)

            const progressMessage = computed(() => {
                if (options.value.isRunning) {
                    return `${(progress.value.generated + progress.value.failed).toLocaleString()} files processed in this run`
                }

                return progress.value.finished ? 'Last run completed' : 'Ready to start'
            })

            const cacheLookups = computed(() => cache.value.hits + cache.value.misses + cache.value.stale)

            const hitRate = computed(() => {
                return cacheLookups.value ? (cache.value.hits / cacheLookups.value) * 100 : 0
            })

            const hitRateColour = computed(() => {
                if (!cacheLookups.value) {
                    return 'var(--text-subtle)'
                }

                if (hitRate.value >= 80) {
                    return 'var(--success)'
                }

                return hitRate.value >= 50 ? 'var(--warning)' : 'var(--danger)'
            })

            const usage = computed(() => {
                return cache.value.capacity ? (cache.value.entries / cache.value.capacity) * 100 : 0
            })

            const formatTime = (value: string | null) => {
                if (!value) {
                    return ''
                }

                const parsed = new Date(value)
                return Number.isNaN(parsed.valueOf()) ? '' : parsed.toLocaleString()
            }

            const cacheSince = computed(() => formatTime(cache.value.startedUtc) || 'startup')
            const lastReset = computed(() => formatTime(cache.value.lastResetUtc))

            const handleStartClick = async () => {
                if (options.value.isRunning) {
                    await store.dispatch('cancelGeneration')
                }
                else {
                    await store.dispatch('startGeneration')
                }
            }

            const runCacheAction = async (action: string) => {
                cacheBusy.value = true

                try {
                    await store.dispatch(action)
                }
                finally {
                    cacheBusy.value = false
                }
            }

            const refreshCache = () => runCacheAction('loadThumbnailCacheStats')
            const clearCache = () => runCacheAction('clearThumbnailCache')

            onMounted(async () => {
                await store.dispatch('loadSettings')
                await store.dispatch('loadThumbnailCacheStats')
                await store.dispatch('subscribeToUpdates')
            })

            onBeforeUnmount(async () => {
                await store.dispatch('unsubscribeFromUpdates')
            })

            return {
                options,
                progress,
                progressMessage,
                handleStartClick,
                cache,
                cacheBusy,
                cacheLookups,
                cacheSince,
                clearCache,
                hitRate,
                hitRateColour,
                lastReset,
                refreshCache,
                usage
            }
        },
    })
</script>
<style lang="scss" scoped>
    .settings-page {
        max-width: 72rem;
        margin: 0 auto;
    }

    .status-dot {
        width: 0.5rem;
        height: 0.5rem;
        border-radius: 50%;
        background: currentcolor;
    }

    .stats {
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        gap: var(--space-4);
    }

    .stat-card {
        padding: var(--space-5);
    }

    .stat-card__label {
        display: block;
        margin-bottom: var(--space-2);
        color: var(--text-muted);
        font-size: var(--text-sm);
    }

    .stat-card__value {
        display: block;
        font-size: var(--text-2xl);
        line-height: var(--leading-tight);
    }

    .stat-card__value--warning {
        color: var(--warning);
    }

    .stat-card__note {
        display: block;
        min-height: 2.5em;
        margin-top: var(--space-2);
        color: var(--text-subtle);
        font-size: var(--text-xs);
    }

    .panel__header {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        gap: var(--space-4);
        padding-bottom: var(--space-5);
        border-bottom: 1px solid var(--border);
    }

    .panel__title {
        margin-bottom: var(--space-1);
        font-size: var(--text-xl);
    }

    .panel__header .btn {
        flex: 0 0 auto;
        --icon-size: 1rem;
    }

    .panel__actions {
        display: flex;
        flex: 0 0 auto;
        gap: var(--space-2);
    }

    .meters {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: var(--space-6);
        padding: var(--space-5) 0;
        border-bottom: 1px solid var(--border);
    }

    .meter__head {
        display: flex;
        align-items: baseline;
        justify-content: space-between;
        gap: var(--space-3);
        margin-bottom: var(--space-2);
    }

    .meter__label {
        color: var(--text-muted);
        font-size: var(--text-sm);
    }

    .meter__value {
        font-size: var(--text-xl);
        font-variant-numeric: tabular-nums;
    }

    .meter__track {
        height: 0.5rem;
        overflow: hidden;
        background: var(--surface-sunken);
        border-radius: var(--radius-pill);
    }

    .meter__fill {
        height: 100%;
        border-radius: inherit;
        transition: width var(--duration) var(--ease);
    }

    .meter__fill--accent {
        background: var(--accent);
    }

    .meter__note {
        display: block;
        margin-top: var(--space-2);
        color: var(--text-subtle);
        font-size: var(--text-xs);
    }

    .settings-list dd.value--warning {
        color: var(--warning);
    }

    .running-state {
        display: flex;
        align-items: center;
        gap: var(--space-3);
        padding: var(--space-4) 0;
        color: var(--text-muted);
        font-size: var(--text-sm);
    }

    .running-state .progress {
        width: 7rem;
    }

    .settings-list {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 0;
        margin: 0;
    }

    .settings-list > div {
        padding: var(--space-4) var(--space-4) var(--space-4) 0;
        border-bottom: 1px solid var(--border);
    }

    .settings-list > div:nth-last-child(-n + 2) {
        border-bottom: 0;
    }

    .settings-list dt {
        margin-bottom: var(--space-1);
        color: var(--text-subtle);
        font-size: var(--text-xs);
    }

    .settings-list dd {
        margin: 0;
        color: var(--text);
        font-weight: 600;
        overflow-wrap: anywhere;
    }

    @media only screen and (max-width: 680px) {
        .page-header,
        .panel__header {
            flex-direction: column;
            align-items: stretch;
        }

        .panel__header .btn {
            width: 100%;
        }

        .stats,
        .settings-list,
        .meters {
            grid-template-columns: 1fr;
        }

        .panel__actions .btn {
            flex: 1 1 0;
        }

        .settings-list > div:nth-last-child(-n + 2) {
            border-bottom: 1px solid var(--border);
        }

        .settings-list > div:last-child {
            border-bottom: 0;
        }
    }
</style>
