# FM Asistanım — Proje Planı

> Durum: Bellek okuma yöntemi gerçek kayıt dosyasında doğrulandı, uygulama geliştirmeye başlanmadı.
> Son güncelleme: 2026-11-09

## 1. Amaç

Football Manager 2027'deki asistan menajer ve teknik heyet önerileri kısa ve yüzeysel.
**FM Asistanım**, FM27 verilerini alıp gerçek bir analist gibi yorumlayan, tek kullanıcılı bir
masaüstü uygulaması olacak.

Temel ilke: Oyundaki metni başka cümlelerle tekrar etmeyecek. Veriyi birleştirecek,
tümevarım ve tümdengelim yoluyla sonuç çıkaracak ve bu sonuçları gerekçeleriyle sunacak.

## 2. Kalite İlkesi (pazarlık konusu değil)

Kullanıcının açık talebi: Uygulama "yapay zekâyla yapıldığı belli olan dandik
uygulamalar" gibi olmayacak.

- Her özellik eklenmeden önce **gerekli mi, doğru mu, kullanıcıya gerçekten değer katıyor mu**
  diye tartılacak. Gösteriş için özellik eklenmeyecek.
- Analizde uydurma olmayacak. Her yorum, dayandığı veriye (öznitelik, istatistik, taktik
  talimatı) izlenebilir olacak.
- Arayüz sade ve profesyonel olacak (bir kulübün analiz departmanı aracı gibi).
  Gereksiz emoji, jenerik kart yığını veya şişirilmiş metin olmayacak.
- **Para birimi kuralı (kullanıcı):** Uygulamada tüm maaşlar **euro ve yıllık** gösterilir.
  Sterlin veya haftalık değer arayüze hiçbir zaman çıkmaz. Bonservis ve piyasa değerleri de
  euro olarak gösterilir. Dönüşüm yalnızca veri katmanında yapılır.
- Kod temiz, test edilmiş ve sürdürülebilir olacak.

### Kesin Kural: Teknik ekip itaatkâr değil, objektiftir
- Asistan menajer, gözlemciler ve tüm teknik ekip **kullanıcıyı onaylamak ya da her dediğini
  yapmak amacı taşımaz.**
- Her ekip üyesinin kendi bağımsız görüşü vardır. Kullanıcının fikrine katılmadığında bunu
  açıkça ve gerekçesiyle söyler. Gerekirse bilinçli olarak **karşı tez** üretir.
- Kullanıcının fikrini benimseyebilir, ama yalnızca veri ve argümanla ikna olduğunda.
  Sırf kullanıcı ısrar ettiği için görüş değiştirmez.
- Görüşleri veriye ve kendi futbol felsefesine (FM'deki tercih ettiği diziliş, oyun tarzı,
  kişilik özellikleri) dayanır. Dalkavukluk açıkça bir hata olarak kabul edilir ve test
  edilir.

## 3. Ana Özellikler

### 3.1 Kendi taktiğimizin analizi
- Diziliş, roller, görevler, takım ve oyuncu talimatları **mevcut kadroyla birlikte**
  değerlendirilecek. Oyuncu profili ile rol uyumu, kilit bölgelerdeki zayıflıklar, top
  kayıplarında açılan alanlar ve geçiş anlarındaki riskler incelenecek.
- Çıktı: güçlü yönler, zayıf yönler, riskler ve somut düzeltme önerileri. Her öneri gerekçeli
  olacak.

### 3.2 Rakip analizi ve maç hazırlığı (örnek: PSG maçı)
- Rakibin muhtemel dizilişi, oyun yapısı, kilit oyuncuları, güçlü ve zayıf yönleri.
- Bizim taktiğimizin bu rakibe karşı nasıl eşleştiği: hangi bölgeler avantajlı, hangileri
  tehlikeli.
- Maça özel **küçük dokunuşlar**: rol veya görev değişiklikleri, pres yüksekliği, belirli
  oyunculara yönelik talimatlar, duran top notları.

