<template>
    <div class="video-player">
        <video controls class="video" ref="video" preload="metadata" playsinline>
            <source :src="file.url" :type="file.contentType">
        </video>
        <TagCloud :file="file" @click="setTime"></TagCloud>
    </div>
</template>
<script lang="ts">
    import ArkaineFile from '@/models/arkaine-file'
    import { PropType, defineComponent, ref } from 'vue'
    import TagCloud from './TagCloud.vue'

    export default defineComponent({
        name: 'VideoPlayer',
        components: {
            TagCloud
        },
        props: {
            file: {
                type: Object as PropType<ArkaineFile>,
                required: true,
            },
        },
        setup() {
            const video = ref<HTMLVideoElement>()
            const setTime = (time: number) => {
                if (video.value) video.value.currentTime = time
            }

            return {
                setTime,
                video
            }
        }    
    })
</script>
<style lang="scss" scoped>
    .video-player {
        display: flex;
        width: 100%;
        height: 100%;
        flex-direction: column;
        padding: 1rem;
        border: 1px solid var(--app-border);
        border-radius: 0.75rem;
        background: var(--app-surface-raised);
    }

    .video {
        display: block;
        width: 100%;
        flex: 1;
        min-height: 0;
        max-height: 70vh;
        object-fit: contain;
        border-radius: 0.5rem;
        background: #05070b;
    }
</style>
