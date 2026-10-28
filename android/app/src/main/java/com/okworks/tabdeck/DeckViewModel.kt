package com.okworks.tabdeck

import android.app.Application
import android.content.Context
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

class DeckViewModel(app: Application) : AndroidViewModel(app) {
    private val prefs = app.getSharedPreferences("tabdeck", Context.MODE_PRIVATE)

    var settings by mutableStateOf(
        prefs.getString("settings", null)?.let { runCatching { settingsFromJson(it) }.getOrNull() } ?: Settings()
    )
        private set

    var pages by mutableStateOf(
        prefs.getString("pages", null)?.let { runCatching { pagesFromJson(it) }.getOrNull() }
            ?.takeIf { it.isNotEmpty() } ?: defaultPages()
    )
        private set

    var pageIndex by mutableIntStateOf(0)
    var editMode by mutableStateOf(false)

    /** null = adres girilmemiş, true/false = son ping sonucu */
    var connected by mutableStateOf<Boolean?>(null)
        private set
    var pcName by mutableStateOf("")
        private set

    val client = AgentClient { settings }

    val slotCount get() = settings.cols * settings.rows
    val currentPage get() = pages[pageIndex.coerceIn(0, pages.lastIndex)]

    init {
        viewModelScope.launch {
            while (true) {
                if (settings.host.isBlank()) {
                    connected = null
                } else {
                    val r = client.ping()
                    connected = r.isSuccess
                    pcName = r.getOrDefault("")
                }
                delay(if (connected == true) 5000L else 3000L)
            }
        }
    }

    fun updateSettings(s: Settings) {
        settings = s
        prefs.edit().putString("settings", settingsToJson(s)).apply()
    }

    private fun savePages(newPages: List<Page>) {
        pages = newPages
        prefs.edit().putString("pages", pagesToJson(newPages)).apply()
    }

    /** Sayfanın slot listesini en az ızgara boyutuna kadar doldurur (küçültmede butonlar silinmez). */
    private fun padded(slots: List<DeckButton?>, size: Int): MutableList<DeckButton?> {
        val l = slots.toMutableList()
        while (l.size < size) l.add(null)
        return l
    }

    fun setSlot(index: Int, button: DeckButton?) {
        val p = currentPage
        val slots = padded(p.slots, maxOf(slotCount, index + 1))
        slots[index] = button
        savePages(pages.map { if (it.id == p.id) it.copy(slots = slots) else it })
    }

    fun swapSlots(from: Int, to: Int) {
        if (from == to) return
        val p = currentPage
        val slots = padded(p.slots, maxOf(slotCount, from + 1, to + 1))
        val tmp = slots[from]
        slots[from] = slots[to]
        slots[to] = tmp
        savePages(pages.map { if (it.id == p.id) it.copy(slots = slots) else it })
    }

    fun addPage(name: String) {
        savePages(pages + Page(name = name))
        pageIndex = pages.lastIndex
    }

    fun renamePage(id: String, name: String) {
        savePages(pages.map { if (it.id == id) it.copy(name = name) else it })
    }

    fun deletePage(id: String) {
        if (pages.size <= 1) return
        savePages(pages.filter { it.id != id })
        pageIndex = pageIndex.coerceIn(0, pages.lastIndex)
    }

    /** Hazır paketi yeni sayfa(lar) olarak ekler; ızgaraya sığmayanlar "Ad 2", "Ad 3" sayfalarına taşar. */
    fun addPackPages(pack: PresetPack) {
        val chunks = pack.buttons.chunked(slotCount)
        val newPages = chunks.mapIndexed { i, buttons ->
            Page(name = if (i == 0) pack.name else "${pack.name} ${i + 1}", slots = buttons)
        }
        val first = pages.size
        savePages(pages + newPages)
        pageIndex = first
    }

    fun goToPage(id: String) {
        val i = pages.indexOfFirst { it.id == id }
        if (i >= 0) pageIndex = i
    }
}
