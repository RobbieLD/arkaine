<template>
    <div class="login-page">
        <article class="login-card">
            <header class="login-card__header">
                <img class="login-card__mark" src="/icon.png" alt="Arkaine logo">
                <p class="eyebrow">Private media library</p>
                <h1>{{ isTotp ? 'Verify your sign-in' : 'Welcome back' }}</h1>
                <p>
                    {{ isTotp
                        ? 'Enter the code from your authenticator app to continue.'
                        : 'Sign in to continue to your library.' }}
                </p>
            </header>

            <div v-if="error" class="message error" role="alert">{{ error }}</div>

            <form class="login-form" @submit.prevent="action">
                <label v-if="!isTotp" for="username">
                    Username
                    <input
                        type="text"
                        id="username"
                        name="username"
                        placeholder="Enter your username"
                        autocomplete="username"
                        v-model="username"
                        required
                    />
                </label>

                <label v-if="!isTotp" for="password">
                    Password
                    <input
                        type="password"
                        id="password"
                        name="password"
                        placeholder="Enter your password"
                        autocomplete="current-password"
                        v-model="password"
                        required
                    />
                </label>

                <label v-if="isTotp" for="totp">
                    Authenticator code
                    <input
                        type="text"
                        id="totp"
                        name="totp"
                        placeholder="6-digit code"
                        autocomplete="one-time-code"
                        inputmode="numeric"
                        maxlength="6"
                        v-model="totp"
                        required
                    />
                </label>

                <label class="remember" for="switch">
                    <input type="checkbox" id="switch" v-model="remember">
                    <span>Remember this device</span>
                </label>

                <button class="login-submit" type="submit" :disabled="loggingIn">
                    {{ loggingIn ? 'Signing in...' : (isTotp ? 'Verify code' : 'Sign in') }}
                </button>
            </form>

            <div v-if="!isTotp && passkeysSupported" class="alternative">
                <span class="alternative__line"></span>
                <span class="alternative__label">or</span>
                <span class="alternative__line"></span>
            </div>

            <button
                v-if="!isTotp && passkeysSupported"
                type="button"
                class="passkey-button secondary outline"
                @click="passkeyLogin"
                :disabled="loggingIn"
            >
                Sign in with passkey
            </button>

            <button
                v-if="isTotp"
                type="button"
                class="back-button"
                @click="returnToPasswordLogin"
                :disabled="loggingIn"
            >
                Use a different sign-in method
            </button>

            <footer class="login-card__footer">Version {{ version }}</footer>
        </article>
    </div>
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

            const returnToPasswordLogin = () => {
                isTotp.value = false
                totp.value = undefined
                error.value = undefined
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
                returnToPasswordLogin,
            }
        },
    })
</script>

<style lang="scss" scoped>
    .login-page {
        display: grid;
        min-height: min(42rem, calc(100vh - 8rem));
        place-items: center;
    }

    .login-card {
        width: min(100%, 28rem);
        margin: 0 auto;
        padding: clamp(1.5rem, 5vw, 2.5rem);
        border: 1px solid var(--app-border);
        border-radius: 1rem;
        background: var(--app-surface);
        box-shadow: var(--pico-card-box-shadow);
    }

    .login-card__header {
        margin-bottom: 1.75rem;
        text-align: center;
    }

    .login-card__mark {
        display: block;
        width: 3rem;
        height: 3rem;
        margin: 0 auto;
        border-radius: 0.9rem;
        object-fit: cover;
        box-shadow: 0 0.5rem 1.25rem var(--pico-primary-focus);
    }

    .eyebrow {
        margin: 1.25rem 0 0.4rem;
        color: var(--pico-primary-hover);
        font-size: 0.75rem;
        font-weight: 700;
        letter-spacing: 0.1em;
        text-transform: uppercase;
    }

    h1 {
        margin: 0;
        color: var(--pico-h1-color);
        font-size: clamp(1.75rem, 5vw, 2.25rem);
    }

    .login-card__header p:last-child {
        max-width: 22rem;
        margin: 0.65rem auto 0;
        color: var(--app-muted);
    }

    .login-form {
        margin: 0;
    }

    .login-form > label:not(.remember) {
        display: block;
        margin-bottom: 0.9rem;
        color: var(--pico-color);
        font-size: 0.9rem;
        font-weight: 600;
    }

    .login-form > label:not(.remember) input {
        margin-top: 0.4rem;
        margin-bottom: 0;
    }

    .remember {
        display: flex;
        align-items: center;
        gap: 0.55rem;
        margin: 0.25rem 0 1.25rem;
        color: var(--app-muted);
        font-size: 0.9rem;
    }

    .remember input {
        margin: 0;
    }

    .login-submit,
    .passkey-button {
        width: 100%;
        margin: 0;
    }

    .login-submit {
        font-weight: 700;
    }

    .message {
        margin: 0 0 1.25rem;
        padding: 0.75rem;
        border-radius: var(--pico-border-radius);
        font-size: 0.9rem;
    }

    .alternative {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        margin: 1.4rem 0 1rem;
        color: var(--app-muted);
        font-size: 0.8rem;
    }

    .alternative__line {
        height: 1px;
        flex: 1;
        background: var(--app-border);
    }

    .alternative__label {
        text-transform: uppercase;
        letter-spacing: 0.08em;
    }

    .passkey-button {
        color: var(--pico-primary-hover);
    }

    .back-button {
        display: block;
        width: 100%;
        margin: 1rem 0 0;
        padding: 0.25rem;
        color: var(--app-muted);
        background: transparent;
        box-shadow: none;
        font-size: 0.9rem;
    }

    .back-button:hover,
    .back-button:focus-visible {
        color: var(--pico-primary-hover);
        background: transparent;
        box-shadow: none;
    }

    .login-card__footer {
        margin-top: 1.75rem;
        color: var(--app-muted);
        font-size: 0.75rem;
        text-align: center;
    }
</style>
