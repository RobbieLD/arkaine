<template>
    <div class="skeleton card" aria-hidden="true">
        <div class="skeleton__media shimmer" :style="style"></div>
        <div class="skeleton__footer">
            <span class="skeleton__line shimmer"></span>
            <span class="skeleton__line skeleton__line--short shimmer"></span>
        </div>
    </div>
</template>
<script lang="ts">
    import { computed, defineComponent } from 'vue'

    export default defineComponent({
        name: 'SkeletonCard',
        props: {
            /** Varying the placeholder ratio keeps a cold folder from looking like a table. */
            ratio: {
                type: Number,
                default: 0.75
            }
        },
        setup(props) {
            const style = computed(() => ({ '--skeleton-ratio': `1 / ${props.ratio}` }))
            return { style }
        }
    })
</script>
<style lang="scss" scoped>
    .skeleton {
        overflow: hidden;
    }

    .skeleton__media {
        aspect-ratio: var(--skeleton-ratio, 4 / 3);
        width: 100%;
    }

    .skeleton__footer {
        display: grid;
        gap: var(--space-2);
        padding: var(--space-3) var(--space-4) var(--space-4);
    }

    .skeleton__line {
        height: 0.7rem;
        border-radius: var(--radius-pill);
    }

    .skeleton__line--short {
        width: 45%;
    }
</style>
