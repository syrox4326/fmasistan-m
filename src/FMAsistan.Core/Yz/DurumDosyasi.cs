using System.Globalization;
using System.Text;
using FMAsistan.Core.Analiz;
using FMAsistan.Core.Veri;

namespace FMAsistan.Core.Yz;

/// <summary>
/// Oyundan okunan durumun yapay zekâya verilen metin dosyası. Teknik ekip sohbeti ve analiz
/// departmanı raporları aynı dosyayı kullanır: hepsi aynı veriye dayanır. Para euro ve yıllık.
/// </summary>
public static class DurumDosyasi
{
    public static string Yaz(TakimDurumu durum, IReadOnlyList<Bulgu> bulgular, KadroRaporu kadro)
    {
        var s = new StringBuilder();
        if (durum.Kaynaklar.Count > 0) s.AppendLine("Veri kaynakları: " + string.Join("; ", durum.Kaynaklar)).AppendLine();
        {
            s.AppendLine("## Oyundan okunan güncel durum");
            s.AppendLine($"- Taktik: {durum.Taktik.Ad} (mentalite: {durum.Taktik.Mentalite}" + (durum.Taktik.TaktikStili != null ? $", taktik stili: {durum.Taktik.TaktikStili}" : "") + ")");
            if (durum.Taktik.EnCokKullanilanDizilis != null) s.AppendLine($"- En çok kullanılan diziliş: {durum.Taktik.EnCokKullanilanDizilis}");
            var v = durum.TakimVerimliligi;
            s.AppendLine($"- Taktiksel verimlilik (0-100): genel {v.Genel}; oyuncu yeteneği {v.Yetenek.Genel} (yeterlilik {v.Yetenek.Yeterlilik}, rol uygunluğu {v.Yetenek.RolUygunlugu}); benimseme {v.Benimseme.Genel}; taktik anlayışı {v.Anlayis.Genel}" + (v.MacBilgisi != null ? $"; sonraki maç bilgisi {v.MacBilgisi.Genel}" : ""));
            s.AppendLine("- Taktik alanları: " + string.Join(", ", v.Anlayis.Alanlar.OrderBy(a => a.Value.Orta).Select(a => $"{TaktikAnalizi.AlanAdi(a.Key)} {a.Value}")));
            s.AppendLine($"- Karmaşıklık {v.Anlayis.Karmasiklik}, kavrayış {v.Anlayis.Kavrayis}");
            if (durum.Dizilis.Count > 0)
            {
                s.AppendLine("- İlk 11 (topa sahipken → top rakipteyken):");
                foreach (var p in durum.Dizilis)
                    s.AppendLine($"  - {p.Ad ?? p.OyuncuKimlik}: {Pozisyon.Ad(p.IpPozisyon)}, {p.IpRolu?.Ad ?? "?"} → {Pozisyon.Ad(p.OopPozisyon)}, {p.OopRolu?.Ad ?? "?"}");
            }
            var yedek = durum.Kadro.Where(k => k.Durum == KadroDurumu.Yedek).ToList();
            var disari = durum.Kadro.Where(k => k.Durum == KadroDurumu.KadroDisi).ToList();
            if (yedek.Count > 0) s.AppendLine("- Yedekler: " + string.Join(", ", yedek.Select(k => k.Ad ?? k.Kimlik)));
            if (disari.Count > 0) s.AppendLine("- Maç kadrosu dışında: " + string.Join(", ", disari.Select(k => k.Ad ?? k.Kimlik)));
            if (durum.Kadro.Count > 0) s.AppendLine("  Oyuncu değişikliği önerirken yalnızca yedekleri kullan; ilk 11'deki bir oyuncuyu 'oyuna alalım' deme.");
            foreach (var u in durum.Uyarilar) s.AppendLine($"- Oyun uyarısı: {u}");
            s.AppendLine();

            if (durum.Ayarlar.Count > 0)
            {
                s.AppendLine("## Taktik ekranındaki ayarlar (FM27'de var olanlar)");
                foreach (var a in durum.Ayarlar)
                    s.AppendLine($"- {a.Ayar}: {a.Deger}" + (a.Secenekler.Count > 0 ? $" (oyundaki seçenekler: {string.Join(", ", a.Secenekler)})" : ""));
                s.AppendLine("Takım talimatlarının seçili değerleri oyundan okundu; Türkçe adları yaklaşık çeviridir, oyundaki etiket birebir farklı olabilir. Oyunun her talimat için sunduğu tam seçenek listesi bilinmiyor: talimat değişikliğini 'Tempo: bir kademe düşür' ya da 'Pres yoğunluğu: talimatı kaldır' gibi talimat adı + yön olarak öner; seçenek adı uydurma. 'Talimat yok (standart)' o talimatın verilmediği anlamına gelir.");
                s.AppendLine();
            }

            var vm = durum.VeriMerkezi;
            if (!vm.BosMu)
            {
                s.AppendLine("## Veri Merkezi (kulübün analiz ekibinin oyundaki raporları, olduğu gibi)");
                if (vm.SonMac is { } sm)
                    s.AppendLine($"- Son maç: {sm.Mac ?? "?"}; xG {sm.Xg?.ToString("0.00") ?? "?"}, şut {sm.Sut?.ToString() ?? "?"} (isabetli {sm.IsabetliSut?.ToString() ?? "?"}). {sm.Degerlendirme}");
                foreach (var grup in vm.Notlar.GroupBy(n => n.Bolum))
                {
                    s.AppendLine($"### {grup.Key}");
                    foreach (var not in grup.Take(15))
                        s.AppendLine($"- {(not.Baslik != null ? not.Baslik + ": " : "")}{not.Metin.Replace("\n", " ")}");
                }
                if (vm.OyuncularSonMac.Count > 0)
                    s.AppendLine("### Son maç pas verileri\n" + string.Join("; ", vm.OyuncularSonMac.Select(o => $"{o.Ad ?? o.OyuncuKimlik} %{o.PasIsabeti}")));
                var sorunlu = vm.Izgara.Where(z => z.Durum.Contains("Sıkıntılı") && !z.Durum.Contains("Sıkıntısız")).ToList();
                if (vm.Izgara.Count > 0)
                    s.AppendLine($"### Taktik ızgarası: {vm.Izgara.Count} bölgenin {sorunlu.Count}'i sıkıntılı" +
                                 (sorunlu.Count > 0 ? ": " + string.Join(" | ", sorunlu.Select(z => $"{z.Ad} ({z.Durum}): {string.Join(" ", z.Yorumlar)}")) : ""));
                s.AppendLine("Bu metinler oyunun kendi analizidir. Yorumunu bunlara dayandır; maç istatistiği uydurma, burada olmayan bir sayıyı biliyormuş gibi yapma.");
                s.AppendLine();
            }

            if (durum.Rakip is { } rk)
            {
                s.AppendLine($"## Sıradaki rakip: {rk.Ad ?? rk.TakimKimlik} (analiz ekibimizin raporu)");
                if (rk.AnalistSeviyesi != null) s.AppendLine($"- Raporun güvenilirliği (analiz ekibimiz hakkında oyunun notu): {rk.AnalistSeviyesi}");
                if (rk.TehditDegerlendirmesi != null) s.AppendLine($"- Tehdit değerlendirmesi: {rk.TehditDegerlendirmesi}");
                if (rk.EnCokKullanilanDizilis != null) s.AppendLine($"- En çok kullandığı diziliş: {rk.EnCokKullanilanDizilis}");
                if (rk.MuhtemelOyunSistemi != null) s.AppendLine($"- Muhtemel oyun sistemi (rapordan): {rk.MuhtemelOyunSistemi}");
                if (rk.MuhtemelTaktik is { } mt)
                {
                    s.AppendLine($"- Muhtemel 11 (rapordan): {mt.Dizilis}" + (mt.Ozet != null ? $" — {mt.Ozet}" : ""));
                    foreach (var o in mt.Oyuncular)
                        s.AppendLine($"  - {Pozisyon.Ad(o.Pozisyon)}: {o.Ad ?? o.OyuncuKimlik}" + (o.Rolu != null ? $" ({o.Rolu.Ad})" : ""));
                }
                foreach (var grup in rk.Dizilisler.GroupBy(x => x.Faz))
                    s.AppendLine($"- {(grup.Key == Faz.TopaSahipken ? "Topa sahipken" : "Top rakipteyken")} kullandığı dizilişler: " +
                                 string.Join(", ", grup.Select(x => $"{x.Dizilis} (%{x.YuzdeKullanim:0}, {x.IlkOnbirMaci} maçta başladı)")));
                if (rk.GucluYonler.Count > 0)
                    s.AppendLine("- Gözlemci raporu, güçlü yönler: " + string.Join(" | ", rk.GucluYonler.Select(m => $"{m.Baslik}: {m.Metin}")));
                if (rk.ZayifYonler.Count > 0)
                    s.AppendLine("- Gözlemci raporu, zayıf yönler: " + string.Join(" | ", rk.ZayifYonler.Select(m => $"{m.Baslik}: {m.Metin}")));
                foreach (var grup in rk.Analiz.Notlar.GroupBy(x => x.Bolum))
                {
                    s.AppendLine($"### Rakip — {grup.Key}");
                    foreach (var not in grup.Take(12))
                        s.AppendLine($"- {(not.Baslik != null ? not.Baslik + ": " : "")}{not.Metin.Replace("\n", " ")}");
                }
                s.AppendLine("Rakip hakkında yalnızca bu rapordakileri bil. Analiz ekibimiz zayıfsa rapor da sınırlıdır; bunu hesaba kat ve raporda olmayanı uydurma.");
                s.AppendLine();
            }
        }


        if (durum.Profiller.Count > 0)
        {
            s.AppendLine("## Kadro profilleri (oyundan otomatik; kendi oyuncularımızın görünür değerleri)");
            s.AppendLine("Sütunlar: ad | durum | yaş | mevkiler | kondisyon | maç keskinliği | sakatlık riski (oyun değeri, yüksek = riskli) | sezon ort. (maç) | gol/asist | antrenman puanı | kişilik | maaş (€/yıl) | sözleşme");
            foreach (var o in durum.Profiller.OrderBy(o => o.Durum ?? KadroDurumu.KadroDisi))
            {
                string durumMetni = o.Durum switch { KadroDurumu.IlkOnbir => "ilk 11", KadroDurumu.Yedek => "yedek", KadroDurumu.KadroDisi => "kadro dışı", _ => "-" };
                s.AppendLine($"- {o.Ad} | {durumMetni} | {o.Yas?.ToString() ?? "?"} | {o.Mevkiler ?? "?"} | %{o.Kondisyon?.ToString() ?? "?"} | %{o.MacKeskinligi?.ToString() ?? "?"} | " +
                             $"{o.SakatlikRiski?.ToString() ?? "?"} | {(o.SezonOrtalamasi is > 0 ? $"{o.SezonOrtalamasi:0.00} ({o.Mac})" : "-")} | {o.Gol?.ToString() ?? "-"}/{o.Asist?.ToString() ?? "-"} | " +
                             $"{o.AntrenmanPuani?.ToString("0.0") ?? "-"} | {o.Kisilik ?? "-"} | {(o.MaasEuroYillik is > 0 ? Para.Euro(o.MaasEuroYillik.Value) : "-")} | {o.SozlesmeBitis ?? "-"}" +
                             (o.Durumlar.Count > 0 ? $" | durum: {string.Join(", ", o.Durumlar)}" : ""));
                if (o.Oznitelikler.Count > 0)
                    s.AppendLine("  öznitelikler: " + string.Join(", ", o.Oznitelikler.Select(x => $"{x.Key} {x.Value}")));
                if (o.Egilimler.Count > 0) s.AppendLine("  eğilimler: " + string.Join("; ", o.Egilimler));
            }
            s.AppendLine();
        }

        if (kadro.Slotlar.Count > 0)
        {
            s.AppendLine("## Slot uygunluk tablosu (oyunun rol yıldızları; 5 üzerinden, ekibin bilgisiyle sınırlı)");
            foreach (var sl in kadro.Slotlar)
                s.AppendLine($"- {sl.Slot}: {sl.Oyuncu} — {sl.Rol} {KadroAnalizi.Yildiz(sl.Mevcut)}" +
                             (sl.OyuncununEnIyiRolu != null && sl.OyuncununEnIyiRolu.RolKimlik != sl.Mevcut?.RolKimlik ? $"; oyuncunun bu pozisyondaki en iyi rolü {sl.OyuncununEnIyiRolu.Rol} {KadroAnalizi.Yildiz(sl.OyuncununEnIyiRolu)}" : "") +
                             (sl.Alternatifler.Count > 0 ? "; bu rol için alternatifler: " + string.Join(", ", sl.Alternatifler.Select(a => $"{a.Oyuncu.Ad} {KadroAnalizi.Yildiz(a.Yildiz)}")) : ""));
            s.AppendLine();
        }

        if (durum.TeknikEkip.Count > 0)
        {
            s.AppendLine("## Teknik ekip (oyundan otomatik)");
            foreach (var e in durum.TeknikEkip)
                s.AppendLine($"- {e.Ad} — {e.Gorev}" + (e.Lisans != null ? $", {e.Lisans}" : "") +
                             (e.Felsefe.Dizilis != null ? $"; tercih: {e.Felsefe.Dizilis}, {e.Felsefe.Mentalite}, pres {e.Felsefe.Pres}, markaj {e.Felsefe.Markaj}" : "") +
                             (e.Oznitelikler.Count > 0 ? "; " + string.Join(", ", e.Oznitelikler.Select(x => $"{x.Key} {x.Value}")) : ""));
            s.AppendLine();
        }

        if (durum.Antrenman.Atamalar.Count > 0)
        {
            s.AppendLine("## Antrenman: antrenör atamaları (oyunun yıldızları, 0-20)");
            foreach (var a in durum.Antrenman.Atamalar)
                s.AppendLine($"- {a.Kategori}: {a.Antrenor ?? "atanmamış"}" + (a.Yildiz != null ? $" ({a.Yildiz})" : ""));
            foreach (var k in durum.Antrenman.Kategoriler) s.AppendLine($"  - kategori içeriği — {k}");
            s.AppendLine();
        }

        if (durum.Kulup is { } ku)
        {
            s.AppendLine("## Kulüp");
            if (ku.TransferButcesiEuro is > 0) s.AppendLine($"- Transfer bütçesi: yaklaşık {Para.Euro(ku.TransferButcesiEuro.Value, yillik: false)}");
            if (ku.KalanMaasButcesi != null) s.AppendLine($"- Kalan maaş bütçesi: {ku.KalanMaasButcesi}");
            if (ku.MaasHarcamasi != null) s.AppendLine($"- Maaş harcaması: {ku.MaasHarcamasi}");
            if (ku.AntrenmanTesisi != null) s.AppendLine($"- Antrenman tesisi: {ku.AntrenmanTesisi}");
            if (ku.YonetimBeklentisi != null) s.AppendLine($"- Yönetim beklentisi: {ku.YonetimBeklentisi}");
            foreach (var x in ku.YonetimOvgu.Take(6)) s.AppendLine($"- Yönetim memnun: {x}");
            foreach (var x in ku.YonetimElestiri.Take(6)) s.AppendLine($"- Yönetim eleştiriyor: {x}");
            s.AppendLine();
        }

        if (durum.Fikstur.Siradakiler.Count > 0 || durum.Fikstur.LigSirasi != null)
        {
            s.AppendLine("## Fikstür");
            if (durum.OyunTarihi != null) s.AppendLine($"- Oyun tarihi: {durum.OyunTarihi}");
            if (durum.Fikstur.LigSirasi != null) s.AppendLine($"- Lig sırası: {durum.Fikstur.LigSirasi}");
            foreach (var m in durum.Fikstur.Siradakiler) s.AppendLine($"- {m.Ne}: {m.Rakip}");
            s.AppendLine();
        }

        var tumBulgular = bulgular.Concat(kadro.Bulgular).ToList();
        if (tumBulgular.Count > 0)
        {
            s.AppendLine("## Analiz departmanının bulguları (deterministik motor)");
            foreach (var b in tumBulgular)
            {
                s.AppendLine($"- [{b.Kural}, {b.Onem}] {b.Baslik}. Kanıt: {string.Join("; ", b.Kanit)}." + (b.Oneri != null ? $" Öneri: {b.Oneri}" : ""));
            }
            s.AppendLine("Bu bulgular analiz ekibinin ham çıktısıdır. Kendi felsefene ve bilgi seviyene göre yorumla; katılmadığın bulgu varsa nedenini söyle.");
            s.AppendLine();
        }

        return s.ToString();
    }
}

/// <summary>Para gösterimi: her zaman euro; maaşlar yıllık.</summary>
public static class Para
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string Euro(double deger, bool yillik = true)
    {
        string metin = deger >= 1_000_000 ? $"€{(deger / 1_000_000).ToString("0.##", Tr)} milyon"
                     : deger >= 1_000 ? $"€{(deger / 1_000).ToString("0.#", Tr)} bin"
                     : $"€{deger.ToString("0", Tr)}";
        return yillik ? metin + "/yıl" : metin;
    }
}
