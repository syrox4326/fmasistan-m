using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using FM.GamePlugin;
using HarmonyLib;
using SI.Bindable.Reference.Core;
using SI.Interop;
using BKey = SI.Bindable.Bindings.Key;

namespace FMAsistan.Mantik
{
    /// <summary>
    /// Keşif aracı: oyunun arayüzü native veritabanından veri isterken tek kapıdan geçer
    /// (GameInteropSubsystem.OpenChannel). Dinleme açıkken bu kapıya gelen her isteği
    /// (hangi nesne türü, hangi kimlikler, hangi özellik) kaydeder. Kullanıcı oyunda taktik,
    /// antrenman veya rakip ekranını açınca o ekranın kullandığı veri yolları ortaya çıkar.
    ///
    /// Yalnızca gözlemler; isteği değiştirmez veya engellemez.
    /// </summary>
    internal static class Dinleyici
    {
        private const string HarmonyId = "com.fmasistan.mantik.dinleyici";
        private const int SatirSiniri = 20000;

        private static Harmony _harmony;
        private static DateTime _bitis = DateTime.MinValue;
        private static readonly object Kilit = new();
        private static readonly StringBuilder Ham = new();
        // referans türü → özellik → sayaç
        private static readonly Dictionary<string, Dictionary<string, int>> Ozet = new();
        // referans türü → örnek kimlik dizisi (ilk görülen)
        private static readonly Dictionary<string, string> Ornek = new();
        private static int _satir;

        public static bool Aktif => _bitis != DateTime.MinValue;

        public static void Kur()
        {
            Harmony.UnpatchID(HarmonyId);
            _harmony = new Harmony(HarmonyId);
            var hedef = AccessTools.Method(typeof(GameInteropSubsystem), nameof(GameInteropSubsystem.OpenChannel));
            _harmony.Patch(hedef, prefix: new HarmonyMethod(typeof(Dinleyici), nameof(OnceOpen)));
            Giris.Gunluk("Dinleyici kuruldu (GameInteropSubsystem.OpenChannel).");
        }

        public static void Kaldir()
        {
            Harmony.UnpatchID(HarmonyId);
            _harmony = null;
        }

        public static void Baslat(int saniye)
        {
            lock (Kilit)
            {
                Ham.Clear();
                Ozet.Clear();
                Ornek.Clear();
                _satir = 0;
            }
            _bitis = DateTime.Now.AddSeconds(saniye);
            Giris.Gunluk($"Dinleme başladı ({saniye} sn).");
        }

        public static void Tick()
        {
            if (!Aktif || DateTime.Now < _bitis) return;
            _bitis = DateTime.MinValue;
            string zaman = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            lock (Kilit)
            {
                Giris.DosyaYaz($"dinle/{zaman}-ham.txt", Ham.ToString());
                var ozet = Ozet.ToDictionary(
                    t => t.Key,
                    t => (object)new Dictionary<string, object>
                    {
                        ["ornekKimlik"] = Ornek.TryGetValue(t.Key, out var o) ? o : null,
                        ["ozellikler"] = t.Value.OrderByDescending(x => x.Value)
                                                .ToDictionary(x => x.Key, x => x.Value),
                    });
                Giris.DosyaYaz($"dinle/{zaman}-ozet.json",
                    JsonSerializer.Serialize(ozet, new JsonSerializerOptions { WriteIndented = true }));
                Giris.DosyaYaz("dinle/son.txt", zaman);
            }
            Giris.Gunluk($"Dinleme bitti: {_satir} istek kaydedildi.");
        }

        // Harmony ön eki. Parametre adları hedef metotla aynı olmalı.
        private static void OnceOpen(InteropReference reference, PropertyID identifier, BKey key)
        {
            // Okuyucu için şablon: oyunun gerçekte kullandığı referanslar (her zaman).
            try { Okuyucu.Yakala(reference); } catch { }
            try { Gozlem.Kanal(reference, identifier, key); } catch { }

            if (!Aktif || DateTime.Now > _bitis || _satir >= SatirSiniri) return;
            try
            {
                string tur = TurAdi(reference);
                string kimlik = Kimlik(reference);
                string ozellik = OzellikAdi(identifier);
                lock (Kilit)
                {
                    Ham.Append(tur).Append(" [").Append(kimlik).Append("] ").AppendLine(ozellik);
                    if (!Ozet.TryGetValue(tur, out var d)) Ozet[tur] = d = new Dictionary<string, int>();
                    d.TryGetValue(ozellik, out int n);
                    d[ozellik] = n + 1;
                    if (!Ornek.ContainsKey(tur)) Ornek[tur] = kimlik;
                    _satir++;
                }
            }
            catch (Exception e)
            {
                lock (Kilit) Ham.AppendLine("HATA: " + e.Message);
            }
        }

        private static string TurAdi(InteropReference r)
        {
            if (r == null) return "(null)";
            try { return r.GetIl2CppType()?.Name ?? "?"; } catch { return "?"; }
        }

        private static string Kimlik(InteropReference r)
        {
            var liste = r?.m_id;
            if (liste == null) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < liste.Count; i++)
            {
                var p = liste[i];
                if (i > 0) sb.Append(' ');
                sb.Append(OzellikAdi(new PropertyID(p.m_id))).Append('=').Append(p.m_value);
            }
            return sb.ToString();
        }

        private static string OzellikAdi(PropertyID p)
        {
            try { return p.ToString(); } catch { return p.id.ToString(); }
        }
    }
}