### 3.3 Antrenman planlaması (FM27'nin yeni antrenman ve taktiksel verimlilik sistemi)
- Takımın taktiksel verimliliğini ve eksiklerini analiz etmek.
- Haftalık programa bakıp **aylık antrenman programı** üretmek. Kullanıcı bu programı
  oyuna elle girecek.

### 3.4 Moneyball Analizcileri (oyuncu keşfi)
- Amaç: **takımın sistemine en uygun ve fiyat/performans oranı en yüksek** oyuncuları ve
  gün yüzüne çıkmamış yetenekleri bulmak.
- Değer ölçütü: sisteme ve rol uyum skoru ile toplam maliyet (bonservis, maaş, sözleşme
  süresi) birlikte değerlendirilir. Yaş, gelişim alanı, yeniden satış değeri, sözleşmesi
  biten ve bedelsiz oyuncular da hesaba katılır.
- Her öneride "neden bu oyuncu, sistemimizde hangi rolü, kime alternatif" sorularının
  gerekçeli cevabı bulunur.

### 3.5 Teknik ekip gerçekçiliği
Uygulama her şeyi bilen biri gibi davranmayacak. FM'nin gerçekçiliğini koruyacak.
- Uygulama kullanıcının **gerçek teknik ekibini** (gözlemciler, asistan, analistler,
  antrenörler) ve özniteliklerini okur.
- Analiz ve keşif kalitesi ekibin özniteliklerine bağlıdır:
  - Elit gözlemciler (yüksek Oyuncu Yeteneğini Değerlendirme / Potansiyel Değerlendirme)
    isabetli ve dar aralıklı değerlendirmeler yapar.
  - Vasat gözlemcilerin tahminleri daha geniş belirsizlik aralığına ve hata payına sahiptir.
    Gerçek veriyi bilsek bile, ekip "bilemeyeceği" bilgiyi kullanmaz.
  - Asistan menajerin taktik bilgisi ve ilgili öznitelikleri, taktik analizinin
    derinliğini ve isabetini belirler.
- Bu sayede elit bir ekibin farkı gerçekten hissedilir.
- **Karar: "Her şeyi bilen" bir mod olmayacak.** Gerçekçilik her zaman açık. Uygulama,
  ekibin bilemeyeceği bilgiyi kullanıcıya hiçbir şekilde göstermez.

### 3.6 Teknik ekiple yazışma
- Kullanıcı her ekip üyesiyle (örn. asistan menajer Jim Fraser) **gerçek bir sohbet**
  yapabilir: fikir alışverişi, tartışma, öneri isteme.
- Her üyenin kişiliği, uzmanlık alanı ve bilgi seviyesi kendi FM profilinden türetilir.
- Ekip üyeleri geçmiş konuşmaları ve kendi önceki görüşlerini hatırlar.
- Yukarıdaki "itaatkâr değil, objektif" kuralına tabidirler.

### 3.7 Futbol ve FM27 bilgi tabanı
- Modern futbol taktiği: pozisyonel oyun, pres tetikleyicileri, rest-defence, asimetrik
  yapılar, oyun kurma varyasyonları vb.
- FM27 taktik motorunun nasıl çalıştığı (roller, talimatlar, verimlilik sistemi).
- Kaynak olarak kullanıcının verdiği videolar (aşağıda).

## 4. Teknik Kararlar (açık konular, yarın netleşecek)

### 4.1 Veriyi oyundan nasıl alacağız?
FM kayıt dosyaları (`.fm`) sıkıştırılmış ve belgelenmemiş bir ikili (binary) formatta.
Doğrudan okumak kırılgan bir yöntem ve her yamada bozulabilir. Değerlendirilecek seçenekler:

