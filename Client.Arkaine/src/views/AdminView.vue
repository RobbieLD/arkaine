<template>
    <div class="page admin-page">
        <header class="page-header">
            <div>
                <p class="eyebrow">Administration</p>
                <h1 class="page-header__title">Media processing</h1>
                <p class="page-header__lead">
                    Manage thumbnail generation and browser-friendly media conversion.
                </p>
            </div>
            <div class="header-badges" aria-label="Job status">
                <span class="badge" :class="{ 'badge--active': adminStatus.thumbnails.isRunning }" aria-live="polite">
                    <span class="status-dot" aria-hidden="true"></span>
                    Thumbnails {{ adminStatus.thumbnails.isRunning ? 'running' : 'idle' }}
                </span>
                <span class="badge" :class="conversionBadgeClass" aria-live="polite">
                    <span class="status-dot" aria-hidden="true"></span>
                    Conversion {{ conversionStatusLabel }}
                </span>
            </div>
        </header>

        <p v-if="loading" class="muted" role="status">Loading admin tools...</p>

        <section v-else-if="accessDenied" class="panel">
            <h2 class="panel__title">Access denied</h2>
            <p class="muted">You do not have access to the admin area.</p>
        </section>

        <section v-else-if="loadError" class="panel">
            <h2 class="panel__title">Unable to load admin tools</h2>
            <p class="muted">{{ loadError }}</p>
        </section>

        <template v-else>
            <section class="stats" aria-label="Admin overview">
                <article class="card stat-card">
                    <span class="stat-card__label">Total thumbnails</span>
                    <strong class="stat-card__value">{{ adminStatus.thumbnails.totalThumbnails.toLocaleString() }}</strong>
                    <small class="stat-card__note">Available in the library</small>
                </article>
                <article class="card stat-card">
                    <span class="stat-card__label">Generated this run</span>
                    <strong class="stat-card__value">{{ thumbnailProgress.generated.toLocaleString() }}</strong>
                    <small class="stat-card__note">{{ thumbnailCardNote }}</small>
                </article>
                <article class="card stat-card">
                    <span class="stat-card__label">Converted this run</span>
                    <strong
                        class="stat-card__value"
                        :class="{ 'stat-card__value--warning': adminStatus.conversion.ffmpegAvailable === false }"
                    >
                        {{ conversionProgress.converted.toLocaleString() }}
                    </strong>
                    <small class="stat-card__note">{{ conversionCardNote }}</small>
                </article>
            </section>

            <job-panel
                title="Thumbnail generation"
                description="Scan the media library and create missing image and video thumbnails."
                :running="adminStatus.thumbnails.isRunning"
                start-label="Start generation"
                stop-label="Cancel run"
                start-aria-label="Start thumbnail generation"
                stop-aria-label="Cancel thumbnail generation"
                progress-aria-label="Thumbnail generation is in progress"
                :progress-message="thumbnailProgressMessage"
                :idle-message="thumbnailIdleMessage"
                @toggle="toggleThumbnails"
            >
                <div>
                    <dt>Thumbnail directory</dt>
                    <dd><code>{{ adminStatus.thumbnails.thumbnailDir || 'Not configured' }}</code></dd>
                </div>
                <div>
                    <dt>Source types</dt>
                    <dd>{{ adminStatus.thumbnails.thumbnailExtensions || 'All supported types' }}</dd>
                </div>
                <div>
                    <dt>Target output</dt>
                    <dd>{{ adminStatus.thumbnails.thumbnailWidth }} px thumbnail</dd>
                </div>
                <div>
                    <dt>Page size</dt>
                    <dd>{{ adminStatus.thumbnails.thumbnailPageSize.toLocaleString() }} files</dd>
                </div>
                <div>
                    <dt>Scanned</dt>
                    <dd>{{ thumbnailProgress.scanned.toLocaleString() }}</dd>
                </div>
                <div>
                    <dt>Generated</dt>
                    <dd>{{ thumbnailProgress.generated.toLocaleString() }}</dd>
                </div>
                <div>
                    <dt>Failed this run</dt>
                    <dd :class="{ 'value--warning': thumbnailProgress.failed > 0 }">
                        {{ thumbnailProgress.failed.toLocaleString() }}
                    </dd>
                </div>
                <div>
                    <dt>Cancelled</dt>
                    <dd>{{ thumbnailProgress.cancelled ? 'Yes' : 'No' }}</dd>
                </div>
                <div>
                    <dt>Current file</dt>
                    <dd>{{ thumbnailProgress.currentFile || 'Waiting for the next update' }}</dd>
                </div>
            </job-panel>

            <job-panel
                title="Browser media conversion"
                description="Convert unsupported media and high-bitrate videos to browser-friendly formats in the selected path. Originals are kept and compressed video targets use a _compressed.mp4 suffix."
                :running="adminStatus.conversion.isRunning"
                :unavailable="adminStatus.conversion.ffmpegAvailable === false"
                :toggle-disabled="conversionStartDisabled"
                start-label="Start conversion"
                stop-label="Cancel run"
                start-aria-label="Start media conversion"
                stop-aria-label="Cancel media conversion"
                progress-aria-label="Media conversion is in progress"
                :progress-message="conversionProgressMessage"
                :idle-message="conversionIdleMessage"
                unavailable-message="FFmpeg is unavailable on the server. Install it and the required encoders before starting conversion."
                @toggle="toggleConversion"
            >
                <div>
                    <dt><label for="conversion-path">Path</label></dt>
                    <dd>
                        <select
                            id="conversion-path"
                            v-model="conversionPath"
                            class="select"
                            :disabled="adminStatus.conversion.isRunning || conversionPaths.length === 0"
                        >
                            <option value="" disabled>Select root or a top-level folder</option>
                            <option v-for="path of conversionPaths" :key="path" :value="path">
                                {{ path === rootConversionPath ? 'Root' : path }}
                            </option>
                        </select>
                    </dd>
                </div>
                <div>
                    <dt>FFmpeg</dt>
                    <dd :class="{ 'value--warning': adminStatus.conversion.ffmpegAvailable === false }">
                        {{ ffmpegStatus }}
                    </dd>
                </div>
                <div>
                    <dt>FFmpeg path</dt>
                    <dd><code>{{ adminStatus.conversion.ffmpegPath || 'Not reported' }}</code></dd>
                </div>
                <div>
                    <dt>Temporary directory</dt>
                    <dd><code>{{ adminStatus.conversion.conversionTempDir || 'Not reported' }}</code></dd>
                </div>
                <div>
                    <dt>Page size</dt>
                    <dd>{{ adminStatus.conversion.conversionPageSize.toLocaleString() }} files</dd>
                </div>
                <div>
                    <dt>Image sources</dt>
                    <dd>{{ adminStatus.conversion.imageExtensions || 'Not reported' }}</dd>
                </div>
                <div>
                    <dt>Video sources</dt>
                    <dd>{{ adminStatus.conversion.videoExtensions || 'Not reported' }}</dd>
                </div>
                <div>
                    <dt>Target formats</dt>
                    <dd>Images → JPEG · Video → _compressed.mp4 (H.264/AAC)</dd>
                </div>
                <div>
                    <dt>Video bitrate limit/output cap</dt>
                    <dd>{{ formatBitrate(adminStatus.conversion.videoMaxBitrate) }}</dd>
                </div>
                <div>
                    <dt>Scanned</dt>
                    <dd>{{ conversionProgress.scanned.toLocaleString() }}</dd>
                </div>
                <div>
                    <dt>Converted</dt>
                    <dd>{{ conversionProgress.converted.toLocaleString() }}</dd>
                </div>
                <div>
                    <dt>Skipped</dt>
                    <dd>{{ conversionProgress.skipped.toLocaleString() }}</dd>
                </div>
                <div>
                    <dt>Failed this run</dt>
                    <dd :class="{ 'value--warning': conversionProgress.failed > 0 }">
                        {{ conversionProgress.failed.toLocaleString() }}
                    </dd>
                </div>
                <div>
                    <dt>Cancelled</dt>
                    <dd>{{ conversionProgress.cancelled ? 'Yes' : 'No' }}</dd>
                </div>
                <div>
                    <dt>Current file</dt>
                    <dd>{{ conversionProgress.currentFile || 'Waiting for the next update' }}</dd>
                </div>
            </job-panel>

            <section class="panel">
                <header class="panel__header">
                    <div>
                        <h2 class="panel__title">Processing reports</h2>
                        <p class="muted">
                            Download a report from a completed thumbnail or conversion run.
                        </p>
                    </div>
                    <button
                        type="button"
                        class="btn btn--sm btn--danger"
                        :disabled="reportBusy || processingReports.length === 0"
                        @click="clearReports"
                    >
                        <app-icon name="trash" />
                        Clear reports
                    </button>
                </header>

                <div class="report-list">
                    <div class="report-row">
                        <div class="report-row__copy">
                            <label for="thumbnail-report">Thumbnail report</label>
                            <small class="muted">Failures and file details</small>
                        </div>
                        <div class="report-row__controls">
                            <select
                                id="thumbnail-report"
                                v-model="selectedThumbnailReport"
                                class="select"
                                :disabled="reportBusy || thumbnailReports.length === 0"
                            >
                                <option value="">
                                    {{ thumbnailReports.length === 0 ? 'No reports available' : 'Select a report' }}
                                </option>
                                <option
                                    v-for="report of thumbnailReports"
                                    :key="report.id"
                                    :value="String(report.id)"
                                >
                                    {{ report.name }}
                                </option>
                            </select>
                            <button
                                type="button"
                                class="btn btn--sm"
                                :disabled="reportBusy || !selectedThumbnailReport"
                                @click="downloadReport(selectedThumbnailReport)"
                            >
                                <app-icon name="external" />
                                Download
                            </button>
                        </div>
                    </div>

                    <div class="report-row">
                        <div class="report-row__copy">
                            <label for="conversion-report">Conversion report</label>
                            <small class="muted">Every skipped, converted, and failed file</small>
                        </div>
                        <div class="report-row__controls">
                            <select
                                id="conversion-report"
                                v-model="selectedConversionReport"
                                class="select"
                                :disabled="reportBusy || conversionReports.length === 0"
                            >
                                <option value="">
                                    {{ conversionReports.length === 0 ? 'No reports available' : 'Select a report' }}
                                </option>
                                <option
                                    v-for="report of conversionReports"
                                    :key="report.id"
                                    :value="String(report.id)"
                                >
                                    {{ report.name }}
                                </option>
                            </select>
                            <button
                                type="button"
                                class="btn btn--sm"
                                :disabled="reportBusy || !selectedConversionReport"
                                @click="downloadReport(selectedConversionReport)"
                            >
                                <app-icon name="external" />
                                Download
                            </button>
                        </div>
                    </div>
                </div>
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

                <dl class="detail-list">
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
        </template>
    </div>
