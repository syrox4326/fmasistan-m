using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using SI.Bindable;
using SI.Bindable.Reference.Core;
using SI.Core;
using SI.Interop;
using BKey = SI.Bindable.Bindings.Key;
using DateTime = System.DateTime;
using Il2CppList = Il2CppSystem.Collections.Generic.List<SI.Core.TypedValue>;
using PairList = Il2CppSystem.Collections.Generic.List<SI.Interop.InteropReference.Pair>;

namespace FMAsistan.Mantik
{
    /// <summary>
    /// Oyunun arayüz veri sisteminden değer okur. Kaynak olarak oyunun kendi kullandığı
    /// referansları (şablon) kullanır; bunlar dinleyici tarafından sürekli yakalanır ve
    /// okunan değerlerin içinden çıkan referanslar da eklenir.
    ///
    /// Komut: veri\oku.json
    ///   { "sure": 6, "istekler": [ { "kaynak": "TeamTacticReference", "yollar": ["Name", ...] },
    ///                              { "kaynak": "TeamReference:402654676", "yollar": [...] } ] }
    /// Sonuç: veri\oku\&lt;zaman&gt;.json ve veri\oku\son.txt
    ///
    /// Kaynak adı "Tür" (o türün son görülen örneği) ya da "Tür:kimlik" (DatabaseReferenceField
    /// değeri) olabilir. Yalnızca okur; oyunda hiçbir değeri değiştirmez.
    /// </summary>
    internal static class Okuyucu
    {
        private const uint DbAlani = 1685218662;     // DatabaseReferenceField
        private const uint OturumAlani = 1198877028;  // her referansta aynı; kimlik metnine yazılmaz
        private const int SablonSiniri = 5000;

        private static readonly Dictionary<string, InteropReference> Sablonlar = new();

        private sealed class Istek
        {
            public string Kaynak;
            public string Yol;
            public object Deger;
            public bool Geldi;
            public BKey Anahtar;
            public Bindings.ValueChangedCallback Geri;
        }

        private static readonly List<Istek> Bekleyenler = new();
        private static DateTime _bitis = DateTime.MinValue;
        private static Il2CppSystem.Object _sahip;

        public static int SablonSayisi => Sablonlar.Count;

        // --- Şablon yakalama (dinleyiciden, her istekte çağrılır) ---

        public static void Yakala(InteropReference r)
        {
            if (r == null) return;
            string tur = TurAdi(r);
            if (tur == null) return;
            Sablonlar[tur] = r;
            string db = DbKimlik(r);
            if (db != null && (Sablonlar.Count < SablonSiniri || Sablonlar.ContainsKey(tur + ":" + db)))
                Sablonlar[tur + ":" + db] = r;
        }

        public static void SablonlariYaz()
        {
            var liste = Sablonlar.Keys.OrderBy(k => k).ToList();
            Giris.DosyaYaz("sablonlar.json", JsonSerializer.Serialize(liste, new JsonSerializerOptions { WriteIndented = true }));
            Giris.Gunluk($"Şablon listesi yazıldı: {liste.Count} kayıt.");
        }

        // --- İstek ---

        public static void Baslat(string json)
        {
            Temizle();
            using var belge = JsonDocument.Parse(json);
            var kok = belge.RootElement;
            int sure = kok.TryGetProperty("sure", out var s) ? Math.Clamp(s.GetInt32(), 2, 60) : 6;

            var agac = Agac();
            if (agac == null) throw new InvalidOperationException("Veri ağacı (BindingSubsystem) bulunamadı.");
            _sahip ??= new Il2CppSystem.Object();

            int eksikKaynak = 0;
            foreach (var istek in kok.GetProperty("istekler").EnumerateArray())
            {
                string kaynakAdi = istek.GetProperty("kaynak").GetString();
                var kaynak = KaynakDeger(kaynakAdi);
                foreach (var y in istek.GetProperty("yollar").EnumerateArray())
                {
                    var i = new Istek { Kaynak = kaynakAdi, Yol = y.GetString() };
                    Bekleyenler.Add(i);
                    if (kaynak == null) { i.Deger = "!kaynak yok"; i.Geldi = true; eksikKaynak++; continue; }
                    try
                    {
                        i.Geri = (Action<BKey, TypedValue>)((k, v) => DegerGeldi(i, v));
                        i.Anahtar = BindingUtils.Request(agac, kaynak, i.Yol, _sahip, i.Geri);
                    }
                    catch (Exception e)
                    {
                        i.Deger = "!" + (e.InnerException ?? e).Message;
                        i.Geldi = true;
                    }
                }
            }
            _bitis = DateTime.Now.AddSeconds(sure);
            Giris.Gunluk($"Okuma başladı: {Bekleyenler.Count} istek ({eksikKaynak} kaynaksız), {sure} sn.");
        }

