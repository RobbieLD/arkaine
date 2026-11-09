package dev.robdavis.arkaine

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.BackHandler
import androidx.activity.compose.setContent
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.itemsIndexed
import androidx.compose.foundation.lazy.grid.rememberLazyGridState
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.fragment.app.FragmentActivity
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.media3.common.MediaItem
import androidx.media3.datasource.okhttp.OkHttpDataSource
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.exoplayer.source.DefaultMediaSourceFactory
import androidx.media3.ui.PlayerView
import coil3.compose.AsyncImage
import dev.robdavis.arkaine.data.ArkaineFile
import dev.robdavis.arkaine.data.UnauthorizedException
import kotlinx.coroutines.launch

class MainActivity : FragmentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent { MaterialTheme(colorScheme = darkColorScheme()) { Surface(Modifier.fillMaxSize()) { Root() } } }
    }
}

private enum class Screen { Loading, Login, Locked, Browser }

@Composable
fun Root() {
    val app = LocalContext.current.arkaine
    val activity = LocalContext.current as FragmentActivity
    val scope = rememberCoroutineScope()
    var screen by remember { mutableStateOf(Screen.Loading) }

    fun lockIfEnabled(): Screen =
        if (app.cookieJar.biometricLock && activity.canAuthenticate()) Screen.Locked else Screen.Browser

    LaunchedEffect(Unit) {
        screen = if (app.cookieJar.baseUrl.isNotBlank() && runCatching { app.api.isLoggedIn() }.getOrDefault(false))
            lockIfEnabled() else Screen.Login
    }

    // Re-lock whenever the app returns from the background.
    DisposableEffect(activity) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_STOP && screen == Screen.Browser) screen = lockIfEnabled()
        }
        activity.lifecycle.addObserver(observer)
        onDispose { activity.lifecycle.removeObserver(observer) }
    }

    when (screen) {
        Screen.Loading -> Box(Modifier.fillMaxSize(), Alignment.Center) { CircularProgressIndicator() }
        Screen.Login -> LoginScreen { screen = Screen.Browser }
        Screen.Locked -> LockScreen(
            onUnlocked = { screen = Screen.Browser },
            onSignOut = {
                scope.launch {
                    app.api.logout()
                    app.cookieJar.clear()
                    app.clearImageCache()
                    screen = Screen.Login
                }
            }
        )
        Screen.Browser -> BrowserScreen(onSignedOut = { screen = Screen.Login }, onLogout = {
            scope.launch {
                app.api.logout()
                app.cookieJar.clear()
                app.clearImageCache()
                screen = Screen.Login
            }
        })
    }
}

@Composable
fun LockScreen(onUnlocked: () -> Unit, onSignOut: () -> Unit) {
    val activity = LocalContext.current as FragmentActivity
    var message by remember { mutableStateOf<String?>(null) }
    val unlock = { activity.promptUnlock(onUnlocked) { message = it } }

    LaunchedEffect(Unit) { unlock() }

    Column(Modifier.fillMaxSize().padding(24.dp), Arrangement.Center, Alignment.CenterHorizontally) {
        Text("Arkaine is locked", style = MaterialTheme.typography.headlineSmall)
        message?.let { Text(it, color = MaterialTheme.colorScheme.error, modifier = Modifier.padding(top = 8.dp)) }
        Spacer(Modifier.height(16.dp))
        Button({ unlock() }) { Text("Unlock") }
        TextButton(onSignOut) { Text("Sign out") }
    }
}

@Composable
fun LoginScreen(onDone: () -> Unit) {
    val app = LocalContext.current.arkaine
    val scope = rememberCoroutineScope()
    var server by remember { mutableStateOf(app.cookieJar.baseUrl) }
    var user by remember { mutableStateOf("") }
    var pass by remember { mutableStateOf("") }
    var code by remember { mutableStateOf("") }
    var needsCode by remember { mutableStateOf(false) }
    var busy by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }

    Column(Modifier.fillMaxSize().padding(24.dp), Arrangement.Center) {
        Text("Arkaine", style = MaterialTheme.typography.headlineLarge)
        Spacer(Modifier.height(16.dp))
        if (!needsCode) {
            OutlinedTextField(server, { server = it }, label = { Text("Server URL (https://...)") }, singleLine = true, modifier = Modifier.fillMaxWidth())
            OutlinedTextField(user, { user = it }, label = { Text("Username") }, singleLine = true, modifier = Modifier.fillMaxWidth())
            OutlinedTextField(pass, { pass = it }, label = { Text("Password") }, singleLine = true,
                visualTransformation = PasswordVisualTransformation(), modifier = Modifier.fillMaxWidth())
        } else {
            OutlinedTextField(code, { code = it }, label = { Text("Authentication code") }, singleLine = true, modifier = Modifier.fillMaxWidth())
        }
        error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        Spacer(Modifier.height(16.dp))
        Button(enabled = !busy, onClick = {
            busy = true; error = null
            scope.launch {
                try {
                    if (!needsCode) {
                        if (!server.startsWith("https://")) throw IllegalArgumentException("Server URL must start with https://")
                        app.cookieJar.baseUrl = server
                        if (app.api.login(user, pass, true)) needsCode = true else onDone()
                    } else {
                        app.api.twoFactor(code, true)
                        onDone()
                    }
                } catch (e: UnauthorizedException) {
                    error = "Invalid credentials"
                } catch (e: Exception) {
                    error = e.message ?: "Login failed"
                } finally {
                    busy = false
                }
            }
        }) { Text(if (needsCode) "Verify" else "Sign in") }
    }
}

