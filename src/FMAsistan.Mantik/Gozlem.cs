using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FM.GamePlugin;
using HarmonyLib;
using SI.Bindable.Reference.Core;
using SI.Core;
using SI.Interop;
using BKey = SI.Bindable.Bindings.Key;
using DKey = SI.Bindable.Bindings.DataKey;
using DateTime = System.DateTime;

namespace FMAsistan.Mantik
{
    /// <summary>
    /// Pasif yakalama: arayüzün native taraftan aldığı değerleri, hangi referansın hangi
    /// özelliği olduğuyla birlikte saklar. İki mod:
    ///
    /// 1. <b>Sürekli izleme</b> (varsayılan açık): yalnızca <see cref="IzlenenOzellikler"/>
    ///    listesindeki özellikler. Kullanıcı oyunda Veri Merkezi'ni, taktik ya da antrenman
    ///    ekranını her açtığında en güncel değer veri\canli.json'a yazılır. Bu özelliklerin
    ///    çoğu yalnızca ekran açıkken hesaplanır; etkin okuma ile alınamaz.
    /// 2. <b>Zamanlı tam gözlem</b> (keşif için): veri\gozle.flag (saniye) → o süre boyunca
    ///    her özellik veri\gozlem\&lt;zaman&gt;.json'a.
    ///
    /// Akış (oyunun kendi veri sistemi):
    ///   OpenChannel(referans, özellik, Key) → SetTarget(Key, DataKey) → SetData(DataKey, değer)
    ///
    /// Kancalar yalnızca doğrulanmış oyun sürümünde takılır (Giris.KancaGuvenli); 27.0.1
    /// yamasında eski arayüz dosyalarıyla takılan kanca oyunu çökertmişti. Listede olmayan bir
    /// özellik için kancanın maliyeti tek bir sözlük kontrolüdür. Yalnızca gözlemler.
    /// </summary>
    internal static class Gozlem
    {
        private const string HarmonyId = "com.fmasistan.mantik.gozlem";
        private const int DegerSiniri = 20000;
        private const double CanliYazmaAraligiSn = 5;

        /// <summary>Sürekli izlenen özellikler (bkz. docs/VERI_YOLLARI.md).</summary>
        internal static readonly HashSet<string> IzlenenOzellikler = new()
        {
            // Veri Merkezi
            "KeyFindingsVisuals", "LastMatchAnalysis", "TeamAnalytics", "PlayerAnalytics",
            "ExpectedFormations", "GoalAnalysisScoring", "GoalAnalysisConceding",
            "SetPieceAnalysis", "SetPieceDataInsights", "ActionZonesVisualisation", "HeatMap",
            "DataHubLastMatchFixtureDropdown",
            // Taktik
            "TacticsGrid", "TacticalEffectiveness", "TacticalPositionsCombined", "TacticalPositions",
            "TacticalStyleDropdown", "MentalityDropdown", "FormationName", "PartnershipScoresForLines",
            "NextOppositionInstructions", "AdviceDropdownList",
            // Rakip raporu
            "KeyFindings", "NextOppositionThreatLevelAssessment", "TeamReportSquadDepth",
            "PreviousMeetings", "RecentFormValues", "HasTeamReport", "DataAnalystLevelInfo",
            "ExpectedTactic", "ExpectedTacticalStyle", "ScoutReportStrengths", "ScoutReportWeaknesses",
            // Antrenman
            "TrainingScheduleWeeks", "TrainingScheduleName", "CalendarWeekAsList", "CurrentTrainingSession",
            "CoachAssignments", "TrainingIntensitySettingsForConditionRanges",
            // Kadro
            "FilteredPlayers", "FilteredSubs", "SquadPlayers", "NextFixture",
            // Takım talimatları (seçili değerler)
            "BuildUpStrategy", "PassingDirectness", "Tempo", "TacticWidth", "CreativeFreedom", "Dribbling",
            "PassReception", "Patience", "ShotsFromDistance", "CrossingStyle", "SupportingRuns",
            "SupportingRunsSecondary", "ProgressThrough", "TimeWasting", "StoppageStrategy",
            "AttackingTransition", "DefensiveTransition", "LineOfEngagement", "DefensiveLine",
            "DefensiveLineBehaviour", "PressingTrigger", "PressingTrap", "Tackling", "PreventGKShort",
            "GoalKicks", "GoalkeeperDistributionSpeed", "GoalkeeperDistributionTarget",
        };

