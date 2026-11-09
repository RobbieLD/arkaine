# Android.Arkaine

Native Android client for the Arkaine backend (same HTTP API as `Client.Arkaine`). See `../ANDROID-APP-PLAN.md`.

## Build

Requires JDK 17, Android SDK 35 and Gradle 8.10+ (CI uses `gradle/actions/setup-gradle`; no wrapper is committed).

```
gradle assembleDebug
```

## Release (APK on GitHub)

Push a tag `android-v1.0.0`. The `Android` workflow builds a signed APK and attaches it to the release.
Required repo secrets: `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD`.
Without them (PRs/pushes) a debug APK is uploaded as a workflow artifact.

## Behaviour

- Enter the server URL (https) and sign in; password + TOTP supported. Passkeys are not yet supported.
- The session cookie is stored encrypted; thumbnails are cached on disk (500 MB, cleared on logout).
- Optional biometric / device-credential lock (toggle "Lock" in the browser header). The app re-locks each time it is backgrounded and unlocks with the prompt; it gates the saved session, it does not replace the server login.
