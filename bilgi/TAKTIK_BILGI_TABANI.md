# FM Asistanım — Taktik Bilgi Tabanı

> Sürüm 2 (2026-11-10, 17 videonun tamamı işlendi). Kullanıcının verdiği videolardan çıkarılan ilkeler, kendi
> ifademizle özetlendi. Her ilke kaynağına (video kimliği) bağlıdır; ham transkript
> projede tutulmaz. Kaynak listesi en sonda.
>
> Bu belge iki yerde kullanılacak:
> 1. **Analiz motoru:** "Koşul → beklenen sonuç" biçimindeki kurallar (K-xx) deterministik
>    kontrollere dönüşür.
> 2. **Teknik ekip sohbeti:** Ekip üyeleri görüşlerini bu ilkelere dayandırır, ama kendi
>    futbol felsefelerine göre farklı ağırlık verir (her ilke herkes için aynı derecede doğru
>    değildir; örneğin bir gegenpress hocası K-30'u, bir pozisyonel oyun hocası K-10'u öne çıkarır).

---

## 1. FM27 motoru: taktiksel verimlilik sistemi

FM27'nin en büyük değişikliği. Eklentimiz bu veriyi doğrudan okuyor
(`TeamTacticReference.TacticalEffectiveness`, bkz. docs/VERI_YOLLARI.md).

### 1.1 Bileşenler (oyunun kendi yapısı)

Genel verimlilik puanı şu bileşenlerden oluşur (oyunda okunan alan adlarıyla):

| Bileşen | Alt faktörler | Ne belirler | Kullanıcının kontrolü |
|---|---|---|---|
| **Oyuncu yeteneği** (`PlayerAbility`) | Yeterlilik (`Capability`), rol uygunluğu (`RoleSuitability`) | Kadronun taktiği oynayabilecek kalitede olması ve oyuncuların rollerine uygunluğu | Kadro seçimi, rol değişikliği, transfer |
| **Oyuncu benimsemesi** (`PlayerBuyIn`) | Menajer desteği (`ManagerSupport`), menajere saygı, takım arkadaşı/personel ilişkileri, menajer eğilimleri | Oyuncuların taktiğe inanması | Yavaş; zamanla, ilişkilerle, başarıyla |
| **Taktik anlayışı** | Antrenmanla gelişen alanlar (pas stili, oyun kurma vb.), oyuncu taktik yeteneği, taktiksel karmaşıklık | Takımın taktiği ne kadar öğrendiği | Antrenman programı, talimat sayısı |
| **Sonraki maç bilgisi** (`NextMatchInformation`) | Rakip oyuncu bilgisi, rapor kalitesi (`QualityOfReport`) | Rakibe hazırlık | Gözlemci/analist kalitesi ve sorumluluk ataması |

Örnek okunan değerler (Galatasaray, 2026-11-10): genel 58; oyuncu yeteneği 66
(rol uygunluğu 97, yeterlilik 68); benimseme 60; sonraki maç bilgisi 70 (rapor kalitesi 85).

### 1.2 Kurallar

- **K-01 Antrenman–taktik bağı.** Antrenman, verimliliğe doğrudan bağlı. Dengeli/varsayılan
  haftalık programlar verimliliği neredeyse hiç artırmaz. Eksik alan (ör. "pas stili zayıf")
  için o alanın antrenmanı hafta boyunca **arka arkaya, yoğun** yapılmalı; haftada bir-iki kez
  dağınık yapmak birikmez. Bir deneyde bir hafta yoğun pas/oyun kurma antrenmanı aynı maçta
  dikine kilit pası ~%50, orta isabetini iki katına çıkardı. [WIwRSNCn_5M, WoPgT_HnFQk]
  → *Antrenman planlayıcı: zayıf bileşeni hedefleyen blok programlar üret; "dengeli" önerme.*
- **K-02 Öğrenme süresi.** Taktiği oturtma süresi = f(taktiksel karmaşıklık ↑, oyuncuların
  taktik yeteneği ↓). Zeki/yetenekli kadro daha hızlı öğrenir. [WIwRSNCn_5M]