</template>

<script lang="ts">
    import AppIcon from '@/components/AppIcon.vue'
    import JobPanel from '@/components/JobPanel.vue'
    import { useAppStore } from '@/store'
    import { computed, defineComponent, onBeforeUnmount, onMounted, ref, watch } from 'vue'

    const isForbiddenError = (error: unknown): boolean => {
        if (!error || typeof error !== 'object') {
            return false
        }

        const value = error as {
            name?: unknown
            status?: unknown
            response?: {
                status?: unknown
            }
        }
        const status = value.response?.status ?? value.status ?? value.name
        return status === 403 || status === '403'
    }

    const errorMessage = (error: unknown): string => {
        if (error && typeof error === 'object' && 'message' in error) {
            const message = (error as { message?: unknown }).message
            return typeof message === 'string' && message.trim() !== ''
                ? message
                : 'Something went wrong.'
        }

        return typeof error === 'string' && error.trim() !== ''
            ? error
            : 'Something went wrong.'
    }

    const rootConversionPath = '/'

    export default defineComponent({
        name: 'AdminView',
        components: {
            AppIcon,
            JobPanel
        },
        setup() {
            const store = useAppStore()
            const adminStatus = computed(() => store.adminStatus)
            const thumbnailProgress = computed(() => store.thumbnailProgress)
            const conversionProgress = computed(() => store.conversionProgress)
            const conversionPaths = computed(() => store.conversionPaths)
            const conversionPath = ref('')
            const cache = computed(() => store.thumbnailCache)
            const processingReports = computed(() => store.processingReports)
            const thumbnailReports = computed(() => processingReports.value.filter(report => report.type === 'thumbnail'))
            const conversionReports = computed(() => processingReports.value.filter(report => report.type === 'conversion'))
            const selectedThumbnailReport = ref('')
            const selectedConversionReport = ref('')
            const reportBusy = ref(false)
            const cacheBusy = ref(false)
            const loading = ref(true)
            const accessDenied = ref(false)
            const loadError = ref('')

            const buildRunningMessage = (count: number, currentFile: string, noun: string) => {
                if (currentFile) {
                    return `${count.toLocaleString()} ${noun} scanned in this run · ${currentFile}`
                }

                return `${count.toLocaleString()} ${noun} scanned in this run`
            }

            const thumbnailProgressMessage = computed(() => {
                return buildRunningMessage(thumbnailProgress.value.scanned, thumbnailProgress.value.currentFile, 'files')
            })

            const thumbnailIdleMessage = computed(() => {
                if (thumbnailProgress.value.cancelled) {
                    return 'Last run was cancelled'
                }

                return thumbnailProgress.value.finished ? 'Last run completed' : 'Ready to start'
            })

            const thumbnailCardNote = computed(() => {
                return adminStatus.value.thumbnails.isRunning
                    ? thumbnailProgressMessage.value
                    : thumbnailIdleMessage.value
            })

            const conversionProgressMessage = computed(() => {
                return buildRunningMessage(conversionProgress.value.scanned, conversionProgress.value.currentFile, 'files')
            })

            const conversionIdleMessage = computed(() => {
                if (conversionProgress.value.cancelled) {
                    return 'Last run was cancelled'
                }

                if (!conversionPath.value) {
                    return conversionPaths.value.length > 0
                        ? 'Select root or a top-level folder before starting'
                        : 'No conversion paths available'
                }

                return conversionProgress.value.finished ? 'Last run completed' : 'Ready to start'
            })

            const conversionCardNote = computed(() => {
                if (adminStatus.value.conversion.ffmpegAvailable === false) {
                    return 'FFmpeg is unavailable on the server'
                }

                return adminStatus.value.conversion.isRunning
                    ? conversionProgressMessage.value
                    : conversionIdleMessage.value
            })

            const conversionStatusLabel = computed(() => {
                if (adminStatus.value.conversion.ffmpegAvailable === false && !adminStatus.value.conversion.isRunning) {
                    return 'unavailable'
                }

                return adminStatus.value.conversion.isRunning ? 'running' : 'idle'
            })

            const conversionBadgeClass = computed(() => ({
                'badge--active': adminStatus.value.conversion.isRunning,
                'badge--warning': adminStatus.value.conversion.ffmpegAvailable === false && !adminStatus.value.conversion.isRunning
            }))

            const conversionStartDisabled = computed(() => {
                if (adminStatus.value.conversion.isRunning) {
                    return false
                }

                return adminStatus.value.conversion.ffmpegAvailable === false || !conversionPath.value
            })

            const ffmpegStatus = computed(() => {
                if (adminStatus.value.conversion.ffmpegAvailable === true) {
                    return 'Available'
                }

                if (adminStatus.value.conversion.ffmpegAvailable === false) {
                    return 'Unavailable'
                }

                return 'Not reported'
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

            const formatBitrate = (value: number) => {
                if (!value) {
                    return 'Not configured'
                }

                const units = ['bps', 'Kbps', 'Mbps', 'Gbps']
                let amount = value
                let unit = 0
                while (amount >= 1000 && unit < units.length - 1) {
                    amount /= 1000
                    unit++
                }

                return `${amount >= 10 || unit === 0 ? Math.round(amount) : amount.toFixed(1)} ${units[unit]}`
            }

            const cacheSince = computed(() => formatTime(cache.value.startedUtc) || 'startup')
            const lastReset = computed(() => formatTime(cache.value.lastResetUtc))

            const runAdminAction = async (action: () => Promise<void>) => {
                try {
                    await action()
                }
                catch (error) {
                    const message = errorMessage(error)
                    store.setAlert({
                        isError: true,
                        message
                    })
                }
            }

            const toggleThumbnails = () => runAdminAction(async () => {
                if (adminStatus.value.thumbnails.isRunning) {
                    await store.stopThumbnails()
                }
                else {
                    await store.startThumbnails()
                }
            })

            const toggleConversion = () => runAdminAction(async () => {
                if (adminStatus.value.conversion.isRunning) {
                    await store.stopConversion()
                }
                else {
                    await store.startConversion(conversionPath.value)
                }
            })

            const runCacheAction = async (action: () => Promise<void>) => {
                cacheBusy.value = true

                try {
                    await action()
                }
                catch (error) {
                    const message = errorMessage(error)
                    store.setAlert({
                        isError: true,
                        message
                    })
                }
                finally {
                    cacheBusy.value = false
                }
            }

            const refreshCache = () => runCacheAction(() => store.loadThumbnailCacheStats())
            const clearCache = () => runCacheAction(() => store.clearThumbnailCache())

            const runReportAction = async (action: () => Promise<void>) => {
                reportBusy.value = true

                try {
                    await action()
                }
                catch (error) {
                    const message = errorMessage(error)
                    store.setAlert({
                        isError: true,
                        message
                    })
                }
                finally {
                    reportBusy.value = false
                }
            }

            const downloadReport = (reportId: string) => runReportAction(async () => {
                const report = processingReports.value.find(item => item.id === Number(reportId))
                if (!report) {
                    throw new Error('The selected report is no longer available.')
                }

                const blob = await store.downloadProcessingReport(report.id)
                const url = URL.createObjectURL(blob)
                const link = document.createElement('a')
                link.href = url
                link.download = `${report.type}-${report.name.replace(/[^a-z0-9._-]+/gi, '-')}.html`
                document.body.appendChild(link)
                link.click()
                link.remove()
                URL.revokeObjectURL(url)
            })

            const clearReports = () => runReportAction(async () => {
                await store.clearProcessingReports()
                selectedThumbnailReport.value = ''
                selectedConversionReport.value = ''
            })

            const synchronizeConversionPath = () => {
                const reportedPath = conversionProgress.value.path

                if (adminStatus.value.conversion.isRunning) {
                    conversionPath.value = reportedPath || rootConversionPath
                    return
                }

                if (conversionPath.value && !conversionPaths.value.includes(conversionPath.value)) {
                    conversionPath.value = ''
                }
            }

            watch(conversionPaths, synchronizeConversionPath)
            watch(
                () => [adminStatus.value.conversion.isRunning, conversionProgress.value.path],
                synchronizeConversionPath
            )

            onMounted(async () => {
                loading.value = true
                accessDenied.value = false
                loadError.value = ''
                store.setAlert(undefined)

                try {
                    await Promise.all([
                        store.loadAdminStatus(),
                        store.loadConversionPaths(),
                        store.loadThumbnailCacheStats(),
                        store.loadProcessingReports(),
                        store.subscribeToUpdates()
                    ])
                    synchronizeConversionPath()
                }
                catch (error) {
                    await store.unsubscribeFromUpdates()

                    if (isForbiddenError(error)) {
                        accessDenied.value = true
                        store.setAlert({
                            isError: true,
                            message: 'You do not have access to the admin area.'
                        })
                    }
                    else {
                        loadError.value = errorMessage(error)
                        store.setAlert({
                            isError: true,
                            message: loadError.value
                        })
                    }
                }
                finally {
                    loading.value = false
                }
            })

            onBeforeUnmount(async () => {
                await store.unsubscribeFromUpdates()
            })

            return {
                accessDenied,
                adminStatus,
                cache,
                cacheBusy,
                cacheLookups,
                cacheSince,
                clearCache,
                clearReports,
                conversionBadgeClass,
                conversionCardNote,
                conversionReports,
                conversionIdleMessage,
                conversionPath,
                conversionPaths,
                rootConversionPath,
                conversionProgress,
                conversionProgressMessage,
                conversionStartDisabled,
                conversionStatusLabel,
                downloadReport,
                ffmpegStatus,
                formatBitrate,
                hitRate,
                hitRateColour,
                lastReset,
                loadError,
                loading,
                processingReports,
                reportBusy,
                refreshCache,
                selectedConversionReport,
                selectedThumbnailReport,
                thumbnailReports,
                thumbnailIdleMessage,
                thumbnailProgress,
                thumbnailProgressMessage,
                thumbnailCardNote,
                toggleConversion,
                toggleThumbnails,
                usage
            }
        }
    })
</script>

<style lang="scss" scoped>
    .admin-page {
        max-width: 72rem;
        margin: 0 auto;
    }

    .header-badges {
        display: flex;
        align-items: flex-start;
        gap: var(--space-2);
        flex-wrap: wrap;
    }

    .status-dot {
        width: 0.5rem;
        height: 0.5rem;
        border-radius: 50%;
        background: currentcolor;
    }

    .badge--warning {
        color: var(--warning);
        border-color: color-mix(in srgb, var(--warning) 45%, transparent);
        background: color-mix(in srgb, var(--warning) 12%, var(--surface-raised));
    }

    .stats {
        display: grid;
        grid-template-columns: repeat(4, minmax(0, 1fr));
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

    .panel__actions {
        display: flex;
        flex: 0 0 auto;
        gap: var(--space-2);
    }

    .report-list {
        display: grid;
        gap: var(--space-4);
    }

    .report-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--space-5);
        padding: var(--space-4) 0;
        border-bottom: 1px solid var(--border);
    }

    .report-row:last-child {
        border-bottom: 0;
    }

    .report-row__copy {
        display: grid;
        gap: var(--space-1);
        min-width: 0;
    }

    .report-row__copy label {
        font-weight: 600;
    }

    .report-row__controls {
        display: flex;
        align-items: center;
        gap: var(--space-2);
        min-width: min(100%, 30rem);
    }

    .report-row__controls .select {
        min-width: 0;
        flex: 1 1 auto;
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

    .detail-list {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 0;
        margin: 0;
    }

    .detail-list > div {
        padding: var(--space-4) var(--space-4) var(--space-4) 0;
        border-bottom: 1px solid var(--border);
    }

    .detail-list > div:nth-last-child(-n + 2) {
        border-bottom: 0;
    }

    .detail-list dt {
        margin-bottom: var(--space-1);
        color: var(--text-subtle);
        font-size: var(--text-xs);
    }

    .detail-list dd {
        margin: 0;
        color: var(--text);
        font-weight: 600;
        overflow-wrap: anywhere;
    }

    .detail-list dd.value--warning {
        color: var(--warning);
    }

    @media only screen and (max-width: 680px) {
        .page-header,
        .panel__header {
            flex-direction: column;
            align-items: stretch;
        }

        .stats,
        .detail-list,
        .meters {
            grid-template-columns: 1fr;
        }

        .panel__actions .btn {
            flex: 1 1 0;
        }

        .report-row {
            align-items: stretch;
            flex-direction: column;
        }

        .report-row__controls {
            min-width: 0;
        }

        .report-row__controls .btn {
            flex: 0 0 auto;
        }

        .detail-list > div:nth-last-child(-n + 2) {
            border-bottom: 1px solid var(--border);
        }

        .detail-list > div:last-child {
            border-bottom: 0;
        }
    }
</style>
