package com.okworks.tabdeck

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLEncoder

data class PcApp(val id: String, val name: String)

/** Ses karıştırıcısındaki bir uygulama: name = süreç adı (aksiyonda kullanılır), title = görünen ad. */
data class AudioApp(val name: String, val title: String, val level: Int, val muted: Boolean)

/** PC'deki TabDeck Agent ile HTTP üzerinden konuşur. */
class AgentClient(private val settings: () -> Settings) {

    suspend fun ping(s: Settings = settings()): Result<String> = withContext(Dispatchers.IO) {
        runCatching {
            val r = request(s, "GET", "/ping", null)
            if (!r.optBoolean("ok")) error(r.optString("error", "Bağlanılamadı"))
            r.optString("name")
        }
    }

    /** Aksiyonu çalıştırır; yanıt ses komutlarında yeni seviyeyi ("level", "muted") içerir. */
    suspend fun run(action: DeckAction): Result<JSONObject> = withContext(Dispatchers.IO) {
        runCatching {
            val s = settings()
            val a = action.copy(value = fill(action.value, s), arg = fill(action.arg, s))
            val r = request(s, "POST", "/action", a.toJson())
            if (!r.optBoolean("ok")) error(r.optString("error", "Hata"))
            r
        }
    }

    /** Windows ses karıştırıcısındaki uygulamalar. */
    suspend fun audioApps(): Result<List<AudioApp>> = withContext(Dispatchers.IO) {
        runCatching {
            val r = request(settings(), "GET", "/audio/apps", null)
            if (!r.optBoolean("ok")) error(r.optString("error", "Hata"))
            val arr = r.optJSONArray("apps")
            if (arr == null) emptyList()
            else (0 until arr.length()).map {
                val o = arr.getJSONObject(it)
                AudioApp(o.getString("name"), o.optString("title", o.getString("name")), o.optInt("level"), o.optBoolean("muted"))
            }
        }
    }

    /** Hazır butonlardaki {twitch}, {kick}, {youtube} yer tutucularını ayarlardaki kullanıcı adlarıyla doldurur. */
    private fun fill(text: String, s: Settings): String {
        var t = text
        for ((key, value, name) in listOf(
            Triple("{twitch}", s.twitchUser, "Twitch kullanıcı adını"),
            Triple("{kick}", s.kickUser, "Kick kullanıcı adını"),
            Triple("{youtube}", s.youtubeHandle, "YouTube kanal adını (@ olmadan)"),
        )) {
            if (key in t) {
                require(value.isNotBlank()) { "Ayarlar'a $name girin" }
                t = t.replace(key, value.trim().removePrefix("@"))
            }
        }
        return t
    }

    /** PC'deki "Tüm uygulamalar" listesi. */
    suspend fun pcApps(): Result<List<PcApp>> = withContext(Dispatchers.IO) {
        runCatching {
            val r = request(settings(), "GET", "/apps", null)
            if (!r.optBoolean("ok")) error(r.optString("error", "Hata"))
            val arr = r.optJSONArray("apps")
            if (arr == null) emptyList()
            else (0 until arr.length()).map { arr.getJSONObject(it).let { o -> PcApp(o.getString("id"), o.getString("name")) } }
        }
    }

    /** Uygulamanın gerçek ikonu (PNG). */
    suspend fun appIcon(id: String, size: Int = 160): Result<ByteArray> = withContext(Dispatchers.IO) {
        runCatching {
            val q = URLEncoder.encode(id, "UTF-8").replace("+", "%20")
            val (code, bytes) = requestRaw(settings(), "GET", "/appicon?id=$q&size=$size", null)
            if (code != 200) error("İkon alınamadı (HTTP $code)")
            bytes
        }
    }

    suspend fun obsScenes(): Result<List<String>> = obsList("/obs/scenes", "scenes")

    suspend fun obsInputs(): Result<List<String>> = obsList("/obs/inputs", "inputs")

    private suspend fun obsList(path: String, key: String): Result<List<String>> = withContext(Dispatchers.IO) {
        runCatching {
            val r = request(settings(), "GET", path, null)
            if (!r.optBoolean("ok")) error(r.optString("error", "Hata"))
            val arr = r.optJSONArray(key)
            if (arr == null) emptyList() else (0 until arr.length()).map { arr.getString(it) }
        }
    }

    private fun request(s: Settings, method: String, path: String, body: JSONObject?): JSONObject {
        val (code, bytes) = requestRaw(s, method, path, body)
        if (code == 401) error("Token hatalı")
        val text = bytes.toString(Charsets.UTF_8)
        return if (text.isBlank()) JSONObject().put("ok", false).put("error", "HTTP $code") else JSONObject(text)
    }

    private fun requestRaw(s: Settings, method: String, path: String, body: JSONObject?): Pair<Int, ByteArray> {
        require(s.host.isNotBlank()) { "PC adresi ayarlanmamış" }
        val conn = URL("http://${s.host.trim()}:${s.port}$path").openConnection() as HttpURLConnection
        try {
            conn.connectTimeout = 2000
            conn.readTimeout = 6000
            conn.requestMethod = method
            conn.setRequestProperty("X-Token", s.token.trim())
            if (body != null) {
                val bytes = body.toString().toByteArray(Charsets.UTF_8)
                conn.doOutput = true
                conn.setRequestProperty("Content-Type", "application/json; charset=utf-8")
                conn.setFixedLengthStreamingMode(bytes.size)
                conn.outputStream.use { it.write(bytes) }
            }
            val code = conn.responseCode
            val stream = if (code in 200..299) conn.inputStream else conn.errorStream
            return code to (stream?.use { it.readBytes() } ?: ByteArray(0))
        } finally {
            conn.disconnect()
        }
    }
}
