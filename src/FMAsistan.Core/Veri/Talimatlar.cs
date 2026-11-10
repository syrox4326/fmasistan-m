namespace FMAsistan.Core.Veri;

/// <summary>
/// FM27 takım talimatları. Talimat özellik adları ve seçili değerler oyundan okunur
/// (TeamTacticReference.* → PropertyID → iç ad, ör. Tempo = MuchHigherTempo). Oyun Türkçe
/// etiketleri arayüzün kendi dosyalarından aldığı için çeviri sisteminden alınamadı; Türkçe
/// adlar bizim çevirimizdir ve öyle işaretlenir. Oyunun tam seçenek listesi okunamadığından
/// öneriler "talimat + yön" olarak yapılmalıdır.
/// </summary>
public static class Talimatlar
{
    public enum TalimatFazi { TopaSahipken, Gecis, TopRakipteyken, Kaleci }

    /// <summary>Özellik adı → (Türkçe ad, faz). 2026-11-10 dinleyici kaydından (27 talimat).</summary>
    public static readonly IReadOnlyDictionary<string, (string ad, TalimatFazi faz)> Ozellikler =
        new Dictionary<string, (string, TalimatFazi)>
        {
            ["BuildUpStrategy"] = ("Oyun kurma", TalimatFazi.TopaSahipken),
            ["PassingDirectness"] = ("Pas stili", TalimatFazi.TopaSahipken),
            ["Tempo"] = ("Tempo", TalimatFazi.TopaSahipken),
            ["TacticWidth"] = ("Genişlik", TalimatFazi.TopaSahipken),
            ["CreativeFreedom"] = ("Yaratıcı özgürlük", TalimatFazi.TopaSahipken),
            ["Dribbling"] = ("Dripling", TalimatFazi.TopaSahipken),
            ["PassReception"] = ("Pas alma", TalimatFazi.TopaSahipken),
            ["Patience"] = ("Sabır", TalimatFazi.TopaSahipken),
            ["ShotsFromDistance"] = ("Uzaktan şut", TalimatFazi.TopaSahipken),
            ["CrossingStyle"] = ("Orta stili", TalimatFazi.TopaSahipken),
            ["SupportingRuns"] = ("Destek koşuları", TalimatFazi.TopaSahipken),
            ["SupportingRunsSecondary"] = ("Destek koşuları (ikincil)", TalimatFazi.TopaSahipken),
            ["ProgressThrough"] = ("İlerleme yönü", TalimatFazi.TopaSahipken),
            ["TimeWasting"] = ("Zaman geçirme", TalimatFazi.TopaSahipken),
            ["StoppageStrategy"] = ("Duran top stratejisi", TalimatFazi.TopaSahipken),
            ["AttackingTransition"] = ("Top kazanıldığında", TalimatFazi.Gecis),
            ["DefensiveTransition"] = ("Top kaybedildiğinde", TalimatFazi.Gecis),
            ["LineOfEngagement"] = ("Baskı hattı", TalimatFazi.TopRakipteyken),
            ["DefensiveLine"] = ("Savunma hattı", TalimatFazi.TopRakipteyken),
            ["DefensiveLineBehaviour"] = ("Savunma hattı davranışı", TalimatFazi.TopRakipteyken),
            ["PressingTrigger"] = ("Pres yoğunluğu", TalimatFazi.TopRakipteyken),
            ["PressingTrap"] = ("Pres tuzağı", TalimatFazi.TopRakipteyken),
            ["Tackling"] = ("Müdahale", TalimatFazi.TopRakipteyken),
            ["PreventGKShort"] = ("Rakip kalecinin kısa dağıtımı", TalimatFazi.TopRakipteyken),
            ["GoalKicks"] = ("Kale vuruşları", TalimatFazi.Kaleci),
            ["GoalkeeperDistributionSpeed"] = ("Kaleci dağıtım hızı", TalimatFazi.Kaleci),
            ["GoalkeeperDistributionTarget"] = ("Kaleci dağıtım hedefi", TalimatFazi.Kaleci),
        };

    /// <summary>Oyunda görülen iç değer adı → Türkçe karşılık (bizim çevirimiz).</summary>
    private static readonly Dictionary<string, string> Degerler = new()
    {
        ["AttackTransitionCounter"] = "Kontra atak",
        ["PlayThroughPress"] = "Presi pasla aş",
        ["BeMoreExpressive"] = "Daha serbest",
        ["HigherDefensiveLine"] = "Daha yüksek",
        ["HoldLine"] = "Hattı koru",
        ["DefenceTransitionCounterpress"] = "Karşı pres",
        ["RunAtDefence"] = "Savunmanın üzerine sür",
        ["GKDistributionPaceQuick"] = "Hızlı",
        ["GKDistributionTargetCentrebacks"] = "Stoperlere",
        ["GoalKicksShort"] = "Kısa",
        ["LineOfEngagementHigher"] = "Daha yüksek",
        ["PassingStyleShorter"] = "Daha kısa",
        ["PassingReceptionIntoSpace"] = "Boşluğa",
        ["PressingIntensityMuchHigher"] = "Çok daha yüksek",
        ["PreventShortGKDistribution"] = "Engelle",
        ["SetupNarrowerInPossession"] = "Daha dar",
        ["MuchHigherTempo"] = "Çok daha yüksek",
        ["LookForOverlapBoth"] = "Her iki kanattan bindirme ara",
        ["PressingTrapOut"] = "Kenara yönlendir",
    };

    /// <summary>İç değer adını Türkçeye çevirir; bilinmeyende iç adı korur.</summary>
    public static string DegerAdi(string icAd)
    {
        if (Degerler.TryGetValue(icAd, out var t)) return t;
        if (icAd.EndsWith("None", StringComparison.Ordinal)) return "Talimat yok (standart)";
        return icAd; // çevirisi bilinmeyen değer: iç adıyla gösterilir, uydurma çeviri yapılmaz
    }
}
