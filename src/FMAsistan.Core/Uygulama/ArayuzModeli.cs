using System.Text.Json;
using System.Text.Json.Serialization;
using FMAsistan.Core.Analiz;
using FMAsistan.Core.Veri;
using FMAsistan.Core.Yz;

namespace FMAsistan.Core.Uygulama;

/// <summary>
/// Arayüzün tek seferde aldığı görünüm modeli. Arayüz hesap yapmaz; adlar çözülmüş,
/// saha koordinatları hesaplanmış, metinler gösterilmeye hazırdır.
/// </summary>
public static class ArayuzModeli
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string JsonOlustur(TakimDurumu d, IReadOnlyList<Bulgu> bulgular, IReadOnlyList<EkipProfili> ekip, DateTime veriZamani)
        => JsonSerializer.Serialize(Olustur(d, bulgular, ekip, veriZamani), Json);

    public static object Olustur(TakimDurumu d, IReadOnlyList<Bulgu> bulgular, IReadOnlyList<EkipProfili> ekip, DateTime veriZamani)
    {
        var v = d.TakimVerimliligi;
        var kadro = KadroAnalizi.Calistir(d);
        bulgular = bulgular.Concat(kadro.Bulgular).OrderByDescending(b => b.Onem).ToList();
        return new
        {
            kaynaklar = d.Kaynaklar,
            oyunTarihi = d.OyunTarihi,
            profiller = d.Profiller.Select(o => new
            {
                kimlik = o.Kimlik, ad = o.Ad, yas = o.Yas, mevkiler = o.Mevkiler,
                durum = o.Durum switch { KadroDurumu.IlkOnbir => "İlk 11", KadroDurumu.Yedek => "Yedek", KadroDurumu.KadroDisi => "Kadro dışı", _ => null },
                kondisyon = o.Kondisyon, keskinlik = o.MacKeskinligi, risk = o.SakatlikRiski, moral = o.Moral,
                ortalama = o.SezonOrtalamasi, mac = o.Mac, gol = o.Gol, asist = o.Asist, antrenman = o.AntrenmanPuani,
                antrenmanYogunlugu = o.AntrenmanYogunlugu, mutluluk = o.AntrenmanMutlulugu, kisilik = o.Kisilik,
                maas = o.MaasEuroYillik is > 0 ? Para.Euro(o.MaasEuroYillik.Value) : null, sozlesme = o.SozlesmeBitis,
                deger = o.DegerAraligi, durumlar = o.Durumlar, egilimler = o.Egilimler,
                oznitelikler = o.Oznitelikler,
            }),
            slotlar = kadro.Slotlar.Select(s => new
            {
                slot = s.Slot, ip = s.TopaSahipken, oyuncu = s.Oyuncu, rol = s.Rol,
                mevcut = YildizJson(s.Mevcut), enIyiRol = s.OyuncununEnIyiRolu?.Rol, enIyi = YildizJson(s.OyuncununEnIyiRolu),
                alternatifler = s.Alternatifler.Select(a => new { ad = a.Oyuncu.Ad, yildiz = YildizJson(a.Yildiz), kondisyon = a.Oyuncu.Kondisyon }),
            }),
            teknikEkip = d.TeknikEkip.Select(e => new
            {
                ad = e.Ad, gorev = e.Gorev, yas = e.Yas, lisans = e.Lisans, kisilik = e.Kisilik,
                felsefe = new { dizilis = e.Felsefe.Dizilis, mentalite = e.Felsefe.Mentalite, pres = e.Felsefe.Pres, markaj = e.Felsefe.Markaj },
                oznitelikler = e.Oznitelikler, maas = e.MaasEuroYillik is > 0 ? Para.Euro(e.MaasEuroYillik.Value) : null,
            }),
            antrenman = new
            {
                atamalar = d.Antrenman.Atamalar.Select(a => new { kategori = a.Kategori, antrenor = a.Antrenor, yildiz = a.Yildiz is { } y ? P(y) : null }),
                kategoriler = d.Antrenman.Kategoriler,
            },
            kulup = d.Kulup is { } ku ? new
            {
                ad = ku.Ad, transfer = ku.TransferButcesiEuro is > 0 ? Para.Euro(ku.TransferButcesiEuro.Value, yillik: false) : null,
                kalanMaas = ku.KalanMaasButcesi, maasHarcamasi = ku.MaasHarcamasi, tesis = ku.AntrenmanTesisi,
                beklenti = ku.YonetimBeklentisi, ovgu = ku.YonetimOvgu, elestiri = ku.YonetimElestiri,
            } : null,
            fikstur = new { lig = d.Fikstur.LigSirasi, siradakiler = d.Fikstur.Siradakiler.Select(m => new { ne = m.Ne, rakip = m.Rakip }), form = d.Fikstur.SonForm },
            veriZamani = veriZamani.ToString("s"),
            takim = d.TakimAdi,
            taktik = new { ad = d.Taktik.Ad, mentalite = d.Taktik.Mentalite, stil = d.Taktik.TaktikStili, dizilis = d.Taktik.EnCokKullanilanDizilis },
            verimlilik = new
            {
                genel = P(v.Genel),
                bilesenler = new object[]
                {
                    new { ad = "Oyuncu yeteneği", puan = P(v.Yetenek.Genel), detay = $"Yeterlilik {v.Yetenek.Yeterlilik} · Rol uygunluğu {v.Yetenek.RolUygunlugu}", tavsiye = v.Yetenek.Tavsiye?.Metin },
                    new { ad = "Benimseme", puan = P(v.Benimseme.Genel), detay = $"Menajer desteği {v.Benimseme.MenajerDestegi} · Eğilimler {v.Benimseme.MenajerEgilimleri}", tavsiye = v.Benimseme.Tavsiye?.Metin },
                    new { ad = "Taktik anlayışı", puan = P(v.Anlayis.Genel), detay = $"Karmaşıklık {v.Anlayis.Karmasiklik} · Kavrayış {v.Anlayis.Kavrayis}", tavsiye = v.Anlayis.KavrayisTavsiyesi?.Metin },
                    new { ad = "Sonraki maç bilgisi", puan = v.MacBilgisi != null ? P(v.MacBilgisi.Genel) : null, detay = v.MacBilgisi != null ? $"Rapor kalitesi {v.MacBilgisi.RaporKalitesi} · Rakip bilgisi {v.MacBilgisi.RakipOyuncuBilgisi}" : null, tavsiye = v.MacBilgisi?.Tavsiye?.Metin },
                },
                alanlar = v.Anlayis.Alanlar.OrderBy(a => a.Key).Select(a => new { ad = TaktikAnalizi.AlanAdi(a.Key), puan = P(a.Value) }),
                alanTavsiyesi = v.Anlayis.AlanTavsiyesi?.Metin,
            },
            ilkOnbir = d.Dizilis.Select(p => new
            {
                ad = p.Ad ?? "?",
                ip = new { poz = Pozisyon.Ad(p.IpPozisyon), rol = p.IpRolu?.Ad, kisa = p.IpRolu?.Kisa, k = Saha.Koordinat(p.IpPozisyon) },
                oop = new { poz = Pozisyon.Ad(p.OopPozisyon), rol = p.OopRolu?.Ad, kisa = p.OopRolu?.Kisa, k = Saha.Koordinat(p.OopPozisyon) },
                verimlilik = d.Oyuncular.FirstOrDefault(o => o.OyuncuKimlik == p.OyuncuKimlik) is { } ov ? P(ov.Verimlilik.Genel) : null,
            }),
            yedekler = d.Kadro.Where(k => k.Durum == KadroDurumu.Yedek).Select(k => k.Ad ?? k.Kimlik),
            ayarlar = d.Ayarlar.Select(a => new { ad = a.Ayar, deger = a.Deger, secenekler = a.Secenekler }),
            bulgular = bulgular.Select(b => new { kural = b.Kural, onem = b.Onem.ToString(), baslik = b.Baslik, aciklama = b.Aciklama, kanit = b.Kanit, oneri = b.Oneri }),
            sonMac = d.VeriMerkezi.SonMac is { } s ? new { mac = s.Mac, xg = s.Xg, sut = s.Sut, isabetli = s.IsabetliSut, degerlendirme = s.Degerlendirme } : null,
            veriMerkezi = d.VeriMerkezi.Notlar.GroupBy(n => n.Bolum).Select(g => new { bolum = g.Key, notlar = g.Select(n => new { baslik = n.Baslik, metin = n.Metin }) }),
            sonMacPas = d.VeriMerkezi.OyuncularSonMac.Where(o => o.PasIsabeti != null).Select(o => new { ad = o.Ad ?? o.OyuncuKimlik, isabet = o.PasIsabeti, pas = o.IsabetliPas }),
            izgara = d.VeriMerkezi.Izgara.Select(z => new { satir = z.Satir, sutun = z.Sutun, durum = z.Durum, puan = z.Puan, ad = z.Ad, yorumlar = z.Yorumlar }),
            rakip = d.Rakip is { } r ? new
            {
                ad = r.Ad,
                tehdit = r.TehditDegerlendirmesi,
                analist = r.AnalistSeviyesi,
                dizilis = r.MuhtemelTaktik?.Dizilis ?? r.EnCokKullanilanDizilis,
                ozet = r.MuhtemelTaktik?.Ozet,
                oyunSistemi = r.MuhtemelOyunSistemi,
                muhtemelOnbir = r.MuhtemelTaktik?.Oyuncular.Select(o => new { ad = o.Ad ?? "?", poz = Pozisyon.Ad(o.Pozisyon), rol = o.Rolu?.Ad, kisa = o.Rolu?.Kisa, k = Saha.Koordinat(o.Pozisyon) }),
                dizilisler = r.Dizilisler.Select(x => new { faz = x.Faz == Faz.TopaSahipken ? "Topa sahipken" : "Top rakipteyken", dizilis = x.Dizilis, yuzde = Math.Round(x.YuzdeKullanim), ilkOnbir = x.IlkOnbirMaci }),
                guclu = r.GucluYonler.Select(m => new { baslik = m.Baslik, metin = m.Metin }),
                zayif = r.ZayifYonler.Select(m => new { baslik = m.Baslik, metin = m.Metin }),
                notlar = r.Analiz.Notlar.GroupBy(n => n.Bolum).Select(g => new { bolum = g.Key, notlar = g.Select(n => new { baslik = n.Baslik, metin = n.Metin }) }),
            } : null,
            ekip = ekip.Select(e => new
            {
                kimlik = e.Kimlik,
                ad = e.Uye.Ad,
                gorev = e.Uye.Gorev,
                felsefe = new { dizilis = e.Uye.Felsefe.Dizilis, mentalite = e.Uye.Felsefe.Mentalite, pres = e.Uye.Felsefe.Pres, markaj = e.Uye.Felsefe.Markaj },
                lisans = e.Uye.Lisans,
            }),
        };
    }

    private static object? YildizJson(RolYildizi? y) => y == null ? null
        : new { min = y.Deger.Min / 4.0, max = y.Deger.Max / 4.0, gozlemci = y.GozlemciGorusu, metin = KadroAnalizi.Yildiz(y) };

    /// <summary>Puan: aralıklıysa iki değer, değilse tek değer.</summary>
    private static object P(Puan p) => p.Aralikli ? new { min = p.Min, max = p.Max } : new { min = p.Min, max = p.Max, tek = true };
}

