<template>
    <div class="tags">
        <div class="tags__container">
            <button
                v-for="(tag, index) of file.tags"
                :key="index"
                type="button"
                class="tags__tag"
                :class="tag.timestamp ? 'tags__tag--time' : ''"
                :disabled="!tag.timestamp"
                @click="handleTagClick(tag.timestamp)"
            >
                {{ tag.name }}{{ tag.timestamp ? ` ${secondsToTime(tag.timestamp)}` : '' }}
            </button>
        </div>
        <div class="tags__actions">
            <button
                type="button"
                class="btn btn--ghost btn--icon btn--sm btn--round"
                aria-label="Remove a tag"
                :disabled="!file.tags?.length"
                @click="openRemoveTag = true"
            >
                <app-icon name="minus" />
            </button>
            <button
                type="button"
                class="btn btn--ghost btn--icon btn--sm btn--round"
                aria-label="Add a tag"
                @click="openAddTag = true"
            >
                <app-icon name="plus" />
            </button>
        </div>
    </div>

    <app-dialog v-model="openAddTag" title="Add a tag">
        <div class="stack">
            <label class="field">
                <span class="label">Name</span>
                <input v-model="newTagName" class="input" placeholder="Chorus" />
            </label>
            <label class="field">
                <span class="label">Timestamp</span>
                <input v-model="newTagTimeStamp" class="input" type="number" placeholder="0000" />
            </label>
        </div>
        <template #footer>
            <button type="button" class="btn" @click="openAddTag = false">Cancel</button>
            <button type="button" class="btn btn--primary" @click="saveTag">Save tag</button>
        </template>
    </app-dialog>

    <app-dialog v-model="openRemoveTag" title="Delete a tag">
        <p v-if="!file.tags?.length" class="muted">There are no tags on this item.</p>
        <div v-else class="cluster">
            <button
                v-for="(tag, key) of file.tags"
                :key="key"
                type="button"
                class="btn btn--sm btn--danger"
                @click="deleteTag(tag.id)"
            >
                <app-icon name="trash" />
                {{ tag.name }}{{ tag.timestamp ? ` ${secondsToTime(tag.timestamp)}` : '' }}
            </button>
        </div>
        <template #footer>
            <button type="button" class="btn btn--primary" @click="openRemoveTag = false">Done</button>
        </template>
    </app-dialog>
</template>
<script lang="ts">
    import ArkaineFile from '@/models/arkaine-file'
    import { secondsToTime } from '@/utils/formatters'
    import { useAppStore } from '@/store'
    import { PropType, defineComponent, ref } from 'vue'
    import AppDialog from './AppDialog.vue'
    import AppIcon from './AppIcon.vue'

    export default defineComponent({
        name: 'TagCloud',
        components: {
            AppDialog,
            AppIcon
        },
        emits: ['click'],
        props: {
            file: {
                type: Object as PropType<ArkaineFile>,
                required: true,
            },
        },
        setup(props, { emit }) {

            const newTagName = ref('')
            const newTagTimeStamp = ref('')
            const openAddTag = ref(false)
            const openRemoveTag = ref(false)
            const store = useAppStore()

            const deleteTag = async (id: number) => {
                await store.deleteTag({
                    id,
                    fileName: props.file.rawFileName
                })
            }

            const handleTagClick = (time: number) => {
                if (time) {
                    emit('click', time)
                }
            }

            const saveTag = async () => {
                openAddTag.value = false

                if (!newTagName.value) {
                    return
                }

                const value = newTagTimeStamp.value.toString()
                const position = value.length > 1 ? value.length - 2 : 1

                await store.addTag({
                    name: newTagName.value,
                    file: props.file.rawFileName,
                    time: value ? (value.length > 1 ? `${value.slice(0, position) || 0}:${value.slice(position)}` : `0:${value}`) : ''
                })

                newTagName.value = ''
                newTagTimeStamp.value = ''
            }

            return {
                newTagName,
                newTagTimeStamp,
                openAddTag,
                openRemoveTag,
                saveTag,
                deleteTag,
                secondsToTime,
                handleTagClick
            }
        }
    })
</script>
<style lang="scss" scoped>
    .tags {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        gap: var(--space-2);
        padding: var(--space-3) var(--space-4);
        border-top: 1px solid var(--border);
    }

    .tags__container {
        display: flex;
        min-width: 0;
        flex: 1 1 auto;
        flex-wrap: wrap;
        gap: var(--space-2);
        padding-top: 0.2rem;
    }

    .tags__tag {
        padding: 0.1rem var(--space-2);
        color: var(--text-muted);
        border: 1px solid var(--border-strong);
        border-radius: var(--radius-pill);
        background: var(--surface-raised);
        font-size: var(--text-xs);
        font-weight: 600;
        cursor: default;
    }

    .tags__tag--time {
        color: var(--accent);
        border-color: color-mix(in srgb, var(--accent) 40%, transparent);
        background: var(--accent-soft);
        cursor: pointer;
    }

    .tags__tag--time:hover {
        border-color: var(--accent);
    }

    .tags__actions {
        display: flex;
        flex: 0 0 auto;
        gap: var(--space-1);
    }
</style>
