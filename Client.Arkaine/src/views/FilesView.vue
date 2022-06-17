<template>
    <div class="content">
        <article :class="{ image: file.isImage, folder: file.isFolder, media: file.isImage }" class="item" v-for="(file, index) of files" :key="index">
            <!-- Image File -->
            <a v-if="file.isImage" class="image" :href="file.url" target="_blank">
                <img :src="file.url"/>
            </a>
            
            <!-- Video File -->

            <!-- Audio File -->

            <!-- Folder -->
            <div v-else-if="file.isFolder">
                <div class="headings">
                    <h2>{{ file.fileName }}</h2>
                    <h3>{{ file.children.length }} children</h3>
                </div>
            </div>
        </article>
    </div>
</template>
<script lang='ts'>
    import { storeKey } from '@/store'
    import { computed, defineComponent } from 'vue'
    import { useRoute } from 'vue-router'
    import { useStore } from 'vuex'

    export default defineComponent({
        name: 'FilesView',
        components: {},
        setup() {
            
            const store = useStore(storeKey)
            const route = useRoute()
            const files = computed(() => store.state.files)

            return {
                files
            }
        },
    })
</script>
<style lang='scss' scoped>
    .item {
        cursor: pointer;
        min-width: 10em;
    }

    .media {
        padding: 0.5em;
    }

    .image {
        max-width: 10em;
    }

    .folder {
        background-color: var(--primary-focus);

        &:hover {
            background-color: var(--primary-inverse);
        }
    }
</style>
