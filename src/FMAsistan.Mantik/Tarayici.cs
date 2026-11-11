using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FMAsistan.Mantik
{
    /// <summary>
    /// Otomatik tarayıcı: kariyer yüklenince, kullanıcı hiçbir ekranı açmadan, oyunun veri
    /// sisteminden takımın, taktiğin, kadronun, teknik ekibin, antrenmanın, kulübün ve sıradaki
    /// rakibin raporunu ister. Her aşama bir öncekinin sonucundan genişler (takım → taktik ve
    /// antrenman referansları → oyuncu ve ekip kimlikleri → oyuncu başına rol uygunluğu ...).
    ///
    /// Ne zaman: kariyer ilk görüldükten 8 sn sonra, sonra her 10 dakikada bir, sıradaki ya da son
    /// maç değiştiğinde (45 sn'de bir hafif yoklama) ve uygulama veri\tara.flag bıraktığında.
    ///
    /// Çıktı: veri\oku\&lt;zaman&gt;-tarama.json (eklenti okuma biçimi; uygulama diğer okumalarla
    /// birleştirir) ve veri\tarama.json (durum). Bir önceki tarama dosyası silinir.
    ///
    /// Gerçekçilik: rakip takımın kendi taktiği okunmaz; yalnızca menajerin de gördüğü rapor
    /// (muhtemel taktik, gözlemci raporu, dizilişler) istenir. Yalnızca okur.
    /// </summary>
    internal static class Tarayici
    {
        private const double TamAralikDk = 10;
        private const double YoklamaAralikSn = 45;
        private const double IlkBeklemeSn = 30;
        private const string SurerIsareti = "tarama-suruyor.flag";
        private const int OyuncuParcasi = 8;     // bir okumada en çok kaç kişi
        private const int RolParcasi = 160;      // bir okumada en çok kaç rol tablosu isteği

        private static string _takim;
        private static DateTime _ilkGorulme = DateTime.MinValue;
        private static DateTime _sonTam = DateTime.MinValue;
        private static DateTime _sonYoklama = DateTime.MinValue;
        private static string _imza;
        private static bool _calisiyor;
        private static Dictionary<string, Dictionary<string, object>> _toplam;
        private static DateTime _baslangic;
        private static readonly List<string> Asamalar = new();
        private static readonly Queue<Action> Adimlar = new();
        private static bool _denetlendi;
        private static bool _kapali;
        private static string _kapanmaNedeni;

        /// <summary>Kalp atışına eklenen kısa durum.</summary>
        public static object Durum => new Dictionary<string, object>
        {
            ["takim"] = _takim,
            ["calisiyor"] = _calisiyor,
            ["son"] = _sonTam == DateTime.MinValue ? null : _sonTam.ToString("s"),
            ["kapali"] = _kapali ? _kapanmaNedeni : null,
        };

        // ---- Okunacak yollar ----

        private static readonly string[] TakimYollari =
        {
            "Name", "ShortDisplayName", "Nickname", "Club", "Division", "LeaguePositionString", "LeagueTableName",
            "Manager", "AnalystPerson", "AssistantManagerOrBestCoach", "Tactic", "TeamTraining", "TeamConfidence",
            "MostUsedFormation", "NextFixture", "LastFixture", "Next5Fixtures", "RecentFormFixtures", "RecentFormValues",
            "DataAnalystLevelInfo", "DataHubLastMatchFixtureDropdown", "KeyFindingsVisuals", "LastMatchAnalysis",
            "TeamAnalytics", "PlayerAnalytics", "GoalAnalysisScoring", "GoalAnalysisConceding", "AnalysisScoringPositions",
            "AnalysisConcedingPositions", "SetPieceAnalysis", "SetPieceDataInsights", "GeneralPerformanceVisual",
            "ActionZonesVisualisation", "TopXGoalscorers", "TopGoalscorerPlayer", "MostAssistsPlayer",
            "HighestAverageRatingPlayer", "StaffAllListFiltered", "StaffCoachingSubGroup", "StaffMedicalSubGroup",
            "StaffRecruitmentSubGroup", "MentoringGroups", "TeamReportSquadDepth", "ExpectedFormations",
            "FitnessTestPlayers", "PreviousMeetings", "Stadium", "TeamReportProvider", "IsHuman",
        };

        private static readonly string[] TaktikYollari =
        {
            "Name", "FormationName", "MentalityString", "MentalityDropdown", "MentalityDropdownOptions", "TacticalStyle",
            "TacticalStyleDropdown", "TacticalStyleDropdownOptions", "TacticalEffectiveness", "TacticalPositionsCombined",
            "TacticalPositions", "TacticsGrid", "PartnershipScoresForLines", "NextOppositionInstructions",
            "AdviceDropdownList", "FilteredPlayers", "FilteredSubs", "SquadPlayers", "TacticActiveTweaks", "TacticCount",
            "TacticSlot1Name", "CurrentTacticSlot", "SetPiece", "KeyAttributesForRole", "ProjectedTacticalPositions",
            // Takım talimatları
            "BuildUpStrategy", "PassingDirectness", "Tempo", "TacticWidth", "CreativeFreedom", "Dribbling",
            "PassReception", "Patience", "ShotsFromDistance", "CrossingStyle", "SupportingRuns",
            "SupportingRunsSecondary", "ProgressThrough", "TimeWasting", "StoppageStrategy", "AttackingTransition",
            "DefensiveTransition", "LineOfEngagement", "DefensiveLine", "DefensiveLineBehaviour", "PressingTrigger",
            "PressingTrap", "Tackling", "PreventGKShort", "GoalKicks", "GoalkeeperDistributionSpeed",
            "GoalkeeperDistributionTarget",
        };

        private static readonly string[] TalimatYollari = TaktikYollari.SkipWhile(y => y != "BuildUpStrategy").ToArray();

        private static readonly string[] RakipYollari =
        {
            "Name", "ShortDisplayName", "Club", "Division", "LeaguePositionString", "Manager", "MostUsedFormation",
            "ExpectedFormations", "ExpectedTactic", "ExpectedTacticalStyle", "ScoutReportStrengths", "ScoutReportWeaknesses",
            "NextOppositionThreatLevelAssessment", "DataAnalystLevelInfo", "HasTeamReport", "TeamReportSquadDepth",
            "KeyFindingsVisuals", "TeamAnalytics", "PlayerAnalytics", "GoalAnalysisScoring", "GoalAnalysisConceding",
            "SetPieceAnalysis", "SetPieceDataInsights", "RecentFormFixtures", "RecentFormValues", "PreviousMeetings",
            "TopGoalscorerPlayer", "MostAssistsPlayer", "HighestAverageRatingPlayer", "Next5Fixtures", "LastFixture",
            "Stadium",
        };

        private static readonly string[] AntrenmanYollari =
        {
            "TrainingSquad", "CoachAssignments", "TrainingIntensities", "TrainingIntensitySettingsForConditionRanges",
            "TrainingMentoring", "IndividualTrainingViews", "AttackingTacticalAttributesTooltip",
            "AttackingTechnicalAttributesTooltip", "DefendingTacticalAttributesTooltip", "DefendingTechnicalAttributesTooltip",
            "PossessionTacticalAttributesTooltip", "PossessionTechnicalAttributesTooltip", "FitnessAttributesTooltip",
            "GoalkeepingAttributesTooltip", "SetPiecesAttributesTooltip",
        };

        private static readonly string[] KulupYollari =
        {
            "Name", "LongName", "TransferBudget", "RemainingWageBudgetWithText", "CurrentWageSpendingWithText",
            "WagesData", "OverallBalance", "FinanceBalanceHistory", "TransferExpenditureThisSeason",
            "TrainingFacilityRating", "YouthFacilityRating", "CorporateFacilityRating", "ProfessionalStatus",
            "ReputationScope", "TeamSettingsEditSession", "MainTeam",
        };

        private static readonly string[] FiksturYollari =
        {
            "HomeTeam", "AwayTeam", "Competition", "FixtureDate", "KickoffTime", "HasResult", "Result", "ResultType",
            "StageName", "MatchVenueShort", "NextMatchDaysText", "TeamOpponent", "Stadium",
        };

        private static readonly string[] OyuncuYollari =
        {
            "Name", "ShirtName", "Age", "DateOfBirth", "Nation", "Position", "PositionCombinedString",
            "BestPositionShortString", "IsGoalkeeper", "Condition", "Sharpness", "Morale", "InjuryRiskLevel",
            "FitnessTestResult", "PlayerStatus", "PlayerStatusFlagsForTeam", "AverageRating", "OverallAverageRating",
            "OverallGoals", "OverallAssists", "Personality", "TendenciesTendsTo", "TendenciesTendsNotTo",
            "TransferValueRange", "FullContract", "PlayerTraining", "HappinessTraining", "TacticTableData",
            "ScoutedBestRole", "InternationalCapsAndGoalsString", "SquadNumberBlankIfInvalid", "PlayerReportForContextTeam",
            "InPossessionRoleStarRangeForSquadPlannerSeason", "RelationshipWithHuman", "UniqueId",
            // Oyuncu öznitelik blokları (adlar teknik ekip bloklarının kalıbından; gelmeyen yok sayılır)
            "AttributesTechnical", "AttributesMental", "AttributesPhysical", "AttributesGoalkeeping",
            "AttributesSetPieces", "AttributesPlayerTechnical", "AttributesPlayerMental", "AttributesPlayerPhysical",
        };

        private static readonly string[] EkipYollari =
        {
            "Name", "Age", "Nation", "JobString", "JobType", "StaffRoles", "Personality", "CoachingBadges",
            "AttributesCoaching", "AttributesCoachingKnowledge", "AttributesCoachingMental", "BestTrainingCoachingCategory",
            "BestTrainingCoachingCategoryName", "PreferredFormationString", "PreferredOutOfPossessionFormationString",
            "PreferredMentality", "PreferredPressing", "PreferredMarking", "MostLikelyTacticalStyle", "FullContract",
            "NonPlayerDisplayableReputation", "RelationshipWithHuman", "IsCurrentHuman", "IsHuman",
            "SetPieceAimForPreferenceString", "SetPieceAttackingNumbersPreferenceString", "SetPieceDefensiveLinePreferenceString",
            "SetPieceDefensiveNumbersPreferenceString", "SetPieceDeliveryPreferenceString", "SetPieceMarkingPreferenceString",
            "SetPiecePostDefencePreferenceString",
        };

        private static readonly string[] SozlesmeYollari =
        {
            "Wage", "EndDate", "EndDateDescription", "ContractStatus", "AgreedPlayingTime", "IsContractExpiringSoon",
        };

        private static readonly string[] OyuncuAntrenmanYollari = { "TrainingRating", "TrainingIntensityOrRest", "IndividualTrainingIntensityList" };

        // ---- Döngü ----

        public static void Tick()
        {
            // Güvenlik: önceki oturumda tarama sürerken oyun kapandıysa (çökme), otomatik tarama
            // kendini kapatır. Yeniden açmak için uygulama veri	ara.flag bırakır (elle güncelle).
            if (!_denetlendi)
            {
                _denetlendi = true;
                string isaret = Path.Combine(Giris.Klasor, SurerIsareti);
                if (File.Exists(isaret))
                {
                    _kapali = true;
                    _kapanmaNedeni = "Önceki oturumda tarama sürerken oyun kapandı; otomatik tarama kapatıldı.";
                    try { File.Move(isaret, Path.Combine(Giris.Klasor, "tarama-coktu.txt"), true); } catch { }
                    Giris.Gunluk("UYARI: " + _kapanmaNedeni);
                }
            }

            if (Adimlar.Count > 0 && !Okuyucu.Mesgul) { Adimlar.Dequeue()(); return; }
            if (_calisiyor || Okuyucu.Mesgul) return;

            string flag = Path.Combine(Giris.Klasor, "tara.flag");
            bool istendi = File.Exists(flag);
            if (istendi) { try { File.Delete(flag); } catch { } }
            if (_kapali && !istendi) return;

            _takim = TakimKimligi();
            if (_takim == null) return;
            // Kariyer yüklü mü: oyunun arayüzü takımımızı kendisi istemiş olmalı.
            if (!Okuyucu.OyundaGoruldu("TeamReference:" + _takim)) { _ilkGorulme = DateTime.MinValue; return; }
            if (_ilkGorulme == DateTime.MinValue) { _ilkGorulme = DateTime.Now; return; }
            if ((DateTime.Now - _ilkGorulme).TotalSeconds < IlkBeklemeSn) return;

            if (istendi) { _kapali = false; _kapanmaNedeni = null; }
            if (istendi || (DateTime.Now - _sonTam).TotalMinutes >= TamAralikDk) { Basla(istendi ? "istek" : "zamanlı"); return; }
            if ((DateTime.Now - _sonYoklama).TotalSeconds >= YoklamaAralikSn)
            {
                _sonYoklama = DateTime.Now;
                Oku(new[] { Istek("TeamReference:" + _takim, "NextFixture", "LastFixture") }, 6, s =>
                {
                    string imza = Imza(s);
                    if (imza != null && _imza != null && imza != _imza) Basla("maç değişti");
                });
            }
        }

        private static string TakimKimligi()
        {
            try
            {
                string ayar = Path.Combine(Giris.Klasor, "ayar.json");
                if (!File.Exists(ayar)) return _takim;
                using var b = JsonDocument.Parse(File.ReadAllText(ayar));
                return b.RootElement.TryGetProperty("takim", out var t) ? t.ToString() : _takim;
            }
            catch { return _takim; }
        }

        private static string Imza(Dictionary<string, Dictionary<string, object>> s)
            => s.TryGetValue("TeamReference:" + _takim, out var t)
                ? $"{(t.TryGetValue("NextFixture", out var n) ? n : null)}|{(t.TryGetValue("LastFixture", out var l) ? l : null)}"
                : null;

        // ---- Tarama aşamaları ----

        private static void Basla(string neden)
        {
            _calisiyor = true;
            _baslangic = DateTime.Now;
            Isaret(true);
            _toplam = new Dictionary<string, Dictionary<string, object>>();
            Asamalar.Clear();
            Giris.Gunluk($"Tarama başladı ({neden}), takım {_takim}.");

            string takim = "TeamReference:" + _takim, taktik = "TeamTacticReference:" + _takim;
            Oku(new[] { Istek(takim, TakimYollari), Istek(taktik, TaktikYollari) }, 10, a =>
            {
                Ekle("takım", a);
                _imza = Imza(a);
                var t = Al(a, takim);

                // Talimat değerlerinin iç adları (oyunun kimlik → ad tablosundan).
                TalimatAdlariniYaz(Al(a, taktik));

                var istekler = new List<string>();
                string antrenman = Metin(t, "TeamTraining");
                if (antrenman != null) istekler.Add(Istek(antrenman, AntrenmanYollari));
                string kulup = Metin(t, "Club");
                if (kulup != null) istekler.Add(Istek(kulup, KulupYollari));
                string guven = Metin(t, "TeamConfidence");
                if (guven != null) istekler.Add(Istek(guven, "BoardFeedbackCriticism", "BoardFeedbackHighlights", "BoardVisionGrade", "BoardVisionString"));
                string sonraki = Metin(t, "NextFixture"), son = Metin(t, "LastFixture");
                if (sonraki != null) istekler.Add(Istek(sonraki, FiksturYollari));
                if (son != null) istekler.Add(Istek(son, FiksturYollari));
                string rakip = RakipKimligi(sonraki);
                if (rakip != null) istekler.Add(Istek("TeamReference:" + rakip, RakipYollari));

                Oku(istekler, 10, b =>
                {
                    Ekle("antrenman, kulüp, fikstür, rakip", b);
                    var oyuncular = Kisiler(Al(a, taktik)).Union(Kisiler(antrenman != null ? Al(b, antrenman) : null)).Distinct().ToList();
                    var ekip = Kisiler(t, "StaffAllListFiltered").ToList();
                    var rakipOyunculari = rakip != null ? Kisiler(Al(b, "TeamReference:" + rakip), "ExpectedTactic").ToList() : new List<string>();
                    Kisileri(oyuncular, ekip, rakipOyunculari, Al(a, taktik));
                });
            });
        }

        private static void Kisileri(List<string> oyuncular, List<string> ekip, List<string> rakipOyunculari, Dictionary<string, object> taktik)
        {
            var parcalar = new List<List<string>>();
            foreach (var parca in Parcala(oyuncular, OyuncuParcasi))
                parcalar.Add(parca.Select(p => Istek(p, OyuncuYollari)).ToList());
            foreach (var parca in Parcala(ekip.Except(oyuncular).ToList(), OyuncuParcasi))
                parcalar.Add(parca.Select(p => Istek(p, EkipYollari)).ToList());
            if (rakipOyunculari.Count > 0)
                parcalar.Add(rakipOyunculari.Except(oyuncular).Select(p => Istek(p, "Name", "Age", "Position", "PositionCombinedString")).ToList());

            foreach (var istekler in parcalar)
            {
                var i = istekler;
                Adimlar.Enqueue(() => Oku(i, 8, s => Ekle("kişiler", s)));
            }

            // Sözleşmeler ve oyuncu antrenman referansları kişilerin değerlerinden çıkar.
            Adimlar.Enqueue(() =>
            {
                var istekler = new List<string>();
                foreach (var k in oyuncular.Concat(ekip).Distinct())
                {
                    var d = Al(_toplam, k);
                    string sozlesme = Metin(d, "FullContract");
                    if (sozlesme != null && sozlesme.StartsWith("ContractReference")) istekler.Add(Istek(sozlesme, SozlesmeYollari));
                    string antrenman = Metin(d, "PlayerTraining");
                    if (antrenman != null && antrenman.StartsWith("PlayerTrainingReference")) istekler.Add(Istek(antrenman, OyuncuAntrenmanYollari));
                }
                if (istekler.Count == 0) { Adimlar.Enqueue(RolTablolari(oyuncular, taktik)); return; }
                Oku(istekler, 8, s => { Ekle("sözleşme, bireysel antrenman", s); Adimlar.Enqueue(RolTablolari(oyuncular, taktik)); });
            });
        }

        /// <summary>
        /// Kadrodaki her oyuncunun, taktiğimizdeki her pozisyon ve fazdaki rol uygunluğu
        /// (oyunun yıldız aralıkları, ekibin bilgisiyle sınırlı). Kim hangi slota uygun sorusunun verisi.
        /// </summary>
        private static Action RolTablolari(List<string> oyuncular, Dictionary<string, object> taktik) => () =>
        {
            var slotlar = new HashSet<(long maske, bool ip)>();
            if (taktik != null && taktik.TryGetValue("TacticalPositionsCombined", out var poz) && poz is List<object> liste)
                foreach (var o in liste.OfType<Dictionary<string, object>>())
                {
                    if (Sayi(o, "IPPosition") is long ip && ip > 0) slotlar.Add((ip, true));
                    if (Sayi(o, "OOPPosition") is long oop && oop > 0) slotlar.Add((oop, false));
                }
            string takim = "TeamReference:" + _takim;
            var istekler = new List<string>();
            foreach (var p in oyuncular)
                foreach (var (maske, ip) in slotlar)
                    istekler.Add(JsonSerializer.Serialize(new
                    {
                        kaynak = p,
                        etiket = $"RolTablosu|{p}|{maske}|{(ip ? 1 : 0)}",
                        yollar = new[] { "PositionalAbilitiesRoleTable" },
                        baglam = new object[]
                        {
                            new { ad = "team", tur = "kaynak", deger = takim },
                            new { ad = "PositionMask", tur = "dinamik", deger = maske },
                            new { ad = "InPossession", tur = "bool", deger = ip },
                        },
                    }));
            var parcalar = Parcala(istekler, RolParcasi).ToList();
            for (int i = 0; i < parcalar.Count; i++)
            {
                var parca = parcalar[i];
                bool sonuncu = i == parcalar.Count - 1;
                Adimlar.Enqueue(() => Oku(parca, 8, s => { Ekle("rol uygunluğu", s); if (sonuncu) Bitir(); }));
            }
            if (parcalar.Count == 0) Bitir();
        };

        private static void Bitir()
        {
            string zaman = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string klasor = Path.Combine(Giris.Klasor, "oku");
            try
            {
                if (Directory.Exists(klasor))
                    foreach (var f in Directory.GetFiles(klasor, "*-tarama.json")) File.Delete(f);
            }
            catch { }
            int deger = _toplam.Values.Sum(d => d.Count);
            int bos = _toplam.Values.Sum(d => d.Values.Count(v => v is string s && s.StartsWith("!")));
            // Gelmeyen değerleri dosyaya yazma: eski geçerli değerin üzerine "!zaman aşımı" yazılmasın.
            var temiz = _toplam.ToDictionary(k => k.Key, k => k.Value.Where(v => !(v.Value is string s && s.StartsWith("!"))).ToDictionary(v => v.Key, v => v.Value));
            Giris.DosyaYaz($"oku/{zaman}-tarama.json", Okuyucu.Json(temiz));
            Giris.DosyaYaz("tarama.json", Okuyucu.Json(new Dictionary<string, object>
            {
                ["zaman"] = DateTime.Now.ToString("s"),
                ["sure"] = Math.Round((DateTime.Now - _baslangic).TotalSeconds, 1),
                ["takim"] = _takim,
                ["kaynak"] = _toplam.Count,
                ["deger"] = deger - bos,
                ["gelmeyen"] = bos,
                ["asamalar"] = Asamalar.ToList(),
                ["gelmeyenYollar"] = _toplam.Values.SelectMany(d => d.Where(v => v.Value is string s && s.StartsWith("!")).Select(v => v.Key))
                    .GroupBy(y => y).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} ({g.Count()})").Take(80).ToList(),
            }));
            Giris.Gunluk($"Tarama bitti: {_toplam.Count} kaynak, {deger - bos} değer ({bos} gelmedi), {(DateTime.Now - _baslangic).TotalSeconds:0} sn.");
            _toplam = null;
            _sonTam = DateTime.Now;
            _calisiyor = false;
            Isaret(false);
        }

        /// <summary>Düzgün kapanış (oyundan çıkış ya da mantığın yeniden yüklenmesi): çökme sayılmasın.</summary>
        public static void Durdur()
        {
            Adimlar.Clear();
            _calisiyor = false;
            Isaret(false);
        }

        private static void Isaret(bool var)
        {
            string yol = Path.Combine(Giris.Klasor, SurerIsareti);
            try { if (var) File.WriteAllText(yol, DateTime.Now.ToString("s")); else if (File.Exists(yol)) File.Delete(yol); } catch { }
        }

        private static void TalimatAdlariniYaz(Dictionary<string, object> taktik)
        {
            if (taktik == null) return;
            string yol = Path.Combine(Giris.Klasor, "kimlik-adlari-sonuc.json");
            var sozluk = new Dictionary<string, string>();
            try { if (File.Exists(yol)) sozluk = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(yol)) ?? sozluk; } catch { }
            bool yeni = false;
            foreach (var y in TalimatYollari)
            {
                if (!taktik.TryGetValue(y, out var v) || !(v is long id) || id <= 0 || id > uint.MaxValue) continue;
                string anahtar = id.ToString(CultureInfo.InvariantCulture);
                if (sozluk.ContainsKey(anahtar)) continue;
                try { sozluk[anahtar] = new SI.Bindable.Reference.Core.PropertyID((uint)id).ToString(); yeni = true; } catch { }
            }
            if (yeni) Giris.DosyaYaz("kimlik-adlari-sonuc.json", JsonSerializer.Serialize(sozluk, new JsonSerializerOptions { WriteIndented = true }));
        }

        // ---- Yardımcılar ----

        private static void Oku(IEnumerable<string> istekler, int sure, Action<Dictionary<string, Dictionary<string, object>>> bitti)
        {
            var liste = istekler.ToList();
            if (liste.Count == 0) { bitti(new Dictionary<string, Dictionary<string, object>>()); return; }
            string json = "{\"sure\":" + sure + ",\"istekler\":[" + string.Join(",", liste) + "]}";
            Okuyucu.Ekle(json, s =>
            {
                try { bitti(s); }
                catch (Exception e)
                {
                    Giris.Gunluk("Tarama adımı hatası: " + e);
                    Adimlar.Clear();
                    _calisiyor = false;
                    _sonTam = DateTime.Now;
                    Isaret(false);
                }
            });
        }

        private static string Istek(string kaynak, params string[] yollar)
            => JsonSerializer.Serialize(new { kaynak, yollar });

        private static void Ekle(string asama, Dictionary<string, Dictionary<string, object>> s)
        {
            if (_toplam == null) return;
            int n = 0;
            foreach (var (k, d) in s)
            {
                if (!_toplam.TryGetValue(k, out var hedef)) _toplam[k] = hedef = new Dictionary<string, object>();
                foreach (var (y, v) in d) { hedef[y] = v; n++; }
            }
            Asamalar.Add($"{asama}: {s.Count} kaynak, {n} değer");
        }

        private static Dictionary<string, object> Al(Dictionary<string, Dictionary<string, object>> s, string k)
            => k != null && s != null && s.TryGetValue(k, out var d) ? d : null;

        private static string Metin(Dictionary<string, object> d, string y)
            => d != null && d.TryGetValue(y, out var v) && v is string s && !s.StartsWith("!") && !s.Contains("NullReference") ? s : null;

        private static long? Sayi(Dictionary<string, object> d, string y)
            => d != null && d.TryGetValue(y, out var v) ? v switch { long l => l, ulong u => (long)u, double x => (long)x, _ => null } : null;

        private static readonly Regex KisiDeseni = new(@"^PersonReference:\d+$");

        /// <summary>Bir değerin (ya da tüm kaynağın) içindeki kişi kimlikleri.</summary>
        private static IEnumerable<string> Kisiler(Dictionary<string, object> d, string yol = null)
        {
            if (d == null) yield break;
            IEnumerable<object> kokler = yol == null ? d.Values : d.TryGetValue(yol, out var v) ? new[] { v } : Array.Empty<object>();
            var yigin = new Stack<object>(kokler);
            while (yigin.Count > 0)
            {
                var o = yigin.Pop();
                switch (o)
                {
                    case string s when KisiDeseni.IsMatch(s): yield return s; break;
                    case List<object> l: foreach (var x in l) yigin.Push(x); break;
                    case Dictionary<string, object> m: foreach (var x in m.Values) yigin.Push(x); break;
                }
            }
        }

        private static string RakipKimligi(string fikstur)
        {
            if (fikstur == null) return null;
            var ev = Regex.Match(fikstur, @"HomeTeam=(\d+)");
            var dep = Regex.Match(fikstur, @"AwayTeam=(\d+)");
            if (!ev.Success || !dep.Success) return null;
            return ev.Groups[1].Value == _takim ? dep.Groups[1].Value : dep.Groups[1].Value == _takim ? ev.Groups[1].Value : null;
        }

        private static IEnumerable<List<T>> Parcala<T>(List<T> l, int n)
        {
            for (int i = 0; i < l.Count; i += n) yield return l.GetRange(i, Math.Min(n, l.Count - i));
        }
    }
}
