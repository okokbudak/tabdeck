package com.okworks.tabdeck

import android.content.Context
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.graphics.vector.addPathNodes
import androidx.compose.ui.unit.dp
import org.json.JSONArray

/** Simple Icons (CC0) marka logosu. Butonda "si:<slug>" olarak saklanır. */
data class BrandIcon(val slug: String, val title: String, val hex: String, val path: String) {
    val color: Long get() = ("FF$hex").toLong(16)
}

object BrandIcons {
    const val PREFIX = "si:"

    var all: List<BrandIcon> = emptyList()
        private set
    private var bySlug: Map<String, BrandIcon> = emptyMap()
    private val vectors = HashMap<String, ImageVector>()

    /** assets/brand_icons.json dosyasını bir kez yükler. */
    fun load(context: Context) {
        if (all.isNotEmpty()) return
        val text = context.assets.open("brand_icons.json").bufferedReader().use { it.readText() }
        val arr = JSONArray(text)
        all = (0 until arr.length()).map { i ->
            val o = arr.getJSONObject(i)
            BrandIcon(o.getString("slug"), o.getString("title"), o.getString("hex"), o.getString("path"))
        }
        bySlug = all.associateBy { it.slug }
    }

    fun isBrand(icon: String) = icon.startsWith(PREFIX)

    fun find(icon: String): BrandIcon? = bySlug[icon.removePrefix(PREFIX)]

    fun vector(icon: BrandIcon): ImageVector = vectors.getOrPut(icon.slug) {
        ImageVector.Builder(
            name = icon.slug,
            defaultWidth = 24.dp,
            defaultHeight = 24.dp,
            viewportWidth = 24f,
            viewportHeight = 24f,
        ).addPath(pathData = addPathNodes(icon.path), fill = SolidColor(Color.Black)).build()
    }
}

/** Arka plan rengine göre okunaklı logo rengi (beyaz ya da siyah). */
fun contentColorFor(background: Color): Color =
    if (background.luminance() > 0.55f) Color(0xFF111111) else Color.White
