package com.okworks.tabdeck

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import kotlinx.coroutines.launch

private val ActionTypes = listOf(
    "none" to "Yok",
    "hotkey" to "Kısayol",
    "text" to "Metin yaz",
    "launch" to "Aç (uygulama/URL)",
    "media" to "Medya / Ses",
    "obs" to "OBS",
    "page" to "Sayfaya git",
)

/** value -> (etiket, varsayılan ikon) */
private val MediaOptions = listOf(
    "playpause" to ("Oynat/Duraklat" to "⏯️"),
    "next" to ("Sonraki" to "⏭️"),
    "prev" to ("Önceki" to "⏮️"),
    "stop" to ("Durdur" to "⏹️"),
    "volup" to ("Ses +" to "🔊"),
    "voldown" to ("Ses -" to "🔉"),
    "mute" to ("Sessiz" to "🔇"),
)

/** OBS komutu: hangi alanları istediği ve OBS'den hangi listeyi önereceği. */
private data class ObsCmd(
    val value: String,
    val label: String,
    val icon: String,
    val argLabel: String? = null,
    val arg2Label: String? = null,
    val argList: String? = null, // "scenes" | "inputs"
    val arg2List: String? = null,
)

private val ObsCommands = listOf(
    ObsCmd("scene", "Sahne değiştir", "🎬", "Sahne adı", argList = "scenes"),
    ObsCmd("preview", "Önizleme sahnesi", "🖼️", "Sahne adı", argList = "scenes"),
    ObsCmd("stream", "Yayın aç/kapat", "📡"),
    ObsCmd("streamStart", "Yayını başlat", "▶️"),
    ObsCmd("streamStop", "Yayını bitir", "⏹️"),
    ObsCmd("record", "Kayıt aç/kapat", "🔴"),
    ObsCmd("recordStart", "Kaydı başlat", "⏺️"),
    ObsCmd("recordStop", "Kaydı bitir", "⏹️"),
    ObsCmd("pauseRecord", "Kaydı duraklat", "⏸️"),
    ObsCmd("chapter", "Bölüm işareti", "🔖", "Bölüm adı (isteğe bağlı)"),
    ObsCmd("split", "Kaydı böl", "✂️"),
    ObsCmd("replay", "Replay kaydet", "💾"),
    ObsCmd("replayToggle", "Replay buffer aç/kapat", "🔁"),
    ObsCmd("virtualcam", "Sanal kamera", "📷"),
    ObsCmd("studio", "Stüdyo modu", "🎛️"),
    ObsCmd("transition", "Geçiş yap (stüdyo)", "➡️"),
    ObsCmd("mute", "Ses kaynağı sustur", "🎙️", "Ses kaynağı", argList = "inputs"),
    ObsCmd("volume", "Ses seviyesi", "🔊", "Ses kaynağı", "dB değişimi (örn: +3 / -3)", argList = "inputs"),
    ObsCmd("source", "Kaynak göster/gizle", "👁️", "Sahne (boş = aktif sahne)", "Kaynak adı", argList = "scenes", arg2List = "inputs"),
    ObsCmd("filter", "Filtre aç/kapat", "🪄", "Kaynak adı", "Filtre adı", argList = "inputs"),
    ObsCmd("media", "Medya kaynağı", "🎞️", "Medya kaynağı", "playpause / restart / stop", argList = "inputs"),
    ObsCmd("screenshot", "Ekran görüntüsü", "📸"),
)

@Composable
private fun SectionTitle(text: String) {
    Text(text, color = Color(0xFFB0B3B8), fontSize = 13.sp, modifier = Modifier.padding(top = 14.dp, bottom = 6.dp))
}

@Composable
private fun ChipRow(content: @Composable () -> Unit) {
    Row(
        Modifier.horizontalScroll(rememberScrollState()),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
    ) { content() }
}

/** Marka logosu ya da emoji gösteren küçük ikon (seçici ve paket listeleri için). */
@Composable
private fun SmallIcon(icon: String, color: Long, size: Dp) {
    val bg = Color(color)
    Box(
        Modifier.size(size).clip(RoundedCornerShape(size * 0.22f)).background(bg),
        contentAlignment = Alignment.Center,
    ) {
        GlyphIcon(icon, contentColorFor(bg), size * 0.55f)
    }
}

