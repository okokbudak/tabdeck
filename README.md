# TabDeck – Tablet için Stream Deck

Android tabletinizi Wi-Fi üzerinden Windows PC'nizi kontrol eden bir Stream Deck'e çevirir.

```
[Android tablet: TabDeck]  --HTTP (Wi-Fi)-->  [PC: TabDeck Agent]  --> klavye, medya, uygulamalar, OBS
```

## Özellikler

- Sayfalar, ayarlanabilir ızgara (2–8 sütun × 1–6 satır), tabletten düzenleme (ekle, taşı, sil)
- **Aksiyonlar:** kısayol tuşları, metin yazma (+Enter ile sohbet makrosu), uygulama/dosya/URL açma, medya ve ses, OBS, sayfa değiştirme
- **OBS (WebSocket v5):** sahne/önizleme, **sahne döngüsü** (seçilen sahneler arasında belirli sürede bir otomatik geçiş, butonda geri sayım), yayın ve kayıt (aç/kapat/başlat/bitir/duraklat), bölüm işareti, kaydı bölme, replay buffer, sanal kamera, stüdyo modu ve geçiş, ses kaynağı susturma ve seviye, kaynak göster/gizle, filtre aç/kapat, medya kaynağı, ekran görüntüsü. Sahne ve kaynak adları OBS'den çekilebilir.
- **Uygulama bazlı ses:** Windows ses karıştırıcısındaki her uygulamanın (Spotify, Discord, tarayıcı, oyun…) sesini ayrı ayrı açma/kısma, susturma ya da belirli seviyeye ayarlama. Yeni seviye butonun üstünde görünür.
- **Hazır paketler:** Ses, OBS, Discord, Twitch, Kick, YouTube – tek dokunuşla sayfa olarak eklenir
- **İkonlar:** PC'deki uygulamaların gerçek ikonları (Başlat menüsündeki masaüstü ve Store uygulamaları) ve 340 marka logosu

## Kurulum

[Releases](../../releases) sayfasından iki dosyayı indirin.

### 1. PC: `TabDeckAgent-Setup-x.y.z.exe`

1. Kurulum dosyasını çalıştırın. Yönetici yetkisi istemez; `%LOCALAPPDATA%\Programs\TabDeck` klasörüne kurar. İsterseniz Windows açılışında simge durumunda başlatır.
   - Dosya imzasız olduğu için SmartScreen uyarı verebilir: **Ek bilgi → Yine de çalıştır**.
2. TabDeck Agent penceresi tablette girilecek **IP**, **port** ve **token**'ı kopyala düğmeleriyle gösterir.
3. Windows Güvenlik Duvarı izin isterse **Özel ağlar** için izin verin.

Pencereyi kapatınca agent sistem tepsisinde çalışmaya devam eder; tepsi simgesine çift tıklayarak açabilir, sağ tıklayıp **Çıkış** ile kapatabilirsiniz. Pencerede:

- **Ayarlar:** port, OBS adresi ve şifresi (**OBS bağlantısını test et**), ekran görüntüsü klasörü, Windows açılışında başlatma, yönetici modu
- **Yeni token** oluşturma, **Yönetici olarak yeniden başlat** düğmesi
- **Etkinlik:** son bağlanan tablet ve gelen komutların sonuçları

Ayarlar kurulum klasöründeki `agent.json` dosyasında saklanır:

```json
{"port":8765,"token":"12345678","obsUrl":"ws://127.0.0.1:4455","obsPassword":"","screenshotDir":"","runAsAdmin":false}
```

Aynı IP'den 60 saniyede 5 hatalı token denemesi yapılırsa o IP 60 saniye engellenir.

### Oyunlar ve yönetici modu

Birçok oyun ve anti-hile sistemi **yönetici olarak** çalışır. Windows, normal yetkili bir programın yönetici olarak çalışan bir pencereye tuş göndermesini sessizce engeller (UIPI). Bu durumda:

- **Medya ve ses** (oynat/duraklat, sonraki, ses ±, sessiz), **OBS**, **uygulama/URL açma** etkilenmez: bunlar tuş göndermeden çalışır.
- **Kısayol** ve **metin yaz** aksiyonları (Discord tuşları dahil) engellenir. Tablet bunu kırmızı bir hata ve oyunun adıyla bildirir.

Çözüm: agent penceresinde **Yönetici olarak yeniden başlat**'a basın; her seferinde yönetici olarak açılması için Ayarlar'da **Yönetici olarak çalıştır**'ı işaretleyin. UAC ayarınıza göre her açılışta onay isteyebilir. Yönetici modunda tabletten gelen tuşlar yönetici yetkisiyle gönderildiği için token'ı kimseyle paylaşmayın.

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


## Agent API

Tüm isteklerde `X-Token: <token>` başlığı gerekir.

| İstek | Yanıt |
|---|---|
| `GET /ping` | `{"ok":true,"name":"PC-ADI"}` |
| `POST /action` | gövde `{"type":"hotkey\|text\|launch\|media\|appvolume\|obs","value":"…","arg":"…","arg2":"…"}` |
| `GET /audio/apps` | `{"ok":true,"apps":[{"name":"Spotify","title":"Spotify","level":80,"muted":false}]}` |
| `GET /obs/scenes`, `GET /obs/inputs` | `{"ok":true,"scenes":[…]}` / `{"ok":true,"inputs":[…]}` |
| `GET /obs/cycle` | `{"ok":true,"cycle":true,"key":"Sahne 1\nSahne 2","remaining":252,"scene":"Sahne 1"}` |
| `GET /apps` | `{"ok":true,"apps":[{"id":"…","name":"…"}]}` |
| `GET /appicon?id=<id>&size=<16-256>` | PNG |

## Derleme

- **Android** (`android/`): JDK 17, Android SDK 34, Gradle 8.10+ → `gradle assembleDebug`. Release imzası `android/keystore.properties` dosyasından okunur (depoda yok).
- **PC agent** (`agent/*.cs`, arayüz `AgentUI.cs`): Windows'taki hazır .NET Framework derleyicisiyle `agent\build.bat`; ek kurulum gerekmez.
- **Kurulum dosyası** (`installer/TabDeckAgent.nsi`): NSIS 3 → `makensis -DVERSION=1.0.0 -DSRC=<klasör> -DOUTFILE=<çıktı.exe> installer/TabDeckAgent.nsi`
- **Marka ikonları** (`tools/`): `npm install` ve `node gen-brand-icons.mjs brand-icons.txt ../android/app/src/main/assets/brand_icons.json`

## Lisans

[GPL-3.0](LICENSE).

Marka logoları [Simple Icons](https://simpleicons.org)'tan alınmıştır (CC0). Markalar ve logolar sahiplerine aittir; TabDeck bu şirketlerle bağlantılı değildir.
