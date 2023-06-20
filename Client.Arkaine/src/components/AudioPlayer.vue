<template>
    <div class="player">
        <audio ref="audio" preload="none"></audio>
        <input type="range" @input="seek" v-bind:value="playerTime" :disabled="!enableSeek" class="player__seek" min="0" :max="audio?.duration" />
        <div class="player__controls">
            <div class="player__current-time">{{ current }}</div>
            <div class="player__button-container" @click="toggle">
                <img src="play.svg" v-show="!playing" class="player__button" />
                <img src="pause.svg" v-show="playing" class="player__button" />
            </div>
            <div class="player__total-time">{{ total }}</div>
        </div>
        <div class="tags">
            <div class="tags__container">
                <span v-for="(tag, index) of file.tags" :key="index" class="tags__tag" @click="setTime(tag.timestamp)"
                    :class="tag.timestamp ? 'tags__tag--time' : ''">{{ tag.name }} {{ tag.timestamp ? formatTime(tag.timestamp) : '' }}</span>
            </div>
            <div class="tags__action" @click="openRemoveTagDialog(file.rawFileName)">-</div>
            <div class="tags__action" @click="openAddTagDialog(file.rawFileName)">+</div>
        </div>
    </div>
    <!-- Add Tag -->
    <dialog id="tag-add" :open="openAddTag">
        <article>
            <h3>Enter Tag Name</h3>
            <input placeholder="Name" v-model="newTagName" />
            <input placeholder="Time Stamp" v-model="newTagTimeStamp" />
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
                :key="key" @click="deleteTag(tag.id)">{{ tag.name }} {{ tag.timestamp ? formatTime(tag.timestamp) : '' }}</button>
            <footer class="dialog__buttons">
                <button href="#" role="button" @click="closeRemoveTagDialog" data-target="remove-add">
                    Done
                </button>
            </footer>
        </article>
    </dialog>
</template>
<script lang='ts'>
    import ArkaineFile from '@/models/arkaine-file'
    import { storeKey } from '@/store'
    import { PropType, defineComponent, onMounted, ref } from 'vue'
    import { useStore } from 'vuex'
    
    export default defineComponent({
        name: 'AudioPlayer',
        components: {},
        props: {
            file: {
                type: Object as PropType<ArkaineFile>,
                required: true,
            },
        },
        setup(props) {
            const audio = ref<HTMLAudioElement>()
            const playing = ref(false)
            const playerTime = ref(0)
            const total = ref('0:00')
            const current = ref('0:00')
            const seekPosition = ref('0%')
            const bufferPosition = ref('0%')
            const enableSeek = ref(false)
            const selectedFile = ref('')
            const newTagName = ref('')
            const newTagTimeStamp = ref('')
            const openAddTag = ref(false)
            const openRemoveTag = ref(false)
            const store = useStore(storeKey)

            const seek = async (ev: Event) => {
                const v = Number.parseInt((ev.target as HTMLInputElement).value)
                if (audio.value) audio.value.currentTime = v
            }

            const setTime = (time: number) => {
                if (audio.value) audio.value.currentTime = time
            }

            const deleteTag = async (id : number) => {
                await store.dispatch('deleteTag', {
                    id,
                    fileName: selectedFile.value
                })
            }

            const toggle = () => {
                if (playing.value) {
                    audio.value?.pause()
                    playing.value = false
                }
                else {
                    if (audio.value && !audio.value?.src) {
                        audio.value.src = props.file.url
                    }

                    audio.value?.play()
                    playing.value = true
                    enableSeek.value = true
                }
            }

            const formatTime = (time: number) => {
                const minutes = Math.floor(time / 60)
                const seconds = Math.floor(time % 60)
                const returnedSeconds = seconds < 10 ? `0${seconds}` : `${seconds}`
                return `${minutes}:${returnedSeconds}`
            }

            const openRemoveTagDialog = (file: string) => {
                selectedFile.value = file
                openRemoveTag.value = true
            }

            const closeRemoveTagDialog = () => {
                openRemoveTag.value = false
            }

            const openAddTagDialog = (file: string) => {
                selectedFile.value = file
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

                await store.dispatch('addTag', {
                    name: newTagName.value,
                    file: selectedFile.value,
                    time: newTagTimeStamp.value
                })

                newTagName.value = ''
                newTagTimeStamp.value = ''
            }

            onMounted(() => {
                if (audio.value) {
                    audio.value.ontimeupdate = () => {
                        playerTime.value = audio.value?.currentTime || 0
                        current.value = formatTime(audio.value?.currentTime || 0)
                        const relativePosition = Math.floor(((audio.value?.currentTime || 0) / (audio.value?.duration || 1)) * 100)
                        seekPosition.value = `${relativePosition}%`
                    }
                    audio.value.ondurationchange = () => {
                        total.value = formatTime(audio.value?.duration || 0)
                    }

                    audio.value.onprogress = () => {
                        const bufferedAmount = Math.floor(audio.value!.buffered.end(audio.value!.buffered.length - 1))
                        const relativePosition = Math.floor((bufferedAmount / (audio.value?.duration || 1)) * 100)
                        bufferPosition.value = `${relativePosition}%`
                    }
                }
            })

            return {
                audio,
                toggle,
                setTime,
                playing,
                seek,
                playerTime,
                current,
                total,
                seekPosition,
                bufferPosition,
                enableSeek,
                openAddTagDialog,
                closeAddTagDialog,
                saveTag,
                newTagName,
                newTagTimeStamp,
                openAddTag,
                formatTime,
                openRemoveTagDialog,
                openRemoveTag,
                closeRemoveTagDialog,
                deleteTag
            }
        },
    })
</script>
<style lang='scss' scoped>

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
        border-radius: var(--border-radius);
        background-color: var(--primary);
        color: var(--primary-inverse);
        text-align: center;
        padding-right: 0.5em;
        padding-left: 0.5em;

        &--time {
            background-color: var(--ins-color);
        }
    }

    &__action {
        font-size: 2.5em;
        font-weight: bold;
        justify-self: end;
        margin-right: 0.5em;
    }
}
    .player {
        display: flex;
        flex-direction: column;

        &__controls {
            display: grid;
            grid-template-columns: auto 1fr auto;
        }

        &__seek {
            width: 100%;
            &::-webkit-slider-runnable-track {
                background: linear-gradient(to right, var(--primary-hover) v-bind(seekPosition), var(--primary) v-bind(seekPosition), var(--primary) v-bind(bufferPosition), var(--primary-focus) v-bind(bufferPosition));
            }

            &::-moz-range-track{
                background: linear-gradient(to right, var(--primary-hover) v-bind(seekPosition), var(--primary) v-bind(seekPosition), var(--primary) v-bind(bufferPosition), var(--primary-focus) v-bind(bufferPosition));
            }
        }

        &__button {
            width: 2.5em;
        }

        &__button-container {

            grid-column: 2;
            justify-self: center;
        }

        &__current-time {
            grid-column: 1;
        }

        &__total-time {
            grid-column: 3;
        }
    }
</style>
