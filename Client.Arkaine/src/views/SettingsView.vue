<template>
    <article>
        <header>
                <h3>Thumbnails {{ options.isRunning ? ' - Running' : '- Stopped' }}</h3>
                <div class="values">
                    <div><b>Total / Bad:</b> {{ options.totalThumbnails }} / {{ options.badThumbnails }}</div>
                    <div><b>Width:</b> {{ options.thumbnailWidth }}</div>
                    <div><b>File Types:</b> {{ options.thumbnailExtensions }}</div>
                    <div><b>Page Size:</b> {{ options.thumbnailPageSize }}</div>
                    <div><b>Diretory:</b> {{ options.thumbnailDir }}</div>
                </div>
                
        </header>
            <div class="values">
                <div><b>Scanned:</b> {{ progress.scanned }}</div>
                <div><b>Generated:</b> {{ progress.generated }}</div>
                <div><b>Failed:</b> {{ progress.failed }}</div>
            </div> 
        <footer>
            <button @click="handleStartClick">{{ options.isRunning ? 'Cancel' : 'Start' }}</button>
        </footer>
    </article>
</template>

<script lang="ts">
    import { storeKey } from '@/store'
    import { computed, defineComponent, onMounted } from 'vue'
    import { useStore } from 'vuex'

    export default defineComponent({
        name: 'SettingsView',
        setup() {
            const store = useStore(storeKey)
            const options = computed(() => store.state.settings)
            const progress = computed(() => store.state.progress)

            const handleStartClick = async () => {
                if (options.value.isRunning) {
                    await store.dispatch('cancelGeneration')
                }
                else {
                    await store.dispatch('startGeneration')
                }
            }

            onMounted(async () => {
                await store.dispatch('loadSettings')
                await store.dispatch('subscribeToUpdates')
            })

            return {
                options,
                progress,
                handleStartClick
            }
        },
    })
</script>
<style scoped>
.values {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(15em, 1fr));
    gap: 1em;
}
</style>
