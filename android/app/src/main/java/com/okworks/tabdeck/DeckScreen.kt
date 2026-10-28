package com.okworks.tabdeck

import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

private val Background = Color(0xFF101114)

@Composable
fun DeckApp(vm: DeckViewModel) {
    val snackbar = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    var editingSlot by remember { mutableStateOf<Int?>(null) }
    var moveFrom by remember { mutableStateOf<Int?>(null) }
    var showSettings by remember { mutableStateOf(vm.settings.host.isBlank()) }
    var pageDialog by remember { mutableStateOf<Page?>(null) } // null olmayan: düzenlenen sayfa
    var newPageDialog by remember { mutableStateOf(false) }

    fun toast(msg: String) {
        scope.launch {
            snackbar.currentSnackbarData?.dismiss()
            snackbar.showSnackbar(msg)
        }
    }

    suspend fun press(b: DeckButton): Boolean = when (b.action.type) {
        "none" -> true
        "page" -> { vm.goToPage(b.action.value); true }
        else -> {
            val r = vm.client.run(b.action)
            r.exceptionOrNull()?.let { toast(it.message ?: "Hata") }
            r.isSuccess
        }
    }

    Scaffold(containerColor = Background, snackbarHost = { SnackbarHost(snackbar) }) { pad ->
        Column(
            Modifier
                .fillMaxSize()
                .padding(pad)
                .padding(horizontal = 16.dp, vertical = 10.dp)
        ) {
            TopBar(
                vm = vm,
                onPageClick = { i ->
                    if (vm.editMode && i == vm.pageIndex) pageDialog = vm.pages[i]
                    vm.pageIndex = i
                    moveFrom = null
                },
                onAddPage = { newPageDialog = true },
                onToggleEdit = {
                    vm.editMode = !vm.editMode
                    moveFrom = null
                },
                onSettings = { showSettings = true },
            )
            Spacer(Modifier.height(12.dp))
            DeckGrid(
                vm = vm,
                moveFrom = moveFrom,
                onPress = { press(it) },
                onEditTap = { i ->
                    val from = moveFrom
                    if (from != null) {
                        vm.swapSlots(from, i)
                        moveFrom = null
                    } else {
                        editingSlot = i
                    }
                },
                onEditLongPress = { i ->
                    if (vm.currentPage.slots.getOrNull(i) != null) {
                        moveFrom = i
                        toast("Taşımak için hedef kutuya dokun")
                    }
                },
            )
        }
    }

    editingSlot?.let { slot ->
        ButtonEditorDialog(
            initial = vm.currentPage.slots.getOrNull(slot),
            vm = vm,
            onDismiss = { editingSlot = null },
            onSave = { vm.setSlot(slot, it); editingSlot = null },
            onDelete = { vm.setSlot(slot, null); editingSlot = null },
        )
    }

    if (showSettings) {
        SettingsDialog(vm = vm, onDismiss = { showSettings = false })
    }

    if (newPageDialog) {
        NewPageDialog(
            initial = "Sayfa ${vm.pages.size + 1}",
            onDismiss = { newPageDialog = false },
            onEmpty = { vm.addPage(it); newPageDialog = false },
            onPack = { pack ->
                vm.addPackPages(pack)
                newPageDialog = false
                toast(pack.note)
            },
        )
    }

    pageDialog?.let { p ->
        PageNameDialog(
            title = "Sayfayı düzenle",
            initial = p.name,
            canDelete = vm.pages.size > 1,
            onDismiss = { pageDialog = null },
            onSave = { vm.renamePage(p.id, it); pageDialog = null },
            onDelete = { vm.deletePage(p.id); pageDialog = null },
        )
    }
}

@Composable
private fun TopBar(
    vm: DeckViewModel,
    onPageClick: (Int) -> Unit,
    onAddPage: () -> Unit,
    onToggleEdit: () -> Unit,
    onSettings: () -> Unit,
) {
    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        LazyRow(Modifier.weight(1f), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            itemsIndexed(vm.pages, key = { _, p -> p.id }) { i, p ->
                FilterChip(
                    selected = i == vm.pageIndex,
                    onClick = { onPageClick(i) },
                    label = { Text(p.name) },
                )
            }
            if (vm.editMode) {
                item {
                    IconButton(onClick = onAddPage) { Icon(Icons.Default.Add, "Sayfa ekle") }
                }
            }
        }

        val (dotColor, status) = when (vm.connected) {
            true -> Color(0xFF66BB6A) to vm.pcName.ifBlank { "Bağlı" }
            false -> Color(0xFFE53935) to "Bağlantı yok"
            null -> Color(0xFF757575) to "Ayarlanmadı"
        }
        Box(Modifier.size(10.dp).clip(CircleShape).background(dotColor))
        Spacer(Modifier.width(6.dp))
        Text(status, color = Color(0xFFB0B3B8), fontSize = 13.sp)
        Spacer(Modifier.width(8.dp))

        IconButton(onClick = onToggleEdit) {
            Icon(
                if (vm.editMode) Icons.Default.Check else Icons.Default.Edit,
                if (vm.editMode) "Düzenlemeyi bitir" else "Düzenle",
                tint = if (vm.editMode) Color(0xFF9575FF) else Color.White,
            )
        }
        IconButton(onClick = onSettings) { Icon(Icons.Default.Settings, "Ayarlar", tint = Color.White) }
    }
}

