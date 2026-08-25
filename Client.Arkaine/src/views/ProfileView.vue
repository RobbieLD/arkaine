<template>
    <article class="profile">
        <header>
            <h2>User profile</h2>
            <p>Manage your account security settings.</p>
        </header>

        <p v-if="error" class="message error">{{ error }}</p>
        <p v-if="success" class="message success">{{ success }}</p>

        <p v-if="loading">Loading profile...</p>
        <p v-if="!profile && !loading">Unable to load your profile.</p>

        <template v-if="profile">
            <section>
                <h3>Account</h3>
                <div class="values">
                    <label>
                        Username
                        <input :value="profile.userName" type="text" readonly>
                    </label>
                    <label>
                        Email
                        <input :value="profile.email || 'Not set'" type="text" readonly>
                    </label>
                </div>
            </section>

            <section>
                <h3>Two-factor authentication</h3>
                <p v-if="profile.twoFactorEnabled">
                    Two-factor authentication is enabled. You have {{ profile.recoveryCodesLeft }} recovery
                    code{{ profile.recoveryCodesLeft === 1 ? '' : 's' }} remaining.
                </p>

                <template v-else>
                    <p>Protect your account with an authenticator app.</p>
                    <form v-if="!twoFactorSetup" @submit.prevent="loadTwoFactorSetup">
                        <button type="submit" :disabled="twoFactorLoading">
                            {{ twoFactorLoading ? 'Preparing...' : 'Set up authenticator' }}
                        </button>
                    </form>

                    <div v-if="twoFactorSetup && !twoFactorSetup.isEnabled" class="setup">
                        <p>Enter this key in your authenticator app:</p>
                        <p><code>{{ twoFactorSetup.sharedKey }}</code></p>
                        <p>
                            <small>
                                Authenticator URI:
                                <code>{{ twoFactorSetup.authenticatorUri }}</code>
                            </small>
                        </p>
                        <p>
                            <a
                                v-if="twoFactorSetup.authenticatorUri"
                                :href="twoFactorSetup.authenticatorUri"
                            >
                                Open authenticator URI
                            </a>
                        </p>
                        <form @submit.prevent="enableTwoFactor">
                            <label for="enable-code">Authenticator code</label>
                            <input
                                id="enable-code"
                                v-model="twoFactorCode"
                                type="text"
                                inputmode="numeric"
                                autocomplete="one-time-code"
                                maxlength="6"
                                required
                            >
                            <button type="submit" :disabled="twoFactorLoading">
                                {{ twoFactorLoading ? 'Enabling...' : 'Enable two-factor authentication' }}
                            </button>
                        </form>
                    </div>
                </template>

                <div v-if="recoveryCodes.length > 0" class="recovery-codes">
                    <h4>Save your recovery codes</h4>
                    <p>Each code can be used once if you lose access to your authenticator app.</p>
                    <textarea :value="recoveryCodes.join('\n')" rows="5" readonly></textarea>
                </div>

                <form v-if="profile.twoFactorEnabled" @submit.prevent="disableTwoFactor">
                    <label for="disable-code">Authenticator or recovery code</label>
                    <input
                        id="disable-code"
                        v-model="twoFactorCode"
                        type="text"
                        autocomplete="one-time-code"
                        required
                    >
                    <button type="submit" class="secondary" :disabled="twoFactorLoading">
                        {{ twoFactorLoading ? 'Disabling...' : 'Disable two-factor authentication' }}
                    </button>
                </form>
            </section>

            <section>
                <h3>Passkeys</h3>
                <p v-if="!passkeysSupported" class="message">
                    Passkeys are not supported by this browser.
                </p>
                <form v-else @submit.prevent="registerPasskey">
                    <label for="passkey-name">Passkey name</label>
                    <input
                        id="passkey-name"
                        v-model="passkeyName"
                        type="text"
                        maxlength="100"
                        placeholder="This device"
                    >
                    <button type="submit" :disabled="passkeyLoading">
                        {{ passkeyLoading ? 'Registering...' : 'Add passkey' }}
                    </button>
                </form>

                <p v-if="profile.passkeys.length === 0">No passkeys have been registered.</p>
                <ul v-else class="passkeys">
                    <li v-for="passkey in profile.passkeys" :key="passkey.id">
                        <div>
                            <strong>{{ passkey.name || 'Unnamed passkey' }}</strong>
                            <small>
                                Added {{ formatDate(passkey.createdAt) }}
                                <span v-if="passkey.isBackedUp"> - Backed up</span>
                            </small>
                        </div>
                        <button
                            type="button"
                            class="secondary"
                            :disabled="passkeyLoading"
                            @click="removePasskey(passkey.id)"
                        >
                            Remove
                        </button>
                    </li>
                </ul>
            </section>
        </template>
    </article>
</template>

<script lang="ts">
    import { defineComponent, onMounted, ref } from 'vue'
    import ArkaineService from '@/services/arkaine.service'
    import Profile, {
        TwoFactorSetup
    } from '@/models/profile'
    import { createPasskey, serializePasskeyRegistration } from '@/services/passkey'

    const maxPasskeyNameLength = 100

    export default defineComponent({
        name: 'ProfileView',
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
        max-width: 60em;
        margin: 0 auto;
    }

    section {
        margin-top: 2em;
    }

    .values {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(15em, 1fr));
        gap: 1em;
    }

    .setup,
    .recovery-codes {
        margin-top: 1em;
    }

    .message {
        padding: 0.75em;
    }

    .success {
        color: var(--app-success);
        border: 1px solid color-mix(in srgb, var(--app-success) 45%, transparent);
        background: var(--app-success-background);
    }

    .passkeys {
        padding-left: 0;
        list-style: none;
    }

    .passkeys li {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 1em;
        margin-bottom: 1em;
    }

    .passkeys small {
        display: block;
        opacity: 0.75;
    }
</style>
