﻿using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutoEquip
{
    public class AutoEquip_JobGiver_OptimizeApparel
    {
        private const int ApparelOptimizeCheckInterval = 3000;

        private static void SetNextOptimizeTick(Pawn pawn)
        {
            pawn.mindState.nextApparelOptimizeTick = Find.TickManager.TicksGame + 500;
            //            pawn.mindState.nextApparelOptimizeTick = Find.TickManager.TicksGame + 3000;
        }

        internal Job _TryGiveTerminalJob(Pawn pawn)
        {
            if (pawn.outfits == null)
            {
                Log.ErrorOnce(pawn + " tried to run JobGiver_OptimizeApparel without an OutfitTracker", 5643897);
                return null;
            }

            if (pawn.Faction != Faction.OfColony)
            {
                Log.ErrorOnce("Non-colonist " + pawn + " tried to optimize apparel.", 764323);
                return null;
            }

            SaveablePawn configurarion = MapComponent_AutoEquip.Get.GetCache(pawn);

            #region [  Wear Apparel  ]

            if (configurarion.ToWearApparel.Count > 0)
            {
                List<Thing> listToWear = Find.ListerThings.ThingsInGroup(ThingRequestGroup.Apparel);
                if (listToWear.Count > 0)
                {
                    foreach (var thing in listToWear)
                    {
                        var ap = (Apparel)thing;
                        if (!configurarion.ToWearApparel.Contains(ap)) continue;
                        if (Find.SlotGroupManager.SlotGroupAt(thing.Position) == null) continue;
                        if (thing.IsForbidden(pawn)) continue;
                        if (!ApparelUtility.HasPartsToWear(pawn, thing.def)) continue;

                        if (!ap.IsInValidStorage()) continue;
                        if (pawn.CanReserveAndReach(ap, PathEndMode.OnCell, pawn.NormalMaxDanger(), 1))
                        //                                if (pawn.CanReserveAndReach(ap, PathEndMode.OnCell, pawn.NormalMaxDanger(), 1))
                        {

                            configurarion.ToWearApparel.Remove(ap);
                            return new Job(JobDefOf.Wear, ap);
                        }
                    }
                }
            }

            #endregion

            #region [  Drops unequiped  ]

            if (configurarion.ToDropApparel != null)
                for (int i = configurarion.ToDropApparel.Count - 1; i >= 0; i--)
                {
                    Apparel a = configurarion.ToDropApparel[i];
                    configurarion.ToDropApparel.Remove(a);

                    if (pawn.apparel.WornApparel.Contains(a))
                    {
                        Apparel t;
                        if (pawn.apparel.TryDrop(a, out t))
                        {
                            t.SetForbidden(false, true);

                            Job job = HaulAIUtility.HaulToStorageJob(pawn, t);

                            if (job != null)
                                return job;
                            else
                            {
                                pawn.mindState.nextApparelOptimizeTick = Find.TickManager.TicksGame + 350;
                                return null;
                            }
                        }
                    }
                }

            #endregion



            //  #region [  If no Apparel is Selected to Wear, Delays the next search  ]
            //
            //  if (thing == null)
            //  {
            //      SetNextOptimizeTick(pawn);
            //      return null;
            //  } 
            //
            //  #endregion
            //
            //  return new Job(JobDefOf.Wear, thing);

            pawn.mindState.nextApparelOptimizeTick = Find.TickManager.TicksGame + 350;
            return null;
        }



    }
}
