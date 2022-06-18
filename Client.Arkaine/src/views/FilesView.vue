<template>
    <div class="content">
        <article :class="{ image: file.isImage, folder: file.isFolder, media: file.isImage }" class="item" v-for="(file, index) of files" :key="index">
            <!-- Image File -->
            <a v-if="file.isImage" class="image" :href="file.url" target="_blank">
                <img :src="file.url"/>
            </a>
            
            <!-- Video File -->
            <div v-else-if="file.isVideo">
                <div class="headings">
                    <a :href="file.url" target="_blank">
                        <h2>{{ file.fileName }}</h2>
                    </a>
                    <h3>{{ file.contentLength }}</h3>
                </div>
            </div>

            <!-- Audio File -->
            <div v-else-if="file.isAudio">
                <div class="headings">
                    <a :href="file.url" target="_blank">
                        <h2>{{ file.fileName }}</h2>
                    </a>
                    <h3>{{ file.contentLength }}</h3>
                </div>
            </div>

            <!-- Folder -->
            <div v-else-if="file.isFolder" @click="open(file.fileName)">
                <div class="headings">
                    <h2>{{ file.fileName }}</h2>
                    <h3>{{ file.children.length }} items</h3>
                </div>
            </div>

            <!-- Other file types -->
            <div v-else>
                <a :href="file.url" target="_blank">{{ files.fileName }}</a>
            </div>
        </article>
    </div>
</template>
<script lang='ts'>
    import { storeKey } from '@/store'
    import { computed, defineComponent } from 'vue'
    import { useStore } from 'vuex'

    export default defineComponent({
        name: 'FilesView',
        components: {},
        setup() {
            
            const store = useStore(storeKey)
            const files = computed(() => store.getters['getFilesList'])
            const open = (name: string) => {
                store.commit('appendPath', name)
            }

            return {
                files,
                open
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
        max-width: 30em;
    }

    .folder {
        background-color: var(--primary-focus);

        &:hover {
            background-color: var(--primary-inverse);
        }
    }
</style>
