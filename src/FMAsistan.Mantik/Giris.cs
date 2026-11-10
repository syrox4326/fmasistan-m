using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FMAsistan.Mantik
{
    /// <summary>
    /// Yükleyicinin çağırdığı giriş noktası. Tüm metotlar Unity ana iş parçacığında çalışır.
    ///
    /// Masaüstü uygulamasıyla iletişim, oyun klasöründeki BepInEx\FMAsistan\veri klasöründeki
    /// dosyalarla yapılır: uygulama bir "*.flag" dosyası bırakır, mantık okuyup siler ve
    /// sonucu yine bu klasöre yazar.
    /// </summary>
    public static class Giris
    {
        public const string Surum = "6";

        /// <summary>İletişim klasörü; yükleyici verir (oyun klasöründe BepInEx\FMAsistan\veri).</summary>
        internal static string Klasor { get; private set; }

        internal static Action<string> Gunluk = _ => { };

        private static readonly Dictionary<string, string> Hatalar = new();
        private static readonly Dictionary<string, int> HataSayisi = new();
        private static DateTime _sonKalp = DateTime.MinValue;

        /// <summary>
        /// Kancaların doğrulandığı oyun sürümleri (Application.version). Yeni bir yamada oyunun
        /// kodu değişir; doğrulanmamış sürümde kanca takmak oyunu çökertebilir (27.0.1'de oldu).
        /// Yeni sürüm: oyunu aç, logdaki sürümü gör, eklentiyi o sürümün arayüz dosyalarıyla
        /// derle, test et, buraya ekle.
        /// </summary>
        private static readonly string[] DogrulananSurumler = { "27.0.0.2493426", "27.0.1.2494769" };

        internal static bool KancaGuvenli { get; private set; }

        public static void Baslat(Action<string> gunluk, string klasor)
        {
            Gunluk = gunluk ?? (_ => { });
            Klasor = klasor;
            Directory.CreateDirectory(Klasor);

            string oyun = null;
            try { oyun = UnityEngine.Application.version; } catch { }
            KancaGuvenli = oyun != null && Array.IndexOf(DogrulananSurumler, oyun) >= 0;
            if (KancaGuvenli)
            {
                Guvenli("Dinleyici kurulumu", Dinleyici.Kur);
                Guvenli("Gözlem kurulumu", Gozlem.Kur);
            }
            else
            {
                Gunluk($"UYARI: Oyun sürümü ({oyun ?? "?"}) doğrulanmamış. Kancalar takılmadı; okuma ve dinleme kapalı. Eklentiyi bu sürüm için doğrulamak gerekiyor.");
            }
            Gunluk($"FM Asistanım mantığı v{Surum} yüklendi ({DateTime.Now:HH:mm:ss}), oyun {oyun ?? "?"}.");
        }

        public static void Durdur()
        {
            Guvenli("Okuyucu temizleme", Okuyucu.Temizle);
            Guvenli("Gözlem kaldırma", Gozlem.Kaldir);
            Guvenli("Dinleyici kaldırma", Dinleyici.Kaldir);
        }

        public static void Tick()
        {
            KalpYaz();
            if (!KancaGuvenli) return;
            Guvenli("Komutlar", Komutlar);
            Guvenli("Dinleyici", Dinleyici.Tick);
            Guvenli("Okuyucu", Okuyucu.Tick);
            Guvenli("Gözlem", Gozlem.Tick);
        }

        private static void Komutlar()
        {
            string dinle = Path.Combine(Klasor, "dinle.flag");
            if (File.Exists(dinle))
            {
                string icerik = File.ReadAllText(dinle).Trim();
                File.Delete(dinle);
                int saniye = int.TryParse(icerik, out var s) ? Math.Clamp(s, 5, 300) : 30;
                Dinleyici.Baslat(saniye);
            }

            string oku = Path.Combine(Klasor, "oku.json");
            if (File.Exists(oku))
            {
                string icerik = File.ReadAllText(oku);
                File.Delete(oku);
                Okuyucu.Baslat(icerik);
            }

            string gozle = Path.Combine(Klasor, "gozle.flag");
            if (File.Exists(gozle))
            {
                string icerik = File.ReadAllText(gozle).Trim();
                File.Delete(gozle);
                Gozlem.Baslat(int.TryParse(icerik, out var gs) ? Math.Clamp(gs, 5, 600) : 60);
            }

            // Sayısal özellik kimliklerini (PropertyID) oyunun iç adlarına çevirir.
            string kimlikler = Path.Combine(Klasor, "kimlik-adlari.json");
            if (File.Exists(kimlikler))
            {
                var liste = JsonSerializer.Deserialize<uint[]>(File.ReadAllText(kimlikler));
                File.Delete(kimlikler);
                var sonuc = new Dictionary<string, string>();
                foreach (var id in liste)
                {
                    try { sonuc[id.ToString()] = new SI.Bindable.Reference.Core.PropertyID(id).ToString(); }
                    catch (Exception e) { sonuc[id.ToString()] = "!" + e.Message; }
                }
                DosyaYaz("kimlik-adlari-sonuc.json", JsonSerializer.Serialize(sonuc, new JsonSerializerOptions { WriteIndented = true }));
            }

            // Deneme: oyunun çeviri sistemi. { "sayilar": [...], "metinler": [...] }
            string ceviri = Path.Combine(Klasor, "ceviri.json");
            if (File.Exists(ceviri))
            {
                using var belge = JsonDocument.Parse(File.ReadAllText(ceviri));
                File.Delete(ceviri);
                var sonuc = new Dictionary<string, string>();
                if (belge.RootElement.TryGetProperty("sayilar", out var sy))
                    foreach (var x in sy.EnumerateArray())
                    {
                        long id = x.GetInt64();
                        try { sonuc["#" + id] = (string)SI.Translation.TranslationManager.Translate(unchecked((int)id)); }
                        catch (Exception e) { sonuc["#" + id] = "!" + e.Message; }
                    }
                if (belge.RootElement.TryGetProperty("metinler", out var mt))
                    foreach (var x in mt.EnumerateArray())
                    {
                        string s = x.GetString();
                        try { sonuc[s] = (string)SI.Translation.TranslationManager.Format(s); }
                        catch (Exception e) { sonuc[s] = "!" + e.Message; }
                    }
                DosyaYaz("ceviri-sonuc.json", JsonSerializer.Serialize(sonuc, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                }));
            }

            string sablon = Path.Combine(Klasor, "sablonlar.flag");
            if (File.Exists(sablon))
            {
                File.Delete(sablon);
                Okuyucu.SablonlariYaz();
            }
        }

        internal static void Guvenli(string ad, Action is_)
        {
            try
            {
                is_();
                Hatalar.Remove(ad);
                HataSayisi.Remove(ad);
            }
            catch (Exception e)
            {
                var ic = e.InnerException ?? e;
                Hatalar[ad] = ic.GetType().Name + ": " + ic.Message;
                HataSayisi.TryGetValue(ad, out int n);
                HataSayisi[ad] = n + 1;
                if (n < 3) Gunluk($"Mantık hatası ({ad}): {ic}");
            }
        }

        /// <summary>Uygulamanın eklentinin canlı olduğunu görmesi için 5 sn'de bir yazılır.</summary>
        private static void KalpYaz()
        {
            if ((DateTime.UtcNow - _sonKalp).TotalSeconds < 5) return;
            _sonKalp = DateTime.UtcNow;
            try
            {
                string oyun = null;
                try { oyun = UnityEngine.Application.version; } catch { }
                DosyaYaz("kalp.json", JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["zaman"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ["mantik"] = Surum,
                    ["oyun"] = oyun,
                    ["dogrulandi"] = KancaGuvenli,
                    ["dinleniyor"] = Dinleyici.Aktif,
                    ["sablon"] = Okuyucu.SablonSayisi,
                    ["hatalar"] = Hatalar,
                }));
            }
            catch { }
        }

        /// <summary>Okuyucu yarım dosya görmesin diye önce geçici dosyaya yazıp taşır.</summary>
        internal static void DosyaYaz(string ad, string icerik)
        {
            string yol = Path.Combine(Klasor, ad);
            Directory.CreateDirectory(Path.GetDirectoryName(yol));
            File.WriteAllText(yol + ".tmp", icerik);
            File.Move(yol + ".tmp", yol, overwrite: true);
        }
    }
}
