﻿using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AutoEquip
{
    public class PawnCalcForApparel
    {

        private Pawn _pawn;
        private SaveablePawn _saveablePawn;
        private Outfit _outfit;
        private Saveable_Pawn_StatDef[] _stats;
        private Saveable_Pawn_WorkStatDef[] _workstats;


        private bool _optimized;

        private List<Apparel> _allApparelsItems;
        private List<float> _allApparelsScore;

        private List<Apparel> _calculatedApparelItems;
        private List<float> _calculatedApparelScore;
        private List<Apparel> _fixedApparels;


        private static NeededWarmth _neededWarmth;

        // new curves -experimental

        private static readonly SimpleCurve InsulationColdScoreFactorCurve_NeedWarm = new SimpleCurve
         {
             new CurvePoint(-30f, 8f),
             new CurvePoint(0f, 1f)
         };

        private static readonly SimpleCurve InsulationWarmScoreFactorCurve_NeedCold = new SimpleCurve
         {
             new CurvePoint(30f, 8f),
             new CurvePoint(0f, 1f),
             new CurvePoint(-10, 0.1f)
         };


        // private static readonly SimpleCurve InsulationColdScoreFactorCurve_NeedWarm = new SimpleCurve
        // {
        //     new CurvePoint(-30f, 8f),
        //     new CurvePoint(0f, 1f)
        // };
        //
        // private static readonly SimpleCurve InsulationWarmScoreFactorCurve_NeedCold = new SimpleCurve
        // {
        //     new CurvePoint(30f, 8f),
        //     new CurvePoint(0f, 1f),
        //     new CurvePoint(-10, 0.1f)
        // };

        private static readonly SimpleCurve HitPointsPercentScoreFactorCurve = new SimpleCurve
        {
            new CurvePoint( 0.0f, 0.05f ),
            new CurvePoint( 0.4f, 0.3f ),
            new CurvePoint( 0.6f, 0.75f ),
            new CurvePoint( 1f, 1f )

  //                    new CurvePoint( 0.0f, 0.0f ),
  //        new CurvePoint( 0.25f, 0.15f ),
  //        new CurvePoint( 0.5f, 0.7f ),
  //        new CurvePoint( 1f, 1f )


        //  new CurvePoint(0f, 0.1f),
        //  new CurvePoint(0.6f, 0.7f),
        //  new CurvePoint(1f, 1f)
        };


        public PawnCalcForApparel(Pawn pawn)
            : this(MapComponent_AutoEquip.Get.GetCache(pawn))
        {
        }


        public PawnCalcForApparel(SaveablePawn saveablePawn)
        {
            _saveablePawn = saveablePawn;
            _pawn = saveablePawn.Pawn;
            _outfit = _pawn.outfits.CurrentOutfit;

            _stats = saveablePawn.NormalizeCalculedStatDef().ToArray();
            _workstats = saveablePawn.NormalizeCalculedWorkStatDef().ToArray();
            _neededWarmth = CalculateNeededWarmth(_pawn, GenDate.CurrentMonth);
        }

        public IEnumerable<Saveable_Pawn_StatDef> Stats => _stats;

        public IEnumerable<Saveable_Pawn_WorkStatDef> WorkStats => _workstats;

        public IEnumerable<Apparel> CalculatedApparel => _calculatedApparelItems;

        public void InitializeFixedApparelsAndGetAvaliableApparels(List<Apparel> allApparels)
        {
            _fixedApparels = new List<Apparel>();
            foreach (Apparel pawnApparel in _pawn.apparel.WornApparel)
                if (_pawn.outfits.forcedHandler.AllowedToAutomaticallyDrop(pawnApparel))
                    allApparels.Insert(0, pawnApparel);
                else
                    _fixedApparels.Add(pawnApparel);
        }

        public void InitializeAllApparelScores(List<Apparel> allApparels)
        {
            _allApparelsItems = new List<Apparel>();
            _allApparelsScore = new List<float>();
            foreach (Apparel apparel in allApparels)
            {

                _allApparelsItems.Add(apparel);
                _allApparelsScore.Add(ApparelScoreRaw(apparel));

            }
        }

        public void DIALOG_InitializeCalculatedApparelScoresFromWornApparel()
        {
            _calculatedApparelItems = new List<Apparel>();
            _calculatedApparelScore = new List<float>();
            foreach (Apparel apparel in _pawn.apparel.WornApparel)
            {
                _calculatedApparelItems.Add(apparel);
                _calculatedApparelScore.Add(ApparelScoreRaw(apparel));
            }
            _optimized = false;
        }

        public void InitializeCalculatedApparelScoresFromFixedApparel()
        {
            _calculatedApparelItems = new List<Apparel>();
            _calculatedApparelScore = new List<float>();
            foreach (Apparel apparel in _fixedApparels)
            {
                _calculatedApparelItems.Add(apparel);
                _calculatedApparelScore.Add(ApparelScoreRaw(apparel));
            }
            _optimized = false;
        }

        public delegate void ApparelScoreRawStatsHandler(Pawn pawn, Apparel apparel, StatDef statDef, ref float num);

        public static event ApparelScoreRawStatsHandler ApparelScoreRaw_PawnStatsHandlers;

        public static void DoApparelScoreRaw_PawnStatsHandlers(Pawn pawn, Apparel apparel, StatDef statDef, ref float num)
        {
            if (ApparelScoreRaw_PawnStatsHandlers != null)
                ApparelScoreRaw_PawnStatsHandlers(pawn, apparel, statDef, ref num);
        }



        #region [  ApparelScoreRaw_PawnWorkStats  ]

        public float ApparelScoreRaw(Apparel ap)
        {
            float num = 1;
            if (ap == null)
                return num;

            num += ApparelScoreRaw_PawnStats(ap);
            num += ApparelScoreRaw_PawnWorkStats(ap);
            num += (0.125f * ApparelScoreRaw_ProtectionBaseStat(ap));
            num *= ApparelScoreRaw_HitPointAdjust(ap);

            float insulation = ApparelScoreRaw_InsulationColdAdjust(ap);
            if (insulation < 1)
                insulation /= 8;
            num *= insulation;

            // new insulation calc

            return num;
        }

        public float ApparelScoreRaw(Apparel ap, bool useInsulation)
        {
            float num = 1;
            if (ap == null)
                return num;

            num += ApparelScoreRaw_PawnStats(ap);
            num += ApparelScoreRaw_PawnWorkStats(ap);
            num += (0.125f * ApparelScoreRaw_ProtectionBaseStat(ap));
            num *= ApparelScoreRaw_HitPointAdjust(ap);


            if (useInsulation)
            {
                float insulation = ApparelScoreRaw_InsulationColdAdjust(ap);
                if (insulation < 1)
                    insulation /= 8;

                num *= insulation;
            }

            // new insulation calc

            return num;
        }

        public float ApparelScoreRaw_ProtectionBaseStat(Apparel ap)
        {
            float score = 1f;
            float protectionScore =
                ap.GetStatValue(StatDefOf.ArmorRating_Sharp) +
                ap.GetStatValue(StatDefOf.ArmorRating_Blunt) * 0.75f;
            score += protectionScore * 1.25f;
            return score;
        }

        public float ApparelScoreRaw_PawnStats(Apparel ap)
        {
            float workScore = 0f;

            foreach (Saveable_Pawn_StatDef stat in Stats)
            {
                try
                {
                    var workStatValue = GetStatValue(ap, stat);

                    var workStatStrength = stat.Strength;

                    if (workStatValue.Equals(0))
                        workStatValue = 1;

                    if (workStatValue < 1)
                    {
                        workStatValue = 1 / workStatValue;  // inverts negative values and 1:x
                        workStatStrength = workStatStrength * -1;
                    }

                    if (workStatValue <= 0.99f || workStatValue >= 1.01f)
                    {
                        workScore += workStatValue * workStatStrength;
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("Error Calculation Stat: " + stat.StatDef, e);
                }
            }
            return workScore;

        }

        public float ApparelScoreRaw_PawnWorkStats(Apparel ap)
        {
            float workScore = 0f;

            foreach (Saveable_Pawn_WorkStatDef workstat in WorkStats)
            {
                try
                {
                    var workStatValue = GetWorkStatValue(ap, workstat);

                    var workStatStrength = workstat.Strength;

                    if (workStatValue.Equals(0))
                        workStatValue = 1;

                    if (workStatValue < 1)
                    {
                        workStatValue = 1 / workStatValue;  // inverts negative values and 1:x
                        workStatStrength = workStatStrength * -1;
                    }

                    if (workStatValue <= 0.99f || workStatValue >= 1.01f)
                    {
                        workScore += workStatValue * workStatStrength;
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("Error Calculation Stat: " + workstat.StatDef, e);
                }
            }
            return workScore;
        }

        public static IEnumerable<KeyValuePair<StatDef, float>> GetStatsOfWorkType(WorkTypeDef worktype)
        {
            switch (worktype.defName)
            {
                case "Research":
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("ResearchSpeed"), 1f);
                    yield break;
                case "Cleaning":
                    //            yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("MoveSpeed"), 1f);
                    yield break;
                case "Hauling":
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("MoveSpeed"), 0.25f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("CarryingCapacity"), 1f);
                    yield break;
                case "Crafting":
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("WorkSpeedGlobal"), 0.125f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("StonecuttingSpeed"), 0.5f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("SmeltingSpeed"), 0.5f);
                    yield break;
                case "Art":
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("WorkSpeedGlobal"), 0.125f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("SculptingSpeed"), 1f);
                    yield break;
                case "Tailoring":
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("WorkSpeedGlobal"), 0.125f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("TailoringSpeed"), 1f);
                    yield break;
                case "Smithing":
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("WorkSpeedGlobal"), 0.125f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("SmithingSpeed"), 1f);
                    yield break;
                case "PlantCutting":
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("HarvestFailChance"), -0.25f);
                    yield break;
                case "Growing":
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("WorkSpeedGlobal"), 0.125f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("MoveSpeed"), 0.125f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("PlantWorkSpeed"), 1f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("HarvestFailChance"), -0.75f);
                    yield break;
                case "Mining":
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("WorkSpeedGlobal"), 0.125f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("MoveSpeed"), 0.0625f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("MiningSpeed"), 1f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("CarryingCapacity"), 0.25f);
                    yield break;
                case "Repair":
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("FixBrokenDownBuildingFailChance"), -1f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("WorkSpeedGlobal"), 0.1f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("MoveSpeed"), 0.0625f);
                    yield break;
                case "Construction":
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("WorkSpeedGlobal"), 0.1f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("MoveSpeed"), 0.0625f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("ConstructionSpeed"), 1f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("SmoothingSpeed"), 0.5f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("CarryingCapacity"), 0.25f);
                    yield break;
                case "Hunting":
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("MoveSpeed"), 0.5f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("AimingDelayFactor"), -0.75f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("ShootingAccuracy"), 1f);
                    //   yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("AimingAccuracy"), 1f); // CR
                    //   yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("ReloadSpeed"), 0.25f); // CR
                    //       yield return new KeyValuePair<StatDef, float>(StatDefOf.ArmorRating_Blunt, 0.0625f);
                    //       yield return new KeyValuePair<StatDef, float>(StatDefOf.ArmorRating_Sharp, 0.0625f);
                    yield break;
                case "Cooking":
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("MoveSpeed"), 0.0625f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("WorkSpeedGlobal"), 0.1f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("CookSpeed"), 1f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("FoodPoisonChance"), -0.5f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("BrewingSpeed"), 0.75f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("ButcheryFleshSpeed"), 0.75f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("ButcheryFleshEfficiency"), 0.5f);
                    yield break;
                case "Handling":
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("MoveSpeed"), 0.25f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("CarryingCapacity"), 0.125f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("TameAnimalChance"), 1f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("TrainAnimalChance"), 1f);
                    //      yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("MeleeDPS"), 1.0f);
                    //        yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("MeleeHitChance"), 0.25f);
                    //               yield return new KeyValuePair<StatDef, float>(StatDefOf.ArmorRating_Blunt, 0.0625f);
                    //                yield return new KeyValuePair<StatDef, float>(StatDefOf.ArmorRating_Sharp, 0.0625f);
                    //         yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("CarryWeight"), 0.25f); // CR
                    //         yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("CarryBulk"), 0.25f); // CR
                    yield break;
                case "Warden":
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("SocialImpact"), 0.5f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("RecruitPrisonerChance"), 1f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("GiftImpact"), 0.25f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("TradePriceImprovement"), 0.5f);
                    yield break;
                case "Flicker":
                    yield break;
                case "Patient":
                    yield break;
                case "Firefighter":
                    yield break;
                case "Doctor":
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("MedicalOperationSpeed"), 0.75f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("SurgerySuccessChance"), 0.75f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("BaseHealingQuality"), 1f);
                    yield return new KeyValuePair<StatDef, float>(DefDatabase<StatDef>.GetNamed("HealingSpeed"), 0.5f);
                    yield break;
                default:
                    yield break;
            }
        }

        public float GetStatValue(Apparel apparel, Saveable_Pawn_StatDef stat)
        {

            float baseStat = apparel.GetStatValue(stat.StatDef, true);
            float currentStat = baseStat + apparel.def.equippedStatOffsets.GetStatOffsetFromList(stat.StatDef);
            //            currentStat += apparel.def.equippedStatOffsets.GetStatOffsetFromList(stat.StatDef);

            DoApparelScoreRaw_PawnStatsHandlers(_pawn, apparel, stat.StatDef, ref currentStat);

            //   if (stat.StatDef.defName.Equals("PsychicSensitivity"))
            //   {
            //       return apparel.def.equippedStatOffsets.GetStatOffsetFromList(stat.StatDef) - baseStat;
            //   }

            if (baseStat != 0)
            {
                currentStat = currentStat / baseStat;
            }

            return currentStat;
        }

        public float GetWorkStatValue(Apparel apparel, Saveable_Pawn_WorkStatDef workStat)
        {
            float baseStat = apparel.GetStatValue(workStat.StatDef, true);

            var currentStat = baseStat + apparel.def.equippedStatOffsets.GetStatOffsetFromList(workStat.StatDef);

            DoApparelScoreRaw_PawnStatsHandlers(_pawn, apparel, workStat.StatDef, ref currentStat);

            if (baseStat != 0)
            {
                currentStat = currentStat / baseStat;
            }

            return currentStat;

        }

        #endregion

        #region [  OLD_CalculateApparelModifierRaw  ]


        public float ApparelScoreRaw_InsulationColdAdjust(Apparel ap)
        {
            switch (_neededWarmth)
            {
                case NeededWarmth.Warm:
                    {
                        float statValueAbstract = ap.def.GetStatValueAbstract(StatDefOf.Insulation_Cold, null);
                        return InsulationColdScoreFactorCurve_NeedWarm.Evaluate(statValueAbstract);
                    }
                case NeededWarmth.Cool:
                    {
                        float statValueAbstract = ap.def.GetStatValueAbstract(StatDefOf.Insulation_Heat, null);
                        return InsulationWarmScoreFactorCurve_NeedCold.Evaluate(statValueAbstract);
                    }
                default:
                    return 1;
            }
        }

        #endregion

        #region [  CalculateApparelScoreGain  ]

        public bool CalculateApparelScoreGain(Apparel apparel, out float gain)
        {
            if (_calculatedApparelItems == null)
                DIALOG_InitializeCalculatedApparelScoresFromWornApparel();

            return CalculateApparelScoreGain(apparel, ApparelScoreRaw(apparel), out gain);
        }

        private bool CalculateApparelScoreGain(Apparel apparel, float score, out float candidateScore)
        {
            if (apparel.def == ThingDefOf.Apparel_PersonalShield && _pawn.equipment.Primary != null &&
                !_pawn.equipment.Primary.def.Verbs[0].MeleeRange)
            {
                candidateScore = -1000f;
                return false;
            }

            candidateScore = score;
            bool willReplace = false;

            for (int i = 0; i < _calculatedApparelItems.Count; i++)
            {
                Apparel wornApparel = _calculatedApparelItems[i];

                if (!ApparelUtility.CanWearTogether(wornApparel.def, apparel.def))
                {
                    if (!_pawn.outfits.forcedHandler.AllowedToAutomaticallyDrop(wornApparel))
                    {
                        return false;
                    }
                    candidateScore -= _calculatedApparelScore[i]; //+= ???? -= old
                    willReplace = true;
                }
            }

            if (!willReplace)
            {
                candidateScore *= 10f;
            }

            return true;
        }

        #endregion

        #region [  DoOptimize  ]

        public static void DoOptimizeApparel(List<PawnCalcForApparel> newCalcList, List<Apparel> allApparels)
        {
            foreach (PawnCalcForApparel pawnCalc in newCalcList)
            {
                pawnCalc.InitializeAllApparelScores(allApparels);
                pawnCalc.InitializeCalculatedApparelScoresFromFixedApparel();
                //          pawnCalc.DIALOG_InitializeCalculatedApparelScoresFromWornApparel();;
            }

            while (true)
            {
                bool changed = false;
                foreach (PawnCalcForApparel pawnCalc in newCalcList)
                {
                    pawnCalc.OptimizeFromList(ref changed);
                }

                if (!CheckForConflicts(newCalcList))
                    break;
            }

            foreach (PawnCalcForApparel pawnCalc in newCalcList)
                pawnCalc.PassToSaveable();
        }

        #region [  Conflict  ]

        private static bool CheckForConflicts(IEnumerable<PawnCalcForApparel> pawns)
        {
            bool any = false;
            PawnCalcForApparel[] pawnCalcForApparels = pawns as PawnCalcForApparel[] ?? pawns.ToArray();
            foreach (PawnCalcForApparel pawnCalc in pawnCalcForApparels)
            {
                foreach (Apparel apparel in pawnCalc.CalculatedApparel)
                {
                    float? apparalGainPercentual = null;

                    foreach (PawnCalcForApparel otherPawnCalc in pawnCalcForApparels)
                    {
                        if (otherPawnCalc == pawnCalc)
                            continue;

                        foreach (Apparel otherApparel in otherPawnCalc.CalculatedApparel)
                        {
                            if (otherApparel == apparel)
                            {
                                any = true;
                                DoConflict(apparel, pawnCalc, otherPawnCalc, ref apparalGainPercentual);
                                break;
                            }
                        }

                        if (!pawnCalc._optimized ||
                            !otherPawnCalc._optimized)
                            break;
                    }

                    if (!pawnCalc._optimized)
                        break;
                }
            }

#if LOG && CONFLICT
            MapComponent_AutoEquip.logMessage.AppendLine();
#endif

            return any;
        }

        private static void DoConflict(Apparel apparel, PawnCalcForApparel pawnX, PawnCalcForApparel pawnY, ref float? xPercentual)
        {
            if (pawnY._saveablePawn.Pawn.apparel.WornApparel.Contains(apparel))
            {
                pawnX.LoseConflict(apparel);
            }

            else
            {
                pawnY.LoseConflict(apparel);
            }

            //    if (!xPercentual.HasValue)
            //    {
            //        if (pawnX._totalStats == null)
            //            pawnX._totalStats = pawnX.ApparelScoreRaw(null);
            //
            //        float xNoStats = pawnX.ApparelScoreRaw(apparel);
            //        xPercentual = pawnX._totalStats / xNoStats;
            //
            //        if (pawnX._saveablePawn.Pawn.apparel.WornApparel.Contains(apparel))
            //            xPercentual += 1000f;
            //    }
            //
            //    if (pawnY._totalStats == null)
            //        pawnY._totalStats = pawnY.ApparelScoreRaw(null);
            //
            //    float yNoStats = pawnY.ApparelScoreRaw(apparel);
            //    float yPercentual = pawnY._totalStats.Value / yNoStats;
            //
            //    if (pawnY._totalStats != null)
            //    {
            //        if (pawnY._saveablePawn.Pawn.apparel.WornApparel.Contains(apparel))
            //            yPercentual += 1000f;
            //
            //        if (xPercentual.Value > yPercentual) // added *10 to minimize apparel switching
            //        {
            //            pawnY.LoseConflict(apparel);
            //        }
            //        else
            //        {
            //            pawnX.LoseConflict(apparel);
            //        }
            //    }
        }


        private void LoseConflict(Apparel apparel)
        {
            _optimized = false;
            int index = _calculatedApparelItems.IndexOf(apparel);
            if (index == -1)
                Log.Warning("Warning on LoseConflict loser didnt have the apparel");
            _calculatedApparelItems.RemoveAt(index);
            _calculatedApparelScore.RemoveAt(index);
        }

        private void OptimizeFromList(ref bool changed)
        {
            if (_optimized)
                return;

            while (true)
            {
                int changeIndex = -1;
                Apparel changeApparel = null;
                float changeApparelRawScore = 0;
                float changeApparelScoreGain = 0f;


                for (int i = 0; i < _allApparelsItems.Count; i++)
                {
                    Apparel apparel = _allApparelsItems[i];
                    if (!apparel.IsForbidden(_saveablePawn.Pawn) && _outfit.filter.Allows(apparel))
                    {
                        float apparelRawScore = _allApparelsScore[i];
                        float apparelGainScore;
                        if (CalculateApparelScoreGain(apparel, apparelRawScore, out apparelGainScore))
                        {
                            //                            if ((apparelGainScore > changeApparelScoreGain)
                            if ((apparelGainScore > changeApparelScoreGain)
                                || ((apparelGainScore == changeApparelScoreGain) && _saveablePawn.Pawn.apparel.WornApparel.Contains(apparel)))
                            {
                                changeIndex = i;
                                changeApparel = apparel;
                                changeApparelRawScore = apparelRawScore;
                                changeApparelScoreGain = apparelGainScore;
                            }
                        }
                    }
                }

                if (changeApparel == null)
                {
                    _optimized = true;
                    return;
                }
                changed = true;
                _allApparelsItems.RemoveAt(changeIndex);
                _allApparelsScore.RemoveAt(changeIndex);

                _calculatedApparelItems.Add(changeApparel);
                _calculatedApparelScore.Add(changeApparelRawScore);
            }
        }

        #endregion

        public float ApparelScoreRaw_HitPointAdjust(Apparel ap)
        {
            if (ap.def.useHitPoints)
            {
                float x = ap.HitPoints / (float)ap.MaxHitPoints;
                return HitPointsPercentScoreFactorCurve.Evaluate(x);
            }
            return 1;
        }


        public static NeededWarmth CalculateNeededWarmth(Pawn pawn, Month month)
        {
            float temperature = GenTemperature.AverageTemperatureAtWorldCoordsForMonth(Find.Map.WorldCoords, month);

            if (Find.MapConditionManager.ActiveConditions.OfType<MapCondition_HeatWave>().Any())
            {
                temperature += 20;
            }

            if (Find.MapConditionManager.ActiveConditions.OfType<MapCondition_ColdSnap>().Any())
            {
                temperature -= 20;
            }


            if (temperature < pawn.def.GetStatValueAbstract(StatDefOf.ComfyTemperatureMin, null) - 10f)
                return NeededWarmth.Warm;

            if (temperature > pawn.def.GetStatValueAbstract(StatDefOf.ComfyTemperatureMax, null) + 10f)
                return NeededWarmth.Cool;

            return NeededWarmth.Any;
        }


        private void PassToSaveable()
        {
            _saveablePawn.ToWearApparel = new List<Apparel>();
            _saveablePawn.ToDropApparel = new List<Apparel>();
            _saveablePawn.TargetApparel = _calculatedApparelItems;

            List<Apparel> pawnApparel = new List<Apparel>(_saveablePawn.Pawn.apparel.WornApparel);
            foreach (Apparel ap in _calculatedApparelItems)
            {
                if (pawnApparel.Contains(ap))
                {
                    pawnApparel.Remove(ap);
                    continue;
                }
                _saveablePawn.ToWearApparel.Add(ap);
            }
            foreach (Apparel ap in pawnApparel)
                _saveablePawn.ToDropApparel.Add(ap);
        }
#if LOG && CHANGES
            if (this.saveablePawn.toWearApparel.Any() || this.saveablePawn.toDropApparel.Any())
            {
                MapComponent_AutoEquip.logMessage.AppendLine();
                MapComponent_AutoEquip.logMessage.AppendLine("Apparel Change for: " + this.pawn.LabelCap);

                foreach (Apparel ap in this.saveablePawn.toDropApparel)
                    MapComponent_AutoEquip.logMessage.AppendLine(" * Drop: " + ap);

                foreach (Apparel ap in this.saveablePawn.toWearApparel)
                    MapComponent_AutoEquip.logMessage.AppendLine(" * Wear: " + ap);
            }
#endif
        #endregion
    }
}