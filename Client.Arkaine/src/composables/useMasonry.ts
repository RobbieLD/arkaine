import { onBeforeUnmount, onMounted, Ref, ref } from 'vue'

/**
 * CSS-grid masonry.
 *
 * The grid uses a small fixed `grid-auto-rows` unit and zero row gap; each card then
 * spans however many of those units its measured height needs. That keeps cards in DOM
 * order (unlike CSS columns) while letting each one size to its own content, so a tall
 * portrait image no longer stretches every card in its row.
 *
 * A single ResizeObserver watches the whole grid rather than one per card, so adding
 * hundreds of items costs one observer, not hundreds.
 */
export interface MasonryOptions {
    /** Height in px of one implicit grid row. Smaller means finer vertical placement. */
    rowHeight?: number
    /** Vertical space between cards, in px. */
    gap?: number
    /** Below this width the grid collapses to a single column and spans are dropped. */
    stackBelow?: number
}

const spanFor = (height: number, rowHeight: number, gap: number): number => {
    return Math.max(1, Math.ceil((height + gap) / rowHeight))
}

export const useMasonry = (
    grid: Ref<HTMLElement | undefined>,
    options: MasonryOptions = {}
) => {
    const rowHeight = options.rowHeight ?? 8
    const gap = options.gap ?? 16
    const stackBelow = options.stackBelow ?? 480

    const enabled = ref(true)

    let observer: ResizeObserver | undefined
    let frame = 0

    const isStacked = (): boolean => {
        const width = grid.value?.clientWidth ?? 0
        return width > 0 && width < stackBelow
    }

    const applySpan = (card: HTMLElement): void => {
        if (!enabled.value) {
            card.style.removeProperty('grid-row-end')
            return
        }

        const height = card.getBoundingClientRect().height
        card.style.gridRowEnd = `span ${spanFor(height, rowHeight, gap)}`
    }

    const layout = (): void => {
        const container = grid.value

        if (!container) {
            return
        }

        enabled.value = !isStacked()
        container.classList.toggle('masonry--stacked', !enabled.value)

        for (const card of Array.from(container.children)) {
            if (card instanceof HTMLElement) {
                applySpan(card)
            }
        }
    }

    /** Coalesces the bursts of resize callbacks that image decoding produces. */
    const scheduleLayout = (): void => {
        if (frame) {
            return
        }

        frame = requestAnimationFrame(() => {
            frame = 0
            layout()
        })
    }

    const observe = (): void => {
        const container = grid.value

        if (!container || !observer) {
            return
        }

        observer.disconnect()
        observer.observe(container)

        for (const card of Array.from(container.children)) {
            if (card instanceof HTMLElement) {
                observer.observe(card)
            }
        }

        scheduleLayout()
    }

    onMounted(() => {
        observer = new ResizeObserver(scheduleLayout)
        observe()
    })

    onBeforeUnmount(() => {
        if (frame) {
            cancelAnimationFrame(frame)
            frame = 0
        }

        observer?.disconnect()
        observer = undefined
    })

    return {
        /** Re-attach the observer after the card list changes. */
        refresh: observe,
        relayout: scheduleLayout
    }
}

export default useMasonry