        public static void Tick()
        {
            if (_bitis == DateTime.MinValue) return;
            bool hepsiGeldi = Bekleyenler.All(b => b.Geldi);
            if (!hepsiGeldi && DateTime.Now < _bitis) return;
            _bitis = DateTime.MinValue;

            var sonuc = new Dictionary<string, Dictionary<string, object>>();
            foreach (var b in Bekleyenler)
            {
                if (!sonuc.TryGetValue(b.Kaynak, out var d)) sonuc[b.Kaynak] = d = new Dictionary<string, object>();
                d[b.Yol] = b.Geldi ? b.Deger : "!zaman aşımı";
            }
            string zaman = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Giris.DosyaYaz($"oku/{zaman}.json", JsonSerializer.Serialize(sonuc, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }));
            Giris.DosyaYaz("oku/son.txt", zaman);
            Giris.Gunluk($"Okuma bitti: {Bekleyenler.Count(b => b.Geldi)}/{Bekleyenler.Count} değer geldi.");
            Temizle();
        }

        public static void Temizle()
        {
            foreach (var b in Bekleyenler)
            {
                if (b.Geri == null) continue;
                try { var k = b.Anahtar; BindingUtils.UnbindIfValid(ref k, b.Geri); } catch { }
            }
            Bekleyenler.Clear();
        }

        private static void DegerGeldi(Istek i, TypedValue v)
        {
            try { i.Deger = Cevir(v, 0); }
            catch (Exception e) { i.Deger = "!" + e.Message; }
            i.Geldi = true;
        }

        // --- Yardımcılar ---

        private static Bindings Agac()
        {
            var alt = GameSubsystems.Get<BindingSubsystem>();
            return alt;
        }

        private static readonly Dictionary<string, (MethodInfo cast, MethodInfo create)> Yansimalar = new();

        /// <summary>Şablonu, oyunun beklediği somut türle bir TypedValue'ya sarar.</summary>
        private static TypedValue KaynakDeger(string ad)
        {
            if (!Sablonlar.TryGetValue(ad, out var r)) return null;
            string tur = TurAdi(r);
            if (!Yansimalar.TryGetValue(tur, out var y))
            {
                var tip = typeof(FM.UI.TeamReference).Assembly.GetType("FM.UI." + tur)
                          ?? AppDomain.CurrentDomain.GetAssemblies()
                                .Select(a => a.GetType("FM.UI." + tur) ?? a.GetType("SI.Interop." + tur))
                                .FirstOrDefault(t => t != null);
                if (tip == null) return null;
                var cast = typeof(Il2CppObjectBase).GetMethod(nameof(Il2CppObjectBase.TryCast)).MakeGenericMethod(tip);
                var create = typeof(TypedValue).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(m => m.Name == "Create" && m.IsGenericMethodDefinition).MakeGenericMethod(tip);
                Yansimalar[tur] = y = (cast, create);
            }
            var somut = y.cast.Invoke(r, null);
            return somut == null ? null : (TypedValue)y.create.Invoke(null, new[] { somut });
        }

        private static string TurAdi(InteropReference r)
        {
            try { return r.GetIl2CppType()?.Name; } catch { return null; }
        }

        private static string DbKimlik(InteropReference r)
        {
            PairList liste = r.m_id;
            if (liste == null) return null;
            for (int i = 0; i < liste.Count; i++)
                if (liste[i].m_id == DbAlani) return liste[i].m_value.ToString(CultureInfo.InvariantCulture);
            return null;
        }

        internal static string OzellikAdiKisa(uint id) => OzellikAdi(id);

