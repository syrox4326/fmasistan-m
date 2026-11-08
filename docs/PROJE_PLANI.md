# FM Asistanım — Proje Planı

> Durum: Gereksinimler toplandı, geliştirmeye başlanmadı.
> Son güncelleme: 2026-11-08

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

Olası yol: Önce dışa aktarma ve ekran görüntüsüyle çalışan bir sürüm yapmak. Bellek okumayı
ayrı bir veri kaynağı modülü olarak sonradan eklemek. Analiz motoru, verinin nereden
geldiğinden bağımsız olacak şekilde tasarlanacak.

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

## 6. Yarın İlk Adımlar

1. FM27'de hangi verilerin dışa aktarılabildiğini kullanıcının bilgisayarında birlikte
   kontrol etmek (kadro, oyuncu öznitelikleri, taktik, rakip raporu, antrenman ekranı).
2. Bilgisayarda Claude Code kurulu mu, hangi abonelik var? Yapay zekâ entegrasyonunu
   kesinleştirmek.
3. Video transkriptlerine erişimi denemek ve bilgi tabanının yapısını belirlemek.
4. Teknoloji yığınını seçip proje iskeletini kurmak.
