<template>
    <article class="login">
        <form>
            <div class="grid">
                <input
                    type="text"
                    id="username"
                    name="username"
                    placeholder="Username"
                    v-model="username"
                    required
                />

                <input
                    type="password"
                    id="password"
                    name="password"
                    placeholder="Password"
                    v-model="password"
                    required
                />
            </div>

            <button type="submit" @click="login" v-bind:disabled="loggingIn">
                Login
            </button>

            <small>{{ error }}</small>
        </form>
    </article>
</template>

<script lang="ts">
    import { storeKey } from '@/store'
    import { defineComponent, ref } from 'vue'
    import { useStore } from 'vuex'

    export default defineComponent({
        name: 'LoginView',
        components: {},
        setup() {
            const username = ref<string>()
            const password = ref<string>()
            const error = ref<string>()
            const loggingIn = ref(false)
            const store = useStore(storeKey)

            const login = async () => {
                loggingIn.value = true
                try {
                    await store.dispatch('login', {
                        username: username.value,
                        password: password.value,
                    })
                } catch (e) {
                    error.value = (e as Error).message
                    loggingIn.value = false
                }
            }

            return {
                login,
                username,
                password,
                loggingIn,
                error
            }
        },
    })
</script>

<style lang="scss">
    .login {
        max-width: 50em;
        margin: 0 auto;
        margin-top: 2em;
    }
</style>
