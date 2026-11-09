# FM27 Arayüz Veri Yolları

> Kaynak: FMAsistan.Mantik dinleyicisi, 2026-11-10, FM 27.0.0.2493426 (Game Pass).
> Kullanıcı 120 sn içinde taktik, antrenman ve rakip raporu ekranlarını açtı: 5.115 istek.
> Ham kayıtlar: `Content\BepInEx\FMAsistan\veri\dinle\`.

Oyunun arayüzü, native veritabanından **referans türü + özellik adı** ile veri ister
(`GameInteropSubsystem.OpenChannel(InteropReference, PropertyID, Key)`). Aşağıdaki
özellikler, ilgili referans üzerinden `BindingUtils.Request(ağaç, kaynak, "Özellik", ...)` ile
istenebilir. Değer türleri (liste, kayıt, metin) henüz çözülmedi.

## Referans kimlikleri

Her referans `(özellik kimliği, değer)` çiftlerinden oluşur. Örnekler:

| Referans | Örnek kimlik |
|---|---|
| `TeamReference` (Galatasaray A takımı) | `DatabaseReferenceField=402654676` (0x180008D4) |
| `PersonReference` | `DatabaseReferenceField=16827620` (0x010CC4E4) |
| `TeamTacticReference` | takım kimliği + `BooleanField=1` |
| `FixtureReference` | `HomeTeam`, `AwayTeam`, `FixtureDate` ... |
| `TeamTrainingReference` | insan menajer + takım kimliği |

`DatabaseReferenceField` değerinin üst baytı tür gibi görünüyor (0x01 kişi, 0x18 takım,
0x03 kulüp, 0x0A ülke, 0x19 lig). Hepsinde `UIRecordID` ve `1198877028=4180674` (oturum?)
çifti var.

## Kendi taktiğimiz — `TeamTacticReference`

- Genel: `Name`, `TacticalStyle`, `MentalityString`, `TacticalIntensity`, `HasTactic`,
  `CurrentTacticSlot`, `TacticSlot1Name`, `TacticCount`
- Diziliş ve roller: `TacticalPositions`, `TacticalPositionsCombined`,
  `ProjectedTacticalPositions`, `PositionRoleOverride`, `TacticsGrid`,
  `TacticsTeamSelectionForIndex`, `IsPlayerOnPitch`, `IsPlayerOnBench`
- Uyum: `PlayerContextRoleAbility`, `PlayerContextTacticAbility`,
  `PartnershipScoresForLines`, `ShowPossessionProximityScoreWarning`
- Talimatlar: `SelectedTeamInstructionOption`, `PhaseAndZoneCombinedOptions`,
  `TacticActiveTweaks`, `TacticPendingTweakOps`
- Verimlilik: `TacticalEffectiveness`, `TacticalEffectivenessForTacticSlot`
- Rakibe özel: `NextOppositionInstructions`, `NextOppositionTacticIsValid`,
  `OppositionPlayerMarking`, `OppositionPlayerPressingTrigger`, `OppositionPlayerTackling`,
  `OppositionPlayerShowOnToFoot`, `CurrentOppositionInstructionSuggestion`
- Asistan tavsiyesi: `AdviceDropdownList`, `SuggestionCount`, `PlayerFeedbackString`
- Duran top: `SetPiece`, `SetPieceCreator`

`TeamReference.TacticalEffectivenessForPlayer` oyuncu bazında taktiksel verimlilik verir.

## Antrenman

- `TeamTrainingReference`: `TrainingIntensities`, `CoachAssignments`, `TrainingMentoring`,
  `IndividualTrainingViews`, `TrainingSquad`, `TrainingIntensitySettingsForConditionRanges`
- `PlayerTrainingReference`: `TrainingRating`, `TrainingIntensityOrRest`,
  `IndividualTrainingIntensityList`
- `CalendarReference` (haftalık program): `TrainingScheduleWeeks`, `CalendarWeekAsList`,
  `TrainingScheduleName`, `CurrentTrainingSession`, `TrainingSessionsForCategoryDropdown`
- `PersonReference`: `PlayerTraining`, `HappinessTraining`, `TrainingCalendar`,
  `CustomTrainingSchedule`

## Rakip — `TeamReference` (rakip takım) ve `FixtureReference`

- Rapor: `HasTeamReport`, `TeamReportProvider`, `KeyFindingsVisuals`, `ExpectedFormations`,
  `MostUsedFormation`, `NextOppositionThreatLevelAssessment`, `TeamAnalytics`,
  `TeamReportSquadDepth`, `DataAnalystLevelInfo`, `AnalystPerson`
- Gol ve duran top analizi: `GoalAnalysisScoring`, `GoalAnalysisConceding`,
  `SetPieceAnalysis`, `SetPieceDataInsights`
- Form ve oyuncular: `RecentFormValues`, `PreviousMeetings`, `TopXGoalscorers`,
  `TopXAssists`, `HighestAverageRatingPlayer`, `PlayersAtRiskList`
- Maç: `FixtureReference.TeamOpponent`, `FixtureDate`, `Competition`, `TeamNews`

## Teknik ekip (sohbet kişilikleri için) — `PersonReference`

`PreferredFormationString`, `PreferredMentality`, `PreferredPressing`, `PreferredMarking`,
`AttributesCoaching`, `AttributesCoachingMental`, `BestTrainingCoachingCategory`,
`CoachingBadges`, `JobType`, `ManagerReputation`

## Sıradaki adım

Dinleyicinin yakaladığı gerçek referans nesnelerini şablon olarak saklayıp bu özellikleri
kendimiz istemek, dönen `TypedValue` değerlerini JSON'a çevirmek.