/// <summary>Pozisyon maskesinden saha koordinatı (x: soldan sağa 0–100, y: kendi kalemizden rakibe 0–100).</summary>
public static class Saha
{
    public static object Koordinat(ulong maske)
    {
        (double x, double y) = Temel(maske);
        bool merkez = x is > 30 and < 70;
        if (merkez && (maske & (1UL << 20)) != 0) x -= 16;
        if (merkez && (maske & (1UL << 21)) != 0) x += 16;
        return new { x = Math.Round(x, 1), y = Math.Round(y, 1) };
    }

    private static (double, double) Temel(ulong m) =>
        (m & 1) != 0 ? (50, 5) :
        (m & 4) != 0 ? (88, 28) :
        (m & 8) != 0 ? (12, 28) :
        (m & 16) != 0 ? (50, 21) :
        (m & 32) != 0 ? (90, 42) :
        (m & 64) != 0 ? (10, 42) :
        (m & 128) != 0 ? (50, 38) :
        (m & 256) != 0 ? (88, 56) :
        (m & 512) != 0 ? (12, 56) :
        (m & 1024) != 0 ? (50, 52) :
        (m & 2048) != 0 ? (84, 74) :
        (m & 4096) != 0 ? (16, 74) :
        (m & 8192) != 0 ? (50, 70) :
        (m & 16384) != 0 ? (50, 88) : (50, 50);
}
