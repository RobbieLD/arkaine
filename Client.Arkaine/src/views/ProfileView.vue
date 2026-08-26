<template>
    <div class="page profile">
        <header class="page-header">
            <div>
                <p class="eyebrow">Account</p>
                <h1 class="page-header__title">User profile</h1>
                <p class="page-header__lead">Manage your account security settings.</p>
            </div>
        </header>

        <p v-if="error" class="alert alert--error" role="alert">{{ error }}</p>
        <p v-if="success" class="alert alert--success" role="status">{{ success }}</p>

        <p v-if="loading" class="muted">Loading profile...</p>
        <p v-if="!profile && !loading" class="muted">Unable to load your profile.</p>

        <template v-if="profile">
            <section class="panel stack">
                <h2>Account</h2>
                <div class="values">
                    <label class="field">
                        <span class="label">Username</span>
                        <input class="input" :value="profile.userName" type="text" readonly>
                    </label>
                    <label class="field">
                        <span class="label">Email</span>
                        <input class="input" :value="profile.email || 'Not set'" type="text" readonly>
                    </label>
                </div>
            </section>

            <section class="panel stack">
                <h2>Two-factor authentication</h2>
                <p v-if="profile.twoFactorEnabled" class="muted">
                    Two-factor authentication is enabled. You have {{ profile.recoveryCodesLeft }} recovery
                    code{{ profile.recoveryCodesLeft === 1 ? '' : 's' }} remaining.
                </p>

                <template v-else>
                    <p class="muted">Protect your account with an authenticator app.</p>
                    <form v-if="!twoFactorSetup" @submit.prevent="loadTwoFactorSetup">
                        <button type="submit" class="btn btn--primary" :disabled="twoFactorLoading">
                            <app-icon name="key" />
                            {{ twoFactorLoading ? 'Preparing...' : 'Set up authenticator' }}
                        </button>
                    </form>

                    <div v-if="twoFactorSetup && !twoFactorSetup.isEnabled" class="stack">
                        <p class="muted">Enter this key in your authenticator app:</p>
                        <p><code>{{ twoFactorSetup.sharedKey }}</code></p>
                        <p class="muted">
                            Authenticator URI: <code>{{ twoFactorSetup.authenticatorUri }}</code>
                        </p>
                        <p>
                            <a
                                v-if="twoFactorSetup.authenticatorUri"
                                :href="twoFactorSetup.authenticatorUri"
                            >
                                Open authenticator URI
                            </a>
                        </p>
                        <form class="stack" @submit.prevent="enableTwoFactor">
                            <label class="field">
                                <span class="label">Authenticator code</span>
                                <input
                                    id="enable-code"
                                    v-model="twoFactorCode"
                                    class="input"
                                    type="text"
                                    inputmode="numeric"
                                    autocomplete="one-time-code"
                                    maxlength="6"
                                    required
                                >
                            </label>
                            <div>
                                <button type="submit" class="btn btn--primary" :disabled="twoFactorLoading">
                                    {{ twoFactorLoading ? 'Enabling...' : 'Enable two-factor authentication' }}
                                </button>
                            </div>
                        </form>
                    </div>
                </template>

                <div v-if="recoveryCodes.length > 0" class="stack">
                    <h3>Save your recovery codes</h3>
                    <p class="muted">Each code can be used once if you lose access to your authenticator app.</p>
                    <textarea class="textarea" :value="recoveryCodes.join('\n')" rows="5" readonly></textarea>
                </div>

                <form v-if="profile.twoFactorEnabled" class="stack" @submit.prevent="disableTwoFactor">
                    <label class="field">
                        <span class="label">Authenticator or recovery code</span>
                        <input
                            id="disable-code"
                            v-model="twoFactorCode"
                            class="input"
                            type="text"
                            autocomplete="one-time-code"
                            required
                        >
                    </label>
                    <div>
                        <button type="submit" class="btn btn--danger" :disabled="twoFactorLoading">
                            {{ twoFactorLoading ? 'Disabling...' : 'Disable two-factor authentication' }}
                        </button>
                    </div>
                </form>
            </section>

            <section class="panel stack">
                <h2>Passkeys</h2>
                <p v-if="!passkeysSupported" class="alert alert--info">
                    Passkeys are not supported by this browser.
                </p>
                <form v-else class="stack" @submit.prevent="registerPasskey">
                    <label class="field">
                        <span class="label">Passkey name</span>
                        <input
                            id="passkey-name"
                            v-model="passkeyName"
                            class="input"
                            type="text"
                            maxlength="100"
                            placeholder="This device"
                        >
                    </label>
                    <div>
                        <button type="submit" class="btn btn--primary" :disabled="passkeyLoading">
                            <app-icon name="plus" />
                            {{ passkeyLoading ? 'Registering...' : 'Add passkey' }}
                        </button>
                    </div>
                </form>

                <p v-if="profile.passkeys.length === 0" class="muted">No passkeys have been registered.</p>
                <ul v-else class="passkeys">
                    <li v-for="passkey in profile.passkeys" :key="passkey.id">
                        <div class="truncate">
                            <strong>{{ passkey.name || 'Unnamed passkey' }}</strong>
                            <small class="muted">
                                Added {{ formatDate(passkey.createdAt) }}
                                <span v-if="passkey.isBackedUp"> - Backed up</span>
                            </small>
                        </div>
                        <button
                            type="button"
                            class="btn btn--sm btn--danger"
                            :disabled="passkeyLoading"
                            @click="removePasskey(passkey.id)"
                        >
                            <app-icon name="trash" />
                            Remove
                        </button>
                    </li>
                </ul>
            </section>
        </template>
    </div>
