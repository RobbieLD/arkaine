package dev.robdavis.arkaine

import android.app.Application
import android.content.Context
import coil3.ImageLoader
import coil3.PlatformContext
import coil3.SingletonImageLoader
import coil3.disk.DiskCache
import coil3.network.okhttp.OkHttpNetworkFetcherFactory
import dev.robdavis.arkaine.data.ArkaineApi
import dev.robdavis.arkaine.data.PersistentCookieJar
import okhttp3.OkHttpClient
import okio.Path.Companion.toOkioPath

class ArkaineApp : Application(), SingletonImageLoader.Factory {
    val cookieJar by lazy { PersistentCookieJar(this) }

    // One client (and cookie jar) is shared by the API, Coil and Media3 so every request is authenticated.
    val client: OkHttpClient by lazy { OkHttpClient.Builder().cookieJar(cookieJar).build() }

    val api by lazy { ArkaineApi(client) { cookieJar.baseUrl } }

    override fun newImageLoader(context: PlatformContext): ImageLoader =
        ImageLoader.Builder(context)
            .components { add(OkHttpNetworkFetcherFactory(callFactory = { client })) }
            .diskCache {
                DiskCache.Builder()
                    .directory(cacheDir.resolve("images").toOkioPath())
                    .maxSizeBytes(500L * 1024 * 1024)
                    .build()
            }
            .build()

    fun clearImageCache() {
        val loader = SingletonImageLoader.get(this)
        loader.memoryCache?.clear()
        loader.diskCache?.clear()
    }
}

val Context.arkaine get() = applicationContext as ArkaineApp