@Composable
fun ButtonEditorDialog(
    initial: DeckButton?,
    vm: DeckViewModel,
    onDismiss: () -> Unit,
    onSave: (DeckButton) -> Unit,
    onDelete: () -> Unit,
) {
    var b by remember { mutableStateOf(initial ?: DeckButton()) }
    var obsLists by remember { mutableStateOf<Map<String, List<String>>>(emptyMap()) }
    var obsError by remember { mutableStateOf("") }
    var showPicker by remember { mutableStateOf(false) }
    var showPcApps by remember { mutableStateOf(false) }
    var openPack by remember { mutableStateOf<String?>(null) }
    val packs = remember { presetPacks() }
    val scope = rememberCoroutineScope()

    fun setAction(a: DeckAction) { b = b.copy(action = a) }

    /** Etiket/ikon boşsa seçilen komuta göre doldurur. */
    fun suggest(label: String, icon: String) {
        b = b.copy(label = b.label.ifBlank { label }, icon = b.icon.ifBlank { icon })
    }

    fun fetchObs(kind: String) {
        scope.launch {
            val r = if (kind == "scenes") vm.client.obsScenes() else vm.client.obsInputs()
            r.onSuccess {
                obsLists = obsLists + (kind to it)
                obsError = if (it.isEmpty()) "Liste boş" else ""
            }.onFailure { obsError = it.message ?: "Hata" }
        }
    }

    Dialog(onDismissRequest = onDismiss, properties = DialogProperties(usePlatformDefaultWidth = false)) {
        Surface(
            shape = RoundedCornerShape(24.dp),
            color = MaterialTheme.colorScheme.surface,
            modifier = Modifier.widthIn(max = 820.dp).fillMaxWidth(0.94f),
        ) {
            Column(Modifier.padding(24.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    DeckKey(button = b, size = 96.dp, editMode = false)
                    Spacer(Modifier.width(20.dp))
                    Column(Modifier.weight(1f)) {
                        Text(if (initial == null) "Yeni buton" else "Butonu düzenle", fontSize = 22.sp, color = Color.White)
                        Spacer(Modifier.height(8.dp))
                        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                            OutlinedTextField(
                                value = b.label,
                                onValueChange = { b = b.copy(label = it) },
                                label = { Text("Etiket") },
                                singleLine = true,
                                modifier = Modifier.weight(2f),
                            )
                            OutlinedTextField(
                                value = b.icon,
                                onValueChange = { b = b.copy(icon = it.take(40)) },
                                label = { Text("İkon (emoji)") },
                                singleLine = true,
                                modifier = Modifier.weight(1f),
                            )
                        }
                    }
                }

                Column(
                    Modifier
                        .weight(1f, fill = false)
                        .verticalScroll(rememberScrollState())
                ) {
                    SectionTitle("Hazır aksiyonlar")
                    ChipRow {
                        packs.forEach { p ->
                            FilterChip(
                                selected = openPack == p.id,
                                onClick = { openPack = if (openPack == p.id) null else p.id },
                                label = { Text(p.name) },
                                leadingIcon = { SmallIcon(p.icon, p.color, 22.dp) },
                            )
                        }
                    }
                    packs.firstOrNull { it.id == openPack }?.let { pack ->
                        Spacer(Modifier.height(8.dp))
                        ChipRow {
                            pack.buttons.forEach { preset ->
                                Column(
                                    Modifier
                                        .width(84.dp)
                                        .clip(RoundedCornerShape(10.dp))
                                        .clickable { b = preset.copy(id = b.id) }
                                        .padding(4.dp),
                                    horizontalAlignment = Alignment.CenterHorizontally,
                                ) {
                                    SmallIcon(preset.icon, preset.color, 48.dp)
                                    Text(
                                        preset.label, fontSize = 11.sp, color = Color(0xFFDDDDDD),
                                        maxLines = 2, textAlign = TextAlign.Center, overflow = TextOverflow.Ellipsis,
                                    )
                                }
                            }
                        }
                        Text(pack.note, color = Color(0xFF8A8D93), fontSize = 12.sp, modifier = Modifier.padding(top = 4.dp))
                    }

                    SectionTitle("İkon")
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Button(onClick = { showPcApps = true }) { Text("PC uygulamaları") }
                        Spacer(Modifier.width(8.dp))
                        OutlinedButton(onClick = { showPicker = true }) {
                            Text("Marka logoları (${BrandIcons.all.size})")
                        }
                        Spacer(Modifier.width(10.dp))
                        LazyRow(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                            items(QuickIcons) { e ->
                                Box(
                                    Modifier
                                        .size(44.dp)
                                        .clip(RoundedCornerShape(10.dp))
                                        .background(if (b.icon == e) Color(0xFF3A3D45) else Color(0xFF23262C))
                                        .clickable { b = b.copy(icon = e) },
                                    contentAlignment = Alignment.Center,
                                ) { Text(e, fontSize = 22.sp) }
                            }
                        }
                    }

                    SectionTitle("Renk")
                    ChipRow {
                        val brandColor = BrandIcons.find(b.icon)?.color
                        (listOfNotNull(brandColor) + ButtonColors).distinct().forEach { c ->
                            Box(
                                Modifier
                                    .size(36.dp)
                                    .clip(CircleShape)
                                    .background(Color(c))
                                    .border(3.dp, if (b.color == c) Color.White else Color.Transparent, CircleShape)
                                    .clickable { b = b.copy(color = c) }
                            )
                        }
                    }

                    SectionTitle("Aksiyon")
                    ChipRow {
                        ActionTypes.forEach { (type, name) ->
                            FilterChip(
                                selected = b.action.type == type,
                                onClick = { if (b.action.type != type) setAction(DeckAction(type)) },
                                label = { Text(name) },
                            )
                        }
                    }
                    Spacer(Modifier.height(10.dp))

                    when (b.action.type) {
                        "hotkey" -> {
                            OutlinedTextField(
                                value = b.action.value,
                                onValueChange = { setAction(b.action.copy(value = it)) },
                                label = { Text("Tuş kombinasyonu") },
                                placeholder = { Text("ctrl+shift+m") },
                                supportingText = { Text("Tuşları + ile ayırın. Örn: alt+tab, win+d, ctrl+alt+f13, f5, printscreen") },
                                singleLine = true,
                                modifier = Modifier.fillMaxWidth(),
                            )
                        }

                        "text" -> {
                            OutlinedTextField(
                                value = b.action.value,
                                onValueChange = { setAction(b.action.copy(value = it)) },
                                label = { Text("Yazılacak metin") },
                                modifier = Modifier.fillMaxWidth(),
                            )
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Checkbox(
                                    checked = b.action.arg == "enter",
                                    onCheckedChange = { setAction(b.action.copy(arg = if (it) "enter" else "")) },
                                )
                                Text("Sonra Enter'a bas (sohbete mesaj gönderir)", color = Color.White)
                            }
                        }

                        "launch" -> {
                            OutlinedTextField(
                                value = b.action.value,
                                onValueChange = { setAction(b.action.copy(value = it)) },
                                label = { Text("Program yolu, dosya veya URL") },
                                placeholder = { Text("C:\\Program Files\\...\\app.exe  veya  https://...") },
                                supportingText = { Text("Örn: notepad.exe, calc, spotify:, steam://rungameid/730, https://youtube.com. {twitch}, {kick}, {youtube} ayarlardaki kullanıcı adıyla değişir.") },
                                singleLine = true,
                                modifier = Modifier.fillMaxWidth(),
                            )
                            OutlinedTextField(
                                value = b.action.arg,
                                onValueChange = { setAction(b.action.copy(arg = it)) },
                                label = { Text("Argümanlar (isteğe bağlı)") },
                                singleLine = true,
                                modifier = Modifier.fillMaxWidth(),
                            )
                        }

                        "media" -> ChipRow {
                            MediaOptions.forEach { (value, info) ->
                                FilterChip(
                                    selected = b.action.value == value,
                                    onClick = { setAction(b.action.copy(value = value)); suggest(info.first, info.second) },
                                    label = { Text("${info.second} ${info.first}") },
                                )
                            }
                        }

                        "obs" -> {
                            ChipRow {
                                ObsCommands.forEach { cmd ->
                                    FilterChip(
                                        selected = b.action.value == cmd.value,
                                        onClick = {
                                            setAction(DeckAction("obs", cmd.value))
                                            suggest(cmd.label, cmd.icon)
                                        },
                                        label = { Text("${cmd.icon} ${cmd.label}") },
                                    )
                                }
                            }
                            val cmd = ObsCommands.firstOrNull { it.value == b.action.value }
                            if (cmd?.argLabel != null) {
                                Spacer(Modifier.height(8.dp))
                                OutlinedTextField(
                                    value = b.action.arg,
                                    onValueChange = { setAction(b.action.copy(arg = it)) },
                                    label = { Text(cmd.argLabel) },
                                    singleLine = true,
                                    modifier = Modifier.fillMaxWidth(),
                                )
                                ObsListPicker(cmd.argList, obsLists, b.action.arg, ::fetchObs) { setAction(b.action.copy(arg = it)) }
                            }
                            if (cmd?.arg2Label != null) {
                                Spacer(Modifier.height(8.dp))
                                OutlinedTextField(
                                    value = b.action.arg2,
                                    onValueChange = { setAction(b.action.copy(arg2 = it)) },
                                    label = { Text(cmd.arg2Label) },
                                    singleLine = true,
                                    modifier = Modifier.fillMaxWidth(),
                                )
                                ObsListPicker(cmd.arg2List, obsLists, b.action.arg2, ::fetchObs) { setAction(b.action.copy(arg2 = it)) }
                            }
                            if (obsError.isNotEmpty()) Text(obsError, color = Color(0xFFEF9A9A), fontSize = 13.sp)
                        }

                        "page" -> ChipRow {
                            vm.pages.forEach { p ->
                                FilterChip(
                                    selected = b.action.value == p.id,
                                    onClick = { setAction(b.action.copy(value = p.id)); suggest(p.name, "📁") },
                                    label = { Text(p.name) },
                                )
                            }
                        }

                        else -> Text("Bu buton bir şey yapmaz (etiket/bölücü olarak kullanılabilir).", color = Color(0xFF8A8D93))
                    }
                }

                Spacer(Modifier.height(16.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    if (initial != null) {
                        TextButton(onClick = onDelete) { Text("Sil", color = Color(0xFFEF5350)) }
                    }
                    Spacer(Modifier.weight(1f))
                    TextButton(onClick = onDismiss) { Text("İptal") }
                    Spacer(Modifier.width(8.dp))
                    Button(onClick = { onSave(b) }) { Text("Kaydet") }
                }
            }
        }
    }

    if (showPicker) {
        BrandIconPicker(
            onDismiss = { showPicker = false },
            onPick = { icon ->
                b = b.copy(
                    icon = BrandIcons.PREFIX + icon.slug,
                    color = icon.color,
                    label = b.label.ifBlank { icon.title },
                )
                showPicker = false
            },
        )
    }

    if (showPcApps) {
        PcAppPicker(
            vm = vm,
            onDismiss = { showPcApps = false },
            onPick = { app, png ->
                b = b.copy(
                    icon = CustomIcons.save(png),
                    label = b.label.ifBlank { app.name },
                    color = if (b.color == DeckButton().color) 0xFF2B2E35 else b.color,
                    action = DeckAction("launch", "shell:AppsFolder\\" + app.id),
                )
                showPcApps = false
            },
        )
    }
}

