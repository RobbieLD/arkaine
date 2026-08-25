<template>
    <div class="settings-page">
        <header class="page-header">
            <div>
                <p class="eyebrow">Administration</p>
                <h1>Media processing</h1>
                <p>Manage thumbnail generation and monitor the library index.</p>
            </div>
            <span class="status-badge" :class="{ 'status-badge--running': options.isRunning }" aria-live="polite">
                <span class="status-badge__dot" aria-hidden="true"></span>
                {{ options.isRunning ? 'Running' : 'Idle' }}
            </span>
        </header>

        <section class="stats" aria-label="Thumbnail statistics">
            <article class="stat-card">
                <span class="stat-card__label">Total thumbnails</span>
                <strong>{{ options.totalThumbnails.toLocaleString() }}</strong>
                <small>Available in the library</small>
            </article>
            <article class="stat-card">
                <span class="stat-card__label">Needs attention</span>
                <strong class="stat-card__value--warning">{{ options.badThumbnails.toLocaleString() }}</strong>
                <small>Thumbnails that could not be generated</small>
            </article>
            <article class="stat-card">
                <span class="stat-card__label">This run</span>
                <strong>{{ progress.generated.toLocaleString() }}</strong>
                <small>{{ progressMessage }}</small>
            </article>
        </section>

        <section class="panel">
            <header class="panel__header">
                <div>
                    <h2>Thumbnail generation</h2>
                    <p>Scan the media library and create missing thumbnails.</p>
                </div>
                <button
                    type="button"
                    :class="{ secondary: options.isRunning }"
                    :aria-label="options.isRunning ? 'Cancel thumbnail generation' : 'Start thumbnail generation'"
                    @click="handleStartClick"
                >
                    {{ options.isRunning ? 'Cancel run' : 'Start generation' }}
                </button>
            </header>

            <div v-if="options.isRunning" class="running-state" role="status">
                <progress aria-label="Thumbnail generation is in progress"></progress>
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
    </div>
</template>

<script lang="ts">
    import { storeKey } from '@/store'
    import { computed, defineComponent, onMounted } from 'vue'
    import { useStore } from 'vuex'

    export default defineComponent({
        name: 'SettingsView',
        setup() {
            const store = useStore(storeKey)
            const options = computed(() => store.state.settings)
            const progress = computed(() => store.state.progress)
            const progressMessage = computed(() => {
                if (options.value.isRunning) {
                    return `${(progress.value.generated + progress.value.failed).toLocaleString()} files processed in this run`
                }

                return progress.value.finished ? 'Last run completed' : 'Ready to start'
            })

            const handleStartClick = async () => {
                if (options.value.isRunning) {
                    await store.dispatch('cancelGeneration')
                }
                else {
                    await store.dispatch('startGeneration')
                }
            }

            onMounted(async () => {
                await store.dispatch('loadSettings')
                await store.dispatch('subscribeToUpdates')
            })

            return {
                options,
                progress,
                progressMessage,
                handleStartClick
            }
        },
    })
</script>
<style scoped>
.settings-page {
    max-width: 72rem;
    margin: 0 auto;
}

.page-header {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 1rem;
    margin-bottom: 2rem;
}

.eyebrow {
    margin: 0 0 0.4rem;
    color: var(--pico-primary-hover);
    font-size: 0.75rem;
    font-weight: 700;
    letter-spacing: 0.1em;
    text-transform: uppercase;
}

h1,
h2 {
    margin-top: 0;
}

.page-header h1 {
    margin-bottom: 0.5rem;
}

.page-header p:last-child,
.panel__header p {
    margin-bottom: 0;
    color: var(--app-muted);
}

.status-badge {
    display: inline-flex;
    align-items: center;
    flex: 0 0 auto;
    gap: 0.45rem;
    padding: 0.4rem 0.7rem;
    color: var(--app-muted);
    border: 1px solid var(--app-border);
    border-radius: 2rem;
    background: var(--app-surface-raised);
    font-size: 0.8rem;
    font-weight: 700;
}

.status-badge__dot {
    width: 0.5rem;
    height: 0.5rem;
    border-radius: 50%;
    background: var(--app-muted);
}

.status-badge--running {
    color: var(--pico-primary-hover);
    border-color: var(--pico-primary);
    background: var(--pico-primary-focus);
}

.status-badge--running .status-badge__dot {
    background: var(--pico-primary);
    box-shadow: 0 0 0 0.2rem var(--pico-primary-focus);
}

.stats {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 1rem;
    margin-bottom: 1rem;
}

.stat-card,
.panel {
    margin: 0;
    border: 1px solid var(--app-border);
    border-radius: 0.75rem;
    background: var(--app-surface);
    box-shadow: var(--pico-card-box-shadow);
}

.stat-card {
    padding: 1.25rem;
}

.stat-card__label {
    display: block;
    margin-bottom: 0.6rem;
    color: var(--app-muted);
    font-size: 0.85rem;
}

.stat-card strong {
    display: block;
    color: var(--pico-h2-color);
    font-size: 1.8rem;
    line-height: 1.1;
}

.stat-card__value--warning {
    color: var(--pico-del-color) !important;
}

.stat-card small {
    display: block;
    min-height: 2.5em;
    margin-top: 0.65rem;
    color: var(--app-muted);
}

.panel {
    padding: clamp(1.25rem, 3vw, 2rem);
}

.panel__header {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 1rem;
    padding-bottom: 1.5rem;
    border-bottom: 1px solid var(--app-border);
}

.panel__header h2 {
    margin-bottom: 0.45rem;
    font-size: 1.35rem;
}

.panel__header button {
    flex: 0 0 auto;
    margin: 0;
}

.running-state {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    padding: 1rem 0;
    color: var(--app-muted);
    font-size: 0.9rem;
}

.running-state progress {
    width: 7rem;
    margin: 0;
}

.settings-list {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 0;
    margin: 0;
}

.settings-list > div {
    padding: 1rem 1rem 1rem 0;
    border-bottom: 1px solid var(--app-border);
}

.settings-list > div:nth-last-child(-n + 2) {
    border-bottom: 0;
}

.settings-list dt {
    margin-bottom: 0.3rem;
    color: var(--app-muted);
    font-size: 0.8rem;
}

.settings-list dd {
    margin: 0;
    color: var(--pico-color);
    font-weight: 600;
    overflow-wrap: anywhere;
}

.settings-list code {
    color: var(--pico-color);
    background: var(--app-surface-raised);
}

@media only screen and (max-width: 680px) {
    .page-header,
    .panel__header {
        flex-direction: column;
    }

    .panel__header button {
        width: 100%;
    }

    .stats,
    .settings-list {
        grid-template-columns: 1fr;
    }

    .settings-list > div:nth-last-child(-n + 2) {
        border-bottom: 1px solid var(--app-border);
    }

    .settings-list > div:last-child {
        border-bottom: 0;
    }
}
</style>