@Composable
private fun DeckGrid(
    vm: DeckViewModel,
    moveFrom: Int?,
    onPress: suspend (DeckButton) -> Boolean,
    onEditTap: (Int) -> Unit,
    onEditLongPress: (Int) -> Unit,
) {
    val cols = vm.settings.cols
    val rows = vm.settings.rows
    val page = vm.currentPage
    BoxWithConstraints(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        val gap = 14.dp
        val cell = minOf((maxWidth - gap * (cols - 1)) / cols, (maxHeight - gap * (rows - 1)) / rows)
        Column(verticalArrangement = Arrangement.spacedBy(gap)) {
            repeat(rows) { r ->
                Row(horizontalArrangement = Arrangement.spacedBy(gap)) {
                    repeat(cols) { c ->
                        val i = r * cols + c
                        DeckKey(
                            button = page.slots.getOrNull(i),
                            size = cell,
                            editMode = vm.editMode,
                            selected = moveFrom == i,
                            onPress = onPress,
                            onEditTap = { onEditTap(i) },
                            onEditLongPress = { onEditLongPress(i) },
                        )
                    }
                }
            }
        }
    }
}

@OptIn(ExperimentalFoundationApi::class)
@Composable
fun DeckKey(
    button: DeckButton?,
    size: Dp,
    editMode: Boolean,
    selected: Boolean = false,
    onPress: suspend (DeckButton) -> Boolean = { true },
    onEditTap: () -> Unit = {},
    onEditLongPress: () -> Unit = {},
) {
    val scope = rememberCoroutineScope()
    val haptic = LocalHapticFeedback.current
    val interaction = remember { MutableInteractionSource() }
    val pressed by interaction.collectIsPressedAsState()
    var flash by remember(button?.id) { mutableStateOf<Boolean?>(null) }

    val scale by animateFloatAsState(if (pressed) 0.92f else 1f, label = "scale")
    val base = button?.let { Color(it.color) } ?: Color(0xFF1A1C21)
    val bg by animateColorAsState(
        when (flash) {
            true -> lerpColor(base, Color.White, 0.35f)
            false -> Color(0xFFB71C1C)
            null -> base
        },
        label = "bg",
    )
    val borderColor = when {
        selected -> Color(0xFF9575FF)
        editMode -> Color(0x55FFFFFF)
        else -> Color.Transparent
    }
    val shape = RoundedCornerShape(size * 0.18f)

    Box(
        Modifier
            .size(size)
            .scale(scale)
            .clip(shape)
            .background(bg)
            .border(if (selected) 3.dp else 1.dp, borderColor, shape)
            .combinedClickable(
                interactionSource = interaction,
                indication = null,
                onClick = {
                    if (editMode) {
                        onEditTap()
                    } else if (button != null) {
                        haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                        scope.launch {
                            flash = onPress(button)
                            delay(300)
                            flash = null
                        }
                    }
                },
                onLongClick = if (editMode) onEditLongPress else null,
            ),
        contentAlignment = Alignment.Center,
    ) {
        if (button == null) {
            if (editMode) Text("+", color = Color(0x88FFFFFF), fontSize = with(LocalDensity.current) { (size * 0.3f).toSp() })
        } else {
            val density = LocalDensity.current
            Column(
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.Center,
                modifier = Modifier.padding(size * 0.06f),
            ) {
                val fg = contentColorFor(base)
                GlyphIcon(button.icon, fg, size * (if (button.label.isBlank()) 0.5f else 0.36f))
                if (button.label.isNotBlank()) {
                    Text(
                        button.label,
                        color = fg,
                        fontWeight = FontWeight.SemiBold,
                        fontSize = with(density) { (size * (if (button.icon.isBlank()) 0.16f else 0.11f)).toSp() },
                        textAlign = TextAlign.Center,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis,
                        lineHeight = with(density) { (size * 0.13f).toSp() },
                    )
                }
            }
        }
    }
}

private fun lerpColor(a: Color, b: Color, t: Float) = Color(
    red = a.red + (b.red - a.red) * t,
    green = a.green + (b.green - a.green) * t,
    blue = a.blue + (b.blue - a.blue) * t,
    alpha = 1f,
)