        private static readonly Dictionary<ulong, string> KeyKaynak = new();      // Key → "Tür:kimlik|Özellik"
        private static readonly Dictionary<ulong, List<ulong>> DataKeyler = new(); // DataKey → Key'ler

        // Zamanlı tam gözlem
        private static DateTime _bitis = DateTime.MinValue;
        private static readonly Dictionary<string, object> Degerler = new();
        private static int _sayac;

        // Sürekli izleme: "Tür:kimlik" → özellik → { deger, zaman }
        private static Dictionary<string, Dictionary<string, Dictionary<string, object>>> _canli;
        private static bool _canliKirli;
        private static DateTime _sonCanliYazma = DateTime.MinValue;
        private static bool _kancaTakili;

        public static bool Aktif => _bitis != DateTime.MinValue;

        // SetData kancası oyunun açılışında takılmaz: oyun yerel veritabanını başlatırken SetData'yı
        // kancanın güvenle karşılayamadığı bir durumda çağırıyor ve oyun çöküyor (2026-11-11,
        // ErrorLog: AccessViolation in DMD<GameInteropSubsystem::SetData>). Kanca, oyunun arayüzü
        // ilk takımı gösterdikten (başlangıç bitti) 15 sn sonra takılır.
        private const double KancaBeklemeSn = 15;

        public static void Kur()
        {
            CanliYukle();
            Giris.Gunluk($"Sürekli izleme hazır ({IzlenenOzellikler.Count} özellik); kanca oyun açıldıktan sonra takılacak.");
        }

        public static void Kaldir()
        {
            Harmony.UnpatchID(HarmonyId);
            _kancaTakili = false;
            try { CanliYaz(); } catch { }
        }

        private static void KancalariTak()
        {
            Harmony.UnpatchID(HarmonyId);
            var h = new Harmony(HarmonyId);
            var t = typeof(GameInteropSubsystem);
            h.Patch(AccessTools.Method(t, nameof(GameInteropSubsystem.SetTarget)), prefix: new HarmonyMethod(typeof(Gozlem), nameof(OnceSetTarget)));
            h.Patch(AccessTools.Method(t, nameof(GameInteropSubsystem.SetData)), prefix: new HarmonyMethod(typeof(Gozlem), nameof(OnceSetData)));
            _kancaTakili = true;
        }

        public static void Baslat(int saniye)
        {
            Degerler.Clear();
            _sayac = 0;
            if (!_kancaTakili && Okuyucu.IlkTakimGorulme != null) KancalariTak();
            _bitis = DateTime.Now.AddSeconds(saniye);
            Giris.Gunluk($"Gözlem başladı ({saniye} sn). Oyunda ekranları açabilirsin.");
        }

        /// <summary>Dinleyici her OpenChannel çağrısında bunu çağırır.</summary>
        public static void Kanal(InteropReference r, PropertyID ozellik, BKey key)
        {
            string ad = Okuyucu.OzellikAdiKisa(ozellik.id);
            if (!Aktif && !IzlenenOzellikler.Contains(ad)) return;
            KeyKaynak[key.m_key] = Okuyucu.KaynakAdi(r) + "|" + ad;
        }

