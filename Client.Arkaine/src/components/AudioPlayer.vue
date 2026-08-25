<template>
    <div class="player">
        <audio ref="audio" preload="none"></audio>
        <div class="player__meta">
            <span class="player__label">Audio</span>
            <span aria-live="polite">{{ current }} / {{ total }}</span>
        </div>
        <input
            type="range"
            @input="seek"
            :value="playerTime"
            :disabled="!enableSeek"
            class="player__seek"
            aria-label="Seek through audio"
            min="0"
            :max="audio?.duration || 0"
        />
        <div class="player__controls">
            <div class="player__button-container">
                <button type="button" class="player__skip" @click="jump(-10)" aria-label="Rewind 10 seconds">-10</button>
                <button
                    type="button"
                    class="player__button player__button--primary"
                    @click="toggle"
                    :aria-label="playing ? 'Pause' : 'Play'"
                >
                    <svg v-if="!playing" viewBox="0 0 24 24" aria-hidden="true">
                        <polygon points="5 3 19 12 5 21 5 3"></polygon>
                    </svg>
                    <svg v-else viewBox="0 0 24 24" aria-hidden="true">
                        <rect x="6" y="4" width="4" height="16"></rect>
                        <rect x="14" y="4" width="4" height="16"></rect>
                    </svg>
                </button>
                <button type="button" class="player__skip" @click="jump(10)" aria-label="Forward 10 seconds">+10</button>
            </div>
        </div>
        <TagCloud :file="file" @click="setTime"></TagCloud>
    </div>
</template>
<script lang='ts'>
    import ArkaineFile from '@/models/arkaine-file'
    import { secondsToTime } from '@/utils/formatters'
    import { PropType, defineComponent, onMounted, ref } from 'vue'
    import TagCloud from './TagCloud.vue'
    
    export default defineComponent({
        name: 'AudioPlayer',
        components: {
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
                if (audio.value) {
                    audio.value.ontimeupdate = () => {
                        playerTime.value = audio.value?.currentTime || 0
                        current.value = secondsToTime(audio.value?.currentTime || 0)
                        const relativePosition = Math.floor(((audio.value?.currentTime || 0) / (audio.value?.duration || 1)) * 100)
                        seekPosition.value = `${relativePosition}%`
                    }
                    audio.value.ondurationchange = () => {
                        total.value = secondsToTime(audio.value?.duration || 0)
                    }

                    audio.value.onended = () => {
                        playing.value = false
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
                jump
            }
        },
    })
</script>
<style lang='scss' scoped>

    .player {
        display: flex;
        flex-direction: column;
        width: 100%;
        min-height: 100%;
        padding: 1rem;
        border: 1px solid var(--app-border);
        border-radius: 0.75rem;
        background: var(--app-surface-raised);

        &__meta {
            display: flex;
            justify-content: space-between;
            gap: 1rem;
            margin-bottom: 0.6rem;
            color: var(--app-muted);
            font-size: 0.8rem;
        }

        &__label {
            color: var(--pico-color);
            font-weight: 700;
        }

        &__controls {
            display: flex;
            justify-content: center;
            margin-top: 0.5rem;
        }

        &__seek {
            width: 100%;
            margin: 0;
            accent-color: var(--pico-primary);

            &::-webkit-slider-runnable-track {
                height: 0.4rem;
                border-radius: 1rem;
                background: linear-gradient(to right, var(--pico-primary) v-bind(seekPosition), var(--pico-secondary-background) v-bind(seekPosition), var(--pico-secondary-background) v-bind(bufferPosition), var(--pico-range-border-color) v-bind(bufferPosition));
            }

            &::-moz-range-track {
                height: 0.4rem;
                border-radius: 1rem;
                background: linear-gradient(to right, var(--pico-primary) v-bind(seekPosition), var(--pico-secondary-background) v-bind(seekPosition), var(--pico-secondary-background) v-bind(bufferPosition), var(--pico-range-border-color) v-bind(bufferPosition));
            }
        }

        &__button {
            display: grid;
            width: 2.75rem;
            height: 2.75rem;
            margin: 0 0.3rem;
            padding: 0.65rem;
            place-items: center;
            color: var(--pico-color);
            border: 1px solid var(--app-border);
            border-radius: 50%;
            background: var(--app-surface);
            box-shadow: none;

            svg {
                width: 100%;
                height: 100%;
                fill: none;
                stroke: currentColor;
                stroke-linecap: round;
                stroke-linejoin: round;
                stroke-width: 2;
            }

            polygon {
                fill: currentColor;
                stroke: none;
            }

            &:hover,
            &:focus-visible {
                color: var(--pico-primary-hover);
                border-color: var(--pico-primary);
                background: var(--pico-primary-focus);
                box-shadow: none;
            }

            &--primary {
                color: var(--pico-primary-inverse);
                border-color: var(--pico-primary-background);
                background: var(--pico-primary-background);

                &:hover,
                &:focus-visible {
                    color: var(--pico-primary-inverse);
                    border-color: var(--pico-primary-hover-background);
                    background: var(--pico-primary-hover-background);
                }
            }
        }

        &__button-container {
            display: grid;
            grid-auto-flow: column;
            align-items: center;
        }

        &__skip {
            min-width: 2.75rem;
            margin: 0;
            padding: 0.35rem;
            color: var(--app-muted);
            border: 0;
            background: transparent;
            box-shadow: none;
            font-size: 0.85rem;
            font-weight: 700;

            &:hover,
            &:focus-visible {
                color: var(--pico-primary-hover);
                background: transparent;
                box-shadow: none;
            }
        }
    }
</style>
