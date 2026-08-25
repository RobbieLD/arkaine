import {
    PasskeyAssertionPayload,
    PasskeyCreationOptions,
    PasskeyCredentialPayload,
    PasskeyRequestOptions
} from '@/models/profile'

const supportedAuthenticatorTransports: AuthenticatorTransport[] = [
    'ble',
    'hybrid',
    'internal',
    'nfc',
    'usb'
]

export const decodeBase64Url = (value: string): ArrayBuffer => {
    const base64 = value.replace(/-/g, '+').replace(/_/g, '/')
    const padding = (4 - (base64.length % 4)) % 4
    const binary = atob(base64 + '='.repeat(padding))
    const bytes = new Uint8Array(new ArrayBuffer(binary.length))

    for (let index = 0; index < binary.length; index++) {
        bytes[index] = binary.charCodeAt(index)
    }

    return bytes.buffer
}

export const encodeBase64Url = (value: ArrayBuffer): string => {
    const bytes = new Uint8Array(value)
    let binary = ''
    for (const byte of bytes) {
        binary += String.fromCharCode(byte)
    }

    return btoa(binary)
        .replace(/\+/g, '-')
        .replace(/\//g, '_')
        .replace(/=+$/, '')
}

export const getAuthenticatorTransports = (
    transports?: string[]
): AuthenticatorTransport[] | undefined => {
    if (!transports) {
        return undefined
    }

    return transports.filter((transport): transport is AuthenticatorTransport =>
        supportedAuthenticatorTransports.includes(transport as AuthenticatorTransport))
}

export const createPasskey = async (
    options: PasskeyCreationOptions
): Promise<PublicKeyCredential> => {
    const selection = options.authenticatorSelection
    const publicKey: PublicKeyCredentialCreationOptions = {
        challenge: decodeBase64Url(options.challenge),
        rp: options.rp,
        user: {
            id: decodeBase64Url(options.user.id),
            name: options.user.name,
            displayName: options.user.displayName
        },
        pubKeyCredParams: options.pubKeyCredParams,
        ...(options.timeout === undefined ? {} : { timeout: options.timeout }),
        ...(options.excludeCredentials === undefined || options.excludeCredentials === null
            ? {}
            : {
                excludeCredentials: options.excludeCredentials.map(credential => ({
                    type: credential.type,
                    id: decodeBase64Url(credential.id),
                    transports: getAuthenticatorTransports(credential.transports)
                }))
            }),
        ...(selection === undefined || selection === null
            ? {}
            : {
                authenticatorSelection: {
                    ...(selection.authenticatorAttachment
                        ? { authenticatorAttachment: selection.authenticatorAttachment }
                        : {}),
                    ...(selection.requireResidentKey === undefined
                        ? {}
                        : { requireResidentKey: selection.requireResidentKey }),
                    ...(selection.residentKey
                        ? { residentKey: selection.residentKey }
                        : {}),
                    ...(selection.userVerification
                        ? { userVerification: selection.userVerification }
                        : {})
                }
            }),
        ...(options.attestation ? { attestation: options.attestation } : {})
    }

    const credential = await navigator.credentials.create({ publicKey })
    if (!(credential instanceof PublicKeyCredential)) {
        throw new Error('No passkey was created.')
    }

    return credential
}

export const getPasskey = async (
    options: PasskeyRequestOptions
): Promise<PublicKeyCredential> => {
    const publicKey: PublicKeyCredentialRequestOptions = {
        challenge: decodeBase64Url(options.challenge),
        ...(options.rpId ? { rpId: options.rpId } : {}),
        ...(options.timeout === undefined ? {} : { timeout: options.timeout }),
        ...(options.allowCredentials === undefined || options.allowCredentials === null
            ? {}
            : {
                allowCredentials: options.allowCredentials.map(credential => ({
                    type: credential.type,
                    id: decodeBase64Url(credential.id),
                    transports: getAuthenticatorTransports(credential.transports)
                }))
            }),
        ...(options.userVerification ? { userVerification: options.userVerification } : {})
    }

    const credential = await navigator.credentials.get({ publicKey })
    if (!(credential instanceof PublicKeyCredential)) {
        throw new Error('No passkey was provided.')
    }

    return credential
}

export const serializePasskeyRegistration = (
    credential: PublicKeyCredential
): PasskeyCredentialPayload => {
    if (!(credential.response instanceof AuthenticatorAttestationResponse)) {
        throw new Error('The browser returned an invalid passkey response.')
    }

    return {
        id: credential.id,
        rawId: encodeBase64Url(credential.rawId),
        type: credential.type,
        authenticatorAttachment: credential.authenticatorAttachment,
        clientExtensionResults: credential.getClientExtensionResults(),
        response: {
            clientDataJSON: encodeBase64Url(credential.response.clientDataJSON),
            attestationObject: encodeBase64Url(credential.response.attestationObject),
            transports: credential.response.getTransports()
        }
    }
}

export const serializePasskeyAssertion = (
    credential: PublicKeyCredential
): PasskeyAssertionPayload => {
    if (!(credential.response instanceof AuthenticatorAssertionResponse)) {
        throw new Error('The browser returned an invalid passkey response.')
    }

    return {
        id: credential.id,
        rawId: encodeBase64Url(credential.rawId),
        type: credential.type,
        authenticatorAttachment: credential.authenticatorAttachment,
        clientExtensionResults: credential.getClientExtensionResults(),
        response: {
            authenticatorData: encodeBase64Url(credential.response.authenticatorData),
            clientDataJSON: encodeBase64Url(credential.response.clientDataJSON),
            signature: encodeBase64Url(credential.response.signature),
            ...(credential.response.userHandle
                ? { userHandle: encodeBase64Url(credential.response.userHandle) }
                : {})
        }
    }
}
