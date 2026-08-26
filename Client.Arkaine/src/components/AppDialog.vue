<template>
    <dialog ref="dialog" class="dialog" @close="onNativeClose">
        <div class="dialog__header">
            <h3 class="dialog__title">{{ title }}</h3>
            <button
                type="button"
                class="btn btn--ghost btn--icon btn--sm"
                aria-label="Close dialog"
                @click="close"
            >
                <app-icon name="close" />
            </button>
        </div>
        <div class="dialog__body">
            <slot></slot>
        </div>
        <footer v-if="$slots.footer" class="dialog__footer">
            <slot name="footer"></slot>
        </footer>
    </dialog>
</template>
<script lang="ts">
    import { defineComponent, onBeforeUnmount, ref, watch } from 'vue'
    import AppIcon from './AppIcon.vue'

    /**
     * Thin wrapper over the native dialog element. Using showModal() rather than the
     * `open` attribute is what buys the top layer, the backdrop, the focus trap and
     * Esc-to-close for free.
     */
    export default defineComponent({
        name: 'AppDialog',
        components: {
            AppIcon
        },
        props: {
            modelValue: {
                type: Boolean,
                default: false
            },
            title: {
                type: String,
                default: ''
            }
        },
        emits: ['update:modelValue'],
        setup(props, { emit }) {
            const dialog = ref<HTMLDialogElement>()

            const close = () => {
                if (dialog.value?.open) {
                    dialog.value.close()
                }
                else {
                    emit('update:modelValue', false)
                }
            }

            const onNativeClose = () => {
                emit('update:modelValue', false)
            }

            watch(() => props.modelValue, open => {
                const element = dialog.value

                if (!element) {
                    return
                }

                if (open && !element.open) {
                    element.showModal()
                }
                else if (!open && element.open) {
                    element.close()
                }
            }, { immediate: true, flush: 'post' })

            onBeforeUnmount(() => {
                if (dialog.value?.open) {
                    dialog.value.close()
                }
            })

            return {
                close,
                dialog,
                onNativeClose
            }
        }
    })
</script>
