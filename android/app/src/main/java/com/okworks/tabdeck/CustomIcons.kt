package com.okworks.tabdeck

import android.content.Context
import android.graphics.BitmapFactory
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.size
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.Dp
import kotlinx.coroutines.sync.Semaphore
import kotlinx.coroutines.sync.withPermit
import java.io.File
import java.security.MessageDigest

/** PC'den alınan gerçek uygulama ikonları. Butonda "img:<sha1>" olarak saklanır, PNG tablette durur. */
object CustomIcons {
    const val PREFIX = "img:"

    private lateinit var dir: File
    private val bitmaps = HashMap<String, ImageBitmap>()
    private val downloaded = HashMap<String, ByteArray>()
    private val limit = Semaphore(4)

    fun init(context: Context) {
        dir = File(context.filesDir, "icons").apply { mkdirs() }
    }

    fun isCustom(icon: String) = icon.startsWith(PREFIX)

    /** PNG'yi kalıcı olarak kaydeder ve butonda kullanılacak ikon değerini döndürür. */
    fun save(png: ByteArray): String {
        val name = MessageDigest.getInstance("SHA-1").digest(png).joinToString("") { "%02x".format(it) }
        val file = File(dir, "$name.png")
        if (!file.exists()) file.writeBytes(png)
        return PREFIX + name
    }

    fun bitmap(icon: String): ImageBitmap? {
        bitmaps[icon]?.let { return it }
        val file = File(dir, icon.removePrefix(PREFIX) + ".png")
        val bmp = BitmapFactory.decodeFile(file.path)?.asImageBitmap() ?: return null
        bitmaps[icon] = bmp
        return bmp
    }

    /** Seçicideki ikonları PC'den en fazla 4 paralel istekle indirir ve bellekte tutar. */
    suspend fun fetch(client: AgentClient, appId: String): ByteArray? {
        synchronized(downloaded) { downloaded[appId]?.let { return it } }
        val bytes = limit.withPermit { client.appIcon(appId).getOrNull() } ?: return null
        synchronized(downloaded) { downloaded[appId] = bytes }
        return bytes
    }

    fun decode(png: ByteArray): ImageBitmap? = BitmapFactory.decodeByteArray(png, 0, png.size)?.asImageBitmap()
}

/** Buton ikonunu çizer: PC ikonu (renkli), marka logosu (tint ile) ya da emoji. */
@Composable
fun GlyphIcon(icon: String, tint: Color, iconSize: Dp) {
    val custom = if (CustomIcons.isCustom(icon)) CustomIcons.bitmap(icon) else null
    val brand = if (BrandIcons.isBrand(icon)) BrandIcons.find(icon) else null
    when {
        custom != null -> Image(custom, contentDescription = null, modifier = Modifier.size(iconSize * 1.15f))
        brand != null -> Icon(BrandIcons.vector(brand), brand.title, tint = tint, modifier = Modifier.size(iconSize))
        icon.isNotBlank() && !CustomIcons.isCustom(icon) && !BrandIcons.isBrand(icon) ->
            Text(icon, fontSize = with(LocalDensity.current) { iconSize.toSp() })
    }
}
