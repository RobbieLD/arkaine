<template>
    <div class="player">
        <audio ref="audio" :src="src" preload="metadata"></audio>
        <input type="range" @input="seek" v-bind:value="playerTime" class="player__seek" min="0" max="100" />
        <div class="player__controls">
            <div @click="toggle">
                <div v-show="!playing">
                    <i data-feather="play" class="player__button"></i>
                </div>
                <div v-show="playing">
                    <i data-feather="pause" class="player__button"></i>
                </div>
            </div>
        </div>
        <!-- <button id="play-icon"></button>
        <span id="current-time" class="time">0:00</span>
        
        <span id="duration" class="time">0:00</span> -->
    </div>
</template>
<script lang='ts'>
    import { defineComponent, onMounted, ref } from 'vue'
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

            const seek = async () => {

                audio.value!.currentTime = Math.floor((playerTime.value / (audio.value?.currentTime || 0)) * 100)

                if (!playing.value) {
                    await audio.value?.play()
                }
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


            onMounted(() => {
                
                if (audio.value !== null) {
                    audio.value!.ontimeupdate = () => {
                        playerTime.value = Math.floor((audio.value?.currentTime || 0 / (audio.value?.duration || 0)) * 100)
                    }
                }
                feather.replace()
            })


            return {
                audio,
                toggle,
                playing,
                seek,
                playerTime
            }
        },
    })
</script>
<style lang='scss' scoped>
    .player {
        display: flex;
        flex-direction: column;

        &__controls {
            display: flex;
            flex-direction: row;
            justify-content: center;
        }

        &__seek {
            width: 100%;
        }

        &__button {
            height: 50px;
            width: 50px;
        }
    }
</style>
