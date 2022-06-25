<template>
    <div class="player">
        <audio ref="audio" :src="src" preload="metadata"></audio>
        <input type="range" @input="seek" v-bind:value="playerTime" class="player__seek" min="0" :max="audio?.duration" />
        <div class="player__controls">
            <div class="player__current-time">{{ current }}</div>
            <div class="player__button-container" @click="toggle">
                <div v-show="!playing">
                    <i data-feather="play" class="player__button"></i>
                </div>
                <div v-show="playing">
                    <i data-feather="pause" class="player__button"></i>
                </div>
            </div>
            <div class="player__total-time">{{ total }}</div>
        </div>
    </div>
</template>
<script lang='ts'>
    import { computed, defineComponent, onMounted, ref, watch } from 'vue'
    import feather from 'feather-icons'

    export default defineComponent({
        name: 'AudioPlayer',
        components: {},
        props: {
            src: {
                type: String,
                required: true,
            },
        },
        setup() {
            const audio = ref<HTMLAudioElement>()
            const playing = ref(false)
            const playerTime = ref(0)
            const total = ref('0:00')
            const current = ref('0:00')

            const seek = async (ev: Event) => {
                const v = Number.parseInt((ev.target as HTMLInputElement).value)
                if (audio.value) audio.value.currentTime = v
            }

            const toggle = async () => {
                if (playing.value) {
                    await audio.value?.pause()
                    playing.value = false
                }
                else {
                    await audio.value?.play()
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
                    }
                    audio.value.ondurationchange = () => {
                        total.value = formatTime(audio.value?.duration || 0)
                    }
                }

                feather.replace()
            })


            return {
                audio,
                toggle,
                playing,
                seek,
                playerTime,
                current,
                total
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
        }

        &__button {
            height: 50px;
            width: 50px;
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
