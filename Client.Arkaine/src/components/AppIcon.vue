<template>
    <svg
        class="icon"
        :class="{ 'icon--filled': definition.filled }"
        viewBox="0 0 24 24"
        aria-hidden="true"
        focusable="false"
    >
        <path v-for="(d, index) of definition.paths" :key="index" :d="d" />
    </svg>
</template>
<script lang="ts">
    import { computed, defineComponent, PropType } from 'vue'
    import { icons, IconName } from '@/assets/icons'

    export default defineComponent({
        name: 'AppIcon',
        props: {
            name: {
                type: String as PropType<IconName>,
                required: true
            }
        },
        setup(props) {
            const definition = computed(() => icons[props.name])
            return { definition }
        }
    })
</script>
<style lang="scss" scoped>
    .icon {
        width: var(--icon-size, 1.25rem);
        height: var(--icon-size, 1.25rem);
        flex: 0 0 auto;
        fill: none;
        stroke: currentcolor;
        stroke-width: 1.75;
        stroke-linecap: round;
        stroke-linejoin: round;
    }

    .icon--filled {
        fill: currentcolor;
        stroke: none;
    }
</style>
