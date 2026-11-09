package dev.robdavis.arkaine.data

import android.net.Uri
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.boolean
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.io.IOException

@Serializable
data class Tag(val id: Int = 0, val name: String = "", val fileName: String = "", val timestamp: Int = 0)

@Serializable
data class B2File(
    val fileName: String = "",
    val contentType: String? = null,
    val action: String = "",
    val fileId: String = "",
    val preview: String = "",
    val previewWidth: Int? = null,
    val previewHeight: Int? = null,
    val favourite: Boolean = false,
    val tags: List<Tag> = emptyList()
)

@Serializable
data class FilesResponse(val files: List<B2File> = emptyList(), val nextFileName: String? = null)

class UnauthorizedException : IOException("Not signed in")

fun encodePath(path: String) = path.split('/').joinToString("/") { Uri.encode(it) }

class ArkaineFile(val raw: B2File, private val base: String) {
    val fileName = raw.fileName
    val name = raw.fileName.trimEnd('/').substringAfterLast('/')
    val isDirectory = raw.action == "folder"
    private val type = raw.contentType.orEmpty()
    val isImage = type.startsWith("image")
    val isVideo = type.startsWith("video")
    val isAudio = type.startsWith("audio")
    val favourite = raw.favourite
    val streamUrl = "$base/stream/${encodePath(fileName)}"
    val thumbUrl: String? = when {
        raw.preview.isNotBlank() -> "$base/preview/${encodePath(raw.preview)}"
        isDirectory -> "$streamUrl" + "thumb.jpg"
        else -> null
    }
}

class ArkaineApi(private val client: OkHttpClient, private val baseUrl: () -> String) {
    private val json = Json { ignoreUnknownKeys = true; coerceInputValues = true }
    private val jsonType = "application/json".toMediaType()

    val base get() = baseUrl()

    private fun url(path: String) = base + path

    private fun body(vararg pairs: Pair<String, JsonElement>) =
        JsonObject(mapOf(*pairs)).toString().toRequestBody(jsonType)

    private suspend fun exec(request: Request): String = withContext(Dispatchers.IO) {
        client.newCall(request).execute().use { response ->
            if (response.code == 401) throw UnauthorizedException()
            if (!response.isSuccessful) throw IOException("Server returned ${response.code}")
            response.body?.string().orEmpty()
        }
    }

    /** Returns true when a two-factor code is still required. */
    suspend fun login(username: String, password: String, remember: Boolean): Boolean {
        val text = exec(
            Request.Builder().url(url("/login")).post(
                body(
                    "username" to JsonPrimitive(username),
                    "password" to JsonPrimitive(password),
                    "remember" to JsonPrimitive(remember)
                )
            ).build()
        )
        return text.trim().equals("true", ignoreCase = true)
    }

    suspend fun twoFactor(code: String, remember: Boolean) {
        exec(
            Request.Builder().url(url("/twofactorauth")).post(
                body("code" to JsonPrimitive(code), "remember" to JsonPrimitive(remember))
            ).build()
        )
    }

    suspend fun isLoggedIn(): Boolean = try {
        exec(Request.Builder().url(url("/loggedin")).get().build())
        true
    } catch (_: UnauthorizedException) {
        false
    }

    suspend fun logout() {
        runCatching { exec(Request.Builder().url(url("/logout")).get().build()) }
    }

    suspend fun files(prefix: String, start: String?): FilesResponse {
        val text = exec(
            Request.Builder().url(url("/files")).post(
                body(
                    "prefix" to JsonPrimitive(prefix),
                    "delimiter" to JsonPrimitive("/"),
                    "startFileName" to (start?.let { JsonPrimitive(it) } ?: JsonNull)
                )
            ).build()
        )
        val response = json.decodeFromString<FilesResponse>(text)
        return response.copy(
            files = response.files.filter { !it.fileName.endsWith("thumb.jpg") && !it.fileName.endsWith(".bzEmpty") }
        )
    }

    suspend fun setFavourite(fileName: String, favourite: Boolean) {
        val payload = body("fileName" to JsonPrimitive(fileName))
        val builder = Request.Builder().url(url("/favourite"))
        exec((if (favourite) builder.put(payload) else builder.delete(payload)).build())
    }
}
