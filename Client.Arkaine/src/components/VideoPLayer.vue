<template>
    <div>
        <video controls class="video" ref="video">
            <source :src="file.url" :type="file.contentType">
        </video>
    </div>
    <TagCloud :file="file" @click="setTime"></TagCloud>
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
            const video = ref<HTMLAudioElement>()
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
    .video {
        max-width: min(90vw, 30em);
    }
</style>
