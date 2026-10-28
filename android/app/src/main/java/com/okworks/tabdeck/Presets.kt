package com.okworks.tabdeck

/** Tek dokunuşla eklenebilen hazır buton paketi (editörde ve "Yeni sayfa"da kullanılır). */
data class PresetPack(
    val id: String,
    val name: String,
    val icon: String,
    val color: Long,
    val note: String,
    val buttons: List<DeckButton>,
)

private const val TWITCH = 0xFF9146FF
private const val KICK = 0xFF53FC18
private const val YOUTUBE = 0xFFFF0000
private const val DISCORD = 0xFF5865F2
private const val OBS = 0xFF302E31
private const val OBS_RED = 0xFFC62828

private fun btn(label: String, icon: String, color: Long, type: String, value: String, arg: String = "", arg2: String = "") =
    DeckButton(label = label, icon = icon, color = color, action = DeckAction(type, value, arg, arg2))

/** Her çağrıda yeni buton kimlikleriyle üretilir. */
fun presetPacks(): List<PresetPack> = listOf(
    PresetPack(
        id = "obs", name = "OBS", icon = "si:obsstudio", color = OBS,
        note = "OBS → Araçlar → WebSocket Sunucu Ayarları'nda sunucuyu etkinleştirin. Kaynak ve sahne adlarını editördeki \"OBS'den getir\" düğmeleriyle seçebilirsiniz.",
        buttons = listOf(
            btn("Yayın", "📡", OBS_RED, "obs", "stream"),
            btn("Kayıt", "🔴", OBS_RED, "obs", "record"),
            btn("Kaydı duraklat", "⏸️", OBS, "obs", "pauseRecord"),
            btn("Replay kaydet", "💾", OBS, "obs", "replay"),
            btn("Replay buffer", "🔁", OBS, "obs", "replayToggle"),
            btn("Sanal kamera", "📷", OBS, "obs", "virtualcam"),
            btn("Stüdyo modu", "🎛️", OBS, "obs", "studio"),
            btn("Geçiş yap", "➡️", OBS, "obs", "transition"),
            btn("Mikrofon", "🎙️", OBS, "obs", "mute", "Mic/Aux"),
            btn("Masaüstü sesi", "🔈", OBS, "obs", "mute", "Desktop Audio"),
            btn("Mikrofon +3 dB", "🔊", OBS, "obs", "volume", "Mic/Aux", "+3"),
            btn("Mikrofon −3 dB", "🔉", OBS, "obs", "volume", "Mic/Aux", "-3"),
            btn("Bölüm işareti", "🔖", OBS, "obs", "chapter"),
            btn("Ekran görüntüsü", "📸", OBS, "obs", "screenshot"),
            btn("OBS'i aç", "si:obsstudio", OBS, "launch", "C:\\Program Files\\obs-studio\\bin\\64bit\\obs64.exe"),
            btn("Sahne 1", "🎬", 0xFF3949AB, "obs", "scene", "Sahne"),
            btn("Sahne 2", "🎬", 0xFF3949AB, "obs", "scene", "Sahne 2"),
            btn("Kamera göster/gizle", "👁️", OBS, "obs", "source", "", "Kamera"),
            btn("Kaydı böl", "✂️", OBS, "obs", "split"),
            btn("Yayını başlat", "▶️", OBS_RED, "obs", "streamStart"),
            btn("Yayını bitir", "⏹️", OBS_RED, "obs", "streamStop"),
        ),
    ),
    PresetPack(
        id = "discord", name = "Discord", icon = "si:discord", color = DISCORD,
        note = "Discord → Ayarlar → Tuş Atamaları'nda ilgili eylemi ekleyin, kısayol kutusuna tıklayıp TabDeck'teki butona basın (F13–F18 kaydedilir). Bu atamalar Discord arka plandayken de çalışır.",
        buttons = listOf(
            btn("Discord", "si:discord", DISCORD, "launch", "discord://"),
            btn("Mikrofon", "🎙️", DISCORD, "hotkey", "f13"),
            btn("Sağırlaştır", "🎧", DISCORD, "hotkey", "f14"),
            btn("Yayıncı modu", "🕶️", DISCORD, "hotkey", "f15"),
            btn("Overlay", "🪟", DISCORD, "hotkey", "f16"),
            btn("Ses etkinliği modu", "🗣️", DISCORD, "hotkey", "f17"),
            btn("Overlay kilidi", "🔒", DISCORD, "hotkey", "f18"),
            btn("Hızlı geçiş", "🔎", DISCORD, "hotkey", "ctrl+k"),
            btn("Okunmamış kanal", "🔔", DISCORD, "hotkey", "alt+shift+down"),
            btn("Son mesajı düzenle", "✏️", DISCORD, "hotkey", "up"),
        ),
    ),
    PresetPack(
        id = "twitch", name = "Twitch", icon = "si:twitch", color = TWITCH,
        note = "Ayarlar'a Twitch kullanıcı adınızı girin. Oynatıcı kısayolları tarayıcıda Twitch sekmesi açıkken çalışır.",
        buttons = listOf(
            btn("Kanalım", "si:twitch", TWITCH, "launch", "https://www.twitch.tv/{twitch}"),
            btn("Yayın Yöneticisi", "🎛️", TWITCH, "launch", "https://dashboard.twitch.tv/u/{twitch}/stream-manager"),
            btn("Sohbet", "💬", TWITCH, "launch", "https://www.twitch.tv/popout/{twitch}/chat?popout="),
            btn("Mod görünümü", "🛡️", TWITCH, "launch", "https://www.twitch.tv/moderator/{twitch}"),
            btn("Klipler", "✂️", TWITCH, "launch", "https://dashboard.twitch.tv/u/{twitch}/content/clips"),
            btn("Kontrol paneli", "📊", TWITCH, "launch", "https://dashboard.twitch.tv/u/{twitch}/home"),
            btn("Takip edilenler", "❤️", TWITCH, "launch", "https://www.twitch.tv/directory/following"),
            btn("Sohbete yaz", "💬", 0xFF6441A5, "text", "Merhaba chat! 👋", "enter"),
            btn("Tam ekran", "⛶", 0xFF6441A5, "hotkey", "f"),
            btn("Tiyatro modu", "🎞️", 0xFF6441A5, "hotkey", "alt+t"),
            btn("Sessiz", "🔇", 0xFF6441A5, "hotkey", "m"),
        ),
    ),
    PresetPack(
        id = "kick", name = "Kick", icon = "si:kick", color = KICK,
        note = "Ayarlar'a Kick kullanıcı adınızı girin.",
        buttons = listOf(
            btn("Kanalım", "si:kick", KICK, "launch", "https://kick.com/{kick}"),
            btn("Sohbet", "💬", KICK, "launch", "https://kick.com/popout/{kick}/chat"),
            btn("Kontrol paneli", "🎛️", KICK, "launch", "https://kick.com/dashboard"),
            btn("Takip edilenler", "❤️", KICK, "launch", "https://kick.com/following"),
            btn("Kick", "si:kick", 0xFF0B0E0F, "launch", "https://kick.com"),
            btn("Sohbete yaz", "💬", 0xFF0B0E0F, "text", "Merhaba chat! 👋", "enter"),
        ),
    ),
    PresetPack(
        id = "youtube", name = "YouTube", icon = "si:youtube", color = YOUTUBE,
        note = "Ayarlar'a YouTube kanal adınızı (@ olmadan) girin. Oynatıcı kısayolları tarayıcıda YouTube sekmesi açıkken çalışır.",
        buttons = listOf(
            btn("YouTube", "si:youtube", YOUTUBE, "launch", "https://www.youtube.com"),
            btn("Kanalım", "si:youtube", 0xFF282828, "launch", "https://www.youtube.com/@{youtube}"),
            btn("Studio", "si:youtubestudio", YOUTUBE, "launch", "https://studio.youtube.com"),
            btn("Canlı yayın odası", "🔴", YOUTUBE, "launch", "https://www.youtube.com/live_dashboard"),
            btn("Video yükle", "⬆️", YOUTUBE, "launch", "https://www.youtube.com/upload"),
            btn("Abonelikler", "📺", 0xFF282828, "launch", "https://www.youtube.com/feed/subscriptions"),
            btn("YouTube Music", "si:youtubemusic", YOUTUBE, "launch", "https://music.youtube.com"),
            btn("Sohbete yaz", "💬", 0xFF282828, "text", "Merhaba chat! 👋", "enter"),
            btn("Oynat/Duraklat", "⏯️", 0xFF282828, "hotkey", "k"),
            btn("10 sn geri", "⏪", 0xFF282828, "hotkey", "j"),
            btn("10 sn ileri", "⏩", 0xFF282828, "hotkey", "l"),
            btn("Sonraki video", "⏭️", 0xFF282828, "hotkey", "shift+n"),
            btn("Tam ekran", "⛶", 0xFF282828, "hotkey", "f"),
            btn("Altyazı", "💬", 0xFF282828, "hotkey", "c"),
            btn("Sessiz", "🔇", 0xFF282828, "hotkey", "m"),
            btn("Tiyatro modu", "🎞️", 0xFF282828, "hotkey", "t"),
        ),
    ),
)