- **K-03 Karmaşıklık ters orantılıdır.** Az oyuncu talimatı = düşük karmaşıklık = iyi.
  Gereksiz bireysel talimat (daha çok dripling yap, içeriden oyna...) karmaşıklığı artırır.
  [WoPgT_HnFQk] → *Taktik analizi: her talimatın getirisini sorgula, gereksizleri işaretle.*
- **K-04 Yeterlilik ≠ rol uygunluğu.** Oyuncu rolüne çok uygun olabilir ama takımın oyununa
  (ör. teknik kalite gerektiren pas oyunu) yeterli olmayabilir. 23–24 yaş üstü oyuncularda
  yeterlilik pek artmaz; gençlerde oynayarak artabilir. [WoPgT_HnFQk]
  → *Moneyball/kadro: yeterliliği düşük yaşlı oyuncu = değiştirme adayı; genç = sabır.*
- **K-05 Rol değişikliğinin maliyeti.** Oyuncunun doğal oyununa ters rol (içe kat eden
  kanada "çizgiye in, orta aç") rol uygunluğunu düşürür. Maç içi kısa çözüm olabilir, sezon
  boyu verim kaybı getirir. Bireysel antrenmanda taktikteki rolü çalıştırmak önemli. [WIwRSNCn_5M, WoPgT_HnFQk]
- **K-06 Menajere saygı ve profil.** Düşük nitelikli/itibarlı menajer profiliyle büyük
  kulübe gelmek benimsemeyi uzun süre düşürür; kulüp itibarıyla uyumlu profil saygıyı hızlı
  kazandırır. [WIwRSNCn_5M, WoPgT_HnFQk]
