<template>
    <div class="video-player">
        <video ref="video" class="video" controls preload="none" playsinline>
            <source :src="file.url" :type="file.contentType">
        </video>
        <tag-cloud :file="file" @click="setTime"></tag-cloud>
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
        flex-direction: column;
    }

    /* Native controls are kept: free fullscreen, PiP, captions and keyboard support. */
    .video {
        display: block;
        width: 100%;
        max-height: 70vh;
        background: var(--surface-sunken);
    }
</style>