        public static void Tick()
        {
            if (!_kancaTakili && Okuyucu.IlkTakimGorulme is DateTime ilk && (DateTime.Now - ilk).TotalSeconds >= KancaBeklemeSn)
            {
                KancalariTak();
                Giris.Gunluk("Sürekli izleme kancası takıldı.");
            }

            if (_canliKirli && (DateTime.Now - _sonCanliYazma).TotalSeconds >= CanliYazmaAraligiSn)
                CanliYaz();

            if (!Aktif || DateTime.Now < _bitis) return;
            _bitis = DateTime.MinValue;
            string zaman = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var sonuc = new SortedDictionary<string, SortedDictionary<string, object>>();
            foreach (var kv in Degerler)
            {
                int ayir = kv.Key.LastIndexOf('|');
                string kaynak = kv.Key.Substring(0, ayir), oz = kv.Key.Substring(ayir + 1);
                if (!sonuc.TryGetValue(kaynak, out var d)) sonuc[kaynak] = d = new SortedDictionary<string, object>();
                d[oz] = kv.Value;
            }
            Giris.DosyaYaz($"gozlem/{zaman}.json", JsonSerializer.Serialize(sonuc, JsonSecenek));
            Giris.DosyaYaz("gozlem/son.txt", zaman);
            Giris.Gunluk($"Gözlem bitti: {Degerler.Count} değer, {sonuc.Count} kaynak.");
            Degerler.Clear();
        }

        private static readonly JsonSerializerOptions JsonSecenek = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        // --- Sürekli izleme deposu ---

        private static string CanliYolu => Path.Combine(Giris.Klasor, "canli.json");

        /// <summary>Önceki değerler korunur: mantık yeniden yüklense ya da oyun yeniden açılsa da.</summary>
        private static void CanliYukle()
        {
            _canli = new();
            try
            {
                if (!File.Exists(CanliYolu)) return;
                var eski = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>>(File.ReadAllText(CanliYolu));
                foreach (var (kaynak, oz) in eski)
                {
                    var d = _canli[kaynak] = new();
                    foreach (var (ad, kayit) in oz)
                        d[ad] = new Dictionary<string, object> { ["deger"] = kayit["deger"], ["zaman"] = kayit["zaman"].GetString() };
                }
            }
            catch (Exception e) { Giris.Gunluk("canli.json okunamadı, sıfırdan başlanıyor: " + e.Message); _canli = new(); }
        }

        private static void CanliYaz()
        {
            if (_canli == null) return;
            Giris.DosyaYaz("canli.json", JsonSerializer.Serialize(_canli, JsonSecenek));
            _canliKirli = false;
            _sonCanliYazma = DateTime.Now;
        }

        private static void CanliKaydet(string kaynakOzellik, object deger)
        {
            int ayir = kaynakOzellik.LastIndexOf('|');
            string kaynak = kaynakOzellik.Substring(0, ayir), oz = kaynakOzellik.Substring(ayir + 1);
            if (!IzlenenOzellikler.Contains(oz)) return;
            if (deger is string s && s.StartsWith("!")) return;
            if (!_canli.TryGetValue(kaynak, out var d)) _canli[kaynak] = d = new();
            d[oz] = new Dictionary<string, object> { ["deger"] = deger, ["zaman"] = DateTime.Now.ToString("s") };
            _canliKirli = true;
        }

        // --- Kancalar ---

        private static void OnceSetTarget(BKey key, DKey targetKey)
        {
            try
            {
                if (!KeyKaynak.ContainsKey(key.m_key)) return;
                if (!DataKeyler.TryGetValue(targetKey.m_key, out var l)) DataKeyler[targetKey.m_key] = l = new List<ulong>(1);
                if (!l.Contains(key.m_key)) l.Add(key.m_key);
            }
            catch { }
        }

        private static void OnceSetData(DKey key, TypedValue value)
        {
            try
            {
                if (!DataKeyler.TryGetValue(key.m_key, out var keyler)) return;
                object deger = null;
                bool cevrildi = false;
                foreach (var k in keyler)
                {
                    if (!KeyKaynak.TryGetValue(k, out var ad)) continue;
                    if (!cevrildi) { deger = Okuyucu.Cevir(value, 0); cevrildi = true; }
                    if (Aktif && _sayac < DegerSiniri) { Degerler[ad] = deger; _sayac++; }
                    CanliKaydet(ad, deger);
                }
            }
            catch { }
        }
    }
}
