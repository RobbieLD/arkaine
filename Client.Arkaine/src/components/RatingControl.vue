<template>
    <div class="rating" ref="root">
        <span class="rating__icon" @click="setValue(1)">{{ icon }}</span>
        <span class="rating__icon" @click="setValue(2)">{{ icon }}</span>
        <span class="rating__icon" @click="setValue(3)">{{ icon }}</span>
        <span class="rating__icon" @click="setValue(4)">{{ icon }}</span>
        <span class="rating__icon" @click="setValue(5)">{{ icon }}</span>
    </div>
</template>
<script lang='ts'>
    import { defineComponent, onMounted, ref, watch } from 'vue'
    
    export default defineComponent({
        name: 'RatingControl',
        components: {},
        emits: ['update:modelValue'],
        props: {
            icon: {
                type: String,
                required: true,
            },
            modelValue: Number
        },
        setup(props, { emit }) {
            const root = ref(null)
            const setValue = (v: number) => {
                emit('update:modelValue', v)
            }

            watch(() => props.modelValue, (val) => {
                updateIcons(val || 0)
            })

            onMounted(() => {
                updateIcons(props.modelValue || 0)
            })

            const updateIcons = (index: number) => {
                const icons = (root.value! as HTMLDivElement).getElementsByClassName('rating__icon')
                let i = 1
                for (const element of icons) {
                    
                    if (i <= index) {
                        element.classList.add('rating__icon--highlight')    
                    }
                    else {
                        element.classList.remove('rating__icon--highlight')
                    }

                    i++
                }
            }

            return {
                setValue,
                root
            }
        },
    })
</script>
<style lang='scss' scoped>
    .rating {
        vertical-align: middle;
        display: inline;
        font-size: 1.5em;

        &__icon {
            text-align: center;
            margin-left: 0.5em;

            &--highlight {
                color: red;
            }

            &:hover {
                color: darkred;
            }
        }
    }
</style>
