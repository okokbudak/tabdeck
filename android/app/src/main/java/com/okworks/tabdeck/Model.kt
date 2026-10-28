package com.okworks.tabdeck

import org.json.JSONArray
import org.json.JSONObject
import java.util.UUID

/** PC ajanına gönderilen komut. type: none | hotkey | text | launch | media | obs | page */
data class DeckAction(
    val type: String = "none",
    val value: String = "",
    val arg: String = "",
    val arg2: String = "",
)

data class DeckButton(
    val id: String = UUID.randomUUID().toString(),
    val label: String = "",
    val icon: String = "",
    val color: Long = 0xFF37474F,
    val action: DeckAction = DeckAction(),
)

/** Bir sayfa, ızgaradaki her kutu için bir slot tutar (boş kutu = null). */
data class Page(
    val id: String = UUID.randomUUID().toString(),
    val name: String,
    val slots: List<DeckButton?> = emptyList(),
)

data class Settings(
    val host: String = "",
    val port: Int = 8765,
    val token: String = "",
    val cols: Int = 5,
    val rows: Int = 3,
    val twitchUser: String = "",
    val kickUser: String = "",
    val youtubeHandle: String = "",
)

val ButtonColors = listOf(
    0xFF37474F, 0xFF5E35B1, 0xFF3949AB, 0xFF1E88E5, 0xFF00897B, 0xFF43A047,
    0xFFF9A825, 0xFFEF6C00, 0xFFE53935, 0xFFD81B60, 0xFF6D4C41, 0xFF212121,
)

val QuickIcons = listOf(
    "▶️", "⏯️", "⏭️", "⏮️", "⏹️", "🔇", "🔊", "🔉", "🎙️", "🎧", "🎵", "🎬",
    "🔴", "📡", "🎥", "📷", "🖥️", "💻", "⌨️", "🖱️", "🌐", "📁", "📝", "💬",
    "🎮", "🕹️", "⚙️", "🔒", "💡", "⭐", "🚀", "🔥", "✅", "❌", "📋", "✂️",
)

// ---------- JSON ----------

fun DeckButton.toJson(): JSONObject = JSONObject()
    .put("id", id).put("label", label).put("icon", icon).put("color", color)
    .put("action", action.toJson())

fun DeckAction.toJson(): JSONObject = JSONObject()
    .put("type", type).put("value", value).put("arg", arg).put("arg2", arg2)

fun buttonFromJson(o: JSONObject): DeckButton {
    val a = o.optJSONObject("action") ?: JSONObject()
    return DeckButton(
        id = o.optString("id", UUID.randomUUID().toString()),
        label = o.optString("label"),
        icon = o.optString("icon"),
        color = o.optLong("color", 0xFF37474F),
        action = DeckAction(a.optString("type", "none"), a.optString("value"), a.optString("arg"), a.optString("arg2")),
    )
}

fun pagesToJson(pages: List<Page>): String {
    val arr = JSONArray()
    for (p in pages) {
        val slots = JSONArray()
        p.slots.forEach { slots.put(it?.toJson() ?: JSONObject.NULL) }
        arr.put(JSONObject().put("id", p.id).put("name", p.name).put("slots", slots))
    }
    return arr.toString()
}

fun pagesFromJson(s: String): List<Page> {
    val arr = JSONArray(s)
    return (0 until arr.length()).map { i ->
        val o = arr.getJSONObject(i)
        val slots = o.optJSONArray("slots") ?: JSONArray()
        Page(
            id = o.optString("id"),
            name = o.optString("name", "Sayfa"),
            slots = (0 until slots.length()).map { j -> slots.optJSONObject(j)?.let(::buttonFromJson) },
        )
    }
}

fun settingsToJson(s: Settings): String = JSONObject()
    .put("host", s.host).put("port", s.port).put("token", s.token)
    .put("cols", s.cols).put("rows", s.rows)
    .put("twitchUser", s.twitchUser).put("kickUser", s.kickUser).put("youtubeHandle", s.youtubeHandle)
    .toString()

fun settingsFromJson(str: String): Settings {
    val o = JSONObject(str)
    return Settings(
        host = o.optString("host"),
        port = o.optInt("port", 8765),
        token = o.optString("token"),
        twitchUser = o.optString("twitchUser"),
        kickUser = o.optString("kickUser"),
        youtubeHandle = o.optString("youtubeHandle"),
        cols = o.optInt("cols", 5),
        rows = o.optInt("rows", 3),
    )
}

/** İlk açılışta gelen örnek sayfa. */
fun defaultPages(): List<Page> {
    fun b(label: String, icon: String, color: Long, type: String, value: String, arg: String = "") =
        DeckButton(label = label, icon = icon, color = color, action = DeckAction(type, value, arg))
    val main = listOf(
        b("Önceki", "⏮️", 0xFF3949AB, "media", "prev"),
        b("Oynat/Duraklat", "⏯️", 0xFF3949AB, "media", "playpause"),
        b("Sonraki", "⏭️", 0xFF3949AB, "media", "next"),
        b("Ses -", "🔉", 0xFF00897B, "media", "voldown"),
        b("Ses +", "🔊", 0xFF00897B, "media", "volup"),
        b("Sessiz", "🔇", 0xFF00897B, "media", "mute"),
        b("Masaüstü", "🖥️", 0xFF37474F, "hotkey", "win+d"),
        b("Görev Yön.", "📋", 0xFF37474F, "hotkey", "ctrl+shift+esc"),
        b("Not Defteri", "📝", 0xFF6D4C41, "launch", "notepad.exe"),
        b("Google", "🌐", 0xFF1E88E5, "launch", "https://www.google.com"),
        b("Kayıt", "🔴", 0xFFE53935, "obs", "record"),
        b("Yayın", "📡", 0xFFD81B60, "obs", "stream"),
    )
    return listOf(Page(name = "Ana Sayfa", slots = main))
}