- **K-07 Eğilim tutarlılığı.** Menajer profilinde "hücum futbolu" seçip sürekli savunma
  oynamak oyuncu tepkisi doğurur; başarıyla zamanla aşılabilir. (Okunan veri: "Bazı oyuncular,
  hücum futbolu oynama eğiliminizden vazgeçmenize olumsuz tepki verdi.") [WIwRSNCn_5M]
- **K-08 İlişkiler.** Oyuncularla düzenli ilgilenmek, doğru kişilikte antrenörler ve gözlemci
  raporunda "takıma uyum sağlayabilir" notu olan transferler benimsemeyi artırır. [WIwRSNCn_5M]
- **K-09 Rakip raporu için personel.** Sonraki maç bilgisi yalnızca kaliteli gözlemci ve
  analistle yükselir. Sorumluluklar ekranında "rakip hakkında gözlemci raporu sunar" ve
  "rakip hakkında analiz raporu sunar" doğru kişilere atanmalı. [WoPgT_HnFQk, WIwRSNCn_5M]
  → *Gerçekçilik kuralıyla örtüşür: rapor kalitesi ekip kalitesine bağlı.*
- **K-10a Düşük verimliliğin maç belirtileri.** Verkaçlarda, koşu yoluna paslarda kopukluk,
  zayıf pres yerleşimi; zayıf rakiplere bile zorlanma. Yüksek verimlilikte pas ve top kapma
  keskinleşir. [WIwRSNCn_5M]
- **K-10b Bilinen motor sorunu (27.0).** Çok ofsayt ve savunma arkası toplarda geride kalan
  stoperler; SI ilk yamada savunma davranışı/pres üzerinde çalışıyor. Yama sonrası yeniden
  değerlendirilecek. [WIwRSNCn_5M]

---

## 2. Oyun kurma (build-up) yapıları

### 2.1 Temel kavram
Oyun kurma yalnızca topun değil oyuncuların hareketidir; amaç çoğu zaman **rakibi istenen
şekle sokmaktır**. "Rakip X yaparsa Y" formülasyonu. Hızlı (rakibi hazırlıksız yakala) ve
yavaş (her katı tamamlamadan ilerleme) kurulum vardır. [bwsUNBYQsps]

### 2.2 Şablonlar

| Yapı | Ne zaman | Gereksinim | Risk | Kaynak |
|---|---|---|---|---|
| **3-2-5** | Sakin, kontrollü top oyunu, arkada fazla adam (Guardiola) | İki merkez orta saha merkezde (kenarda değil) | Ön beşliye çok yük | mMLcOQHnI1E, cdGSnUsoUdo |
| **2-3-5** | Hızlı, ikinci top ve gegenpress odaklı ("rock'n'roll", Klopp/PSG) | **Ayağı iyi kaleci** (rakip 2'li presle iki stoperi kapatır); en az bir **çok hızlı stoper**; bek kademesini çok iyi yapan 6 numara | Stoperlerin kenarları boş | mMLcOQHnI1E, cdGSnUsoUdo |
| **3-1-6 (3-2-5'ten dönüşen)** | Dikine oyun, savunmayı 5'e 5 eşitlikten kurtarmak için geriden gelen ekstra adam | Çok kaliteli sahte 9; dikine oyun | Arkada sayıca eksik | mMLcOQHnI1E, bwsUNBYQsps |

- **K-11 Neden 3+2 / 2+3?** Çoğu takım 4-4-2 ile savunur/preslerse, üçlü kurulum her zaman bir
  adam fazlası verir; ön beşli dörtlü savunmaya karşı +1 yaratır, sahanın 5 dikey koridorunu
  doldurur. [cdGSnUsoUdo, sbJyQszXSMM]
- **K-12 Dinlenme savunması (rest defence).** 3+2 / 2+3 top kaybında kontra riskini azaltır.
  2+3'te ortadaki üç adam iç koridorları kapatır, kontra presi yoğunlaştırır, rakibi kenara
  iter (kenardan başlayan kontra yavaştır). Kural: **top bizdeyken genişle, rakipteyken daral.**
  [cdGSnUsoUdo, bwsUNBYQsps]
- **K-13 Roller dizilişten önemli.** Aynı kağıt dizilişi (4-3-3) ve aynı 3-2-5 / 2-3-5, farklı
  oyuncu tipleri ve rollerle çöker ya da çalışır. "Teknik direktör terzidir." Basit rotasyon
  (az oyuncunun yer değiştirdiği) sürdürülebilir; çok oyuncunun her atakta yer değiştirdiği
  karmaşık rotasyon (Arteta'nın ilk 4-2-3-1'i) kafa karışıklığı ve yanma yapar.
  [cdGSnUsoUdo] → *FM27'de K-03 ile birebir örtüşür.*

### 2.3 FM rol bilgisi (FM26/27 rol sistemi)
- **K-20 Oyun kurucu rolleri odak rolleridir.** Takıma "topu buna atın" der; tahmin
  edilebilirlik riski. Kroos/Trent gibi elit pasörde değerli, aksi halde az kullan. [mMLcOQHnI1E]
- **K-21 Stoperler geniş oynarsa** derin oyun kurucu aralarına iner → 3-2-5. Tek stopere geniş
  oyna verilirse o tarafa açılır, oyun kurucu ortada kalır. [mMLcOQHnI1E]
- **K-22 Çakışan rol çiftleri.** Kanat oyuncusu + kanat bek; dış kanat forvet + kanat bek
  (oyun kurulumunda aynı alanı kullanırlar). Çözüm: iç koridorda oynayan bek ya da
  half-space'i kapatan ileri orta saha. [mMLcOQHnI1E]
  → *Taktik analizi: rol çakışması kontrolü.*
- **K-23 Kanat forvetlerle** genelde derinleşen santrafor (sahte 9, pivot, yardımcı forvet)
  uyar; ama bitirici, pası zayıf santraforu içeride tutmak stoperleri sabitler, kanat
  forvetleri yay hattında boş bırakır (uzaktan şut iyiyse avantaj). Santraforun profiline göre
  seç. [mMLcOQHnI1E]
- **K-24 Serbest/gezgin roller boşluğa gider.** Serbest oyuncunun yeri taktiğin bıraktığı
  boşluğa göre değişir; iki yönlü orta saha ise kutu içinde kalır. [mMLcOQHnI1E]
- **K-25 Oyun kurmayı öldüren roller.** Risksiz oynayan kaleci + "topu uzaklaştıran" stoper
  kombinasyonu, hangi kurulumu seçersen seç oyun kurmayı imkânsız kılar (topu dikine vurur).
  Ayağı kötü oyuncuya da odak rolü verme; nötr kaleci/stoper rolleri kullan. [mMLcOQHnI1E]

---

## 3. Modern futbol kavramları (sözlük + analizde kullanım)

[bwsUNBYQsps ağırlıklı]

- **Half-space (yarım alan):** Sahayı dikine 5'e bölünce merkezle kenar arasındaki iki koridor.
  Rakip bek–stoper arasıdır. Büyük 5 ligdeki asistlerin ~%73'ü ceza sahasındaki yarım alan
  bölümünden (asist bölgesi) gelir; en sık asist örüntüsü **cutback** (geri çevirme).
  → *Yaratıcı oyuncuları yüzü kaleye dönük yarım alanda buluşturmak modern hedeftir.*
- **Zone 14 artık kapalı:** Merkez çok sıkı kapatıldığı için modern 10 numaralar yarım alanlara
  kaydı. Blok arasında oynayan oyuncu 360° görmeli ve çabuk oynamalı.
- **Pozisyonel oyun (juego de posición):** Saha kutucuklara bölünür; yatayda en fazla 3, dikeyde
  en fazla 2 oyuncu kuralı. Basit kural, karmaşık sistem. [bwsUNBYQsps]
- **Üçüncü adam koşusu:** Hedefe direkt değil bir ara istasyonla oynamak; savunmanın kör
  noktasına oynayıp kapalı savunmayı aşmanın en etkili yolu. [bwsUNBYQsps, G9Ls5jjZw3s]
- **Kanat rotasyonu / bek manipülasyonu:** Bek–kanat–10 üçgeninde yer değiştirerek savunmacıyı
  sürüklemek; overlap (dıştan), underlap (içten bindirme). Sahte bek içe kat edince rakip ya
  takip eder (kanat 1'e 1 kalır) ya etmez (orta sahada +1). [bwsUNBYQsps]
- **Geçiş ve gegenpress:** Takımın en savunmasız anı top kazanma/kaybetmeden sonraki birkaç
  saniye. Gegenpress topu kaybettiği yere 10–15 m koşup hemen geri kazanmaktır. Pres
  tetikleyicileri (ör. stoper topu zayıf ayağına aldığında, rakip kalesine döndüğünde).
  PPDA düşükse pres yırtıcıdır. [bwsUNBYQsps]
- **Kompaktlık:** Hatlar arası mesafeyi boydan ve enden azaltmak (Simeone'nin 4-4-2'si).
- **Metrikler:** xG, xGOT (kaleci performansı), progresif pas (≥20 m), progresif taşıma (≥10 m),
  hat kıran pas, deep completion (kaleye 20 m yarıçap içine tamamlanan pas), büyük fırsat,
  ortalama pozisyon (ısı haritasından daha bilgilendirici: topsuz hareketi de içerir).

---

## 4. Elit takım örnekleri (rakip ve kendi taktik analizinde örüntü)

### 4.1 Luis Enrique'nin PSG'si [sMTN-ytGPRU, RwliIP39_Eo]
- **K-30 Güçlü yöne yatırım.** Zaafları düzeltmek yerine oyuncuların güçlü yönlerine göre sistem
  kurmak (dripling ustası kanatlar için 1'e 1 alan yaratmak).
- **K-31 Genişlik = alan.** Sahayı çizgiye kadar kullanmak savunulacak alanı neredeyse ikiye
  katlar (basketboldaki spacing & isolation).
- **K-32 Aşırı yükleme ile merkezi boşaltmak.** Bir kanada 4. oyuncuyu (ters kanat veya 9) sokup
  rakibin markaj şemasını kaydırmak, merkezden adam çekmek; sonra boşalan alana koşu.
- **K-33 Dönerek oynayan orta saha.** 3+2'yi kuran oyuncular sürekli değişir; birebir eşleşmeyi
  imkânsız kılar. Tam saha adam adama prese karşı dripling ve rotasyon en etkili çözüm: bir
  çalım domino etkisi yaratır.
- **K-34 Çift ayaklı hücumcular** içe ve dışa çalımla savunmayı zorlar.
- **K-35 Faz esnekliği.** Önde top hakimiyeti, öne geçince derin savunma + geçiş; tek oyun planına
  bağlı kalmamak.
- **K-36 Kadro mühendisliği.** Yıldız yerine sisteme uyan takım oyuncusu (Campos'un transferleri;
  Vinícius yerine Kvaratskhelia). → *Moneyball modülünün felsefesiyle aynı.*

### 4.2 PSG'ye karşı Arsenal (antitez) [RwliIP39_Eo]
- **K-40 Aşırı yüklemeye karşı stoper switch'i.** Kanada gelen 4. adamı 6 numarayla değil kenar
  stoperle karşıla; açılan stoper arasını 6 numara doldursun. Rakibin topla yaptığı kaymayı
  topsuz kaymayla eşle.
- **K-41 Agresif adam adama presi delmek.** Derinleşen iki 10 numaralı bağlantı oyuncusu (Havertz,
  Ødegaard) stoperleri öne çekip arkada yarık açar.
- **K-42 Futbol anların oyunu.** Mükemmel plan bile duran top sonrası değişen eşleşmede kırılabilir;
  duran top sonrası organizasyon ayrı çalışılmalı.

### 4.3 De Zerbi'nin "boğayı davet et" kurulumu [G9Ls5jjZw3s]
- 2019'da kale vuruşu kuralı değişince stoperler ceza sahası içinde başlayabildi. Yüksek pres
  yapan takımda ön hat ile arka hat arası açılır.
- **K-50 Baskıyı davet et.** Stoperler topla bekleyip rakibi baskıya çeker, sonra baskıcının
  arkasındaki oyuncuya **üçüncü adam** üzerinden ulaşılır ("S" şekli). Alternatif: ters dönüşle
  baskıyı boşa çıkarmak.
- FM karşılığı: 4-2-3-1 / derin 4+2 kurulum, oyun kurucu kaleci, bir pivot derin oyun kurucu
  (üçüncü adam), pozitif mentalite, geriden oyna + presi pasla geç, kısa pas, yüksek tempo,
  son üçte birde karşı pres, yüksek savunma hattı + orta blok.
- **K-51 Hibrit pres antitezi.** Biri adamını bırakıp topa basınca bir başkası boşta kalan adamı
  hemen alır; uzun topa zorlanınca topun geldiği adam her yönden sıkıştırılır. Bir sistem çok
  yaygınlaştıkça antitezi ortaya çıkar.

### 4.4 Guardiola City'si [sbJyQszXSMM, cdGSnUsoUdo]
- 2-3-5: stoperler + içe kat eden bekler + 6 numara; iki 8 yarım alanlarda. Geri düşen
  hücumcuları takip etmek zordur; beşli hücum hattı dörtlü savunmaya +1 verir. Beşli savunmaya
  karşı etkisi azalır (West Ham: orta sahacı savunmaya düşerek eşledi).

### 4.5 Fàbregas'ın Como'su [K_SwvoJKKOs, qLmV-m0SxhI]
- **K-55 Dar kanatlar + çizgi bekleri = orta saha kutusu.** 4-2-3-1 / 4-2-2-2'de geniş oyuncular
  (doğası 8–10 olan oyuncular) içeri girip yarım alanlarda cep tutar; genişliği bekler verir.
  Orta sahada ~6 oyunculuk aşırı yük oluşur. Adam adama 4-4-2 presine karşı rakip bek içeri
  takip ederse arkasında koşu alanı açılır.
- **K-56 Baskıyı çek, hat atla.** Stoper baskıyı bekler; baskıcı gelince bir hattı atlayıp
  "10" gibi oynayan iki forvete ya da üçüncü adama oyna. İki pivot aynı hatta durmaz
  (üçgen için biri hafif derin). Topa sahip olmak amaç değil; amaç bir an önce son üçte bire varmak.
- **K-57 Bek profili.** Bu yapıda bekler teknik olarak parlak olmak zorunda değil ama genişte
  kalmayı isteyen, hızlı ve orta yapabilen oyuncular olmalı (FM: kanat bek + geniş kal + geniş sür).
- FM27 kurulumu (video): oyun kurucu kaleci; iki topla oynayan stoper; DM + derin oyun kurucu;
  iki ters kanat (dar dur, biri daha yüksek); ileri orta saha; fırsatçı forvet. Top rakipteyken
  4-1-4-1, süpürücü kaleci, perdeleyici. Takım talimatları: pozitif, kısa pas, standart tempo,
  dar, şekli koru, daha serbest, ceza sahasına kadar işle, şutu azalt, ortadan ilerle, ayağa pas,
  presi pasla geç, kısa degaj; yüksek hat (aşırı değil), daha çok öne çık, karşı pres, dışarı
  yönlendir tuzağı, ortaları engelle.
- **Rol değişikliği gözlemi:** Aynı oyuncuyu ters kanattan kanat oyuncusuna çevirmek başlangıç
  pozisyonunu genişletir, içe kat edişi bırakır ve beke bindirme alanı açar.

### 4.6 2025-26 taktik trendleri [aXBEmz69XPs]
Hepsi aynı soruna tepki: **hibrit adam adama pres** (çoğunlukla bölgesel 4-2-4, top bir tarafa
gidince tetikleyicilerle herkes adamını alır, kaleciye geri pas yolu kapatılır).
- **K-60 Çizgi bekleri geri döndü.** Genişliği bekler tutunca hücumda birçok "10" birbirine yakın
  oynar: kısa kombinasyonlar ve kaybedilen topa yakın çok adam. Arkada 3, önde 6–7 ayrımı.
- **K-61 Serbest (gezgin) bek.** Adam adama sistemde bek en kolay boşa çıkan oyuncudur (kanat onu
  sonuna kadar takip etmez); hücumu bitiren fazla adama dönüşür (Hakimi, Cucurella).
- **K-62 Bölünmüş forvetler ve yüksek kutu.** Genişliği bekler verirken iki merkez forvet + iki
  derin iç oyuncu = pres hattının arkasında kutu; son hatta 4 forvet aşırı yükü.
- **K-63 Akışkan üçlü savunma.** Rakip iki forvetle preslediğinde arkada +1 için 6 numara ya da bek
  geriye kayarak üçlü kurar.
- **K-64 Ortayı boşalt, çaprazı aç.** Kısa paslarla adam adama markajcıları kenarlara sürükleyip
  merkezi tamamen boşaltmak, sonra boş kalan oyuncuya uzun çapraz pas (PSG'nin Arsenal ve
  Liverpool golleri). Tuzağı kur, presi çek, patlat.

### 4.7 PSG–Barcelona: modern pres ve kırma ilkeleri [fXaP2rdo0sQ, 1PPIwZ1MSLw]
- **K-70 Adam odaklı yüksek pres, yüksek hat ister.** Hat orta saha çizgisinde değilse hatlar arası
  açılır, top sahibine zaman kalır ve sistem çözülür.
- **K-71 Kaleci (fazla adam) sorunu.** Stopere pas gidince karşı taraftaki oyuncu kaleciye geri pas
  yolunu kapatır; tüm takım top tarafına kayar, uzak kanattaki boş adam oyundan çıkar.
- **K-72 Tetikleyiciyle bölgeselden adam adamaya.** Dinlenik durumda bölgesel 4-2-3-1; kaleciye pas
  gibi bir tetikleyicide 10 saniye içinde herkes adamını alır. Rakip ilerledikçe yeniden bölgesele
  döner (önde 5 bastırır, arkada 5 korur).
- **K-73 Adam adamayı kırmak.** (a) Bekleri geniş ve stoperlere bağlı tutmak presi gerer;
  (b) ağır kayan prese karşı uzak kanattaki boş oyuncuya çapraz; (c) **dikey koşular ve rotasyon**
  (takip edilmeyen bek koşusu); (d) bire bir kazanmak: tek bir ikili mücadele kazanıldığında
  sistem domino gibi açılır.
- **K-74 Yüksekte top kazanmak uzaktan şut alanı açar.** Orta saha geri dönmeden savunma önünde boşluk
  oluşur; bu tip takımların şutlarının %30–40'ı ceza sahası dışından.
- **K-75 Topa sahip olmadan oyunu domine etmek mümkün.** Agresif savunma + geçiş, daha az topla
  daha fazla xG üretebilir.

### 4.8 Pozisyonel oyunun karar kuralları [XvFxQmiSRoI]
- **K-80 Yer tutmak bir eylemdir.** Doğru yerde durmak koşmaktan değerlidir; topa dokunmadan bir
  savunmacıyı yerinde sabitleyen oyuncu, arkadaşına alan açar. Top almak için yerini terk eden
  oyuncu kendini oyunda hisseder ama takıma zarar verir.
- **K-81 Kim çıkarsa ona göre oyna.** Stopere rakip kanat çıkarsa bek boştur; forvet çıkarsa diğer
  stoper boştur; 10 numara çıkarsa 6 numara boştur. Top boş adama gitmeli.
- **K-82 Disiplin sonra özgürlük.** Son üçte bire kadar pozisyon disiplini; orada yaratıcı oyunculara
  özgürlük ve çalım. Sistem, yıldızın topu en çok aldığı durumu yaratmak içindir.

---

## 4b. FM27 pratik ipuçları [Eoymf9r2g58]
- **K-90 Birincil antrenman taktiği.** Ana taktiği "birincil antrenman taktiği" yapmak antrenmanda o
  taktiğe odaklanmayı hızlandırır ve verimliliği artırır.
- **K-91 Bireysel rol antrenmanı (topa sahipken ve rakipteyken ayrı).** Rolde düşük yıldızlı oyuncuyu
  hem IP hem OOP rolünde bireysel antrenmana al; verimlilikteki rol uygunluğunu doğrudan besler.
- **K-92 Asistanın taktik değerlendirmesi.** Taktikler ekranındaki "tavsiye" rol çakışmalarını yakalar
  (ör. box-to-box oyun kurucu + box-to-box orta saha orta sahayı boş bırakır). Eklenti bu tavsiyeleri
  okuyabilir (`AdviceDropdownList`).
- **K-93 Yoğun taktikte antrenman yükü.** Yüksek yoğunluk sakatlık ve yorgunluk getirir; maç arası
  dinlendirme ve kondisyona göre yoğunluk ayarı formu korur.
- **K-94 Oyuncu mutluluğu verimliliğe girer.** Beklenen süre (ör. "rotasyon oyuncusu" yerine
  "düzenli ilk 11") oyuncuyu mutsuz etmeden ayarlanabilir; takım dinamikleri benimsemeyi etkiler.
- **K-95 Özel duran top rutinleri** oyuncuların pozisyona dönüşünü bozabilir; duran topları
  antrenöre devretmek bir seçenektir.
- **K-96 Transferde rol bazlı arama.** Gözlem odağı rol ve taktik bazlı kurulabilir (IP/OOP rol,
  asgari mevcut/potansiyel yıldız); Moneyball modülü aynı mantığı kullanır.
- **27.0 motor notu:** Kanat oyuncuları baskısız şekilde orta yapabiliyor; "ortaları engelle" geçici
  önlem olarak kullanılıyor. [K_SwvoJKKOs] (bkz. K-10b)

---

## 4c. Maç / rakip analizi yöntemi [aW2NLCrPpQk]
Rakip analizi modülünün iskeleti. Oyun 4 faz + duran top olarak incelenir:
1. **Topa sahipken**, üç bölgede:
   - *Oyun kurma:* şekil ve yapı; ilk hatta +1 ilkesi (rakip 1 forvetle preslerse 2, 2 forvetle
     preslerse 3 kişiyle kurmak).
   - *İlerleme:* pozisyon/yapı rotasyonları; bloğun etrafından mı, içinden mi, üstünden mi geçiyor;
     hatlar arasında yarı dönük oyuncular, dikey/çapraz paslar.
   - *Bitiriş:* kombinasyon mu doğrudan mı (orta + koşucular); bireysel özellikler (bek bindiriyor
     mu, orta saha cutback için geç mi geliyor, kanat içe kat edip orta mı yapıyor).
2. **Top rakipteyken**, 3. **hücum geçişi**, 4. **savunma geçişi**, 5. **duran toplar**.

---

## 5. Analiz motoru için kural özeti

| Kod | Kontrol | Girdi verisi |
|---|---|---|
| K-01 | Zayıf taktik anlayışı bileşeni var mı, antrenman programı onu hedefliyor mu? | `TacticalEffectiveness`, `CalendarReference.TrainingScheduleWeeks` |
| K-03 | Talimat sayısı / karmaşıklık yüksek mi? | `SelectedTeamInstructionOption`, oyuncu talimatları |
| K-04/05 | Oyuncu bazında yeterlilik ve rol uygunluğu düşük olanlar | `PlayerContextRoleAbility`, `TacticalEffectivenessForPlayer` |
| K-09 | Rakip raporu sorumlulukları atanmış mı, atanan kişinin kalitesi | Sorumluluklar, teknik ekip öznitelikleri |
| K-12 | Top kaybında arkada kaç kişi kalıyor (dinlenme savunması) | `TacticalPositionsCombined` (OOP/IP pozisyonları) |
| K-22 | Çakışan rol çiftleri | IP rolleri + pozisyonlar |
| K-25 | Oyun kurmayı öldüren kaleci/stoper rol kombinasyonu | IP rolleri |
| 2.2 | Seçilen kurulumun gereksinimleri karşılanıyor mu (2-3-5 → ayağı iyi kaleci, hızlı stoper) | Öznitelikler + roller |

---

## Kaynaklar

| Kimlik | Kanal | Başlık (kısaltılmış) | Durum |
|---|---|---|---|
| mMLcOQHnI1E | Futbol Gurmesi | 3-2-5 oyun kurulumu FM26'da nasıl yapılır | işlendi |
| WIwRSNCn_5M | Hoca Maçı Bitir | 27 saat FM27: hazır taktik devri bitti | işlendi |
| WoPgT_HnFQk | Taner Işık | FM27'de taktiksel başarı için bilmeniz gerekenler | işlendi |
| G9Ls5jjZw3s | DK Falcon | The tactic that is hacking modern football (De Zerbi) | işlendi |
| RwliIP39_Eo | Hastalık Bu Futbol | PSG–Arsenal ŞL finali taktik analiz | işlendi |
| bwsUNBYQsps | Hastalık Bu Futbol | Modern futbola giriş rehberi | işlendi |
| sMTN-ytGPRU | Hastalık Bu Futbol | PSG: Luis Enrique'nin şifreleri | işlendi |
| cdGSnUsoUdo | Socrates Dergi | 3-2-5, 4-4-2, diziliş mi roller mi (Katenaçyo #02) | işlendi |
| sbJyQszXSMM | SpreadSheet Soccer | Man City 2-3-5 explained | işlendi |
| K_SwvoJKKOs | Trequinho | Fàbregas'ın Como taktiği FM27'de | işlendi |
| Eoymf9r2g58 | GYR | The only FM27 guide you need | işlendi |
| aXBEmz69XPs | The Athletic FC | Futbolun sıradaki taktik trendleri | işlendi |
| qLmV-m0SxhI | Coaches' Voice | Fàbregas — building up to attack | işlendi |
| fXaP2rdo0sQ | Football Meta | PSG–Barça: modern futbolun örneği | işlendi |
| aW2NLCrPpQk | The Tactics Guy | Futbol maçı nasıl analiz edilir (1. bölüm) | işlendi |
| 1PPIwZ1MSLw | Football Meta | Barcelona–PSG: modern futbolun taktik planı | işlendi |
| XvFxQmiSRoI | FÚLBO | Juego de posición: Cruyff'tan Guardiola'ya (İspanyolca) | işlendi |