        /// <summary>Referansın okunabilir adı: "Tür:kimlik" ya da "Tür{alan=değer,...}".</summary>
        internal static string KaynakAdi(InteropReference r)
        {
            if (r == null) return "(null)";
            string tur = TurAdi(r) ?? "?";
            string db = DbKimlik(r);
            if (db != null) return $"{tur}:{db}";
            var sb = new StringBuilder(tur).Append('{');
            PairList p = r.m_id;
            for (int i = 0; p != null && i < p.Count; i++)
                if (p[i].m_id != OturumAlani && OzellikAdi(p[i].m_id) != "UIRecordID")
                    sb.Append(OzellikAdi(p[i].m_id)).Append('=').Append(p[i].m_value).Append(',');
            return sb.Append('}').ToString();
        }

        private static string OzellikAdi(uint id)
        {
            string s;
            try { s = new PropertyID(id).ToString(); } catch { s = id.ToString(CultureInfo.InvariantCulture); }
            int iki = s.IndexOf(':');
            return iki > 0 ? s.Substring(0, iki) : s;
        }

        /// <summary>TypedValue → JSON'a yazılabilir .NET nesnesi.</summary>
        internal static object Cevir(TypedValue v, int derinlik)
        {
            if (v == null || v.IsNull) return null;
            Il2CppSystem.Object o = v.Get();
            if (o != null && derinlik < 8)
            {
                var liste = o.TryCast<Il2CppList>();
                if (liste != null)
                {
                    var l = new List<object>();
                    for (int i = 0; i < liste.Count && i < 200; i++)
                    {
                        try { l.Add(Cevir(liste[i], derinlik + 1)); } catch (Exception e) { l.Add("!" + e.Message); }
                    }
                    return l;
                }

                var r = o.TryCast<InteropReference>();
                if (r != null)
                {
                    Yakala(r); // sonraki okumalarda kaynak olarak kullanılabilsin
                    string tur = TurAdi(r) ?? "?";
                    string db = DbKimlik(r);
                    if (db != null) return $"{tur}:{db}";
                    var sb = new StringBuilder(tur).Append('{');
                    PairList p = r.m_id;
                    for (int i = 0; p != null && i < p.Count; i++)
                        if (p[i].m_id != OturumAlani) sb.Append(OzellikAdi(p[i].m_id)).Append('=').Append(p[i].m_value).Append(',');
                    return sb.Append('}').ToString();
                }

                var kayit = o.TryCast<Record>();
                if (kayit != null)
                {
                    var d = new Dictionary<string, object>();
                    var sozluk = kayit.Cast<Il2CppSystem.Collections.Generic.Dictionary<uint, TypedValue>>();
                    var e = sozluk.GetEnumerator();
                    while (e.MoveNext())
                    {
                        var kv = e.Current;
                        string ad = OzellikAdi(kv.Key);
                        try { d[ad] = Cevir(kv.Value, derinlik + 1); } catch (Exception ex) { d[ad] = "!" + ex.Message; }
                    }
                    return d;
                }
            }

            string metin;
            try
            {
                metin = o != null && o.GetIl2CppType().FullName == "System.String"
                    ? IL2CPP.Il2CppStringToManaged(o.Pointer)
                    : v.AsString();
            }
            catch { metin = v.AsString(); }
            if (metin == null) return null;
            metin = Sadelestir(metin);
            if (double.TryParse(metin, NumberStyles.Float, CultureInfo.InvariantCulture, out var sayi)) return Math.Round(sayi, 4);
            if (metin == "True") return true;
            if (metin == "False") return false;
            return metin;
        }

        /// <summary>FM metin işaretlemesini (\u0001bağlantı\u0002metin\u0001) düz metne indirger.</summary>
        private static string Sadelestir(string s)
        {
            if (s.IndexOf('\u0002') < 0) return s;
            var sb = new StringBuilder();
            int i = 0;
            while (i < s.Length)
            {
                int a = s.IndexOf('\u0001', i);
                if (a < 0) { sb.Append(s, i, s.Length - i); break; }
                sb.Append(s, i, a - i);
                int b = s.IndexOf('\u0002', a);
                int c = b < 0 ? -1 : s.IndexOf('\u0001', b + 1);
                if (b < 0 || c < 0) { sb.Append(s, a + 1, s.Length - a - 1); break; }
                sb.Append(s, b + 1, c - b - 1);
                i = c + 1;
            }
            return sb.ToString();
        }
    }
}
