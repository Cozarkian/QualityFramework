﻿using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace QualityFramework
{
    public class Quality_Generator
    {
        public static QualityCategory GenerateQualityCreatedByPawn(Pawn pawn, SkillDef relevantSkill, Thing thing, int supplyQuality = -1, List<SkillRequirement> skillRequirements = null)
        {
            //Log.Message("Applying custom quality with " + relevantSkill.label);
            if (ModSettings_QFramework.skilledStoneCutting && thing.HasThingCategory(ThingCategoryDefOf.StoneBlocks))
            {
                relevantSkill = SkillDefOf.Crafting;
            }
            if (relevantSkill == null || (!ModSettings_QFramework.skilledButchering && (thing.def.IsMeat || thing.def.IsLeather)))
            {
                return QualityUtility.GenerateQuality(QualityGenerator.BaseGen);
                //Log.Message("Generated random quality for " + thing.LabelShort);
            }
            QualityCategory qualityCategory;
            int minQuality = GetMinQuality(thing);
            int maxQuality = GetMaxQuality(thing);
            int level = pawn.skills.GetSkill(relevantSkill).Level; //Log.Message(relevantSkill.label + " without supplies is level " + level);
            if (ModSettings_QFramework.useSkillReq)
            {
                //Log.Message("Applying " + relevantSkill.label + " skill requirements.");
                if (skillRequirements != null)
                {
                    for (int sk = 0; sk < skillRequirements.Count; sk++)
                    {
                        level -= skillRequirements[sk].minLevel;
                    }
                }
                else if (relevantSkill == SkillDefOf.Plants)
                {
                    int plantSkill = 0;
                    foreach (var plantDef in DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def.plant?.sowMinSkill != null))
                    {
                        if (plantDef.plant.harvestedThingDef == thing.def) plantSkill = Mathf.Max(plantSkill, plantDef.plant.sowMinSkill);
                    }
                    level -= plantSkill;
                    //Log.Message("Deducted " + plantSkill + " from harvest quality");
                }
                else if (thing.def is BuildableDef)
                {
                    level -= thing.def.constructionSkillPrerequisite;
                    //Log.Message("Deducting " + thing.def.constructionSkillPrerequisite / 2 + " from construction skill level");
                }
            }
            InspirationDef inspirationDef = InspirationUtility.CheckInspired(thing.def, relevantSkill);
            bool inspired = (inspirationDef != null && pawn.InspirationDef == inspirationDef);
            
            if (ModSettings_QFramework.useMaterialQuality || ModSettings_QFramework.useTableQuality)
            {
                if (supplyQuality >= 0)
                {
                    level += supplyQuality - Mathf.Min(maxQuality, ModSettings_QFramework.stdSupplyQuality);
                }
                level = Mathf.Clamp(level, 0, 20); //Log.Message("Level with supplies is " + level.ToString());
            }

            qualityCategory = QualityUtility.GenerateQualityCreatedByPawn(level, inspired);
            if (ModsConfig.IdeologyActive && pawn.Ideo != null)
            {
                Precept_Role role = pawn.Ideo.GetRole(pawn);
                if (role != null && role.def.roleEffects != null)
                {
                    RoleEffect roleEffect = role.def.roleEffects.FirstOrDefault((RoleEffect eff) => eff is RoleEffect_ProductionQualityOffset);
                    if (roleEffect != null)
                    {
                        qualityCategory = (QualityCategory)Mathf.Min((int)(qualityCategory + (byte)((RoleEffect_ProductionQualityOffset)roleEffect).offset), 6);
                    }
                }
            }
            if (inspired && maxQuality > 4)
            {
                pawn.mindState.inspirationHandler.EndInspiration(inspirationDef);
            }
            return (QualityCategory)Mathf.Clamp((int)qualityCategory, minQuality, maxQuality);
        }

        public static int GetMinQuality(Thing thing)
        {
            //Log.Message("Finding minimum quality");
            ThingDef def = thing.def;
            int minQuality = 0;
            if (def.IsStuff && ModSettings_QFramework.stuffQuality) minQuality = ModSettings_QFramework.minStuffQuality;
            else if (def.building != null)
            {
                if (def.IsWorkTable && ModSettings_QFramework.workQuality) minQuality = ModSettings_QFramework.minWorkQuality;
                else if ((def.IsWithinCategory(ThingCategoryDef.Named("BuildingsSecurity")) || def.building.IsTurret) && ModSettings_QFramework.securityQuality) minQuality = ModSettings_QFramework.minSecurityQuality;
                else if (def.building != null && ModSettings_QFramework.edificeQuality) minQuality = ModSettings_QFramework.minEdificeQuality;
            }         
            else if (def.IsDrug && ModSettings_QFramework.drugQuality) minQuality = ModSettings_QFramework.minDrugQuality;
            else if (def.IsMedicine && ModSettings_QFramework.medQuality) minQuality = ModSettings_QFramework.minMedQuality;
            else if (def.ingestible != null)
            {
                if (def.thingCategories.Contains(ThingCategoryDefOf.FoodMeals) && ModSettings_QFramework.mealQuality) minQuality = ModSettings_QFramework.minMealQuality;
                else if (def.IsNutritionGivingIngestible)
                {
                    if (def.ingestible.preferability == FoodPreferability.RawTasty) minQuality = ModSettings_QFramework.minTastyQuality;
                    else minQuality = ModSettings_QFramework.minIngQuality;
                }
            }
            else if (def.IsWeapon) minQuality = ModSettings_QFramework.minWeaponQuality;
            else if (def.IsApparel) minQuality = ModSettings_QFramework.minApparelQuality;
            else if (def.IsShell && ModSettings_QFramework.shellQuality) minQuality = ModSettings_QFramework.minShellQuality;
            else if (def.thingCategories.Contains(ThingCategoryDefOf.Manufactured)) minQuality = ModSettings_QFramework.minManufQuality;
            return minQuality;
        }

        public static int GetMaxQuality(Thing thing)
        {
            //Log.Message("Finding maximum quality");
            ThingDef def = thing.def;
            int maxQuality = 6;
            //Art can always be legendary
            if (thing.TryGetComp<CompArt>() != null)
            {
                return maxQuality;
            }
            if (def.IsStuff && ModSettings_QFramework.stuffQuality) maxQuality = ModSettings_QFramework.maxStuffQuality;
            else if (def.building != null)
            {
                if (def.IsWorkTable && ModSettings_QFramework.workQuality) maxQuality = ModSettings_QFramework.maxWorkQuality;
                else if ((def.IsWithinCategory(ThingCategoryDef.Named("BuildingsSecurity")) || def.building.IsTurret) && ModSettings_QFramework.securityQuality) maxQuality = ModSettings_QFramework.maxSecurityQuality;
                else if (def.building != null && ModSettings_QFramework.edificeQuality) maxQuality = ModSettings_QFramework.maxEdificeQuality;
            }
            else if (def.IsDrug && ModSettings_QFramework.drugQuality) maxQuality = ModSettings_QFramework.maxDrugQuality;
            else if (def.IsMedicine && ModSettings_QFramework.medQuality) maxQuality = ModSettings_QFramework.maxMedQuality;
            else if (def.ingestible != null)
            {
                if (def.thingCategories.Contains(ThingCategoryDefOf.FoodMeals) && ModSettings_QFramework.mealQuality) maxQuality = ModSettings_QFramework.maxMealQuality;
                else if (def.IsNutritionGivingIngestible)
                {
                    if (def.ingestible.preferability == FoodPreferability.RawTasty) maxQuality = ModSettings_QFramework.maxTastyQuality;
                    else maxQuality = ModSettings_QFramework.maxIngQuality;
                }
            }
            else if (def.IsWeapon) maxQuality = ModSettings_QFramework.maxWeaponQuality;
            else if (def.IsApparel) maxQuality = ModSettings_QFramework.maxApparelQuality;
            else if (def.IsShell && ModSettings_QFramework.shellQuality) maxQuality = ModSettings_QFramework.maxShellQuality;
            else if (def.thingCategories.Contains(ThingCategoryDefOf.Manufactured)) maxQuality = ModSettings_QFramework.maxManufQuality;
            return maxQuality;
        }
    }
}
