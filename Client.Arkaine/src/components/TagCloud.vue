<template>
    <div class="tags">
        <div class="tags__container">
            <span v-for="(tag, index) of file.tags" :key="index" class="tags__tag" @click="handleTagClick(tag.timestamp)"
                :class="tag.timestamp ? 'tags__tag--time' : ''">{{ tag.name }} {{ tag.timestamp ? secondsToTime(tag.timestamp) : '' }}</span>
        </div>
        <button type="button" class="tags__action" @click="openRemoveTagDialog" aria-label="Remove a tag">-</button>
        <button type="button" class="tags__action" @click="openAddTagDialog" aria-label="Add a tag">+</button>
    </div>
    <!-- Add Tag -->
    <dialog id="tag-add" :open="openAddTag">
        <article>
            <h3>Enter Tag Name</h3>
            <input placeholder="Name" v-model="newTagName" />
            <input type="number" placeholder="0000" v-model="newTagTimeStamp"/>
            <!-- <input placeholder="Time Stamp" v-model="newTagTimeStamp" /> -->
            <footer class="dialog__buttons">
                <button href="#" role="button" class="secondary" @click="closeAddTagDialog" data-target="tag-add">
                    Cancel
                </button>
                <button href="#" role="button" @click="saveTag" data-target="tag-add">
                    Confirm
                </button>
            </footer>
        </article>
    </dialog>

    <!-- Remove Tag -->
    <dialog id="remove-add" :open="openRemoveTag">
        <article>
            <h3>Click To Delete</h3>
            <button v-for="(tag, key) of file.tags" href="#" class="secondary" role="button"
                :key="key" @click="deleteTag(tag.id)">{{ tag.name }} {{ tag.timestamp ? secondsToTime(tag.timestamp) : '' }}</button>
            <footer class="dialog__buttons">
                <button href="#" role="button" @click="closeRemoveTagDialog" data-target="remove-add">
                    Done
                </button>
            </footer>
        </article>
    </dialog>
</template>
<script lang="ts">
    import ArkaineFile from '@/models/arkaine-file'
    import { secondsToTime } from '@/utils/formatters'
    import { storeKey } from '@/store'
    import { PropType, defineComponent, ref } from 'vue'
    import { useStore } from 'vuex'

    export default defineComponent({
        name: 'TagCloud',
        components: {},
        emits: [ 'click' ],
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
            const store = useStore(storeKey)

            const openRemoveTagDialog = () => {
                openRemoveTag.value = true
            }

            const deleteTag = async (id : number) => {
                await store.dispatch('deleteTag', {
                    id,
                    fileName: props.file.rawFileName
                })
            }

            const handleTagClick = (time: number) => {
                emit('click', time)
            }

            const closeRemoveTagDialog = () => {
                openRemoveTag.value = false
            }

            const openAddTagDialog = () => {
                openAddTag.value = true
            }

            const closeAddTagDialog = () => {
                openAddTag.value = false
            }

            const saveTag = async () => {
                closeAddTagDialog()

                if (!newTagName.value) {
                    return
                }

                const value = newTagTimeStamp.value.toString()
                const position = value.length > 1 ? value.length - 2 : 1

                await store.dispatch('addTag', {
                    name: newTagName.value,
                    file: props.file.rawFileName,
                    time: value ? (value.length > 1 ? `${value.slice(0, position) || 0}:${value.slice(position)}` : `0:${value}`) : 0
                })

                newTagName.value = ''
                newTagTimeStamp.value = ''
            }

            return {
                newTagName,
                newTagTimeStamp,
                openAddTag,
                openRemoveTag,
                openAddTagDialog,
                openRemoveTagDialog,
                closeRemoveTagDialog,
                closeAddTagDialog,
                saveTag,
                deleteTag,
                secondsToTime,
                handleTagClick
            }
        }
    })
</script>
<style lang="scss" scoped>
    .dialog__buttons {
        display: grid;
        grid-auto-flow: column;
    }
    .tags {
        margin: 0.5em;
        display: grid;
        grid-auto-flow: column;
        align-items: center;
        grid-template-columns: 1fr auto auto;

        &__container {
            display: flex;
            flex-direction: row;
            flex-wrap: wrap;
            gap: 0.5em;
        }

        &__tag {
            border-radius: var(--pico-border-radius);
            background-color: var(--pico-primary-background);
            color: var(--pico-primary-inverse);
            text-align: center;
            padding-right: 0.5em;
            padding-left: 0.5em;

            &--time {
                background-color: var(--pico-secondary-background);
                color: var(--pico-secondary-inverse);
            }
        }

        &__action {
            min-width: 2rem;
            min-height: 2rem;
            margin: 0 0 0 0.35rem;
            padding: 0;
            color: var(--pico-color);
            border: 1px solid var(--app-border);
            border-radius: 50%;
            background: var(--app-surface);
            box-shadow: none;
            font-size: 1.4em;
            font-weight: bold;
            line-height: 1;

            &:hover,
            &:focus-visible {
                color: var(--pico-primary-hover);
                border-color: var(--pico-primary);
                background: var(--pico-primary-focus);
                box-shadow: none;
            }
        }
    }

    input::-webkit-outer-spin-button,
    input::-webkit-inner-spin-button {
        -webkit-appearance: none;
        margin: 0;
    }

    input[type=number] {
        -moz-appearance: textfield;
    }
</style>