</template>

<script lang="ts">
    import { defineComponent, onMounted, ref } from 'vue'
    import ArkaineService from '@/services/arkaine.service'
    import Profile, {
        TwoFactorSetup
    } from '@/models/profile'
    import { createPasskey, serializePasskeyRegistration } from '@/services/passkey'
    import AppIcon from '@/components/AppIcon.vue'

    const maxPasskeyNameLength = 100

    export default defineComponent({
        name: 'ProfileView',
        components: {
            AppIcon
        },
        setup() {
            const service = new ArkaineService()
            const profile = ref<Profile>()
            const twoFactorSetup = ref<TwoFactorSetup>()
            const recoveryCodes = ref<string[]>([])
            const twoFactorCode = ref('')
            const passkeyName = ref('')
            const loading = ref(true)
            const twoFactorLoading = ref(false)
            const passkeyLoading = ref(false)
            const error = ref<string>()
            const success = ref<string>()
            const passkeysSupported = typeof window !== 'undefined' &&
                typeof window.PublicKeyCredential !== 'undefined' &&
                typeof navigator.credentials !== 'undefined'

            const getErrorMessage = (value: unknown): string => {
                if (typeof value === 'object' && value !== null && 'message' in value) {
                    const message = value.message
                    if (typeof message === 'string') {
                        return message
                    }

                    if (typeof message === 'object' && message !== null && 'message' in message) {
                        const nestedMessage = message.message
                        if (typeof nestedMessage === 'string') {
                            return nestedMessage
                        }
                    }

                    if (typeof message === 'object' && message !== null && 'detail' in message) {
                        const detail = message.detail
                        if (typeof detail === 'string') {
                            return detail
                        }
                    }
                }

                if (value instanceof Error) {
                    return value.message
                }

                return 'The request could not be completed.'
            }

            const loadProfile = async () => {
                loading.value = true
                try {
                    profile.value = await service.GetProfile()
                }
                catch (e: unknown) {
                    error.value = getErrorMessage(e)
                }
                finally {
                    loading.value = false
                }
            }

            const loadTwoFactorSetup = async () => {
                error.value = undefined
                success.value = undefined
                twoFactorLoading.value = true
                try {
                    twoFactorSetup.value = await service.GetTwoFactorSetup()
                }
                catch (e: unknown) {
                    error.value = getErrorMessage(e)
                }
                finally {
                    twoFactorLoading.value = false
                }
            }

            const enableTwoFactor = async () => {
                const code = twoFactorCode.value.replace(/[\s-]/g, '')
                if (!/^\d{6}$/.test(code)) {
                    error.value = 'Enter the six-digit code from your authenticator app.'
                    return
                }

                error.value = undefined
                success.value = undefined
                twoFactorLoading.value = true
                try {
                    const result = await service.EnableTwoFactor(code)
                    recoveryCodes.value = result.recoveryCodes
                    twoFactorCode.value = ''
                    twoFactorSetup.value = undefined
                    await loadProfile()
                    success.value = 'Two-factor authentication is now enabled.'
                }
                catch (e: unknown) {
                    error.value = getErrorMessage(e)
                }
                finally {
                    twoFactorLoading.value = false
                }
            }

            const disableTwoFactor = async () => {
                if (!twoFactorCode.value.trim()) {
                    error.value = 'Enter an authenticator or recovery code.'
                    return
                }

                error.value = undefined
                success.value = undefined
                twoFactorLoading.value = true
                try {
                    await service.DisableTwoFactor(twoFactorCode.value.trim())
                    twoFactorCode.value = ''
                    recoveryCodes.value = []
                    twoFactorSetup.value = undefined
                    await loadProfile()
                    success.value = 'Two-factor authentication is now disabled.'
                }
                catch (e: unknown) {
                    error.value = getErrorMessage(e)
                }
                finally {
                    twoFactorLoading.value = false
                }
            }

            const registerPasskey = async () => {
                if (!passkeysSupported || !profile.value) {
                    return
                }

                const name = passkeyName.value.trim()
                if (name.length > maxPasskeyNameLength) {
                    error.value = `The passkey name must be ${maxPasskeyNameLength} characters or fewer.`
                    return
                }

                error.value = undefined
                success.value = undefined
                passkeyLoading.value = true
                try {
                    const options = await service.GetPasskeyCreationOptions()
                    const credential = await createPasskey(options)
                    const passkey = await service.RegisterPasskey(
                        serializePasskeyRegistration(credential),
                        name || undefined)
                    profile.value.passkeys.push(passkey)
                    passkeyName.value = ''
                    success.value = 'Passkey registered.'
                }
                catch (e: unknown) {
                    error.value = getErrorMessage(e)
                }
                finally {
                    passkeyLoading.value = false
                }
            }

            const removePasskey = async (id: string) => {
                if (!profile.value || !window.confirm('Remove this passkey?')) {
                    return
                }

                error.value = undefined
                success.value = undefined
                passkeyLoading.value = true
                try {
                    await service.RemovePasskey(id)
                    profile.value.passkeys = profile.value.passkeys.filter(passkey => passkey.id !== id)
                    success.value = 'Passkey removed.'
                }
                catch (e: unknown) {
                    error.value = getErrorMessage(e)
                }
                finally {
                    passkeyLoading.value = false
                }
            }

            const formatDate = (value: string): string => {
                return new Intl.DateTimeFormat(undefined, {
                    dateStyle: 'medium'
                }).format(new Date(value))
            }

            onMounted(loadProfile)

            return {
                error,
                formatDate,
                loadTwoFactorSetup,
                loading,
                passkeyLoading,
                passkeyName,
                passkeysSupported,
                profile,
                recoveryCodes,
                registerPasskey,
                removePasskey,
                success,
                twoFactorCode,
                twoFactorLoading,
                twoFactorSetup,
                enableTwoFactor,
                disableTwoFactor
            }
        }
    })
</script>

<style lang="scss" scoped>
    .profile {
        max-width: 60rem;
        margin: 0 auto;
    }

    .values {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(15rem, 1fr));
        gap: var(--space-4);
    }

    .passkeys {
        display: grid;
        gap: var(--space-2);
        margin: 0;
        padding: 0;
        list-style: none;
    }

    .passkeys li {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--space-4);
        padding: var(--space-3);
        border: 1px solid var(--border);
        border-radius: var(--radius-md);
        background: var(--surface-raised);
    }

    .passkeys small {
        display: block;
        font-size: var(--text-xs);
    }

    .btn {
        --icon-size: 1rem;
    }
</style>
