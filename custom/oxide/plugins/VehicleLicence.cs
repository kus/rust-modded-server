using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Vehicle Licence", "Sorrow/TheDoc/Arainrr", "1.6.0")]
    [Description("Allows players to buy vehicles and then spawn or store it")]
    public class VehicleLicence : RustPlugin
    {
        #region Fields

        [PluginReference] private readonly Plugin Economics, ServerRewards, Friends, Clans, NoEscape;
        private const string PERMISSION_USE = "vehiclelicence.use";
        private const string PERMISSION_ALL = "vehiclelicence.all";
        private const string PERMISSION_BYPASS_COST = "vehiclelicence.bypasscost";
        private const string PREFAB_ITEM_DROP = "assets/prefabs/misc/item drop/item_drop.prefab";

        private const string PREFAB_ROWBOAT = "assets/content/vehicles/boats/rowboat/rowboat.prefab";
        private const string PREFAB_RHIB = "assets/content/vehicles/boats/rhib/rhib.prefab";
        private const string PREFAB_SEDAN = "assets/content/vehicles/sedan_a/sedantest.entity.prefab";
        private const string PREFAB_HOTAIRBALLOON = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab";
        private const string PREFAB_MINICOPTER = "assets/content/vehicles/minicopter/minicopter.entity.prefab";
        private const string PREFAB_TRANSPORTCOPTER = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab";
        private const string PREFAB_CHINOOK = "assets/prefabs/npc/ch47/ch47.entity.prefab";
        private const string PREFAB_RIDABLEHORSE = "assets/rust.ai/nextai/testridablehorse.prefab";

        private const string PREFAB_CHASSIS_SMALL = "assets/content/vehicles/modularcar/car_chassis_2module.entity.prefab";
        private const string PREFAB_CHASSIS_MEDIUM = "assets/content/vehicles/modularcar/car_chassis_3module.entity.prefab";
        private const string PREFAB_CHASSIS_LARGE = "assets/content/vehicles/modularcar/car_chassis_4module.entity.prefab";

        private const string PREFAB_MODULAR_CAR_SMALL = "assets/content/vehicles/modularcar/2module_car_spawned.entity.prefab";
        private const string PREFAB_MODULAR_CAR_MEDIUM = "assets/content/vehicles/modularcar/3module_car_spawned.entity.prefab";
        private const string PREFAB_MODULAR_CAR_LARGE = "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab";

        private readonly Dictionary<BaseEntity, Vehicle> vehiclesCache = new Dictionary<BaseEntity, Vehicle>();
        private static readonly int LAYER_GROUND = Rust.Layers.Solid | Rust.Layers.Mask.Water;//LayerMask.GetMask("Terrain", "World", "Construction", "Deployed","Water");

        private enum VehicleType
        {
            Rowboat,
            RHIB,
            Sedan,
            HotAirBalloon,
            MiniCopter,
            TransportHelicopter,
            Chinook,
            RidableHorse,
            ChassisSmall,
            ChassisMedium,
            ChassisLarge,
            ModularCarSmall,
            ModularCarMedium,
            ModularCarLarge,
        }

        #endregion Fields

        #region Oxide Hooks

        private void Init()
        {
            LoadData();
            permission.RegisterPermission(PERMISSION_USE, this);
            permission.RegisterPermission(PERMISSION_ALL, this);
            permission.RegisterPermission(PERMISSION_BYPASS_COST, this);
            foreach (var vehicleS in configData.vehicleS.Values)
            {
                if (string.IsNullOrEmpty(vehicleS.permission)) continue;
                if (permission.PermissionExists(vehicleS.permission, this)) continue;
                permission.RegisterPermission(vehicleS.permission, this);
            }
            foreach (var perm in configData.permCooldown.Keys)
            {
                if (permission.PermissionExists(perm, this)) continue;
                permission.RegisterPermission(perm, this);
            }
            if (configData.chatS.useUniversalCommand)
            {
                foreach (var command in configData.vehicleS.Values.SelectMany(x => x.commands))
                {
                    if (string.IsNullOrEmpty(command)) continue;
                    cmd.AddChatCommand(command, this, nameof(CmdUniversal));
                }
            }
            cmd.AddChatCommand(configData.chatS.helpCommand, this, nameof(CmdLicenseHelp));
            cmd.AddChatCommand(configData.chatS.buyCommand, this, nameof(CmdBuyVehicle));
            cmd.AddChatCommand(configData.chatS.spawnCommand, this, nameof(CmdSpawnVehicle));
            cmd.AddChatCommand(configData.chatS.recallCommand, this, nameof(CmdRecallVehicle));
            cmd.AddChatCommand(configData.chatS.killCommand, this, nameof(CmdKillVehicle));
        }

        private void OnServerInitialized()
        {
            foreach (VehicleType vehicleType in Enum.GetValues(typeof(VehicleType)))
            {
                if (!configData.vehicleS.ContainsKey(vehicleType))
                {
                    var vehicleS = new ConfigData.VehicleS();
                    configData.vehicleS.Add(vehicleType, vehicleS);
                }
            }
            if (!configData.globalS.preventMounting) Unsubscribe(nameof(CanMountEntity));
            if (configData.globalS.checkVehiclesInterval > 0) CheckVehicles();
            else Unsubscribe(nameof(OnEntityDismounted));
            if (!configData.globalS.noDecay) Unsubscribe(nameof(OnEntityTakeDamage));
        }

        private void Unload()
        {
            foreach (var entry in vehiclesCache.ToList())
            {
                if (entry.Key != null && !entry.Key.IsDestroyed)
                {
                    RefundFuel(entry.Value, entry.Key);
                    entry.Key.Kill(BaseNetworkable.DestroyMode.Gib);
                }
            }
            SaveData();
        }

        private void OnServerSave() => timer.Once(UnityEngine.Random.Range(0f, 60f), SaveData);

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            if (permission.UserHasPermission(player.UserIDString, PERMISSION_BYPASS_COST))
                PurchaseAllVehicles(player.userID);
        }

        private void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            var vehicleParent = entity?.VehicleParent();
            if (vehicleParent == null || vehicleParent.IsDestroyed) return;
            Vehicle vehicle;
            if (!vehiclesCache.TryGetValue(vehicleParent, out vehicle)) return;
            vehicle.OnDismount();
        }

        private object CanMountEntity(BasePlayer friend, BaseMountable entity)
        {
            var vehicleParent = entity?.VehicleParent();
            if (vehicleParent == null || vehicleParent.IsDestroyed) return null;
            Vehicle vehicle;
            if (!vehiclesCache.TryGetValue(vehicleParent, out vehicle)) return null;
            if (AreFriends(vehicle.playerID, friend.userID)) return null;
            if (configData.globalS.blockDriverSeat && vehicleParent.HasMountPoints() && entity != vehicleParent.mountPoints[0].mountable) return null;
            return false;
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null) return;
            if (!vehiclesCache.ContainsKey(entity)) return;
            if (hitInfo?.damageTypes?.Get(Rust.DamageType.Decay) > 0)
                hitInfo.damageTypes.Scale(Rust.DamageType.Decay, 0);
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info) => CheckEntity(entity, true);

        private void OnEntityKill(BaseCombatEntity entity) => CheckEntity(entity);

        #endregion Oxide Hooks

        #region Helpers

        private void CheckEntity(BaseCombatEntity entity, bool isCrash = false)
        {
            if (entity == null) return;
            Vehicle vehicle;
            if (!vehiclesCache.TryGetValue(entity, out vehicle)) return;
            vehiclesCache.Remove(entity);
            var vehicleS = configData.vehicleS[vehicle.vehicleType];

            RefundFuel(vehicle, entity, isCrash);
            Dictionary<VehicleType, Vehicle> vehicles;
            if (storedData.playerData.TryGetValue(vehicle.playerID, out vehicles) && vehicles.ContainsKey(vehicle.vehicleType))
            {
                if (isCrash && vehicleS.removeLicenseOnCrash)
                {
                    vehicles.Remove(vehicle.vehicleType);
                }
                else vehicles[vehicle.vehicleType].OnDeath();
            }
        }

        private void CheckVehicles()
        {
            foreach (var entry in vehiclesCache.ToList())
            {
                if (entry.Key == null || entry.Key.IsDestroyed) continue;
                if (VehicleIsActive(entry.Value)) continue;
                if (VehicleAnyMounted(entry.Key)) continue;
                RefundFuel(entry.Value, entry.Key);
                entry.Key.Kill(BaseNetworkable.DestroyMode.Gib);
            }
            timer.Once(configData.globalS.checkVehiclesInterval, CheckVehicles);
        }

        private bool VehicleIsActive(Vehicle vehicle)
        {
            var vehicleS = configData.vehicleS[vehicle.vehicleType];
            if (vehicleS.wipeTime <= 0) return true;
            return TimeEx.currentTimestamp - vehicle.lastDismount < vehicleS.wipeTime;
        }

        private readonly FieldInfo habFuelSystemField = typeof(HotAirBalloon).GetField("fuelSystem", BindingFlags.Instance | BindingFlags.NonPublic);

        private void RefundFuel(Vehicle vehicle, BaseEntity entity = null, bool isCrash = false)
        {
            var vehicleS = configData.vehicleS[vehicle.vehicleType];
            bool refundFuel = vehicleS.refundFuel;
            bool refundInventory = vehicleS.refundInventory;
            if (isCrash)
            {
                refundFuel = !vehicleS.notRefundFuelOnCrash;
                refundInventory = !vehicleS.notRefundInventoryOnCrash;
            }
            if (!refundFuel && !refundInventory) return;

            if (entity == null) entity = vehicle.entity;
            ItemContainer itemContainer;
            switch (vehicle.vehicleType)
            {
                case VehicleType.Sedan:
                case VehicleType.Chinook:
                    return;

                case VehicleType.MiniCopter:
                case VehicleType.TransportHelicopter:
                    if (!refundFuel) return;
                    itemContainer = (entity as MiniCopter)?.GetFuelSystem()?.GetFuelContainer()?.inventory;
                    break;

                case VehicleType.HotAirBalloon:
                    if (!refundFuel) return;
                    var hotAirBalloon = entity as HotAirBalloon;
                    var fuelSystem = habFuelSystemField?.GetValue(hotAirBalloon) as EntityFuelSystem;
                    itemContainer = fuelSystem?.GetFuelContainer()?.inventory;
                    break;

                case VehicleType.RHIB:
                case VehicleType.Rowboat:
                    if (!refundFuel) return;
                    itemContainer = (entity as MotorRowboat)?.fuelSystem?.GetFuelContainer()?.inventory;
                    break;

                case VehicleType.RidableHorse:
                    if (!refundInventory) return;
                    itemContainer = (entity as RidableHorse)?.inventory;
                    break;

                case VehicleType.ChassisSmall:
                case VehicleType.ChassisMedium:
                case VehicleType.ChassisLarge:
                case VehicleType.ModularCarSmall:
                case VehicleType.ModularCarMedium:
                case VehicleType.ModularCarLarge:
                    var modularCar = entity as ModularCar;
                    if (modularCar == null) return;
                    List<Item> collect = new List<Item>();
                    if (refundFuel)
                    {
                        itemContainer = modularCar.fuelSystem?.GetFuelContainer()?.inventory;
                        if (itemContainer != null)
                        {
                            collect.AddRange(itemContainer.itemList);
                        }
                    }

                    if (refundInventory)
                    {
                        foreach (var moduleEntity in modularCar.AttachedModuleEntities)
                        {
                            /*var moduleEngine = moduleEntity as VehicleModuleEngine;
                            if (moduleEngine != null)
                            {
                                var engineContainer = moduleEngine.GetContainer()?.inventory;
                                if (engineContainer != null)
                                {
                                    collect.AddRange(engineContainer.itemList);
                                }
                                continue;
                            }*/
                            var moduleStorage = moduleEntity as VehicleModuleStorage;
                            if (moduleStorage != null)
                            {
                                var storageContainer = moduleStorage.GetContainer()?.inventory;
                                if (storageContainer != null)
                                {
                                    collect.AddRange(storageContainer.itemList);
                                }
                            }
                        }
                        var moduleContainer = modularCar.Inventory?.ModuleContainer;
                        if (moduleContainer != null)
                        {
                            collect.AddRange(moduleContainer.itemList);
                        }
                        /*var chassisContainer = modularCar.Inventory?.ChassisContainer;
                        if (chassisContainer != null)
                        {
                            collect.AddRange(chassisContainer.itemList);
                        }*/
                    }
                    if (collect.Count <= 0) return;
                    itemContainer = new ItemContainer
                    {
                        itemList = collect
                    };
                    break;

                default: return;
            }
            if (itemContainer?.itemList == null || itemContainer.itemList.Count <= 0) return;
            var player = RustCore.FindPlayerById(vehicle.playerID);
            if (player == null) itemContainer.Drop(PREFAB_ITEM_DROP, entity.GetDropPosition(), entity.transform.rotation);
            else
            {
                foreach (var item in itemContainer.itemList.ToList())
                {
                    player.GiveItem(item);
                }
                Print(player, Lang("RefundedVehicleFuel", player.UserIDString, configData.vehicleS[vehicle.vehicleType].displayName));
            }
        }

        private void PurchaseAllVehicles(ulong playerID)
        {
            Dictionary<VehicleType, Vehicle> vehicles;
            var array = Enum.GetValues(typeof(VehicleType));
            if (!storedData.playerData.TryGetValue(playerID, out vehicles))
            {
                vehicles = new Dictionary<VehicleType, Vehicle>();
                foreach (VehicleType vehicleType in array)
                    vehicles.Add(vehicleType, new Vehicle());
                storedData.playerData.Add(playerID, vehicles);
            }
            else
            {
                if (vehicles.Count == array.Length) return;
                foreach (VehicleType vehicleType in array)
                {
                    if (!vehicles.ContainsKey(vehicleType))
                        vehicles.Add(vehicleType, new Vehicle());
                }
            }
            SaveData();
        }

        private bool PlayerIsBlocked(BasePlayer player)
        {
            if (configData.globalS.useRaidBlocker && IsRaidBlocked(player.UserIDString))
            {
                Print(player, Lang("RaidBlocked", player.UserIDString));
                return true;
            }
            if (configData.globalS.useCombatBlocker && IsCombatBlocked(player.UserIDString))
            {
                Print(player, Lang("CombatBlocked", player.UserIDString));
                return true;
            }
            return false;
        }

        private bool IsRaidBlocked(string playerID) => (bool)(NoEscape?.Call("IsRaidBlocked", playerID) ?? false);

        private bool IsCombatBlocked(string playerID) => (bool)(NoEscape?.Call("IsCombatBlocked", playerID) ?? false);

        #region Methods

        private bool HasPermission(BasePlayer player, VehicleType vehicleType)
        {
            var vehicleS = configData.vehicleS[vehicleType];
            if (!vehicleS.usePermission || string.IsNullOrEmpty(vehicleS.permission)) return true;
            if (permission.UserHasPermission(player.UserIDString, PERMISSION_ALL)) return true;
            return permission.UserHasPermission(player.UserIDString, vehicleS.permission);
        }

        private double GetCooldownForPlayer(BasePlayer player, VehicleType vehicleType, double defaultCooldown)
        {
            foreach (var entry in configData.permCooldown)
            {
                float cooldown;
                if (entry.Value.TryGetValue(vehicleType, out cooldown) && defaultCooldown > cooldown && permission.UserHasPermission(player.UserIDString, entry.Key))
                    defaultCooldown = cooldown;
            }
            return defaultCooldown;
        }

        private bool AreFriends(ulong playerID, ulong friendID)
        {
            if (playerID == friendID) return true;
            if (configData.globalS.useTeams && SameTeam(playerID, friendID)) return true;
            if (configData.globalS.useFriends && HasFriend(playerID, friendID)) return true;
            if (configData.globalS.useClans && SameClan(playerID, friendID)) return true;
            return false;
        }

        private bool SameTeam(ulong playerID, ulong friendID)
        {
            if (!RelationshipManager.TeamsEnabled()) return false;
            var playerTeam = RelationshipManager.Instance.FindPlayersTeam(playerID);
            if (playerTeam == null) return false;
            var friendTeam = RelationshipManager.Instance.FindPlayersTeam(friendID);
            if (friendTeam == null) return false;
            return playerTeam == friendTeam;
        }

        private bool HasFriend(ulong playerID, ulong friendID)
        {
            if (Friends == null) return false;
            return (bool)Friends.Call("HasFriend", playerID, friendID);
        }

        private bool SameClan(ulong playerID, ulong friendID)
        {
            if (Clans == null) return false;
            //Clans
            var isMember = Clans.Call("IsClanMember", playerID.ToString(), friendID.ToString());
            if (isMember != null) return (bool)isMember;
            //Rust:IO Clans
            var playerClan = Clans.Call("GetClanOf", playerID);
            if (playerClan == null) return false;
            var friendClan = Clans.Call("GetClanOf", friendID);
            if (friendClan == null) return false;
            return (string)playerClan == (string)friendClan;
        }

        private static string GetVehiclePrefab(VehicleType vehicleType)
        {
            switch (vehicleType)
            {
                case VehicleType.Rowboat: return PREFAB_ROWBOAT;
                case VehicleType.RHIB: return PREFAB_RHIB;
                case VehicleType.Sedan: return PREFAB_SEDAN;
                case VehicleType.HotAirBalloon: return PREFAB_HOTAIRBALLOON;
                case VehicleType.MiniCopter: return PREFAB_MINICOPTER;
                case VehicleType.TransportHelicopter: return PREFAB_TRANSPORTCOPTER;
                case VehicleType.Chinook: return PREFAB_CHINOOK;
                case VehicleType.RidableHorse: return PREFAB_RIDABLEHORSE;
                case VehicleType.ChassisSmall: return PREFAB_CHASSIS_SMALL;
                case VehicleType.ChassisMedium: return PREFAB_CHASSIS_MEDIUM;
                case VehicleType.ChassisLarge: return PREFAB_CHASSIS_LARGE;
                case VehicleType.ModularCarSmall: return PREFAB_MODULAR_CAR_SMALL;
                case VehicleType.ModularCarMedium: return PREFAB_MODULAR_CAR_MEDIUM;
                case VehicleType.ModularCarLarge: return PREFAB_MODULAR_CAR_LARGE;
            }
            return string.Empty;
        }

        private static bool VehicleAnyMounted(BaseEntity entity)
        {
            var vehicle = entity as BaseVehicle;
            if (vehicle != null && vehicle.AnyMounted())
            {
                return true;
            }
            return entity.GetComponentsInChildren<BasePlayer>()?.Length > 0;
        }

        private static Vector3 GetLookingAtGroundPos(BasePlayer player, float distance)
        {
            RaycastHit hit;
            Ray ray = player.eyes.HeadRay();
            if (Physics.Raycast(ray, out hit, distance, LAYER_GROUND))
                return hit.point;
            var position = ray.origin + ray.direction * distance;
            if (Physics.Raycast(position + Vector3.up * 200, Vector3.down, out hit, 500, LAYER_GROUND))
                return hit.point;
            position.y = TerrainMeta.HeightMap.GetHeight(position);
            return position;
        }

        private static bool IsLookingAtWater(BasePlayer player, float distance)
        {
            Vector3 lookingAt = GetLookingAtGroundPos(player, distance);
            return WaterLevel.Test(lookingAt);
        }

        private static Vector3 GetGroundPosition(Vector3 position)
        {
            RaycastHit hitInfo;
            if (Physics.Raycast(position + Vector3.up * 200, Vector3.down, out hitInfo, 500f, LAYER_GROUND)) position.y = hitInfo.point.y;
            else position.y = TerrainMeta.HeightMap.GetHeight(position);
            return position;
        }

        private static void DismountAllPlayers(BaseEntity vehicle)
        {
            var baseVehicle = vehicle as BaseVehicle;
            if (baseVehicle != null)
            {
                //(vehicle as BaseVehicle).DismountAllPlayers();
                var array = baseVehicle.mountPoints;
                foreach (var mountPointInfo in array)
                {
                    var mounted = mountPointInfo.mountable?._mounted;
                    if (mounted != null)
                    {
                        mountPointInfo.mountable.DismountPlayer(mounted);
                    }
                }
            }
            var players = vehicle.GetComponentsInChildren<BasePlayer>();
            foreach (var p in players)
            {
                p.SetParent(null, true, true);
            }
        }

        #endregion Methods

        #region API

        private bool HaveVehicleLicense(ulong playerID, string license)
        {
            VehicleType vehicleType;
            if (!Enum.TryParse(license, true, out vehicleType)) return false;
            Dictionary<VehicleType, Vehicle> vehicles;
            return storedData.playerData.TryGetValue(playerID, out vehicles) && vehicles.ContainsKey(vehicleType);
        }

        private List<string> GetPlayerLicenses(ulong playerID)
        {
            Dictionary<VehicleType, Vehicle> vehicles;
            if (storedData.playerData.TryGetValue(playerID, out vehicles))
            {
                return vehicles.Keys.Select(x => x.ToString()).ToList();
            }
            return new List<string>();
        }

        private BaseEntity GetLicensedVehicle(ulong playerID, string license)
        {
            VehicleType vehicleType;
            if (!Enum.TryParse(license, true, out vehicleType))
            {
                return null;
            }
            Dictionary<VehicleType, Vehicle> vehicles;
            if (storedData.playerData.TryGetValue(playerID, out vehicles))
            {
                Vehicle vehicle;
                if (vehicles.TryGetValue(vehicleType, out vehicle))
                {
                    return vehicle.entity;
                }
            }
            return null;
        }

        private bool IsLicensedVehicle(BaseEntity entity)
        {
            foreach (var playerData in storedData.playerData)
            {
                foreach (var vehicle in playerData.Value)
                {
                    if (vehicle.Value.entity == entity)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion API

        #endregion Helpers

        #region Commands

        #region Universal Command

        private void CmdUniversal(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                Print(player, Lang("NotAllowed", player.UserIDString));
                return;
            }
            command = command.ToLower();
            foreach (var entry in configData.vehicleS)
            {
                if (entry.Value.commands.Any(x => x.ToLower() == command))
                {
                    HandleUniversalCmd(player, entry.Key);
                    return;
                }
            }
        }

        private void HandleUniversalCmd(BasePlayer player, VehicleType vehicleType)
        {
            if (!HasPermission(player, vehicleType))
            {
                Print(player, Lang("NotAllowed", player.UserIDString));
                return;
            }
            if (PlayerIsBlocked(player)) return;
            Dictionary<VehicleType, Vehicle> vehicles;
            if (!storedData.playerData.TryGetValue(player.userID, out vehicles))
            {
                vehicles = new Dictionary<VehicleType, Vehicle>();
            }
            string reason;
            Vehicle vehicle;
            if (vehicles.TryGetValue(vehicleType, out vehicle))
            {
                bool checkWater = vehicleType == VehicleType.Rowboat || vehicleType == VehicleType.RHIB;
                if (vehicle.entity != null && !vehicle.entity.IsDestroyed)//recall
                {
                    if (CanRecall(player, vehicle.entity, vehicleType, out reason, checkWater))
                    {
                        RecallVehicle(player, vehicle.entity, vehicleType, checkWater);
                        return;
                    }
                }
                else//spawn
                {
                    if (CanSpawn(player, vehicleType, out reason, checkWater))
                    {
                        SpawnVehicle(player, vehicleType, checkWater);
                        return;
                    }
                }
                Print(player, reason);
                return;
            }

            if (!BuyVehicle(player, vehicleType, out reason))//buy
            {
                Print(player, reason);
            }
        }

        #endregion Universal Command

        #region Help Command

        private void CmdLicenseHelp(BasePlayer player, string command, string[] args)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(Lang("Help", player.UserIDString));
            stringBuilder.AppendLine(Lang("HelpLicence1", player.UserIDString, configData.chatS.buyCommand));
            stringBuilder.AppendLine(Lang("HelpLicence2", player.UserIDString, configData.chatS.spawnCommand));
            stringBuilder.AppendLine(Lang("HelpLicence3", player.UserIDString, configData.chatS.recallCommand));
            stringBuilder.AppendLine(Lang("HelpLicence4", player.UserIDString, configData.chatS.killCommand));
            foreach (var entry in configData.vehicleS)
            {
                if (entry.Value.purchasable && entry.Value.commands.Count > 0)
                    stringBuilder.AppendLine(Lang("HelpLicence5", player.UserIDString, entry.Value.commands[0]));
            }
            Print(player, stringBuilder.ToString());
        }

        #endregion Help Command

        #region Buy Command

        [ConsoleCommand("vl.buy")]
        private void CCmdBuyVehicle(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (arg.IsAdmin && arg.Args != null && arg.Args.Length == 2)
            {
                player = RustCore.FindPlayer(arg.Args[1]);
                if (player == null)
                {
                    Print(arg, $"Player '{arg.Args[1]}' not found");
                    return;
                }
                HandleBuyCmd(player, arg.Args[0].ToLower(), false);
                return;
            }
            if (player != null) CmdBuyVehicle(player, string.Empty, arg.Args);
            else Print(arg, $"The server console cannot use '{arg.cmd.FullName}'");
        }

        private void CmdBuyVehicle(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                Print(player, Lang("NotAllowed", player.UserIDString));
                return;
            }
            if (args == null || args.Length < 1)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(Lang("Help", player.UserIDString));
                foreach (var entry in configData.vehicleS)
                {
                    if (entry.Value.purchasable && entry.Value.commands.Count > 0)
                    {
                        var prices = string.Join(", ", from p in entry.Value.price select $"{p.Value.displayName} x{p.Value.amount}");
                        stringBuilder.AppendLine(Lang("HelpBuy", player.UserIDString, configData.chatS.buyCommand, entry.Value.commands[0], entry.Value.displayName, prices));
                    }
                }
                Print(player, stringBuilder.ToString());
                return;
            }
            if (PlayerIsBlocked(player)) return;
            HandleBuyCmd(player, args[0].ToLower());
        }

        private void HandleBuyCmd(BasePlayer player, string option, bool shouldPay = true)
        {
            VehicleType vehicleType;
            if (IsValidOption(player, option, out vehicleType))
            {
                string reason;
                if (!BuyVehicle(player, vehicleType, out reason, shouldPay))
                {
                    Print(player, reason);
                }
            }
        }

        private bool BuyVehicle(BasePlayer player, VehicleType vehicleType, out string reason, bool shouldPay = true)
        {
            var vehicleS = configData.vehicleS[vehicleType];
            if (!vehicleS.purchasable)
            {
                reason = Lang("VehicleCannotBeBought", player.UserIDString, vehicleS.displayName);
                return false;
            }
            Dictionary<VehicleType, Vehicle> vehicles;
            if (!storedData.playerData.TryGetValue(player.userID, out vehicles))
            {
                vehicles = new Dictionary<VehicleType, Vehicle>();
                storedData.playerData.Add(player.userID, vehicles);
            }
            if (vehicles.ContainsKey(vehicleType))
            {
                reason = Lang("VehicleAlreadyPurchased", player.UserIDString, vehicleS.displayName);
                return false;
            }
            string missingResources;
            if (shouldPay && !TryPay(player, vehicleS.price, out missingResources))
            {
                reason = Lang("NotEnoughCost", player.UserIDString, vehicleS.displayName, missingResources);
                return false;
            }
            vehicles.Add(vehicleType, new Vehicle());
            Print(player, Lang("VehiclePurchased", player.UserIDString, vehicleS.displayName, configData.chatS.spawnCommand));
            reason = null;
            SaveData();
            return true;
        }

        private bool TryPay(BasePlayer player, Dictionary<string, ConfigData.PriceInfo> prices, out string missingResources)
        {
            if (permission.UserHasPermission(player.UserIDString, PERMISSION_BYPASS_COST))
            {
                missingResources = null;
                return true;
            }

            if (!CanPay(player, prices, out missingResources))
            {
                return false;
            }
            List<Item> collect = new List<Item>();
            foreach (var entry in prices)
            {
                if (entry.Value.amount <= 0) continue;
                var item = ItemManager.FindItemDefinition(entry.Key);
                if (item != null)
                {
                    player.inventory.Take(collect, item.itemid, entry.Value.amount);
                    player.Command("note.inv", item.itemid, -entry.Value.amount);
                    continue;
                }
                switch (entry.Key.ToLower())
                {
                    case "economics":
                        Economics?.Call("Withdraw", player.userID, (double)entry.Value.amount);
                        continue;

                    case "serverrewards":
                        ServerRewards?.Call("TakePoints", player.userID, entry.Value.amount);
                        continue;
                }
            }
            foreach (Item item in collect) item.Remove();
            missingResources = null;
            return true;
        }

        private bool CanPay(BasePlayer player, Dictionary<string, ConfigData.PriceInfo> prices, out string missingResources)
        {
            Dictionary<string, int> resources = new Dictionary<string, int>();
            foreach (var entry in prices)
            {
                if (entry.Value.amount <= 0) continue;
                int missingAmount;
                var item = ItemManager.FindItemDefinition(entry.Key);
                if (item != null) missingAmount = entry.Value.amount - player.inventory.GetAmount(item.itemid);
                else missingAmount = MissingMoney(entry.Key, entry.Value.amount, player.userID);
                if (missingAmount > 0)
                {
                    if (!resources.ContainsKey(entry.Value.displayName))
                    {
                        resources.Add(entry.Value.displayName, 0);
                    }
                    resources[entry.Value.displayName] += missingAmount;
                }
            }
            if (resources.Count > 0)
            {
                missingResources = string.Empty;
                foreach (var entry in resources)
                {
                    missingResources += $"\n* {entry.Key} x{entry.Value}";
                }
                return false;
            }
            missingResources = null;
            return true;
        }

        private int MissingMoney(string key, int price, ulong playerID)
        {
            switch (key.ToLower())
            {
                case "economics":
                    var balance = Economics?.Call("Balance", playerID);
                    if (balance is double)
                    {
                        var n = price - (double)balance;
                        return n <= 0 ? 0 : (int)Math.Ceiling(n);
                    }
                    return price;

                case "serverrewards":
                    var points = ServerRewards?.Call("CheckPoints", playerID);
                    if (points is int)
                    {
                        var n = price - (int)points;
                        return n <= 0 ? 0 : n;
                    }
                    return price;

                default:
                    PrintError($"Unknown Currency Type '{key}'");
                    return price;
            }
        }

        #endregion Buy Command

        #region Spawn Command

        [ConsoleCommand("vl.spawn")]
        private void CCmdSpawnVehicle(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) Print(arg, $"The server console cannot use '{arg.cmd.FullName}'");
            else CmdSpawnVehicle(player, string.Empty, arg.Args);
        }

        private void CmdSpawnVehicle(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                Print(player, Lang("NotAllowed", player.UserIDString));
                return;
            }
            if (args == null || args.Length < 1)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(Lang("Help", player.UserIDString));
                foreach (var entry in configData.vehicleS)
                {
                    if (entry.Value.purchasable && entry.Value.commands.Count > 0)
                    {
                        stringBuilder.AppendLine(Lang("HelpSpawn", player.UserIDString, configData.chatS.spawnCommand, entry.Value.commands[0], entry.Value.displayName));
                    }
                }
                Print(player, stringBuilder.ToString());
                return;
            }
            if (PlayerIsBlocked(player)) return;
            HandleSpawnCmd(player, args[0].ToLower());
        }

        private void HandleSpawnCmd(BasePlayer player, string option)
        {
            VehicleType vehicleType;
            if (IsValidOption(player, option, out vehicleType))
            {
                string reason;
                bool checkWater = vehicleType == VehicleType.Rowboat || vehicleType == VehicleType.RHIB;
                if (CanSpawn(player, vehicleType, out reason, checkWater))
                {
                    SpawnVehicle(player, vehicleType, checkWater);
                }
                else Print(player, reason);
            }
        }

        private bool CanSpawn(BasePlayer player, VehicleType vehicleType, out string reason, bool checkWater = false)
        {
            var vehicleS = configData.vehicleS[vehicleType];
            if (player.IsBuildingBlocked())
            {
                reason = Lang("BuildingBlocked", player.UserIDString, vehicleS.displayName);
                return false;
            }
            if (player.isMounted || player.HasParent())
            {
                reason = Lang("MountedOrParented", player.UserIDString, vehicleS.displayName);
                return false;
            }
            Dictionary<VehicleType, Vehicle> vehicles;
            storedData.playerData.TryGetValue(player.userID, out vehicles);
            Vehicle vehicle;
            if (vehicles == null || !vehicles.TryGetValue(vehicleType, out vehicle))
            {
                reason = Lang("VehicleNotYetPurchased", player.UserIDString, vehicleS.displayName);
                return false;
            }
            if (vehicle.entity != null && !vehicle.entity.IsDestroyed)
            {
                reason = Lang("AlreadyVehicleOut", player.UserIDString, vehicleS.displayName, configData.chatS.recallCommand);
                return false;
            }
            if (checkWater && !IsLookingAtWater(player, vehicleS.distance))
            {
                reason = Lang("NotLookingAtWater", player.UserIDString, vehicleS.displayName);
                return false;
            }
            var cooldown = GetCooldownForPlayer(player, vehicleType, vehicleS.cooldown);
            if (cooldown > 0)
            {
                var timeleft = Math.Ceiling(cooldown - (TimeEx.currentTimestamp - vehicle.lastDeath));
                if (timeleft > 0)
                {
                    reason = Lang("VehicleOnCooldown", player.UserIDString, timeleft, vehicleS.displayName);
                    return false;
                }
            }
            reason = null;
            return true;
        }

        private void SpawnVehicle(BasePlayer player, VehicleType vehicleType, bool checkWater = false)
        {
            var prefab = GetVehiclePrefab(vehicleType);
            if (string.IsNullOrEmpty(prefab)) return;
            var vehicleS = configData.vehicleS[vehicleType];
            Vector3 position; Quaternion rotation;
            GetVehicleSpawnPos(player, vehicleS.distance, checkWater, vehicleType, out position, out rotation);
            var entity = GameManager.server.CreateEntity(prefab, position, rotation);
            if (entity == null) return;
            entity.enableSaving = false;
            entity.OwnerID = player.userID;
            entity.Spawn();

            if (vehicleS.maxHealth > 0 && Math.Abs(vehicleS.maxHealth - entity.MaxHealth()) > 0f)
            {
                (entity as BaseCombatEntity)?.InitializeHealth(vehicleS.maxHealth, vehicleS.maxHealth);
            }

            var helicopterVehicle = entity as BaseHelicopterVehicle;
            if (helicopterVehicle != null)
            {
                if (configData.globalS.noServerGibs)
                    helicopterVehicle.serverGibs.guid = string.Empty;
                if (configData.globalS.noFireBall)
                    helicopterVehicle.fireBall.guid = string.Empty;
            }

            if (configData.globalS.noMapMarker && entity is CH47Helicopter)
            {
                var helicopter = entity as CH47Helicopter;
                helicopter.mapMarkerInstance?.Kill();
                helicopter.mapMarkerEntityPrefab.guid = string.Empty;
            }

            var vehicle = new Vehicle { playerID = player.userID, vehicleType = vehicleType, entity = entity, lastDismount = TimeEx.currentTimestamp };
            vehiclesCache.Add(entity, vehicle);
            storedData.playerData[player.userID][vehicleType] = vehicle;
            Print(player, Lang("VehicleSpawned", player.UserIDString, vehicleS.displayName));

            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            Interface.CallHook("OnLicensedVehicleSpawned", entity);
        }

        #endregion Spawn Command

        #region Recall Command

        [ConsoleCommand("vl.recall")]
        private void CCmdRecallVehicle(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) Print(arg, $"The server console cannot use '{arg.cmd.FullName}'");
            else CmdRecallVehicle(player, string.Empty, arg.Args);
        }

        private void CmdRecallVehicle(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                Print(player, Lang("NotAllowed", player.UserIDString));
                return;
            }
            if (args == null || args.Length < 1)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(Lang("Help", player.UserIDString));
                foreach (var entry in configData.vehicleS)
                    if (entry.Value.purchasable && entry.Value.commands.Count > 0)
                        stringBuilder.AppendLine(Lang("HelpRecall", player.UserIDString, configData.chatS.recallCommand, entry.Value.commands[0], entry.Value.displayName));
                Print(player, stringBuilder.ToString());
                return;
            }
            if (PlayerIsBlocked(player)) return;
            HandleRecallCmd(player, args[0].ToLower());
        }

        private void HandleRecallCmd(BasePlayer player, string option)
        {
            VehicleType vehicleType;
            if (IsValidOption(player, option, out vehicleType))
            {
                RecallVehicle(player, vehicleType);
            }
        }

        private bool RecallVehicle(BasePlayer player, VehicleType vehicleType)
        {
            var vehicleS = configData.vehicleS[vehicleType];
            Dictionary<VehicleType, Vehicle> vehicles;
            storedData.playerData.TryGetValue(player.userID, out vehicles);
            Vehicle vehicle;
            if (vehicles == null || !vehicles.TryGetValue(vehicleType, out vehicle))
            {
                Print(player, Lang("VehicleNotYetPurchased", player.UserIDString, vehicleS.displayName));
                return false;
            }
            if (vehicle.entity != null && !vehicle.entity.IsDestroyed)
            {
                string reason;
                bool checkWater = vehicleType == VehicleType.Rowboat || vehicleType == VehicleType.RHIB;
                if (CanRecall(player, vehicle.entity, vehicleType, out reason, checkWater))
                {
                    RecallVehicle(player, vehicle.entity, vehicleType, checkWater);
                    return true;
                }
                Print(player, reason);
                return false;
            }
            Print(player, Lang("VehicleNotOut", player.UserIDString, vehicleS.displayName));
            return false;
        }

        private bool CanRecall(BasePlayer player, BaseEntity vehicle, VehicleType vehicleType, out string reason, bool checkWater = false)
        {
            var vehicleS = configData.vehicleS[vehicleType];
            if (configData.globalS.anyMountedRecall && VehicleAnyMounted(vehicle))
            {
                reason = Lang("PlayerMountedOnVehicle", player.UserIDString, vehicleS.displayName);
                return false;
            }
            if (vehicleS.recallMinDistance > 0 && Vector3.Distance(player.transform.position, vehicle.transform.position) < vehicleS.recallMinDistance)
            {
                reason = Lang("RecallTooFar", player.UserIDString, vehicleS.recallMinDistance, vehicleS.displayName);
                return false;
            }
            if (player.IsBuildingBlocked())
            {
                reason = Lang("BuildingBlocked", player.UserIDString, vehicleS.displayName);
                return false;
            }
            if (player.isMounted || player.HasParent())
            {
                reason = Lang("MountedOrParented", player.UserIDString, vehicleS.displayName);
                return false;
            }
            if (checkWater && !IsLookingAtWater(player, vehicleS.distance))
            {
                reason = Lang("NotLookingAtWater", player.UserIDString, vehicleS.displayName);
                return false;
            }
            reason = null;
            return true;
        }

        private void RecallVehicle(BasePlayer player, BaseEntity entity, VehicleType vehicleType, bool checkWater = false)
        {
            if (configData.globalS.dismountAllPlayersRecall)
            {
                DismountAllPlayers(entity);
            }
            var vehicleS = configData.vehicleS[vehicleType];
            if (vehicleS.dropItemsOnRecall)
            {
                DropItems(vehicleType, entity);
            }
            Vector3 position; Quaternion rotation;
            GetVehicleSpawnPos(player, vehicleS.distance, checkWater, vehicleType, out position, out rotation);
            entity.transform.position = position;
            entity.transform.rotation = rotation;
            entity.transform.hasChanged = true;
            Print(player, Lang("VehicleRecalled", player.UserIDString, vehicleS.displayName));
        }

        private static void DropItems(VehicleType vehicleType, BaseEntity entity)
        {
            switch (vehicleType)
            {
                case VehicleType.RidableHorse:
                    var itemContainer = (entity as RidableHorse)?.inventory;
                    itemContainer?.Drop(PREFAB_ITEM_DROP, entity.GetDropPosition(), entity.transform.rotation);
                    return;

                case VehicleType.ChassisSmall:
                case VehicleType.ChassisMedium:
                case VehicleType.ChassisLarge:
                case VehicleType.ModularCarSmall:
                case VehicleType.ModularCarMedium:
                case VehicleType.ModularCarLarge:
                    var modularCar = entity as ModularCar;
                    if (modularCar == null) return;
                    foreach (var moduleEntity in modularCar.AttachedModuleEntities)
                    {
                        if (moduleEntity is VehicleModuleEngine) continue;
                        var moduleStorage = moduleEntity as VehicleModuleStorage;
                        if (moduleStorage != null)
                        {
                            var storageContainer = moduleStorage.GetContainer()?.inventory;
                            storageContainer?.Drop(PREFAB_ITEM_DROP, entity.GetDropPosition(), entity.transform.rotation);
                        }
                    }
                    return;
            }
        }

        #endregion Recall Command

        #region Kill Command

        [ConsoleCommand("vl.kill")]
        private void CCmdKillVehicle(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) Print(arg, $"The server console cannot use '{arg.cmd.FullName}'");
            else CmdKillVehicle(player, string.Empty, arg.Args);
        }

        private void CmdKillVehicle(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                Print(player, Lang("NotAllowed", player.UserIDString));
                return;
            }
            if (args == null || args.Length < 1)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(Lang("Help", player.UserIDString));
                foreach (var entry in configData.vehicleS)
                {
                    if (entry.Value.purchasable && entry.Value.commands.Count > 0)
                    {
                        stringBuilder.AppendLine(Lang("HelpKill", player.UserIDString, configData.chatS.killCommand, entry.Value.commands[0], entry.Value.displayName));
                    }
                }
                Print(player, stringBuilder.ToString());
                return;
            }
            if (PlayerIsBlocked(player)) return;
            HandleKillCmd(player, args[0].ToLower());
        }

        private void HandleKillCmd(BasePlayer player, string option)
        {
            VehicleType vehicleType;
            if (IsValidOption(player, option, out vehicleType))
            {
                KillVehicle(player, vehicleType);
            }
        }

        private bool KillVehicle(BasePlayer player, VehicleType vehicleType)
        {
            var vehicleS = configData.vehicleS[vehicleType];
            Dictionary<VehicleType, Vehicle> vehicles;
            storedData.playerData.TryGetValue(player.userID, out vehicles);
            Vehicle vehicle;
            if (vehicles == null || !vehicles.TryGetValue(vehicleType, out vehicle))
            {
                Print(player, Lang("VehicleNotYetPurchased", player.UserIDString, vehicleS.displayName));
                return false;
            }
            if (vehicle.entity != null && !vehicle.entity.IsDestroyed)
            {
                if (configData.globalS.anyMountedKill && VehicleAnyMounted(vehicle.entity))
                {
                    Print(player, Lang("PlayerMountedOnVehicle", player.UserIDString, vehicleS.displayName));
                    return false;
                }
                RefundFuel(vehicle);
                vehicle.entity.Kill(BaseNetworkable.DestroyMode.Gib);
                Print(player, Lang("VehicleKilled", player.UserIDString, vehicleS.displayName));
                return true;
            }
            Print(player, Lang("VehicleNotOut", player.UserIDString, vehicleS.displayName));
            return false;
        }

        #endregion Kill Command

        #region Command Helper

        private bool IsValidOption(BasePlayer player, string option, out VehicleType vehicleType)
        {
            foreach (var entry in configData.vehicleS)
            {
                if (entry.Value.commands.Any(x => x.ToLower() == option))
                {
                    if (!HasPermission(player, entry.Key))
                    {
                        Print(player, Lang("NotAllowed", player.UserIDString));
                        vehicleType = default(VehicleType);
                        return false;
                    }
                    vehicleType = entry.Key;
                    return true;
                }
            }
            Print(player, Lang("OptionNotFound", player.UserIDString, option));
            vehicleType = default(VehicleType);
            return false;
        }

        private void GetVehicleSpawnPos(BasePlayer player, float distance, bool checkWater, VehicleType vehicleType, out Vector3 spawnPos, out Quaternion spawnRot)
        {
            if (configData.globalS.spawnLookingAt) spawnPos = GetLookingAtGroundPos(player, distance);
            else
            {
                if (checkWater)
                {
                    spawnPos = player.transform.position;
                    for (int i = 0; i < 10; i++)
                    {
                        var originPos = GetLookingAtGroundPos(player, distance);
                        var sphere = UnityEngine.Random.insideUnitSphere * distance;
                        sphere.y = 0;
                        spawnPos = originPos + sphere;
                        if (Vector3.Distance(spawnPos, player.transform.position) > 2f) break;
                    }
                }
                else
                {
                    var sphere = UnityEngine.Random.insideUnitSphere * distance;
                    sphere.y = 0;
                    spawnPos = player.transform.position + sphere;
                }
            }
            spawnPos = GetGroundPosition(spawnPos);
            var normalized = (spawnPos - player.transform.position).normalized;
            var angle = normalized != Vector3.zero ? Quaternion.LookRotation(normalized).eulerAngles.y : UnityEngine.Random.Range(0f, 360f);
            spawnRot = Quaternion.Euler(new Vector3(0f, angle + 90f, 0f));
            if (vehicleType != VehicleType.RidableHorse) spawnPos += Vector3.up * 1f;
        }

        #endregion Command Helper

        #endregion Commands

        #region ConfigurationFile

        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Settings")]
            public Settings globalS = new Settings();

            public class Settings
            {
                [JsonProperty(PropertyName = "Interval to check vehicle for wipe (Seconds)")] public float checkVehiclesInterval = 300;
                [JsonProperty(PropertyName = "Spawn vehicle in the direction you are looking at")] public bool spawnLookingAt = true;

                [JsonProperty(PropertyName = "Check if any player mounted when recalling a vehicle")] public bool anyMountedRecall = true;
                [JsonProperty(PropertyName = "Check if any player mounted when killing a vehicle")] public bool anyMountedKill = true;
                [JsonProperty(PropertyName = "Dismount all players when a vehicle is recalled")] public bool dismountAllPlayersRecall = true;

                [JsonProperty(PropertyName = "Prevent other players from mounting vehicle")] public bool preventMounting = true;
                [JsonProperty(PropertyName = "Prevent mounting on driver's seat only")] public bool blockDriverSeat = true;
                [JsonProperty(PropertyName = "Use Teams")] public bool useTeams;
                [JsonProperty(PropertyName = "Use Clans")] public bool useClans = true;
                [JsonProperty(PropertyName = "Use Friends")] public bool useFriends = true;

                [JsonProperty(PropertyName = "Vehicle No Decay")] public bool noDecay;
                [JsonProperty(PropertyName = "Vehicle No Fire Ball")] public bool noFireBall = true;
                [JsonProperty(PropertyName = "Vehicle No Server Gibs")] public bool noServerGibs = true;
                [JsonProperty(PropertyName = "Chinook No Map Marker")] public bool noMapMarker = true;

                [JsonProperty(PropertyName = "Clear Vehicle Data On Map Wipe")] public bool clearVehicleOnWipe;
                [JsonProperty(PropertyName = "Use Raid Blocker (Need NoEscape Plugin)")] public bool useRaidBlocker;
                [JsonProperty(PropertyName = "Use Combat Blocker (Need NoEscape Plugin)")] public bool useCombatBlocker;
            }

            [JsonProperty(PropertyName = "Chat Settings")]
            public ChatSettings chatS = new ChatSettings();

            public class ChatSettings
            {
                [JsonProperty(PropertyName = "Use Universal Chat Command")] public bool useUniversalCommand = true;
                [JsonProperty(PropertyName = "Help Chat Command")] public string helpCommand = "license";
                [JsonProperty(PropertyName = "Buy Chat Command")] public string buyCommand = "buy";
                [JsonProperty(PropertyName = "Spawn Chat Command")] public string spawnCommand = "spawn";
                [JsonProperty(PropertyName = "Recall Chat Command")] public string recallCommand = "recall";
                [JsonProperty(PropertyName = "Kill Chat Command")] public string killCommand = "kill";
                [JsonProperty(PropertyName = "Chat Prefix")] public string prefix = "[VehicleLicense]: ";
                [JsonProperty(PropertyName = "Chat Prefix Color")] public string prefixColor = "#B366FF";
                [JsonProperty(PropertyName = "Chat SteamID Icon")] public ulong steamIDIcon = 76561198924840872;
            }

            [JsonProperty(PropertyName = "Cooldown Permission Settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, Dictionary<VehicleType, float>> permCooldown = new Dictionary<string, Dictionary<VehicleType, float>>
            {
                ["vehiclelicence.vip"] = new Dictionary<VehicleType, float>
                {
                    [VehicleType.Rowboat] = 90f,
                    [VehicleType.RHIB] = 150f,
                    [VehicleType.Sedan] = 450f,
                    [VehicleType.HotAirBalloon] = 450f,
                    [VehicleType.MiniCopter] = 900f,
                    [VehicleType.TransportHelicopter] = 1200f,
                    [VehicleType.Chinook] = 1500f,
                    [VehicleType.RidableHorse] = 1500f,
                    [VehicleType.ChassisSmall] = 500f,
                    [VehicleType.ChassisMedium] = 700f,
                    [VehicleType.ChassisLarge] = 900f,
                    [VehicleType.ModularCarSmall] = 1800f,
                    [VehicleType.ModularCarMedium] = 2000f,
                    [VehicleType.ModularCarLarge] = 2200f,
                }
            };

            [JsonProperty(PropertyName = "Vehicle Settings")]
            public Dictionary<VehicleType, VehicleS> vehicleS = new Dictionary<VehicleType, VehicleS>
            {
                [VehicleType.Rowboat] = new VehicleS
                {
                    purchasable = true,
                    displayName = "Row Boat",
                    maxHealth = 400,
                    cooldown = 180,
                    distance = 5,
                    permission = "vehiclelicence.rowboat",
                    commands = new List<string> { "row", "rowboat" },
                    price = new Dictionary<string, ConfigData.PriceInfo> { ["scrap"] = new ConfigData.PriceInfo { amount = 500, displayName = "Scrap" } }
                },
                [VehicleType.RHIB] = new VehicleS
                {
                    purchasable = true,
                    displayName = "RHIB",
                    maxHealth = 500,
                    cooldown = 300,
                    distance = 10,
                    permission = "vehiclelicence.rhib",
                    commands = new List<string> { "rhib" },
                    price = new Dictionary<string, ConfigData.PriceInfo> { ["scrap"] = new ConfigData.PriceInfo { amount = 1000, displayName = "Scrap" } }
                },
                [VehicleType.Sedan] = new VehicleS
                {
                    purchasable = true,
                    displayName = "Sedan",
                    maxHealth = 300,
                    cooldown = 180,
                    distance = 5,
                    permission = "vehiclelicence.sedan",
                    commands = new List<string> { "car", "sedan" },
                    price = new Dictionary<string, ConfigData.PriceInfo> { ["scrap"] = new ConfigData.PriceInfo { amount = 300, displayName = "Scrap" } }
                },
                [VehicleType.HotAirBalloon] = new VehicleS
                {
                    purchasable = true,
                    displayName = "Hot Air Balloon",
                    maxHealth = 1500,
                    cooldown = 900,
                    distance = 20,
                    permission = "vehiclelicence.hotairballoon",
                    commands = new List<string> { "hab", "hotairballoon" },
                    price = new Dictionary<string, ConfigData.PriceInfo> { ["scrap"] = new ConfigData.PriceInfo { amount = 5000, displayName = "Scrap" } }
                },
                [VehicleType.MiniCopter] = new VehicleS
                {
                    purchasable = true,
                    displayName = "Mini Copter",
                    maxHealth = 750,
                    cooldown = 1800,
                    distance = 8,
                    permission = "vehiclelicence.minicopter",
                    commands = new List<string> { "mini", "minicopter" },
                    price = new Dictionary<string, ConfigData.PriceInfo> { ["scrap"] = new ConfigData.PriceInfo { amount = 10000, displayName = "Scrap" } }
                },
                [VehicleType.TransportHelicopter] = new VehicleS
                {
                    purchasable = true,
                    displayName = "Transport Copter",
                    maxHealth = 1000,
                    cooldown = 2400,
                    distance = 10,
                    permission = "vehiclelicence.transportcopter",
                    commands = new List<string> { "tcop", "transportcopter" },
                    price = new Dictionary<string, ConfigData.PriceInfo> { ["scrap"] = new ConfigData.PriceInfo { amount = 20000, displayName = "Scrap" } }
                },
                [VehicleType.Chinook] = new VehicleS
                {
                    purchasable = true,
                    displayName = "Chinook",
                    maxHealth = 1000,
                    cooldown = 3000,
                    distance = 15,
                    permission = "vehiclelicence.chinook",
                    commands = new List<string> { "ch47", "chinook" },
                    price = new Dictionary<string, ConfigData.PriceInfo> { ["scrap"] = new ConfigData.PriceInfo { amount = 30000, displayName = "Scrap" } }
                },
                [VehicleType.RidableHorse] = new VehicleS
                {
                    purchasable = true,
                    displayName = "Ridable Horse",
                    maxHealth = 400,
                    cooldown = 3000,
                    distance = 5,
                    permission = "vehiclelicence.ridablehorse",
                    commands = new List<string> { "horse", "ridablehorse" },
                    price = new Dictionary<string, ConfigData.PriceInfo> { ["scrap"] = new ConfigData.PriceInfo { amount = 700, displayName = "Scrap" } }
                },
                [VehicleType.ChassisSmall] = new VehicleS
                {
                    purchasable = false,
                    displayName = "Small Chassis",
                    maxHealth = 200,
                    cooldown = 3300,
                    distance = 5,
                    permission = "vehiclelicence.smallchassis",
                    commands = new List<string> { "smallchassis" },
                    price = new Dictionary<string, ConfigData.PriceInfo> { ["scrap"] = new ConfigData.PriceInfo { amount = 1000, displayName = "Scrap" } }
                },
                [VehicleType.ChassisMedium] = new VehicleS
                {
                    purchasable = false,
                    displayName = "Medium Chassis",
                    maxHealth = 250,
                    cooldown = 3600,
                    distance = 5,
                    permission = "vehiclelicence.mediumchassis",
                    commands = new List<string> { "mediumchassis" },
                    price = new Dictionary<string, ConfigData.PriceInfo> { ["scrap"] = new ConfigData.PriceInfo { amount = 1300, displayName = "Scrap" } }
                },
                [VehicleType.ChassisLarge] = new VehicleS
                {
                    purchasable = false,
                    displayName = "Large Chassis",
                    maxHealth = 300,
                    cooldown = 3900,
                    distance = 5,
                    permission = "vehiclelicence.largechassis",
                    commands = new List<string> { "largechassis" },
                    price = new Dictionary<string, ConfigData.PriceInfo> { ["scrap"] = new ConfigData.PriceInfo { amount = 1600, displayName = "Scrap" } }
                },
                [VehicleType.ModularCarSmall] = new VehicleS
                {
                    purchasable = true,
                    displayName = "Small Modular Car",
                    cooldown = 4900,
                    distance = 5,
                    permission = "vehiclelicence.smallmodularcar",
                    commands = new List<string> { "smallcar" },
                    price = new Dictionary<string, ConfigData.PriceInfo> { ["scrap"] = new ConfigData.PriceInfo { amount = 1600, displayName = "Scrap" } }
                },
                [VehicleType.ModularCarMedium] = new VehicleS
                {
                    purchasable = true,
                    displayName = "Medium Modular Car",
                    cooldown = 5100,
                    distance = 5,
                    permission = "vehiclelicence.mediummodularcar",
                    commands = new List<string> { "mediumcar" },
                    price = new Dictionary<string, ConfigData.PriceInfo> { ["scrap"] = new ConfigData.PriceInfo { amount = 1800, displayName = "Scrap" } }
                },
                [VehicleType.ModularCarLarge] = new VehicleS
                {
                    purchasable = true,
                    displayName = "Large Modular Car",
                    cooldown = 5400,
                    distance = 5,
                    permission = "vehiclelicence.largemodularcar",
                    commands = new List<string> { "largecar" },
                    price = new Dictionary<string, ConfigData.PriceInfo> { ["scrap"] = new ConfigData.PriceInfo { amount = 2000, displayName = "Scrap" } }
                },
            };

            public class VehicleS
            {
                [JsonProperty(PropertyName = "Purchasable")] public bool purchasable;
                [JsonProperty(PropertyName = "Use Permission")] public bool usePermission = true;
                [JsonProperty(PropertyName = "Permission")] public string permission;
                [JsonProperty(PropertyName = "Display Name")] public string displayName;
                [JsonProperty(PropertyName = "Cooldown (Seconds)")] public double cooldown;
                [JsonProperty(PropertyName = "Max Health")] public float maxHealth;
                [JsonProperty(PropertyName = "Distance To Spawn")] public float distance;
                [JsonProperty(PropertyName = "Can Recall Min Distance")] public float recallMinDistance;
                [JsonProperty(PropertyName = "Time Before Vehicle Wipe (Seconds)")] public double wipeTime;
                [JsonProperty(PropertyName = "Remove License On Crash")] public bool removeLicenseOnCrash;
                [JsonProperty(PropertyName = "Refund Fuel")] public bool refundFuel = true;
                [JsonProperty(PropertyName = "Refund Inventory")] public bool refundInventory = true;
                [JsonProperty(PropertyName = "Not Refund Fuel On Crash")] public bool notRefundFuelOnCrash;
                [JsonProperty(PropertyName = "Not Refund Inventory On Crash")] public bool notRefundInventoryOnCrash;
                [JsonProperty(PropertyName = "Drop Inventory Items When Vehicle Recall")] public bool dropItemsOnRecall;
                [JsonProperty(PropertyName = "Commands")] public List<string> commands;
                [JsonProperty(PropertyName = "Price")] public Dictionary<string, PriceInfo> price;
            }

            public struct PriceInfo
            {
                public int amount;
                public string displayName;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
                    LoadDefaultConfig();
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(configData);

        #endregion ConfigurationFile

        #region DataFile

        private StoredData storedData;

        private class StoredData
        {
            public readonly Dictionary<ulong, Dictionary<VehicleType, Vehicle>> playerData = new Dictionary<ulong, Dictionary<VehicleType, Vehicle>>();
        }

        private class Vehicle
        {
            public double lastDeath;
            [JsonIgnore] public BaseEntity entity;
            [JsonIgnore] public ulong playerID;
            [JsonIgnore] public double lastDismount;
            [JsonIgnore] public VehicleType vehicleType;

            public void OnDismount() => lastDismount = TimeEx.currentTimestamp;

            public void OnDeath()
            {
                entity = null;
                lastDeath = TimeEx.currentTimestamp;
            }
        }

        private void LoadData()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
                storedData = null;
            }
            finally
            {
                if (storedData == null)
                {
                    ClearData();
                }
            }
        }

        private void ClearData()
        {
            storedData = new StoredData();
            SaveData();
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

        private void OnNewSave(string filename)
        {
            if (configData.globalS.clearVehicleOnWipe)
            {
                ClearData();
            }
        }

        #endregion DataFile

        #region LanguageFile

        private void Print(BasePlayer player, string message)
        {
            Player.Message(player, message, string.IsNullOrEmpty(configData.chatS.prefix) ? string.Empty : $"<color={configData.chatS.prefixColor}>{configData.chatS.prefix}</color>", configData.chatS.steamIDIcon);
        }

        private void Print(ConsoleSystem.Arg arg, string message)
        {
            var player = arg.Player();
            if (player == null) Puts(message);
            else PrintToConsole(player, message);
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Help"] = "These are the available commands:",
                ["HelpLicence1"] = "<color=#4DFF4D>/{0}</color> -- To buy a vehicle",
                ["HelpLicence2"] = "<color=#4DFF4D>/{0}</color> -- To spawn a vehicle",
                ["HelpLicence3"] = "<color=#4DFF4D>/{0}</color> -- To recall a vehicle",
                ["HelpLicence4"] = "<color=#4DFF4D>/{0}</color> -- To kill a vehicle",
                ["HelpLicence5"] = "<color=#4DFF4D>/{0}</color> -- To buy, spawn or recall a vehicle",

                ["HelpBuy"] = "<color=#4DFF4D>/{0} {1}</color> -- To buy a {2}. Price: <color=#FF1919>{3}</color>",
                ["HelpSpawn"] = "<color=#4DFF4D>/{0} {1}</color> -- To spawn a {2}",
                ["HelpRecall"] = "<color=#4DFF4D>/{0} {1}</color> -- To recall a {2}",
                ["HelpKill"] = "<color=#4DFF4D>/{0} {1}</color> -- To kill a {2}",

                ["NotAllowed"] = "You do not have permission to use this command.",
                ["NotEnoughCost"] = "You don't have enough resources to buy a {0}. You are missing:{1}",
                ["RaidBlocked"] = "<color=#FF1919>You may not do that while raid blocked</color>.",
                ["CombatBlocked"] = "<color=#FF1919>You may not do that while combat blocked</color>.",
                ["OptionNotFound"] = "This '{0}' option doesn't exist.",
                ["VehiclePurchased"] = "You have purchased a {0}, type <color=#4DFF4D>/{1}</color> for more information.",
                ["VehicleAlreadyPurchased"] = "You have already purchased {0}.",
                ["VehicleCannotBeBought"] = "{0} is unpurchasable",
                ["VehicleNotOut"] = "{0} is not out.",
                ["AlreadyVehicleOut"] = "You already have a {0} outside, type <color=#4DFF4D>/{1}</color> for more information.",
                ["VehicleNotYetPurchased"] = "You have not yet purchased a {0}.",
                ["VehicleSpawned"] = "You spawned your {0}.",
                ["VehicleRecalled"] = "You recalled your {0}.",
                ["VehicleKilled"] = "You killed your {0}.",
                ["VehicleOnCooldown"] = "You must wait {0} seconds before you can spawn your {1}.",
                ["NotLookingAtWater"] = "You must be looking at water to spawn or recall a {0}.",
                ["BuildingBlocked"] = "You can't spawn a {0} appear if you don't have the building privileges.",
                ["RefundedVehicleFuel"] = "Your {0} fuel was refunded to your inventory.",
                ["PlayerMountedOnVehicle"] = "It cannot be recalled when players mounted on your {0}.",

                ["MountedOrParented"] = "You cannot spawn a vehicle when mounted or parented.",
                ["RecallTooFar"] = "You must be within {0} meters of {1} to recall.",
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Help"] = "可用命令列表:",
                ["HelpLicence1"] = "<color=#4DFF4D>/{0}</color> -- 购买一辆载具",
                ["HelpLicence2"] = "<color=#4DFF4D>/{0}</color> -- 生成一辆载具",
                ["HelpLicence3"] = "<color=#4DFF4D>/{0}</color> -- 召回一辆载具",
                ["HelpLicence4"] = "<color=#4DFF4D>/{0}</color> -- 删除一辆载具",
                ["HelpLicence5"] = "<color=#4DFF4D>/{0}</color> -- 购买，生成，召回一辆载具",

                ["HelpBuy"] = "<color=#4DFF4D>/{0} {1}</color> -- 购买一辆 {2}. 价格: <color=#FF1919>{3}</color>",
                ["HelpSpawn"] = "<color=#4DFF4D>/{0} {1}</color> -- 生成一辆 {2}",
                ["HelpRecall"] = "<color=#4DFF4D>/{0} {1}</color> -- 召回一辆 {2}",
                ["HelpKill"] = "<color=#4DFF4D>/{0} {1}</color> -- 删除一辆 {2}",

                ["NotAllowed"] = "您没有权限使用该命令",
                ["NotEnoughCost"] = "您没有足够的资源购买 {0}，还需要:{1}",
                ["RaidBlocked"] = "<color=#FF1919>您被突袭阻止了，不能使用该命令</color>.",
                ["CombatBlocked"] = "<color=#FF1919>您被战斗阻止了，不能使用该命令</color>.",
                ["OptionNotFound"] = "该 '{0}' 选项不存在",
                ["VehiclePurchased"] = "您购买了 {0}, 输入 <color=#4DFF4D>/{1}</color> 了解更多信息",
                ["VehicleAlreadyPurchased"] = "您已经购买了 {0}",
                ["VehicleCannotBeBought"] = "{0} 是不可购买的",
                ["VehicleNotOut"] = "您还没有生成您的 {0}",
                ["AlreadyVehicleOut"] = "您已经生成了您的 {0}, 输入 <color=#4DFF4D>/{1}</color> 了解更多信息",
                ["VehicleNotYetPurchased"] = "您还没有购买 {0}.",
                ["VehicleSpawned"] = "您生成了您的 {0}.",
                ["VehicleRecalled"] = "您召回了您的 {0}.",
                ["VehicleKilled"] = "您删除了您的 {0}.",
                ["VehicleOnCooldown"] = "您必须等待 {0} 秒才能生成您的 {1}",
                ["NotLookingAtWater"] = "您必须看着水面才能生成您的 {0}",
                ["BuildingBlocked"] = "您没有领地柜权限，无法生成您的 {0}",
                ["BuildingBlocked"] = "您的 {0} 燃料已经归还回您的库存",
                ["PlayerMountedOnVehicle"] = "您的 {0} 上坐着玩家，无法被召回",

                ["MountedOrParented"] = "当您坐着或者在附着在实体上时无法生成载具",
                ["RecallTooFar"] = "您必须在 {0} 米内才能召回您的 {1}",
            }, this, "zh-CN");
        }

        #endregion LanguageFile
    }
}