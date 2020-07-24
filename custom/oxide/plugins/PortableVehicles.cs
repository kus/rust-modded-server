using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Portable Vehicles", "Orange", "1.0.9")]
    [Description("Give vehicles as item to your players")]
    public class PortableVehicles : RustPlugin
    {
        #region Vars

        private Dictionary<ulong, string> skins = new Dictionary<ulong, string>
        {
            {1742627792, "assets/content/vehicles/boats/rhib/rhib.prefab"},
            {1742651766, "assets/content/vehicles/boats/rowboat/rowboat.prefab"},
            {1742653197, "assets/content/vehicles/minicopter/minicopter.entity.prefab"},
            {1742652663, "assets/content/vehicles/sedan_a/sedantest.entity.prefab"},
            {1771792500, "assets/prefabs/npc/ch47/ch47.entity.prefab"},
            {1771792987, "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab"},
            {1773898864, "assets/rust.ai/nextai/testridablehorse.prefab"},
            {1856165291, "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab"},
            {2160249787, "assets/content/vehicles/modularcar/2module_car_spawned.entity.prefab"},
            {2160250208, "assets/content/vehicles/modularcar/3module_car_spawned.entity.prefab"},
            {2160251723, "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab"},
        };

        private const string itemName = "box.repair.bench";
        private const string command = "portablevehicles.give";
        private const string permPickup = "portablevehicles.pickup";

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            cmd.AddConsoleCommand(command, this, nameof(cmdGiveConsole));
            permission.RegisterPermission(permPickup, this);
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            CheckPlacement(plan, go);
        }

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            CheckPickup(player, info?.HitEntity?.GetComponent<BaseVehicle>());
        }

        #endregion

        #region Commands

        private void cmdGiveConsole(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin == false)
            {
                Message(arg, "Permission");
                return;
            }

            var args = arg.Args;
            if (args == null || args?.Length < 2)
            {
                Message(arg, "Usage");
                return;
            }

            var player = FindPlayer(arg, args[0]);
            if (player == null)
            {
                return;
            }

            var skin = GetSkin(args[1]);
            if (skin == 0)
            {
                Message(arg, "Usage");
                return;
            }

            GiveItem(player, skin);
        }

        #endregion

        #region Core

        private void CheckPlacement(Planner plan, GameObject go)
        {
            var entity = go.ToBaseEntity();
            if (entity == null)
            {
                return;
            }

            var player = plan.GetOwnerPlayer();
            var prefab = (string) null;
            if (!skins.TryGetValue(entity.skinID, out prefab))
            {
                return;
            }

            var transform = entity.transform;
            var position = transform.position;
            var rotation = transform.rotation;
            var owner = entity.OwnerID;
            entity.Kill();

            var vehicle = GameManager.server.CreateEntity(prefab, position, rotation)?.GetComponent<BaseVehicle>();
            if (vehicle != null)
            {
                vehicle.OwnerID = owner;
                vehicle.Spawn();

                if (vehicle.mountPoints != null && vehicle.mountPoints.Count > 0)
                {
                    var driverSeat = vehicle.mountPoints.FirstOrDefault()?.mountable;
                    if (driverSeat != null)
                    {
                        driverSeat.MountPlayer(player);
                        player.SendNetworkUpdate();
                    }
                }
            }
        }

        private void CheckPickup(BasePlayer player, BaseVehicle entity)
        {
            if (entity == null)
            {
                return;
            }

            if (entity.OwnerID != player.userID)
            {
                Message(player, "Pickup Ownership");
                return;
            }

            var container = entity.GetFuelSystem()?.GetFuelContainer();
            if (container != null && container.IsValid() && container.IsLocked() == false && container.inventory.IsLocked() == false)
            {
                Message(player, "Fuel");
                return;
            }

            foreach (var value in skins)
            {
                if (value.Value == entity.PrefabName)
                {
                    if (permission.UserHasPermission(player.UserIDString, permPickup))
                    {
                        entity.Kill();
                        GiveItem(player, value.Key);
                    }

                    return;
                }
            }
        }

        private BasePlayer FindPlayer(ConsoleSystem.Arg arg, string nameOrID)
        {
            var targets = BasePlayer.activePlayerList.Where(x =>
                x.UserIDString == nameOrID || x.displayName.ToLower().Contains(nameOrID.ToLower())).ToList();

            if (targets.Count == 0)
            {
                Message(arg, "No Player");
                return null;
            }

            if (targets.Count > 1)
            {
                Message(arg, "Multiple Players");
                return null;
            }

            return targets[0];
        }

        private void GiveItem(BasePlayer player, ulong skinID)
        {
            var item = ItemManager.CreateByName(itemName, 1, skinID);
            if (item != null)
            {
                item.name = "Portable Vehicle";
                player.GiveItem(item);
                Message(player, "Received");
            }
        }

        private ulong GetSkin(string name)
        {
            switch (name.ToLower())
            {
                case "rhib":
                case "militaryboat":
                case "military":
                    return 1742627792;

                case "boat":
                case "rowboat":
                case "motorboat":
                    return 1742651766;

                case "copter":
                case "minicopter":
                    return 1742653197;

                case "car":
                case "sedan":
                    return 1742652663;

                case "balloon":
                case "hotairballoon":
                    return 1771792987;

                case "ch":
                case "ch47":
                case "chinook":
                    return 1771792500;

                case "horse":
                case "testridablehorse":
                    return 1773898864;

                case "scrap":
                case "scraphelicopter":
                case "helicopter":
                    return 1856165291;
                
                case "car2":
                    return 2160249787;
                
                case "car3":
                    return 2160250208;
                
                case "car4":
                    return 2160251723;

                default:
                    return 0;
            }
        }

        #endregion

        #region Localization 1.1.1

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Usage", "Usage: portablevehicles.give [steamID / player name] [vehicle name]\n"},
                {"Permission", "You don't have permission to use that!"},
                {"Received", "You received portable vehicle!"},
                {"No Player", "There are no players with that Name or steamID!"},
                {"Multiple Players", "There are many players with that Name:\n{0}"},
                {"Pickup Ownership", "Only owner can pickup vehicles!"},
                {"Fuel", "You need to remove fuel from vehicle first!"}
            }, this);
        }

        private void Message(ConsoleSystem.Arg arg, string messageKey, params object[] args)
        {
            var message = GetMessage(messageKey, null, args);
            var player = arg.Player();
            if (player != null)
            {
                player.ChatMessage(message);
            }
            else
            {
                Puts(message);
            }
        }

        private void Message(BasePlayer player, string messageKey, params object[] args)
        {
            if (player == null)
            {
                return;
            }

            var message = GetMessage(messageKey, player.UserIDString, args);
            player.ChatMessage(message);
        }

        private string GetMessage(string messageKey, string playerID, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerID), args);
        }

        #endregion
    }
}