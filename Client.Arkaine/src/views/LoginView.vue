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
            <button
                v-if="!isTotp && passkeysSupported"
                type="button"
                @click="passkeyLogin"
                v-bind:disabled="loggingIn"
            >
                Sign in with passkey
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
    import { useRoute, useRouter } from 'vue-router'
    import { version } from '@/config'
    import { getPasskey, serializePasskeyAssertion } from '@/services/passkey'

    export default defineComponent({
        name: 'LoginView',
        components: {},
        setup() {
            const username = ref<string>('')
            const password = ref<string>('')
            const remember = ref(false)
            const totp = ref<string>()
            const error = ref<string>()
            const isTotp = ref(false)
            const loggingIn = ref(false)
            const passkeysSupported = typeof window !== 'undefined' &&
                typeof window.PublicKeyCredential !== 'undefined' &&
                typeof navigator.credentials !== 'undefined'
            const store = useStore(storeKey)
            const router = useRouter()
            const route = useRoute()

            const goAfterLogin = async () => {
                const redirect = route.query.redirect
                const destination = typeof redirect === 'string' &&
                    redirect.startsWith('/') &&
                    !redirect.startsWith('//')
                    ? redirect
                    : '/'
                await router.push(destination)
            }

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
                    const requires2Fa = await store.dispatch('login', {
                        username: DOMPurify.sanitize(username.value || ''),
                        password: DOMPurify.sanitize(password.value || ''),
                        remember: remember.value
                    })

                    loggingIn.value = false
                    
                    if (requires2Fa) {
                        isTotp.value = true
                    }
                    else {
                        await store.dispatch('checkLogin')
                        await goAfterLogin()
                    }

                } catch (e) {
                    error.value = (e as Error).message
                    loggingIn.value = false
                }
            }

            const authenticate = async () => {
                loggingIn.value = true

                try {
                    await store.dispatch('twoFactorAuth', {
                        code: DOMPurify.sanitize(totp.value || ''),
                        remember: remember.value
                    })

                    await store.dispatch('checkLogin')
                    await goAfterLogin()

                } catch (e) {
                    error.value = (e as Error).message
                    loggingIn.value = false
                }
            }

            const passkeyLogin = async () => {
                if (!passkeysSupported) {
                    return
                }

                loggingIn.value = true
                error.value = undefined

                try {
                    const options = await store.dispatch(
                        'passkeyRequestOptions',
                        username.value.trim() || undefined)
                    const credential = await getPasskey(options)
                    await store.dispatch('passkeyLogin', {
                        credential: serializePasskeyAssertion(credential),
                        remember: remember.value
                    })
                    await store.dispatch('checkLogin')
                    await goAfterLogin()
                }
                catch (e) {
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
                version,
                passkeyLogin,
                passkeysSupported,
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
