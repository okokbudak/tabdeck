# TabDeck – Tablet için Stream Deck

Android tabletinizi Wi-Fi üzerinden Windows PC'nizi kontrol eden bir Stream Deck'e çevirir.

```
[Android tablet: TabDeck]  --HTTP (Wi-Fi)-->  [PC: TabDeck Agent]  --> klavye, medya, uygulamalar, OBS
```

## Özellikler

- Sayfalar, ayarlanabilir ızgara (2–8 sütun × 1–6 satır), tabletten düzenleme (ekle, taşı, sil)
- **Aksiyonlar:** kısayol tuşları, metin yazma (+Enter ile sohbet makrosu), uygulama/dosya/URL açma, medya ve ses, OBS, sayfa değiştirme
- **OBS (WebSocket v5):** sahne/önizleme, yayın ve kayıt (aç/kapat/başlat/bitir/duraklat), bölüm işareti, kaydı bölme, replay buffer, sanal kamera, stüdyo modu ve geçiş, ses kaynağı susturma ve seviye, kaynak göster/gizle, filtre aç/kapat, medya kaynağı, ekran görüntüsü. Sahne ve kaynak adları OBS'den çekilebilir.
- **Hazır paketler:** OBS, Discord, Twitch, Kick, YouTube – tek dokunuşla sayfa olarak eklenir
- **İkonlar:** PC'deki uygulamaların gerçek ikonları (Başlat menüsündeki masaüstü ve Store uygulamaları) ve 340 marka logosu

## Kurulum

[Releases](../../releases) sayfasından iki dosyayı indirin.

### 1. PC: `TabDeckAgent-Setup-x.y.z.exe`

1. Kurulum dosyasını çalıştırın. Yönetici yetkisi istemez; `%LOCALAPPDATA%\Programs\TabDeck` klasörüne kurar. İsterseniz Windows açılışında simge durumunda başlatır.
   - Dosya imzasız olduğu için SmartScreen uyarı verebilir: **Ek bilgi → Yine de çalıştır**.
2. TabDeck Agent penceresinde **IP**, **port** ve **token** yazar.
3. Windows Güvenlik Duvarı izin isterse **Özel ağlar** için izin verin.

Ayarlar kurulum klasöründeki `agent.json` dosyasındadır:

```json
{"port":8765,"token":"123456","obsUrl":"ws://127.0.0.1:4455","obsPassword":"","screenshotDir":""}
```

**OBS için:** OBS → Araçlar → WebSocket Sunucu Ayarları → "WebSocket sunucusunu etkinleştir". Şifre koyduysanız `obsPassword` alanına yazıp agent'ı yeniden başlatın.

### 2. Tablet: `TabDeck-x.y.z.apk`

APK'yı tablete kopyalayıp açın (bilinmeyen kaynaklardan yüklemeye izin vermeniz gerekebilir). Android 8.0 ve üstü.
İlk açılışta PC'nin IP'sini, portu ve token'ı girip **Bağlantıyı test et**'e basın. Tablet ve PC aynı ağda olmalı.

## Kullanım

- **Butona dokun** → aksiyon çalışır (başarılıysa parlar, hata olursa kırmızı yanar ve mesaj gösterir).
- **✏️ Düzenleme modu:**
  - Butona veya boş kutuya (+) dokun → düzenle / ekle
  - Butona **uzun bas**, sonra hedef kutuya dokun → taşı / yer değiştir
  - Seçili sayfa sekmesine tekrar dokun → yeniden adlandır / sil
  - **+** → boş sayfa ya da hazır paket
- **⚙️ Ayarlar:** bağlantı, ızgara boyutu, Twitch / Kick / YouTube kullanıcı adları (hazır bağlantılar için)

### Discord

Discord'un yerel bir kontrol arayüzü olmadığı için Discord paketi F13–F18 tuşlarını gönderir. Discord → Ayarlar → **Tuş Atamaları**'nda eylemi (Mikrofonu Kapat, Sağırlaştır, Yayıncı Modu, Overlay…) ekleyin, kısayol kutusuna tıklayıp TabDeck'teki butona basın. Bu atamalar Discord arka plandayken de çalışır.

### Aksiyon değer örnekleri

| Tür | Örnek |
|---|---|
| Kısayol | `ctrl+shift+m`, `alt+tab`, `win+d`, `ctrl+alt+f13`, `printscreen` |
| Aç | `notepad.exe`, `C:\...\app.exe`, `https://youtube.com`, `steam://rungameid/730`, `shell:AppsFolder\<uygulama kimliği>` |
| Yer tutucu | `{twitch}`, `{kick}`, `{youtube}` → ayarlardaki kullanıcı adı |

> İpucu: Kısayol çakışmasın diye `ctrl+alt+f13`…`f24` gibi kullanılmayan tuşları programlarda atayıp TabDeck'ten tetikleyebilirsiniz.

Agent normal yetkiyle çalıştığı için yönetici olarak açılmış pencerelere (ör. Görev Yöneticisi) tuş gönderemez; bu bir Windows kısıtlamasıdır.

## Agent API

Tüm isteklerde `X-Token: <token>` başlığı gerekir.

| İstek | Yanıt |
|---|---|
| `GET /ping` | `{"ok":true,"name":"PC-ADI"}` |
| `POST /action` | gövde `{"type":"hotkey\|text\|launch\|media\|obs","value":"…","arg":"…","arg2":"…"}` |
| `GET /obs/scenes`, `GET /obs/inputs` | `{"ok":true,"scenes":[…]}` / `{"ok":true,"inputs":[…]}` |
| `GET /apps` | `{"ok":true,"apps":[{"id":"…","name":"…"}]}` |
| `GET /appicon?id=<id>&size=<16-256>` | PNG |

## Derleme

- **Android** (`android/`): JDK 17, Android SDK 34, Gradle 8.10+ → `gradle assembleDebug`. Release imzası `android/keystore.properties` dosyasından okunur (depoda yok).
- **PC agent** (`agent/TabDeckAgent.cs`): Windows'taki hazır .NET Framework derleyicisiyle `agent\build.bat`; ek kurulum gerekmez.
- **Kurulum dosyası** (`installer/TabDeckAgent.nsi`): NSIS 3 → `makensis -DVERSION=1.0.0 -DSRC=<klasör> -DOUTFILE=<çıktı.exe> installer/TabDeckAgent.nsi`
- **Marka ikonları** (`tools/`): `npm install` ve `node gen-brand-icons.mjs brand-icons.txt ../android/app/src/main/assets/brand_icons.json`

## Lisans

[GPL-3.0](LICENSE).

Marka logoları [Simple Icons](https://simpleicons.org)'tan alınmıştır (CC0). Markalar ve logolar sahiplerine aittir; TabDeck bu şirketlerle bağlantılı değildir.
