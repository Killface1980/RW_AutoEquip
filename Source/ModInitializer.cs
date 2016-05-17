﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;

namespace AutoEquip
{
    public class ModInitializer : ITab
    {
        protected GameObject modInitializerControllerObject;

        public ModInitializer()
        {
            modInitializerControllerObject = new GameObject("ModInitializer");
            modInitializerControllerObject.AddComponent<ModInitializerBehaviour>();
            UnityEngine.Object.DontDestroyOnLoad((UnityEngine.Object)modInitializerControllerObject);
        }

        protected override void FillTab() { }
    }

    class ModInitializerBehaviour : MonoBehaviour
    {
        protected GameObject ModObject;
        protected bool ReinjectNeeded = false;
        protected float ReinjectTime = 0;

        public void OnLevelWasLoaded(int level)
        {
            ReinjectNeeded = true;
            if (level >= 0)
                ReinjectTime = 1;
            else
                ReinjectTime = 0;
        }        

        public void FixedUpdate()
        {
            if (ReinjectNeeded)
            {
                ReinjectTime -= Time.fixedDeltaTime;

                if (ReinjectTime <= 0)
                {
                    ReinjectNeeded = false;
                    ReinjectTime = 0;

#if LOG
                    Log.Message("AutoEquip Injected");
#endif
                    MapComponent_AutoEquip component = MapComponent_AutoEquip.Get;
                }
            }
        }

        public void Start()
        {
            MethodInfo coreMethod = typeof(JobGiver_OptimizeApparel).GetMethod("TryGiveTerminalJob", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo autoEquipMethod = typeof(AutoEquip_JobGiver_OptimizeApparel).GetMethod("_TryGiveTerminalJob", BindingFlags.Static | BindingFlags.NonPublic);

            if (!CommunityCoreLibrary.Detours.TryDetourFromTo(coreMethod, autoEquipMethod))
                Log.Error("Could not Detour AutoEquip.");

            OnLevelWasLoaded(-1);            
        }
    }
}