/** PC'deki uygulamaları gerçek ikonlarıyla listeler; seçilen uygulama butona ikon + "aç" aksiyonu olur. */
@Composable
fun PcAppPicker(vm: DeckViewModel, onDismiss: () -> Unit, onPick: (PcApp, ByteArray) -> Unit) {
    var apps by remember { mutableStateOf<List<PcApp>?>(null) }
    var error by remember { mutableStateOf("") }
    var query by remember { mutableStateOf("") }
    val scope = rememberCoroutineScope()

    LaunchedEffect(Unit) {
        vm.client.pcApps().onSuccess { apps = it }.onFailure { error = it.message ?: "Hata" }
    }

    val q = query.trim().lowercase()
    val list = apps.orEmpty().filter { q.isEmpty() || it.name.lowercase().contains(q) }

    Dialog(onDismissRequest = onDismiss, properties = DialogProperties(usePlatformDefaultWidth = false)) {
        Surface(
            shape = RoundedCornerShape(24.dp),
            color = MaterialTheme.colorScheme.surface,
            modifier = Modifier.widthIn(max = 900.dp).fillMaxWidth(0.94f),
        ) {
            Column(Modifier.padding(20.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text("PC uygulamaları", fontSize = 22.sp, color = Color.White, modifier = Modifier.weight(1f))
                    TextButton(onClick = onDismiss) { Text("Kapat") }
                }
                OutlinedTextField(
                    value = query,
                    onValueChange = { query = it },
                    label = { Text("Ara (${list.size})") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                )
                Spacer(Modifier.height(12.dp))
                when {
                    error.isNotEmpty() -> Text("❌ $error", color = Color(0xFFEF9A9A))
                    apps == null -> Text("PC'den liste alınıyor...", color = Color(0xFFB0B3B8))
                    else -> LazyVerticalGrid(
                        columns = GridCells.Adaptive(104.dp),
                        modifier = Modifier.heightIn(max = 560.dp),
                        verticalArrangement = Arrangement.spacedBy(10.dp),
                        horizontalArrangement = Arrangement.spacedBy(10.dp),
                    ) {
                        items(list, key = { it.id }) { app ->
                            var png by remember(app.id) { mutableStateOf<ByteArray?>(null) }
                            LaunchedEffect(app.id) { png = CustomIcons.fetch(vm.client, app.id) }
                            val bmp = remember(png) { png?.let(CustomIcons::decode) }
                            Column(
                                Modifier
                                    .clip(RoundedCornerShape(12.dp))
                                    .clickable {
                                        scope.launch {
                                            val bytes = png ?: CustomIcons.fetch(vm.client, app.id)
                                            if (bytes != null) onPick(app, bytes) else error = "İkon alınamadı: ${app.name}"
                                        }
                                    }
                                    .padding(6.dp),
                                horizontalAlignment = Alignment.CenterHorizontally,
                            ) {
                                Box(Modifier.size(64.dp), contentAlignment = Alignment.Center) {
                                    if (bmp != null) Image(bmp, app.name, modifier = Modifier.size(56.dp))
                                }
                                Spacer(Modifier.height(4.dp))
                                Text(
                                    app.name, fontSize = 12.sp, color = Color(0xFFDDDDDD),
                                    maxLines = 2, textAlign = TextAlign.Center, overflow = TextOverflow.Ellipsis,
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}

/** OBS'den sahne/kaynak listesini çekip seçtiren satır. */
@Composable
private fun ObsListPicker(
    kind: String?,
    lists: Map<String, List<String>>,
    selected: String,
    fetch: (String) -> Unit,
    onPick: (String) -> Unit,
) {
    if (kind == null) return
    Spacer(Modifier.height(6.dp))
    val items = lists[kind]
    ChipRow {
        OutlinedButton(onClick = { fetch(kind) }) {
            Text(if (kind == "scenes") "OBS'den sahneleri getir" else "OBS'den kaynakları getir")
        }
        items?.forEach { s ->
            FilterChip(selected = selected == s, onClick = { onPick(s) }, label = { Text(s) })
        }
    }
}

@Composable
fun BrandIconPicker(onDismiss: () -> Unit, onPick: (BrandIcon) -> Unit) {
    var query by remember { mutableStateOf("") }
    val q = query.trim().lowercase()
    val list = if (q.isEmpty()) BrandIcons.all
    else BrandIcons.all.filter { it.title.lowercase().contains(q) || it.slug.contains(q) }

    Dialog(onDismissRequest = onDismiss, properties = DialogProperties(usePlatformDefaultWidth = false)) {
        Surface(
            shape = RoundedCornerShape(24.dp),
            color = MaterialTheme.colorScheme.surface,
            modifier = Modifier.widthIn(max = 900.dp).fillMaxWidth(0.94f),
        ) {
            Column(Modifier.padding(20.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text("Uygulama ikonları", fontSize = 22.sp, color = Color.White, modifier = Modifier.weight(1f))
                    TextButton(onClick = onDismiss) { Text("Kapat") }
                }
                OutlinedTextField(
                    value = query,
                    onValueChange = { query = it },
                    label = { Text("Ara (${list.size})") },
                    placeholder = { Text("spotify, steam, discord...") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                )
                Spacer(Modifier.height(12.dp))
                LazyVerticalGrid(
                    columns = GridCells.Adaptive(96.dp),
                    modifier = Modifier.heightIn(max = 560.dp),
                    verticalArrangement = Arrangement.spacedBy(10.dp),
                    horizontalArrangement = Arrangement.spacedBy(10.dp),
                ) {
                    items(list, key = { it.slug }) { icon ->
                        Column(
                            Modifier
                                .clip(RoundedCornerShape(12.dp))
                                .clickable { onPick(icon) }
                                .padding(6.dp),
                            horizontalAlignment = Alignment.CenterHorizontally,
                        ) {
                            SmallIcon(BrandIcons.PREFIX + icon.slug, icon.color, 64.dp)
                            Spacer(Modifier.height(4.dp))
                            Text(
                                icon.title, fontSize = 12.sp, color = Color(0xFFDDDDDD),
                                maxLines = 1, overflow = TextOverflow.Ellipsis,
                            )
                        }
                    }
                }
                Text(
                    "Logolar Simple Icons'tan (CC0). Markalar sahiplerine aittir.",
                    color = Color(0xFF8A8D93), fontSize = 11.sp, modifier = Modifier.padding(top = 8.dp),
                )
            }
        }
    }
}

@Composable
fun SettingsDialog(vm: DeckViewModel, onDismiss: () -> Unit) {
    val s = vm.settings
    var host by remember { mutableStateOf(s.host) }
    var port by remember { mutableStateOf(s.port.toString()) }
    var token by remember { mutableStateOf(s.token) }
    var cols by remember { mutableStateOf(s.cols) }
    var rows by remember { mutableStateOf(s.rows) }
    var twitch by remember { mutableStateOf(s.twitchUser) }
    var kick by remember { mutableStateOf(s.kickUser) }
    var youtube by remember { mutableStateOf(s.youtubeHandle) }
    var testResult by remember { mutableStateOf("") }
    val scope = rememberCoroutineScope()

    fun current() = Settings(
        host.trim(), port.toIntOrNull() ?: 8765, token.trim(), cols, rows,
        twitch.trim(), kick.trim(), youtube.trim().removePrefix("@"),
    )

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Ayarlar") },
        text = {
            Column(Modifier.verticalScroll(rememberScrollState())) {
                Text(
                    "PC'de TabDeckAgent.exe'yi çalıştırın; penceresinde yazan IP ve token'ı buraya girin. Tablet ve PC aynı Wi-Fi ağında olmalı.",
                    color = Color(0xFFB0B3B8), fontSize = 13.sp,
                )
                Spacer(Modifier.height(12.dp))
                Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    OutlinedTextField(
                        value = host, onValueChange = { host = it },
                        label = { Text("PC IP adresi") }, placeholder = { Text("192.168.1.20") },
                        singleLine = true, modifier = Modifier.weight(2f),
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri),
                    )
                    OutlinedTextField(
                        value = port, onValueChange = { port = it.filter(Char::isDigit).take(5) },
                        label = { Text("Port") }, singleLine = true, modifier = Modifier.weight(1f),
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                    )
                }
                OutlinedTextField(
                    value = token, onValueChange = { token = it },
                    label = { Text("Token") }, singleLine = true, modifier = Modifier.fillMaxWidth(),
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                )
                Spacer(Modifier.height(8.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    OutlinedButton(onClick = {
                        testResult = "Deneniyor..."
                        scope.launch {
                            testResult = vm.client.ping(current()).fold(
                                onSuccess = { "✅ Bağlandı: $it" },
                                onFailure = { "❌ ${it.message ?: "Bağlanılamadı"}" },
                            )
                        }
                    }) { Text("Bağlantıyı test et") }
                    Spacer(Modifier.width(12.dp))
                    Text(testResult, fontSize = 13.sp)
                }

                Spacer(Modifier.height(12.dp))
                Text("Kanallar (hazır butonlardaki bağlantılar için)", color = Color.White)
                Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    OutlinedTextField(
                        value = twitch, onValueChange = { twitch = it },
                        label = { Text("Twitch") }, singleLine = true, modifier = Modifier.weight(1f),
                    )
                    OutlinedTextField(
                        value = kick, onValueChange = { kick = it },
                        label = { Text("Kick") }, singleLine = true, modifier = Modifier.weight(1f),
                    )
                    OutlinedTextField(
                        value = youtube, onValueChange = { youtube = it },
                        label = { Text("YouTube @") }, singleLine = true, modifier = Modifier.weight(1f),
                    )
                }

                Spacer(Modifier.height(12.dp))
                Text("Izgara: $cols sütun × $rows satır", color = Color.White)
                Stepper("Sütun", cols, 2..8) { cols = it }
                Stepper("Satır", rows, 1..6) { rows = it }
            }
        },
        confirmButton = {
            Button(onClick = { vm.updateSettings(current()); onDismiss() }) { Text("Kaydet") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("İptal") } },
    )
}

@Composable
private fun Stepper(name: String, value: Int, range: IntRange, onChange: (Int) -> Unit) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Text(name, modifier = Modifier.width(70.dp), color = Color(0xFFB0B3B8))
        OutlinedButton(onClick = { onChange((value - 1).coerceIn(range)) }) { Text("−") }
        Text("$value", modifier = Modifier.width(40.dp), fontSize = 18.sp, color = Color.White, textAlign = TextAlign.Center)
        OutlinedButton(onClick = { onChange((value + 1).coerceIn(range)) }) { Text("+") }
    }
}

@Composable
fun NewPageDialog(
    initial: String,
    onDismiss: () -> Unit,
    onEmpty: (String) -> Unit,
    onPack: (PresetPack) -> Unit,
) {
    var name by remember { mutableStateOf(initial) }
    val packs = remember { presetPacks() }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Yeni sayfa") },
        text = {
            Column {
                OutlinedTextField(value = name, onValueChange = { name = it }, label = { Text("Boş sayfa adı") }, singleLine = true)
                Spacer(Modifier.height(16.dp))
                Text("ya da hazır paket ekle:", color = Color(0xFFB0B3B8), fontSize = 13.sp)
                Spacer(Modifier.height(8.dp))
                ChipRow {
                    packs.forEach { p ->
                        Column(
                            Modifier
                                .clip(RoundedCornerShape(12.dp))
                                .clickable { onPack(p) }
                                .padding(6.dp),
                            horizontalAlignment = Alignment.CenterHorizontally,
                        ) {
                            SmallIcon(p.icon, p.color, 56.dp)
                            Text("${p.name} (${p.buttons.size})", fontSize = 12.sp, color = Color.White)
                        }
                    }
                }
            }
        },
        confirmButton = {
            Button(onClick = { if (name.isNotBlank()) onEmpty(name.trim()) }) { Text("Boş sayfa ekle") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("İptal") } },
    )
}

@Composable
fun PageNameDialog(
    title: String,
    initial: String,
    canDelete: Boolean,
    onDismiss: () -> Unit,
    onSave: (String) -> Unit,
    onDelete: () -> Unit,
) {
    var name by remember { mutableStateOf(initial) }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(title) },
        text = {
            OutlinedTextField(value = name, onValueChange = { name = it }, label = { Text("Sayfa adı") }, singleLine = true)
        },
        confirmButton = {
            Button(onClick = { if (name.isNotBlank()) onSave(name.trim()) }) { Text("Kaydet") }
        },
        dismissButton = {
            Row {
                if (canDelete) {
                    TextButton(
                        onClick = onDelete,
                        colors = ButtonDefaults.textButtonColors(contentColor = Color(0xFFEF5350)),
                    ) { Text("Sayfayı sil") }
                }
                TextButton(onClick = onDismiss) { Text("İptal") }
            }
        },
    )
}
