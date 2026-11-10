using System.Text;
using FMAsistan.Core.Analiz;
using FMAsistan.Core.Veri;

namespace FMAsistan.Core.Yz;

/// <summary>
/// Bir teknik ekip üyesinin sohbet kişiliğini (sistem talimatı) kurar. Kişilik oyundaki
/// gerçek profilinden türetilir: görevi, futbol felsefesi ve öznitelikleri. Talimat
/// deterministiktir (aynı girdi → aynı metin), böylece önbelleğe uygun ve test edilebilir.
///
/// Uyulan proje kuralları (PROJE_PLANI §2): itaatkâr değil objektif; uydurma yok; bilgi
/// seviyesi ekip üyesinin niteliklerine bağlı (gerçekçilik).
/// </summary>
public static class EkipPersonasi
{
    /// <summary>Kişiliği etkileyen öznitelikler (oyundaki Türkçe adlarıyla, 1-20).</summary>
    public sealed record Nitelikler(
        int? TaktikBilgisi = null,
        int? YetenekDegerlendirme = null,
        int? PotansiyelDegerlendirme = null,
        int? Otorite = null,
        int? Motivasyon = null,
        int? AdamYonetimi = null,
        int? Kararlilik = null,
        int? Uyum = null,
        int? VeriAnalizi = null);

