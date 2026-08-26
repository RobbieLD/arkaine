<template>
    <div class="login-page">
        <div class="login-card panel">
            <header class="login-card__header">
                <img class="login-card__mark" src="/icon.png" alt="Arkaine logo">
                <p class="eyebrow">Private media library</p>
                <h1 class="login-card__title">{{ isTotp ? 'Verify your sign-in' : 'Welcome back' }}</h1>
                <p class="login-card__lead">
                    {{ isTotp
                        ? 'Enter the code from your authenticator app to continue.'
                        : 'Sign in to continue to your library.' }}
                </p>
            </header>

            <div v-if="error" class="alert alert--error login-card__alert" role="alert">{{ error }}</div>

            <form class="login-form" @submit.prevent="action">
                <label v-if="!isTotp" class="field" for="username">
                    <span class="label">Username</span>
                    <input
                        id="username"
                        v-model="username"
                        class="input"
                        type="text"
                        name="username"
                        placeholder="Enter your username"
                        autocomplete="username"
                        required
                    />
                </label>

                <label v-if="!isTotp" class="field" for="password">
                    <span class="label">Password</span>
                    <input
                        id="password"
                        v-model="password"
                        class="input"
                        type="password"
                        name="password"
                        placeholder="Enter your password"
                        autocomplete="current-password"
                        required
                    />
                </label>

                <label v-if="isTotp" class="field" for="totp">
                    <span class="label">Authenticator code</span>
                    <input
                        id="totp"
                        v-model="totp"
                        class="input"
                        type="text"
                        name="totp"
                        placeholder="6-digit code"
                        autocomplete="one-time-code"
                        inputmode="numeric"
                        maxlength="6"
                        required
                    />
                </label>

                <label class="checkbox" for="switch">
                    <input id="switch" v-model="remember" type="checkbox">
                    <span>Remember this device</span>
                </label>

                <button class="btn btn--primary btn--block" type="submit" :disabled="loggingIn">
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
                class="btn btn--block"
                :disabled="loggingIn"
                @click="passkeyLogin"
            >
                <app-icon name="key" />
                Sign in with passkey
            </button>

            <button
                v-if="isTotp"
                type="button"
                class="btn btn--ghost btn--block back-button"
                :disabled="loggingIn"
                @click="returnToPasswordLogin"
            >
                Use a different sign-in method
            </button>

            <footer class="login-card__footer">Version {{ version }}</footer>
        </div>
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
    import AppIcon from '@/components/AppIcon.vue'

    export default defineComponent({
        name: 'LoginView',
        components: {
            AppIcon
        },
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
        min-height: min(42rem, calc(100vh - 10rem));
        place-items: center;
    }

    .login-card {
        width: min(100%, 26rem);
        margin: 0 auto;
        box-shadow: var(--shadow-lg);
    }

    .login-card__header {
        margin-bottom: var(--space-6);
        text-align: center;
    }

    .login-card__mark {
        display: block;
        width: 3rem;
        height: 3rem;
        margin: 0 auto var(--space-5);
        border-radius: var(--radius-lg);
        object-fit: cover;
    }

    .login-card__title {
        margin: 0;
        font-size: var(--text-2xl);
    }

    .login-card__lead {
        max-width: 22rem;
        margin: var(--space-2) auto 0;
        color: var(--text-muted);
        font-size: var(--text-sm);
    }

    .login-card__alert {
        margin-bottom: var(--space-4);
    }

    .login-form {
        display: grid;
        gap: var(--space-4);
    }

    .alternative {
        display: flex;
        align-items: center;
        gap: var(--space-3);
        margin: var(--space-5) 0 var(--space-4);
        color: var(--text-subtle);
        font-size: var(--text-xs);
    }

    .alternative__line {
        height: 1px;
        flex: 1;
        background: var(--border);
    }

    .alternative__label {
        letter-spacing: 0.08em;
        text-transform: uppercase;
    }

    .back-button {
        margin-top: var(--space-4);
    }

    .login-card__footer {
        margin-top: var(--space-6);
        color: var(--text-subtle);
        font-size: var(--text-xs);
        text-align: center;
    }
</style>
