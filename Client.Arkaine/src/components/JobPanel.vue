<template>
    <section class="panel">
        <header class="panel__header">
            <div class="panel__copy">
                <div class="panel__title-row">
                    <h2 class="panel__title">{{ title }}</h2>
                    <span class="badge" :class="badgeClass" aria-live="polite">
                        <span class="status-dot" aria-hidden="true"></span>
                        {{ statusLabel }}
                    </span>
                </div>
                <p class="muted">{{ description }}</p>
            </div>
            <button
                type="button"
                class="btn"
                :class="running ? 'btn--danger' : 'btn--primary'"
                :disabled="toggleDisabled"
                :aria-label="running ? stopAriaLabel : startAriaLabel"
                @click="$emit('toggle')"
            >
                <app-icon :name="running ? 'close' : 'play'" />
                {{ running ? stopLabel : startLabel }}
            </button>
        </header>

        <div v-if="running" class="running-state" role="status">
            <progress class="progress" :aria-label="progressAriaLabel"></progress>
            <span>{{ progressMessage }}</span>
        </div>
        <p v-else class="panel__status muted" role="status">
            {{ statusMessage }}
        </p>

        <dl class="detail-list">
            <slot />
        </dl>
    </section>
</template>

<script lang="ts">
    import { computed, defineComponent } from 'vue'
    import AppIcon from '@/components/AppIcon.vue'

    export default defineComponent({
        name: 'JobPanel',
        components: {
            AppIcon
        },
        emits: ['toggle'],
        props: {
            title: {
                type: String,
                required: true
            },
            description: {
                type: String,
                required: true
            },
            running: {
                type: Boolean,
                required: true
            },
            unavailable: {
                type: Boolean,
                default: false
            },
            toggleDisabled: {
                type: Boolean,
                default: false
            },
            startLabel: {
                type: String,
                default: 'Start'
            },
            stopLabel: {
                type: String,
                default: 'Stop'
            },
            startAriaLabel: {
                type: String,
                default: 'Start job'
            },
            stopAriaLabel: {
                type: String,
                default: 'Stop job'
            },
            progressAriaLabel: {
                type: String,
                default: 'Job is in progress'
            },
            progressMessage: {
                type: String,
                default: ''
            },
            idleMessage: {
                type: String,
                default: 'Ready to start'
            },
            unavailableMessage: {
                type: String,
                default: 'This job is unavailable.'
            }
        },
        setup(props) {
            const statusLabel = computed(() => {
                if (props.unavailable && !props.running) {
                    return 'Unavailable'
                }

                return props.running ? 'Running' : 'Idle'
            })

            const badgeClass = computed(() => {
                if (props.unavailable && !props.running) {
                    return 'badge--warning'
                }

                return props.running ? 'badge--active' : ''
            })

            const statusMessage = computed(() => {
                if (props.unavailable && !props.running) {
                    return props.unavailableMessage
                }

                return props.idleMessage
            })

            return {
                badgeClass,
                statusLabel,
                statusMessage
            }
        }
    })
</script>

<style lang="scss" scoped>
    .panel__header {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        gap: var(--space-4);
        padding-bottom: var(--space-5);
        border-bottom: 1px solid var(--border);
    }

    .panel__copy {
        min-width: 0;
        flex: 1 1 auto;
    }

    .panel__title-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--space-3);
        margin-bottom: var(--space-1);
        flex-wrap: wrap;
    }

    .panel__title {
        font-size: var(--text-xl);
    }

    .panel__header .btn {
        flex: 0 0 auto;
        --icon-size: 1rem;
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

    .running-state,
    .panel__status {
        display: flex;
        align-items: center;
        gap: var(--space-3);
        padding: var(--space-4) 0;
        color: var(--text-muted);
        font-size: var(--text-sm);
        border-bottom: 1px solid var(--border);
    }

    .running-state .progress {
        width: 7rem;
    }

    .detail-list {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 0;
        margin: 0;
    }

    :deep(.detail-list > div) {
        padding: var(--space-4) var(--space-4) var(--space-4) 0;
        border-bottom: 1px solid var(--border);
    }

    :deep(.detail-list > div:nth-last-child(-n + 2)) {
        border-bottom: 0;
    }

    :deep(.detail-list dt) {
        margin-bottom: var(--space-1);
        color: var(--text-subtle);
        font-size: var(--text-xs);
    }

    :deep(.detail-list dd) {
        margin: 0;
        color: var(--text);
        font-weight: 600;
        overflow-wrap: anywhere;
    }

    :deep(.detail-list dd.value--warning) {
        color: var(--warning);
    }

    @media only screen and (max-width: 680px) {
        .panel__header {
            flex-direction: column;
            align-items: stretch;
        }

        .panel__header .btn,
        .detail-list {
            width: 100%;
            grid-template-columns: 1fr;
        }

        :deep(.detail-list > div:nth-last-child(-n + 2)) {
            border-bottom: 1px solid var(--border);
        }

        :deep(.detail-list > div:last-child) {
            border-bottom: 0;
        }
    }
</style>