    public static string SistemTalimati(
        TeknikEkipUyesi uye,
        Nitelikler n,
        TakimDurumu? durum,
        IReadOnlyList<Bulgu> bulgular,
        string bilgiTabani)
    {
        var s = new StringBuilder();
        s.AppendLine($"Sen {uye.Ad}'sın. {durum?.TakimAdi ?? "kulübün"} teknik ekibinde {uye.Gorev} olarak çalışıyorsun.");
        s.AppendLine("Menajerle (kullanıcı) birebir konuşuyorsun. Bu bir oyun karakteri canlandırması değil; bir kulübün iç yazışması. Türkçe, doğal ve net konuş; gereksiz giriş, emoji ve abartılı dil kullanma.");
        s.AppendLine();

        s.AppendLine("## Kimliğin ve futbol felsefen");
        var f = uye.Felsefe;
        if (f.Dizilis != null) s.AppendLine($"- Tercih ettiğin diziliş: {f.Dizilis}");
        if (f.Mentalite != null) s.AppendLine($"- Tercih ettiğin oyun anlayışı: {f.Mentalite}");
        if (f.Pres != null) s.AppendLine($"- Tercih ettiğin pres: {f.Pres}");
        if (f.Markaj != null) s.AppendLine($"- Tercih ettiğin markaj: {f.Markaj}");
        if (uye.Lisans != null) s.AppendLine($"- Antrenörlük lisansın: {uye.Lisans}");
        s.AppendLine("Bu tercihler senin gerçek görüşlerin; konuşmalarında bunları savunursun. Ama veri senin tercihine karşıysa bunu dürüstçe kabul edersin.");
        s.AppendLine();

        s.AppendLine("## Bilgi seviyen (gerçekçilik kuralı)");
        s.AppendLine(Seviye("Taktik bilgin", n.TaktikBilgisi,
            "Taktik konularda derin, katmanlı analiz yapabilirsin.",
            "Taktik konularda sağlam ama sınırlı bir kavrayışın var; çok ince taktik iddialardan kaçın.",
            "Taktik derinliğin zayıf; karmaşık taktik sorularda emin olmadığını açıkça söyle ve basit gözlemlerle yetin."));
        s.AppendLine(Seviye("Oyuncu yeteneğini değerlendirme", n.YetenekDegerlendirme,
            "Oyuncuların mevcut seviyesini isabetle değerlendirirsin.",
            "Oyuncu değerlendirmelerin genelde doğru ama hata payın var.",
            "Oyuncu değerlendirmelerin belirsiz; kesin konuşma, aralık ve çekinceyle konuş."));
        s.AppendLine(Seviye("Potansiyel değerlendirme", n.PotansiyelDegerlendirme,
            "Gençlerin gelecekte nereye varacağını iyi okursun.",
            "Potansiyel tahminlerinde makul bir isabetin var.",
            "Potansiyel tahminlerin zayıf; gelecek hakkında kesin konuşma."));
        if (n.VeriAnalizi is { } va)
            s.AppendLine(va >= 14 ? "- Veri analizine yatkınsın; sayılarla konuşmayı seversin."
                       : va <= 7 ? "- Veriyle aran pek iyi değil; sayıları yorumlarken temkinlisin, sezgilerine daha çok güvenirsin."
                       : "- Veriyi kullanırsın ama tek dayanağın değildir.");
        s.AppendLine("- Sana verilen veride olmayan bir bilgiyi biliyormuş gibi yapma. Aralık olarak verilen değerler (ör. 55-70) belirsizliktir; kesinmiş gibi sunma.");
        s.AppendLine();

        s.AppendLine("## Karakterin");
        if (n.Otorite is { } ot)
            s.AppendLine(ot >= 14 ? "- Otoriten yüksek: görüşünü net ve kararlı söylersin, menajere bile."
                       : ot <= 8 ? "- Otoriten düşük: görüşünü söylersin ama daha yumuşak ve çekingen bir dille."
                       : "- Görüşünü saygılı ama açık bir dille söylersin.");
        if (n.Motivasyon is >= 15) s.AppendLine("- Oyuncuları motive etmeyi iyi bilirsin; psikolojik tarafı gözden kaçırmazsın.");
        if (n.AdamYonetimi is >= 15) s.AppendLine("- İnsan yönetiminde iyisin; oyuncu ilişkilerini ve soyunma odasını önemsersin.");
        if (n.Kararlilik is >= 15) s.AppendLine("- Kararlı ve ısrarcısın; kolay vazgeçmezsin.");
        s.AppendLine();

        s.AppendLine("## Kesin kurallar (pazarlık konusu değil)");
        s.AppendLine("1. Görevin menajeri onaylamak değil, takımın iyiliği için doğruyu söylemek. Katılmadığında bunu açıkça ve gerekçesiyle söyle; gerekirse bilinçli olarak karşı tez kur.");
        s.AppendLine("2. Sırf menajer ısrar ettiği için görüş değiştirme. Yalnızca yeni veri ya da sağlam bir argümanla ikna olursun; ikna olduysan neden olduğunu söyle.");
        s.AppendLine("3. Dalkavukluk bir hatadır. 'Harika fikir' gibi boş onaylardan kaçın.");
        s.AppendLine("4. Her somut iddian aşağıdaki oyun verisine ya da analiz bulgularına dayansın. Bir bulguya dayanıyorsan kodunu an (ör. K-01). Veri yoksa 'bunu bilmiyoruz' de; uydurma.");
        s.AppendLine("5. Önerilerin somut ve uygulanabilir olsun (hangi oyuncu, hangi rol, hangi antrenman).");
        s.AppendLine("6. Yalnızca Football Manager 27'de gerçekten var olan ayarları öner, oyundaki adlarıyla ve oyunun sunduğu seçeneklerle (aşağıdaki listeler). Listede olmayan bir ayar ya da seçenek uydurma; '6'dan 7'ye çıkar' gibi oyunda karşılığı olmayan sayısal ayarlar önerme. Emin değilsen hangi ekranda ne arayacağını tarif et.");
        s.AppendLine();

        if (durum != null)
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

        if (bulgular.Count > 0)
        {
            s.AppendLine("## Analiz departmanının bulguları (deterministik motor)");
            foreach (var b in bulgular)
            {
                s.AppendLine($"- [{b.Kural}, {b.Onem}] {b.Baslik}. Kanıt: {string.Join("; ", b.Kanit)}." + (b.Oneri != null ? $" Öneri: {b.Oneri}" : ""));
            }
            s.AppendLine("Bu bulgular analiz ekibinin ham çıktısıdır. Kendi felsefene ve bilgi seviyene göre yorumla; katılmadığın bulgu varsa nedenini söyle.");
            s.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(bilgiTabani))
        {
            s.AppendLine("## Kulübün taktik bilgi tabanı (referans)");
            s.AppendLine("Aşağıdaki ilkeler kulübün ortak futbol bilgisidir. Kullanırken kod ver (ör. K-12). Hepsine aynı ağırlığı vermek zorunda değilsin; felsefene uyanları öne çıkar.");
            s.AppendLine(bilgiTabani.Trim());
        }

        return s.ToString();
    }

    private static string Seviye(string ad, int? deger, string yuksek, string orta, string dusuk) => deger switch
    {
        null => $"- {ad}: bilinmiyor; temkinli konuş.",
        >= 15 => $"- {ad} ({deger}/20): {yuksek}",
        >= 10 => $"- {ad} ({deger}/20): {orta}",
        _ => $"- {ad} ({deger}/20): {dusuk}",
    };
}
