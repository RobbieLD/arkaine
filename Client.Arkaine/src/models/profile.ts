export default interface Profile {
    userName: string
    email: string | null
    twoFactorEnabled: boolean
    recoveryCodesLeft: number
    passkeys: Passkey[]
}

export interface Passkey {
    id: string
    name: string | null
    createdAt: string
    isUserVerified: boolean
    isBackedUp: boolean
    isBackupEligible: boolean
}

export interface TwoFactorSetup {
    isEnabled: boolean
    sharedKey: string | null
    authenticatorUri: string | null
}

export interface TwoFactorEnableResponse {
    recoveryCodes: string[]
}

export interface PasskeyCreationOptions {
    challenge: string
    rp: {
        name: string
        id?: string
    }
    user: {
        id: string
        name: string
        displayName: string
    }
    pubKeyCredParams: Array<{
        type: PublicKeyCredentialType
        alg: number
    }>
    timeout?: number
    excludeCredentials?: Array<{
        type: PublicKeyCredentialType
        id: string
        transports?: string[]
    }>
    authenticatorSelection?: AuthenticatorSelectionCriteria
    attestation?: AttestationConveyancePreference
}

export interface PasskeyRequestOptions {
    challenge: string
    rpId?: string
    timeout?: number
    allowCredentials?: Array<{
        type: PublicKeyCredentialType
        id: string
        transports?: string[]
    }>
    userVerification?: UserVerificationRequirement
}

export interface PasskeyCredentialPayload {
    id: string
    rawId: string
    type: string
    authenticatorAttachment?: string | null
    clientExtensionResults: object
    response: {
        clientDataJSON: string
        attestationObject: string
        transports?: string[]
    }
}

export interface PasskeyAssertionPayload {
    id: string
    rawId: string
    type: string
    authenticatorAttachment?: string | null
    clientExtensionResults?: object
    response: {
        authenticatorData: string
        clientDataJSON: string
        signature: string
        userHandle?: string
    }
}
