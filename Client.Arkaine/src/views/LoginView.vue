<template>
    <article class="login">
        <form class="login-form">
            <div class="grid">
                <input
                    v-if="!isTotp"
                    type="text"
                    id="username"
                    name="username"
                    placeholder="Username"
                    v-model="username"
                    required
                />

                <input
                    v-if="!isTotp"
                    type="password"
                    id="password"
                    name="password"
                    placeholder="Password"
                    v-model="password"
                    required
                />

                <input
                    v-if="isTotp"
                    type="text"
                    id="totp"
                    name="totp"
                    placeholder="TOTP"
                    v-model="totp"
                    required
                />

            </div>

            <button type="submit" @click="action" v-bind:disabled="loggingIn">
                Submit
            </button>
            <label for="switch">
                <input type="checkbox" id="switch" v-model="remember">
                Remember Me
            </label>
            <div class="version">{{ version }}</div>
            <div>{{ error }}</div>
        </form>
    </article>
</template>

<script lang="ts">
    import { storeKey } from '@/store'
    import { defineComponent, ref } from 'vue'
    import { useStore } from 'vuex'
    import DOMPurify from 'dompurify'
    import { useRouter } from 'vue-router'

    export default defineComponent({
        name: 'LoginView',
        components: {},
        setup() {
            const isLocal = process.env?.VUE_APP_ARKAINE_VERSION == 'DEV'
            const username = ref<string>(isLocal ? 'user' : '')
            const password = ref<string>(isLocal ? '.Password1' : '')
            const remember = ref(false)
            const totp = ref<string>()
            const error = ref<string>()
            const isTotp = ref(false)
            const loggingIn = ref(false)
            const store = useStore(storeKey)
            const router = useRouter()

            const action = async (e: Event) => {
                e.preventDefault()
                if (isTotp.value) {
                    await authenticate()
                }
                else {
                    await login()
                }
                
            }

            const login = async () => {
                loggingIn.value = true
                try {
                    await store.dispatch('login', {
                        username: DOMPurify.sanitize(username.value || ''),
                        password: DOMPurify.sanitize(password.value || ''),
                        remember: remember.value
                    })

                    isTotp.value = true
                    loggingIn.value = false

                } catch (e) {
                    error.value = (e as Error).message
                    loggingIn.value = false
                }
            }

            const authenticate = async () => {
                loggingIn.value = true

                try {
                    await store.dispatch('twoFactorAuth', {
                        username: DOMPurify.sanitize(username.value || ''),
                        code: DOMPurify.sanitize(totp.value || ''),
                    })

                    await store.dispatch('checkLogin')
                    await router.push('/')

                } catch (e) {
                    error.value = (e as Error).message
                    loggingIn.value = false
                }
            }

            return {
                action,
                username,
                password,
                loggingIn,
                totp,
                isTotp,
                error,
                remember,
                version: process.env?.VUE_APP_ARKAINE_VERSION,
            }
        },
    })
</script>

<style lang="scss" scoped>
    .login {
        max-width: 35em;
        margin: 0 auto;
        margin-top: 20vh;
    }

    .login-form {
        margin-bottom: 0em;
    }

    .version {
        font-size: 0.8em;
        text-align: right;
    }
</style>