| Yöntem | Artı | Eksi |
|---|---|---|
| Oyun içi görünümleri HTML/metin olarak dışa aktarma (Yazdır → Web sayfası) | Topluluk araçlarının çoğunun kullandığı, kararlı ve yasal yöntem | Kullanıcı birkaç dışa aktarma yapmalı |
| Ekran görüntüsü + görüntü okuyan yapay zekâ | Taktik ekranı ve rakip raporu kolayca alınır | Hata payı var, doğrulama gerekir |
| Bellek okuma (FMRTE/Genie Scout benzeri) | Tam otomatik | Çok karmaşık, sürüme bağımlı, riskli |
| Kayıt dosyasını çözme | Tam veri | Format kapalı, büyük tersine mühendislik işi |

Ön öneri: **Dışa aktarma (birincil) + ekran görüntüsü (taktik ekranları için)**.
FM27'de bunun nasıl çalıştığı kullanıcının bilgisayarında test edilerek doğrulanacak.

**Referans: Teknik Oda (https://teknikodafm.com).** FM verisini çekip oyuncu bulma ve CA/PA
gösterme odaklı bir asistan. Taktik analizi hedefimiz kadar derin değil, ama veri alma
yöntemi örnek alınabilir. CA/PA ve gizli öznitelikler oyunda görünmediği ve dışa
aktarılamadığı için büyük ihtimalle **bellek okuma** kullanıyor. Bu henüz doğrulanmadı.
Site bulut oturumundan erişilemedi. Yerelde incelenecek.

Bellek okumanın bu proje için değeri:
- Kullanıcı hiçbir şey dışa aktarmadan tüm veri otomatik gelir.
- Gizli öznitelikler (tutarlılık, büyük maç, sakatlık yatkınlığı vb.) analize katılabilir.
- Taktik ve antrenman verisi de muhtemelen bellekte bulunuyor. Araştırılacak.

Maliyeti: Her FM yamasında bellek adresleri değişebilir. Bakım yükü getirir ve ciddi
tersine mühendislik ister.

**Karar (kullanıcı): Birincil veri kaynağı bellek okuma olacak.** Detaylar yerel
oturumda konuşulacak. Tasarım ilkeleri:
- Bellek okuma ayrı bir veri kaynağı modülü olacak. Analiz motoru verinin nereden
  geldiğinden bağımsız kalacak.
- Yama sonrası bozulmaları hızlı tespit edip düzeltmek için sürüm kontrolü ve
  doğrulama testleri olacak.
- Okunamayan veri için yedek yol (dışa aktarma veya ekran görüntüsü) kalabilir.

#### 4.1.1 Yerel inceleme ve doğrulama (2026-11-09)

**Kurulum:** FM27, Xbox/Game Pass sürümü (`C:\XboxGames\Football Manager 27`). Oyun Unity +
IL2CPP ile derlenmiş. Oyuncu veritabanı Unity tarafında değil, yerel C++ kütüphanesinde
(`fm_Data/Plugins/x86_64/game_plugin.dll`, sürüm 27.0). BepInEx kurulu.

**Teknik Oda'nın yöntemi doğrulandı:** Ana program belleği dışarıdan, yalnızca okuyarak
tarıyor (`OpenProcess`, `VirtualQueryEx`, `ReadProcessMemory`). Okuma çekirdeği
**FMSuperScout** (https://github.com/mavarobli/FMSuperScout, MIT lisansı). Ofis, roller ve
"Oyunda aç" özellikleri için ayrıca BepInEx eklentisi kullanıyor.

**FMSuperScout nasıl çalışıyor:**
- Oyun işlemini yalnızca okuma izniyle açıyor. Yazma yok, ağ erişimi yok (kaynak kod denetlendi).
- Belleği tarayıp `game_plugin.dll` içini gösteren sanal tablo (vtable) işaretçilerini arıyor.
  Nesnenin türünü derleyicinin RTTI bilgisinden çıkarıyor. Bu yaklaşım yamalara dayanıklı.
- Alan konumları (CA, öznitelikler vb.) `core/Fields.cs` içinde FM sürümü başına elle sabitlenmiş.
  Yama sonrası düzeltme kılavuzu: `docs/repin-guide.md`.
- Okuduğu veri **kariyerin o anki durumu**dur (güncel CA, sonradan üretilen oyuncular dahil).

**Gerçek kayıt dosyasında test sonucu:** Kaynaktan derlendi (.NET 8 SDK) ve FM27 açıkken
çalıştırıldı. 17,5 saniyede 92.110 oyuncu ve 45.447 teknik ekip üyesi okundu. Menajer, kulüp
(Galatasaray) ve oyun tarihi doğru algılandı. Kullanıcı oyuncu (Osimhen, Barış Alper Yılmaz,
Uğurcan Çakır) ve teknik ekip (Jim Fraser, Emre Utkucan, Simon Wells) değerlerini oyun
ekranlarıyla karşılaştırdı: **hepsi tutuyor.**

**Kapsam:**

| Okunabilen | Okunamayan (bizim eklememiz gerekecek) |
|---|---|
| Oyuncu: görünür + gizli öznitelikler, CA/PA, mevki yeterlilikleri, kişilik, boy, kondisyon, moral, değer | Taktik (diziliş, roller, talimatlar) |
| Teknik ekip: tüm öznitelikler, CA/PA, iş tercihi, görev | Antrenman ve taktiksel verimlilik |
| Sözleşme, kulüp, kadro, lig, uyruk, anlaşmalı transferler, oyun tarihi | Maç istatistikleri, sakatlıklar, rakip raporları |
| | **Gözlemcilerin hangi oyuncuyu ne kadar tanıdığı** (gerçekçilik kuralı için kritik) |

**Notlar:**
- Bellekteki para birimi **sterlin**, maaşlar **haftalık**. Kontrol: Emre Utkucan haftalık
  £1.327 = oyunda yıllık €80.500, yani kur yaklaşık 1,1666. Kur sabit kodlanmayacak. Oyunun
  kullandığı kur bellekten okunacak ya da bilinen bir değerle kalibre edilecek.
- **Otorite FMSuperScout'ta yanlış eşleştirilmiş.** Ham blok okumasıyla bulundu: Otorite,
  teknik ekip öznitelik tabanının (`NPLO_ATTRS`) **+0x04** konumunda, **ham 1-20** değer
  olarak duruyor (5 ile çarpılmamış). Doğrulama: Jim Fraser 7, Eren Mert 16 (iki değer de oyunla
  aynı). FMSuperScout'un `KV_distributie` (+0x2A) ve `KV_vangen` (+0x29) alanları Otorite
  değil, karşılıkları bilinmiyor. FM27'de teknik ekip için ayrı bir Disiplin özniteliği yok.
- Teknik ekip bloğunda FMSuperScout'un okumadığı başka değerler de var: +0x00..+0x0B ve
  +0x0E..+0x19 aralığında ham değerler, +0x1A, +0x28, +0x2B, +0x2D, +0x2E, +0x30 konumlarında
  ×5 değerler. Bunların ne olduğu ileride aynı yöntemle (ham blok + oyun ekranı) çözülecek.
- FM27 resmi çıkışı 10 Kasım 2026. İlk gün yaması alan konumlarını kaydırabilir. Çekirdeği
  alırsak FMSuperScout'taki yama düzeltmelerini takip edip kendimize aktarmamız gerekir.

**Ön karar:** FMSuperScout'un okuma çekirdeği (MIT, lisans metni korunarak) temel alınacak.
Bu, teknoloji yığınını **C# / .NET** yönüne çekiyor. Eksik veriler (yukarıdaki sağ sütun) aynı
yöntemle ya da gerekirse oyun içi BepInEx eklentisiyle Unity tarafından okunacak.

### 4.2 Yapay zekâ gerekli mi?
Değerlendirme: **Evet, ama tek başına değil.**

- **Deterministik analiz motoru (yapay zekâsız):** Rol-oyuncu uyumu, öznitelik
  karşılaştırmaları, istatistik hesapları ve taktik kuralı kontrolleri. Sayısal gerçekleri
  üretir. Tekrarlanabilir ve test edilebilir olur.
- **Dil modeli (yorum katmanı):** Bu gerçekleri bağlam içinde yorumlar, rapor yazar ve öneri
  gerekçelendirir. Yalnızca motorun ürettiği verilere dayanır (uydurmayı önlemek için).

Entegrasyon seçenekleri:
1. **Bilgisayarda kurulu Claude Code üzerinden** (`claude -p` başsız mod): Kullanıcının
   mevcut Claude aboneliğini kullanır, ayrı API ücreti gerektirmez. Abonelik kullanım
   limitleri geçerlidir. Tek kişilik kullanım için en mantıklı seçenek.
2. **Anthropic API anahtarı:** Kullanım başına ücret ödenir, daha esnektir.
3. **Gemini vb. ucuz bir API:** Maliyet düşük, ama analiz kalitesi düşük olabilir.

Ön öneri: Seçenek 1 (Claude Code entegrasyonu). Mimari, gerekirse API'ye geçilebilecek
şekilde soyutlanacak.

### 4.3 Platform
Windows masaüstü (kullanıcının bilgisayarı). Kesin teknoloji yığını, veri alma yöntemi
netleştikten sonra seçilecek.

### 4.4 Videolar
Videoları doğrudan izleyemem. Plan: Mümkünse transkriptlerini çekip bilgi tabanına
özetlemek. Olmazsa kullanıcıyla birlikte içerikleri çıkarmak. Her videodan çıkarılan
taktiksel ilkeler yapılandırılmış bilgi tabanına yazılacak.

## 5. Kaynak Videolar

FM26'ya ait olanlar da geçerli, çünkü kullanıcıya göre taktik sistemi değişmedi.

1. https://youtu.be/mMLcOQHnI1E
2. https://youtu.be/WIwRSNCn_5M
3. https://youtu.be/WoPgT_HnFQk
4. https://youtu.be/K_SwvoJKKOs
5. https://youtu.be/Eoymf9r2g58
6. https://youtu.be/aXBEmz69XPs
7. https://youtu.be/qLmV-m0SxhI
8. https://youtu.be/G9Ls5jjZw3s
9. https://youtu.be/fXaP2rdo0sQ
10. https://youtu.be/aW2NLCrPpQk (futbol maçı nasıl analiz edilir)
11. https://youtu.be/1PPIwZ1MSLw
12. https://youtu.be/bwsUNBYQsps
13. https://youtu.be/sMTN-ytGPRU
14. https://youtu.be/RwliIP39_Eo
15. https://youtu.be/XvFxQmiSRoI
16. https://youtu.be/cdGSnUsoUdo
17. https://youtu.be/sbJyQszXSMM

## 6. Sıradaki Adımlar

1. ~~Bellek okuma yöntemini doğrulamak~~ (tamamlandı, bkz. 4.1.1).
2. Gözlemci bilgisinin (hangi oyuncu ne kadar tanınıyor) bellekte nerede tutulduğunu
   araştırmak.
3. Taktik ve antrenman verisinin bellekte (yerel C++ veya Unity tarafında) yerini araştırmak.
4. Oyunun sterlin/euro kurunu bellekten okumanın yolunu bulmak.
5. Bilgisayarda Claude Code kurulu mu, hangi abonelik var? Yapay zekâ entegrasyonunu
   kesinleştirmek.
6. Video transkriptlerine erişimi denemek ve bilgi tabanının yapısını belirlemek.
7. Teknoloji yığınını (C# / .NET yönünde) kesinleştirip proje iskeletini kurmak.
