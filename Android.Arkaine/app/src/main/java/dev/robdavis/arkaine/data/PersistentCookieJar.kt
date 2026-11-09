package dev.robdavis.arkaine.data

import android.content.Context
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey
import okhttp3.Cookie
import okhttp3.CookieJar
import okhttp3.HttpUrl
import okhttp3.HttpUrl.Companion.toHttpUrl

/** Stores the Arkaine session cookie (and server URL) in Keystore-backed encrypted preferences. */
class PersistentCookieJar(context: Context) : CookieJar {
    private val prefs = EncryptedSharedPreferences.create(
        context,
        "arkaine_session",
        MasterKey.Builder(context).setKeyScheme(MasterKey.KeyScheme.AES256_GCM).build(),
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
    )

    private val cookies = mutableMapOf<String, Cookie>()

    var baseUrl: String
        get() = prefs.getString(BASE_URL, "") ?: ""
        set(value) = prefs.edit().putString(BASE_URL, value.trim().trimEnd('/')).apply()

    var biometricLock: Boolean
        get() = prefs.getBoolean(BIOMETRIC_LOCK, false)
        set(value) = prefs.edit().putBoolean(BIOMETRIC_LOCK, value).apply()

    init {
        for ((key, value) in prefs.all) {
            if (!key.contains('|') || value !is String) continue
            val (url, text) = value.split('\n', limit = 2).let { it[0] to it.getOrElse(1) { "" } }
            Cookie.parse(url.toHttpUrl(), text)?.let { cookies[key] = it }
        }
    }

    @Synchronized
    override fun saveFromResponse(url: HttpUrl, cookies: List<Cookie>) {
        val editor = prefs.edit()
        for (cookie in cookies) {
            val key = "${url.host}|${cookie.name}|${cookie.path}"
            if (cookie.expiresAt < System.currentTimeMillis()) {
                this.cookies.remove(key)
                editor.remove(key)
            } else {
                this.cookies[key] = cookie
                editor.putString(key, "$url\n$cookie")
            }
        }
        editor.apply()
    }

    @Synchronized
    override fun loadForRequest(url: HttpUrl): List<Cookie> =
        cookies.values.filter { it.expiresAt >= System.currentTimeMillis() && it.matches(url) }

    @Synchronized
    fun clear() {
        val editor = prefs.edit()
        cookies.keys.forEach { editor.remove(it) }
        editor.apply()
        cookies.clear()
    }

    private companion object {
        const val BASE_URL = "baseUrl"
        const val BIOMETRIC_LOCK = "biometricLock"
    }
}
