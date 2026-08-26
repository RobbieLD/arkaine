<template>
    <div class="player">
        <audio ref="audio" preload="none"></audio>
        <div class="player__meta">
            <span class="player__label">
                <app-icon name="music" />
                Audio
            </span>
            <span class="player__time" aria-live="polite">{{ current }} / {{ total }}</span>
        </div>
        <input
            type="range"
            class="range player__seek"
            :value="playerTime"
            :disabled="!enableSeek"
            aria-label="Seek through audio"
            min="0"
            :max="audio?.duration || 0"
            @input="seek"
        />
        <div class="player__controls">
            <button
                type="button"
                class="btn btn--ghost btn--icon btn--round"
                aria-label="Rewind 10 seconds"
                @click="jump(-10)"
            >
                <app-icon name="rewind" />
            </button>
            <button
                type="button"
                class="btn btn--primary btn--icon btn--round player__play"
                :aria-label="playing ? 'Pause' : 'Play'"
                @click="toggle"
            >
                <app-icon :name="playing ? 'pause' : 'play'" />
            </button>
            <button
                type="button"
                class="btn btn--ghost btn--icon btn--round"
                aria-label="Forward 10 seconds"
                @click="jump(10)"
            >
                <app-icon name="forward" />
            </button>
        </div>
        <tag-cloud :file="file" @click="setTime"></tag-cloud>
    </div>
</template>
<script lang="ts">
    import ArkaineFile from '@/models/arkaine-file'
    import { secondsToTime } from '@/utils/formatters'
    import { PropType, defineComponent, onMounted, ref } from 'vue'
    import AppIcon from './AppIcon.vue'
    import TagCloud from './TagCloud.vue'

    export default defineComponent({
        name: 'AudioPlayer',
        components: {
            AppIcon,
            TagCloud
        },
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

            const seek = async (ev: Event) => {
                const v = Number.parseInt((ev.target as HTMLInputElement).value)
                if (audio.value) audio.value.currentTime = v
            }

            const setTime = (time: number) => {
                if (audio.value) audio.value.currentTime = time
            }

            const jump = (offset: number) => {
                if (audio.value) audio.value.currentTime += offset
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

            onMounted(() => {
                const element = audio.value

                if (!element) {
                    return
                }

                element.ontimeupdate = () => {
                    playerTime.value = element.currentTime
                    current.value = secondsToTime(element.currentTime)
                    const relativePosition = Math.floor((element.currentTime / (element.duration || 1)) * 100)
                    seekPosition.value = `${relativePosition}%`
                }

                element.ondurationchange = () => {
                    total.value = secondsToTime(element.duration || 0)
                }

                element.onended = () => {
                    playing.value = false
                }

                element.onpause = () => {
                    playing.value = false
                }

                element.onprogress = () => {
                    // buffered is empty until the first range arrives; end(-1) throws.
                    if (element.buffered.length === 0) {
                        return
                    }

                    const bufferedAmount = element.buffered.end(element.buffered.length - 1)
                    const relativePosition = Math.floor((bufferedAmount / (element.duration || 1)) * 100)
                    bufferPosition.value = `${relativePosition}%`
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
                jump
            }
        },
    })
</script>
<style lang="scss" scoped>
    .player {
        display: flex;
        width: 100%;
        flex-direction: column;
        gap: var(--space-3);
        padding: var(--space-4);
    }

    .player__meta {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--space-4);
        color: var(--text-subtle);
        font-size: var(--text-xs);
    }

    .player__label {
        display: inline-flex;
        align-items: center;
        gap: var(--space-2);
        color: var(--text-muted);
        font-weight: 700;
        letter-spacing: 0.04em;
        text-transform: uppercase;
        --icon-size: 0.9rem;
    }

    .player__time {
        font-variant-numeric: tabular-nums;
    }

    .player__seek {
        --range-track:
            linear-gradient(
                to right,
                var(--accent) v-bind(seekPosition),
                var(--surface-hover) v-bind(seekPosition),
                var(--surface-hover) v-bind(bufferPosition),
                var(--border) v-bind(bufferPosition)
            );
    }

    .player__controls {
        display: flex;
        align-items: center;
        justify-content: center;
        gap: var(--space-2);
    }

    .player__play {
        width: 2.75rem;
        height: 2.75rem;
        --icon-size: 1.15rem;
    }
</style>
