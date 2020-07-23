using System;
using System.Collections.Generic;
using System.Text;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoDecay", "RFC1920", "1.0.40", ResourceId = 1160)]  //Original Credit to Deicide666ra/Piarb and Diesel_42o
    [Description("Scales or disables decay of items")]

    class NoDecay : RustPlugin
    {
        private float c_twigMultiplier;
        private float c_woodMultiplier;
        private float c_stoneMultiplier;
        private float c_sheetMultiplier;
        private float c_armoredMultiplier;

        private float c_campfireMultiplier;
        private float c_highWoodWallMultiplier;
        private float c_highStoneWallMultiplier;
        private float c_barricadeMultiplier;
        private float c_trapMultiplier;
        private float c_deployablesMultiplier;
        private float c_boxMultiplier;
        private float c_sedanMultiplier;
        private float c_samMultiplier;
        private float c_baloonMultiplier;
        private float c_furnaceMultiplier;
        private float c_bbqMultiplier;
        private float c_boatMultiplier;
        private float c_minicopterMultiplier;
        private float c_scrapcopterMultiplier;
        private float c_watchtowerMultiplier;
        private float c_horseMultiplier;

        private bool c_outputToRcon;
        private bool c_outputMundane;
        private bool c_usePermission;
        private bool c_requireCupboard;
        private bool c_CupboardEntity;
        private bool c_blockCupboardResources;
        private bool c_blockCupboardWood;
        private float c_cupboardRange;

        private bool g_configChanged;

        void Loaded() => LoadConfigValues();

        protected override void LoadDefaultConfig() => Puts("New configuration file created.");

        void Init()
        {
            permission.RegisterPermission("nodecay.use", this);
        }

        void LoadConfigValues()
        {
            c_twigMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "twigMultiplier", 1.0));
            c_woodMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "woodMultiplier", 0.0));
            c_stoneMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "stoneMultiplier", 0.0));
            c_sheetMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "sheetMultiplier", 0.0));
            c_armoredMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "armoredMultiplier", 0.0));

            c_deployablesMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "deployablesMultiplier", 0.0));
            c_watchtowerMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "watchtowerMultiplier", 0.0));
            c_horseMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "horseMultiplier", 0.0));
            c_boxMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "boxMultiplier", 0.0));
            c_sedanMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "sedanMultiplier", 0.0));
            c_baloonMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "baloonMultiplier", 0.0));
            c_furnaceMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "furnaceMultiplier", 0.0));
            c_bbqMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "bbqMultiplier", 0.0));
            c_samMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "samMultiplier", 0.0));
            c_campfireMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "campfireMultiplier", 0.0));
            c_barricadeMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "barricadesMultiplier", 0.0));
            c_trapMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "trapMultiplier", 0.0));
            c_highWoodWallMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "highWoodWallMultiplier", 0.0));
            c_highStoneWallMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "highStoneWallMultiplier", 0.0));
            c_boatMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "boatMultiplier", 0.0));
            c_minicopterMultiplier  = Convert.ToSingle(GetConfigValue("Mutipliers", "minicopterMultiplier", 0.0));
            c_scrapcopterMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "scrapcopterMultiplier", 0.0));

            c_outputToRcon  = Convert.ToBoolean(GetConfigValue("Debug", "outputToRcon", false));
            c_outputMundane = Convert.ToBoolean(GetConfigValue("Debug", "outputMundane", false));
            c_usePermission = Convert.ToBoolean(GetConfigValue("Global", "usePermission", false));
            c_blockCupboardResources = Convert.ToBoolean(GetConfigValue("Global", "blockCupboardResources", false));
            c_blockCupboardWood = Convert.ToBoolean(GetConfigValue("Global", "blockCupboardWood", false));

            try
            {
                c_requireCupboard = Convert.ToBoolean(GetConfigValue("Global", "requireCupboard", false));
                c_CupboardEntity  = Convert.ToBoolean(GetConfigValue("Global", "cupboardCheckEntity", false));
                c_cupboardRange   = Convert.ToSingle(GetConfigValue("Global", "cupboardRange", 30.0));
            }
            catch
            {
                c_requireCupboard = false;
                c_CupboardEntity  = false;
                c_cupboardRange   = 30f;
            }

            if (g_configChanged)
            {
                Puts("Configuration file updated.");
                SaveConfig();
            }
        }

        object GetConfigValue(string category, string setting, object defaultValue)
        {
            Dictionary<string, object> data = Config[category] as Dictionary<string, object>;
            object value;

            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
                g_configChanged = true;
            }

            if (data.TryGetValue(setting, out value)) return value;
            value = defaultValue;
            data[setting] = value;
            g_configChanged = true;
            return value;
        }

        [HookMethod("SendHelpText")]
        private void SendHelpText(BasePlayer player)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<color=#05eb59>" + Name + " " + Version + "</color> · Controls decay\n");
            sb.Append("  · ").AppendLine($"twig={c_twigMultiplier} - campfire={c_campfireMultiplier}");
            sb.Append("  · ").Append($"wood ={ c_woodMultiplier} - stone ={ c_stoneMultiplier} - sheet ={ c_sheetMultiplier} - armored ={ c_armoredMultiplier}\n");

            if(c_requireCupboard == true)
            {
                if(c_CupboardEntity == true)
                {
                    string range = c_cupboardRange.ToString();
                    sb.Append("  · ").Append($"cupboard check ={ true } - entity range ={ range }");
                }
                else
                {
                    sb.Append("  · ").Append($"cupboard check ={ true } - entity check ={ false }");
                }
            }
            else
            {
                sb.Append("  · ").Append($"cupboard check ={ false }");
            }
            player.ChatMessage(sb.ToString());
        }

        // Just here to cleanup the code a bit
        private void OutputRcon(string message, bool mundane = false)
        {
            if(c_outputToRcon)
            {
                if (!mundane) Puts($"{message}");

                else if (mundane && c_outputMundane) Puts($"{message}");
            }
        }

        // Prevent players from adding building resources to cupboard
        private object CanMoveItem(Item item, PlayerInventory inventory, uint targetContainer, int targetSlot)
        {
            if(!(c_blockCupboardResources || c_blockCupboardWood)) return null;
            if(item == null) return null;
            if(targetContainer == null) return null;

            ItemContainer container = inventory.FindContainer(targetContainer);

            try
            {
                var cup = container.entityOwner as BaseEntity;

                if(cup.name.Contains("cupboard.tool"))
                {
                    string res = item.info.shortname;
                    if(res.Contains("wood") && c_blockCupboardWood)
                    {
                        OutputRcon($"Player tried to add {res} to a cupboard!");
                        return false;
                    }
                    else if((res.Contains("stones") || res.Contains("metal.frag") || res.Contains("metal.refined")) && c_blockCupboardResources)
                    {
                        OutputRcon($"Player tried to add {res} to a cupboard!");
                        return false;
                    }
                }
            }
            catch {}
            return null;
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            DateTime tick = DateTime.Now;

            string entity_name = entity.LookupPrefab().name;
            string owner = entity.OwnerID.ToString();
            if(c_usePermission)
            {
                if(permission.UserHasPermission(owner, "nodecay.use") || owner == "0")
                {
                    if(owner != "0")
                    {
                        OutputRcon($"{entity_name} owner {owner} has NoDecay permission!");
                    }
                }
                else
                {
                    OutputRcon($"{entity_name} owner {owner} does NOT have NoDecay permission.  Standard decay in effect.");
                    return;
                }
            }

            try
            {
                if (!hitInfo.damageTypes.Has(Rust.DamageType.Decay)) return;

                var block = entity as BuildingBlock;
                float before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);

                if (block != null)
                {
                    ProcessBuildingDamage(block, entity, hitInfo);
                }
                else if (entity_name == "campfire" || entity_name == "skull_fire_pit")
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_campfireMultiplier);
                    OutputRcon($"Decay campfire before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity_name == "box.wooden.large" ||
                        entity_name == "woodbox_deployed" ||
                        entity_name == "CoffinStorage")
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_boxMultiplier);

                    OutputRcon($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity_name.Contains("deployed") ||
                        entity_name.Contains("reinforced") ||
                        entity_name.Contains("shopfront") ||
                        entity_name.Contains("bars") ||
                        entity_name.Contains("shutter") ||
                        entity_name.Contains("netting") ||
                        (entity_name.Contains("door") && !entity_name.Contains("doorway")) ||
                        entity_name.Contains("hatch") ||
                        entity_name.Contains("garagedoor") ||
                        entity_name.Contains("cell") ||
                        entity_name.Contains("fence") ||
                        entity_name.Contains("grill") ||
                        entity_name.Contains("Candle") ||
                        entity_name.Contains("Strobe") ||
                        entity_name.Contains("speaker") ||
                        entity_name.Contains("Fog") ||
                        entity_name.Contains("Graveyard"))
                {
                    if(c_requireCupboard && c_CupboardEntity)
                    {
                        // Verify that we should check for a cupboard and ensure that one exists.
                        // If not, multiplier will be standard of 1.0f.
                        OutputRcon($"NoDecay checking for local cupboard.");

                        if(CheckCupboard(entity))
                        {
                            hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_deployablesMultiplier);
                        }
                    }
                    else
                    {
                        hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_deployablesMultiplier);
                    }

                    OutputRcon($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity_name.Contains("furnace"))
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_furnaceMultiplier);
                    OutputRcon($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity_name.Contains("sedan"))
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_sedanMultiplier);
                    OutputRcon($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity_name == "SAM_Static")
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_samMultiplier);
                    OutputRcon($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity_name == "HotAirBalloon")
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_baloonMultiplier);
                    OutputRcon($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}", true);
                }
                else if (entity_name == "BBQ.Deployed")
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_bbqMultiplier);
                    OutputRcon($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity_name.Contains("watchtower"))
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_watchtowerMultiplier);
                    OutputRcon($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity_name == "WaterBarrel" ||
                        entity_name == "jackolantern.angry" ||
                        entity_name == "jackolantern.happy" ||
                        entity_name == "water_catcher_small" ||
                        entity_name == "water_catcher_large")
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_deployablesMultiplier);
                    OutputRcon($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity_name == "beartrap" ||
                        entity_name == "landmine" ||
                        entity_name == "spikes.floor")
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_trapMultiplier);
                    OutputRcon($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity_name.Contains("barricade"))
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_barricadeMultiplier);
                    OutputRcon($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity_name == "gates.external.high.stone")
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_highStoneWallMultiplier);
                    OutputRcon($"Decay (high stone gate) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity_name == "gates.external.high.wood")
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_highWoodWallMultiplier);
                    OutputRcon($"Decay (high wood gate) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity_name == "wall.external.high.stone")
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_highStoneWallMultiplier);
                    OutputRcon($"Decay (high stone wall) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity_name == "wall.external.high.wood")
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_highWoodWallMultiplier);
                    OutputRcon($"Decay (high wood wall) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity_name == "mining.pumpjack")
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, 0.0f);
                    OutputRcon($"Decay (pumpjack) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity_name == "Rowboat" || entity_name == "RHIB")
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_boatMultiplier);
                    OutputRcon($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}", true);
                }
                else if (entity_name == "minicopter.entity")
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_minicopterMultiplier);
                    OutputRcon($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}", true);
                }
                else if (entity_name.Contains("TestRidableHorse"))
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_horseMultiplier);
                    OutputRcon($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}", true);
                }
                else if (entity_name == "ScrapTransportHelicopter")
                {
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_scrapcopterMultiplier);
                    OutputRcon($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}", true);
                }
                else
                {
                    Puts($"Unsupported decaying entity detected: {entity_name} --- please notify author");
                }
            }
            finally
            {
                double ms = (DateTime.Now - tick).TotalMilliseconds;
                if (ms > 10 || c_outputMundane) Puts($"NoDecay.OnEntityTakeDamage on {entity_name} took {ms} ms to execute.");
            }
        }

        void ProcessBuildingDamage(BuildingBlock block, BaseEntity entity, HitInfo hitInfo)
        {
            float multiplier = 1.0f;
            bool isHighWall = block.LookupPrefab().name.Contains("wall.external");
            bool isHighGate = block.LookupPrefab().name.Contains("gates.external");

            string type = "other";
            bool hascup = true; // Assume true (has cupboard or we don't care)

            OutputRcon($"NoDecay checking for block damage to {block.LookupPrefab().name}");
            if(c_requireCupboard == true)
            {
                // Verify that we should check for a cupboard and ensure that one exists.
                // If not, multiplier will be standard of 1.0f.
                OutputRcon($"NoDecay checking for local cupboard.");
                hascup = CheckCupboardBlock(block,hitInfo,entity.LookupPrefab().name);
            }
            else
            {
                OutputRcon($"NoDecay not checking for local cupboard.");
            }

            if(hascup)
            {
                switch (block.grade)
                {
                    case BuildingGrade.Enum.Twigs:
                        multiplier = c_twigMultiplier;
                        type = "twig";
                        break;
                    case BuildingGrade.Enum.Wood:
                        if (isHighWall)
                        {
                            multiplier = c_highWoodWallMultiplier;
                            type = "high wood wall";
                        }
                        else if(isHighWall)
                        {
                            multiplier = c_highWoodWallMultiplier;
                            type = "high wood gate";
                        }
                        else
                        {
                            multiplier = c_woodMultiplier;
                            type = "wood";
                        }
                        break;
                    case BuildingGrade.Enum.Stone:
                        if (isHighWall)
                        {
                            multiplier = c_highStoneWallMultiplier;
                            type = "high stone wall";
                        }
                        else if(isHighWall)
                        {
                            multiplier = c_highWoodWallMultiplier;
                            type = "high stone gate";
                        }
                        else
                        {
                            multiplier = c_stoneMultiplier;
                            type = "stone";
                        }
                        break;
                    case BuildingGrade.Enum.Metal:
                        multiplier = c_sheetMultiplier;
                        type = "sheet";
                        break;
                    case BuildingGrade.Enum.TopTier:
                        multiplier = c_armoredMultiplier;
                        type = "armored";
                        break;
                    default:
                        OutputRcon($"Decay ({type}) has unknown grade type.");
                        type = "unknown";
                        break;
                }
            }

            float before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
            hitInfo.damageTypes.Scale(Rust.DamageType.Decay, multiplier);

            OutputRcon($"Decay ({type}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
        }

        // Check that a building block is owned by/attached to a cupboard
        private bool CheckCupboardBlock(BuildingBlock block, HitInfo hitInfo, string ename = "unknown")
        {
            BuildingManager.Building building = block.GetBuilding();

            OutputRcon($"CheckCupboardBlock:   Checking for cupboard connected to {ename}.");

            if(building != null)
            {
                // cupboard overlap.  Block safe from decay.
                //if(building.buildingPrivileges == null) // OR Privs: ListHashSet`1[BuildingPrivlidge]
                if(building.GetDominatingBuildingPrivilege() == null) // OR Privs: ListHashSet`1[BuildingPrivlidge]
                {
                    OutputRcon($"CheckCupboardBlock:     Block NOT owned by cupboard!");
                    return false;
                }

                OutputRcon($"CheckCupboardBlock:     Block owned by cupboard!");
                //Puts($"Privs: {building.buildingPrivileges}");
                return true;
            }
            else
            {
                OutputRcon($"CheckCupboardBlock:     Unable to find cupboard.");
            }
            return false;
        }

        // Non-block entity check
        bool CheckCupboard(BaseEntity entity)
        {
            int targetLayer = LayerMask.GetMask("Construction", "Construction Trigger", "Trigger", "Deployed");
            Collider[] hit = Physics.OverlapSphere(entity.transform.position, c_cupboardRange, targetLayer);

            if(c_outputToRcon)
            {
                string hits = hit.Length.ToString();
                string name = entity.name.ToString();
                string range = c_cupboardRange.ToString();
                Puts($"NoDecay Checking for cupboard within {range}m of {name}.  Found {hits} layer hits.");
            }
            // loop through hit layers and check for 'Building Privlidge'
            foreach(var ent in hit)
            {
                BuildingPrivlidge privs = ent.GetComponentInParent<BuildingPrivlidge>();
                if(privs != null)
                {
                    // cupboard overlap.  Entity safe from decay
                    OutputRcon($"Found entity layer in range of cupboard!");
                    return true;
                }
            }

            if(hit.Length > 0)
            {
                OutputRcon($"Unable to find entity layer in range of cupboard.");
                return false;
            }
            else
            {
                OutputRcon($"NoDecay unable to check for cupboard.");
            }
            return true;
        }
    }
}
