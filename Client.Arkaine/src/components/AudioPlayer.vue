<template>
    <div class="player">
        <audio ref="audio" preload="none"></audio>
        <input type="range" @input="seek" v-bind:value="playerTime" class="player__seek" min="0" :max="audio?.duration" />
        <div class="player__controls">
            <div class="player__current-time">{{ current }}</div>
            <div class="player__button-container" @click="toggle">
                <img src="play.svg" v-show="!playing" class="player__button" />
                <img src="pause.svg" v-show="playing" class="player__button" />
            </div>
            <div class="player__total-time">{{ total }}</div>
        </div>
    </div>
</template>
<script lang='ts'>
    import { defineComponent, onMounted, ref } from 'vue'
    
    export default defineComponent({
        name: 'AudioPlayer',
        components: {},
        props: {
            src: {
                type: String,
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

            const seek = async (ev: Event) => {
                const v = Number.parseInt((ev.target as HTMLInputElement).value)
                if (audio.value) audio.value.currentTime = v
            }

            const toggle = () => {
                if (playing.value) {
                    audio.value?.pause()
                    playing.value = false
                }
                else {
                    if (audio.value && !audio.value?.src) {
                        audio.value.src = props.src
                    }

                    audio.value?.play()
                    playing.value = true
                }
            }

            const formatTime = (time: number) => {
                const minutes = Math.floor(time / 60)
                const seconds = Math.floor(time % 60)
                const returnedSeconds = seconds < 10 ? `0${seconds}` : `${seconds}`
                return `${minutes}:${returnedSeconds}`
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
                playing,
                seek,
                playerTime,
                current,
                total,
                seekPosition,
                bufferPosition
            }
        },
    })
</script>
<style lang='scss' scoped>
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