@Composable
fun BrowserScreen(onSignedOut: () -> Unit, onLogout: () -> Unit) {
    val app = LocalContext.current.arkaine
    val scope = rememberCoroutineScope()
    var path by remember { mutableStateOf("") }
    val items = remember { mutableStateListOf<ArkaineFile>() }
    var next by remember { mutableStateOf<String?>(null) }
    var loading by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }
    var viewing by remember { mutableStateOf<ArkaineFile?>(null) }
    val gridState = rememberLazyGridState()
    val activity = LocalContext.current as FragmentActivity
    var lockOn by remember { mutableStateOf(app.cookieJar.biometricLock) }

    fun load(reset: Boolean) {
        if (loading) return
        loading = true
        scope.launch {
            try {
                if (reset) { items.clear(); next = null }
                val res = app.api.files(path, next)
                items.addAll(res.files.map { ArkaineFile(it, app.api.base) })
                next = res.nextFileName?.takeIf { it.isNotBlank() }
                error = null
            } catch (e: UnauthorizedException) {
                app.cookieJar.clear(); onSignedOut()
            } catch (e: Exception) {
                error = e.message
            } finally {
                loading = false
            }
        }
    }

    LaunchedEffect(path) { load(true) }

    val lastVisible by remember { derivedStateOf { gridState.layoutInfo.visibleItemsInfo.lastOrNull()?.index ?: 0 } }
    LaunchedEffect(lastVisible, items.size, next) {
        if (next != null && lastVisible >= items.size - 6) load(false)
    }

    BackHandler(enabled = viewing != null || path.isNotEmpty()) {
        if (viewing != null) viewing = null
        else path = path.trimEnd('/').substringBeforeLast('/', "").let { if (it.isEmpty()) "" else "$it/" }
    }

    val current = viewing
    if (current != null) {
        Viewer(current, items.filter { it.isImage }) { viewing = it }
        return
    }

    Column(Modifier.fillMaxSize().statusBarsPadding()) {
        Row(Modifier.fillMaxWidth().padding(8.dp), verticalAlignment = Alignment.CenterVertically) {
            Text("/$path", Modifier.weight(1f), style = MaterialTheme.typography.titleMedium)
            TextButton({ path = if (path.startsWith("Favourites")) "" else "Favourites" }) { Text("Favourites") }
            TextButton({
                if (lockOn) { app.cookieJar.biometricLock = false; lockOn = false }
                else activity.promptUnlock({ app.cookieJar.biometricLock = true; lockOn = true }) { error = it }
            }, enabled = lockOn || activity.canAuthenticate()) { Text(if (lockOn) "Lock: on" else "Lock: off") }
            TextButton(onLogout) { Text("Log out") }
        }
        error?.let { Text(it, color = MaterialTheme.colorScheme.error, modifier = Modifier.padding(8.dp)) }
        LazyVerticalGrid(GridCells.Adaptive(120.dp), Modifier.fillMaxSize(), gridState,
            contentPadding = PaddingValues(4.dp)) {
            itemsIndexed(items, key = { i, f -> "$i${f.fileName}" }) { _, file ->
                Card(Modifier.padding(4.dp).aspectRatio(1f).clickable {
                    if (file.isDirectory) path = file.fileName else viewing = file
                }) {
                    Box(Modifier.fillMaxSize()) {
                        file.thumbUrl?.let {
                            AsyncImage(it, file.name, Modifier.fillMaxSize().clip(MaterialTheme.shapes.small), contentScale = ContentScale.Crop)
                        }
                        Text(file.name, Modifier.align(Alignment.BottomStart).padding(4.dp),
                            style = MaterialTheme.typography.labelSmall, maxLines = 1)
                    }
                }
            }
        }
    }
}

@Composable
fun Viewer(file: ArkaineFile, images: List<ArkaineFile>, onChange: (ArkaineFile?) -> Unit) {
    val app = LocalContext.current.arkaine
    val scope = rememberCoroutineScope()
    var favourite by remember(file.fileName) { mutableStateOf(file.favourite) }

    Box(Modifier.fillMaxSize()) {
        when {
            file.isVideo || file.isAudio -> MediaPlayer(file.streamUrl)
            file.isImage -> {
                AsyncImage(file.streamUrl, file.name, Modifier.fillMaxSize(), contentScale = ContentScale.Fit)
                val index = images.indexOfFirst { it.fileName == file.fileName }
                Row(Modifier.align(Alignment.BottomCenter).padding(16.dp)) {
                    TextButton({ images.getOrNull(index - 1)?.let(onChange) }, enabled = index > 0) { Text("Prev") }
                    TextButton({ images.getOrNull(index + 1)?.let(onChange) }, enabled = index in 0 until images.lastIndex) { Text("Next") }
                }
            }
            else -> Text("Unsupported file type", Modifier.align(Alignment.Center))
        }
        Row(Modifier.align(Alignment.TopEnd).statusBarsPadding().padding(8.dp)) {
            TextButton({
                favourite = !favourite
                scope.launch { runCatching { app.api.setFavourite(file.fileName, favourite) } }
            }) { Text(if (favourite) "Unfavourite" else "Favourite") }
            TextButton({ onChange(null) }) { Text("Close") }
        }
    }
}

@Composable
fun MediaPlayer(url: String) {
    val context = LocalContext.current
    val player = remember(url) {
        val factory = DefaultMediaSourceFactory(OkHttpDataSource.Factory(context.arkaine.client))
        ExoPlayer.Builder(context).setMediaSourceFactory(factory).build().apply {
            setMediaItem(MediaItem.fromUri(url)); prepare(); playWhenReady = true
        }
    }
    DisposableEffect(player) { onDispose { player.release() } }
    AndroidView({ PlayerView(it).apply { this.player = player } }, Modifier.fillMaxSize())
}
