using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Blueprint Manager", "Orange", "1.1.4")]
    [Description("Manage blueprints on your server easily")]
    public class BlueprintManager : RustPlugin
    {
        #region Vars
        
        private Blueprints data = new Blueprints();
        private const string permLVL1 = "blueprintmanager.lvl1";
        private const string permLVL2 = "blueprintmanager.lvl2";
        private const string permLVL3 = "blueprintmanager.lvl3";
        private const string permAll = "blueprintmanager.all";
        private const string permDefault = "blueprintmanager.default";
        private const string permAdmin = "blueprintmanager.admin";
        
        private class Blueprints
        {
            public List<int> workbench1 = new List<int>();
            public List<int> workbench2 = new List<int>();
            public List<int> workbench3 = new List<int>();
            public List<int> allBlueprints = new List<int>();
            public List<int> defaultBlueprints = new List<int>();
        }
        
        #endregion

        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(permAll, this);
            permission.RegisterPermission(permLVL1, this);
            permission.RegisterPermission(permLVL2, this);
            permission.RegisterPermission(permLVL3, this);
            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permDefault, this);
            cmd.AddConsoleCommand("blueprintmanager", this, nameof(cmdBlueprintsConsole));
        }

        private void OnServerInitialized()
        {
            CheckBlueprints();
            CheckPlayers();
        }
        
        private void OnPlayerConnected(BasePlayer player)
        {
            CheckPlayer(player);
        }

        #endregion

        #region Core
        
        private void CheckBlueprints()
        {
            foreach (var bp in ItemManager.bpList)
            {
                if (bp.userCraftable && bp.defaultBlueprint == false)
                {
                    var itemID = bp.targetItem.itemid;
                    var shortname = bp.targetItem.shortname;
                    if (config.blacklist?.Contains(shortname) ?? false)
                    {
                        continue;
                    }

                    switch (bp.workbenchLevelRequired)
                    {
                        case 1:
                            data.workbench1.Add(itemID);
                            break;
                        
                        case 2:
                            data.workbench2.Add(itemID);
                            break;
                        
                        case 3:
                            data.workbench3.Add(itemID);
                            break;
                    }

                    if (config.defaultBlueprints?.Contains(shortname) ?? false)
                    {
                        data.defaultBlueprints.Add(itemID);
                    }
                    
                    data.allBlueprints.Add(itemID);
                }
            }
        }

        private void CheckPlayers()
        {
            timer.Once(5f, () =>
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    OnPlayerConnected(player);
                }
            });
        }

        private void CheckPlayer(BasePlayer player)
        {
            var blueprints = GetBlueprints(player);
            UnlockBlueprints(player, blueprints);
        }

        private List<int> GetBlueprints(BasePlayer player)
        {
            var list = new List<int>();

            if (permission.UserHasPermission(player.UserIDString, permDefault))
            {
                list.AddRange(data.defaultBlueprints);
            }

            if (permission.UserHasPermission(player.UserIDString, permAll))
            {
                list.AddRange(data.allBlueprints);
                return list;
            }
            
            if (permission.UserHasPermission(player.UserIDString, permLVL3))
            {
                list.AddRange(data.workbench3);
            }
            
            if (permission.UserHasPermission(player.UserIDString, permLVL2))
            {
                list.AddRange(data.workbench2);
            }
            
            if (permission.UserHasPermission(player.UserIDString, permLVL1))
            {
                list.AddRange(data.workbench1);
            }

            return list;
        }

        private void UnlockBlueprints(BasePlayer player, List<int> blueprints)
        {
            var playerInfo = SingletonComponent<ServerMgr>.Instance.persistance.GetPlayerInfo(player.userID);
            
            foreach (var blueprint in blueprints)
            {
                if (playerInfo.unlockedItems.Contains(blueprint) == false)
                {
                    playerInfo.unlockedItems.Add(blueprint);
                }
            }
            
            SingletonComponent<ServerMgr>.Instance.persistance.SetPlayerInfo(player.userID, playerInfo);
            player.SendNetworkUpdateImmediate();
            player.ClientRPCPlayer(null, player, "UnlockedBlueprint", 0);
        }

        private void ResetBlueprints(BasePlayer player)
        {
            var playerInfo = SingletonComponent<ServerMgr>.Instance.persistance.GetPlayerInfo(player.userID);
            playerInfo.unlockedItems = new List<int>();
            SingletonComponent<ServerMgr>.Instance.persistance.SetPlayerInfo(player.userID, playerInfo);
            player.SendNetworkUpdateImmediate();
            player.ClientRPCPlayer(null, player, "UnlockedBlueprint", 0);
        }
        
        private BasePlayer FindPlayer(string nameOrID)
        {
            var targets = BasePlayer.activePlayerList.Where(x => x.UserIDString == nameOrID || x.displayName.ToLower().Contains(nameOrID.ToLower())).ToArray();
            
            if (targets.Length == 0)
            {
                PrintWarning(GetMessage("No Players", nameOrID));
                return null;
            }

            if (targets.Length > 1)
            {
                PrintWarning(GetMessage("Multiple Players", targets.Select(x => x.displayName).ToSentence()));
                return null;
            }

            return targets[0];
        }

        #endregion

        #region Commands

        private void cmdBlueprintsConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && permission.UserHasPermission(player.UserIDString, permAdmin) == false)
            {
                Message(arg, "Permission");
                return;
            }

            var args = arg.Args;
            if (args == null || args.Length < 2)
            {
                Message(arg, "Usage");
                return;
            }

            var action = args[0].ToLower();

            var target = FindPlayer(args[1]);
            if (target == null)
            {
                return;
            }

            switch (action)
            {
                case "reset":
                    ResetBlueprints(target);
                    break;
                
                case "unlock":
                    if (args.Length < 3)
                    {
                        Message(arg, "Usage");
                        return;
                    }

                    var itemID = ItemManager.FindItemDefinition(args[2])?.itemid ?? 0;
                    UnlockBlueprints(target, new List<int>{itemID});
                    break;
                
                case "unlockall":
                    UnlockBlueprints(target, data.allBlueprints);
                    break;
                
                default:
                    Message(arg, "Usage");
                    return;
            }
            
            Message(arg, "Success", target.displayName);
        }

        #endregion
        
        #region Configuration 1.1.2

        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Blacklist")]
            public List<string> blacklist = new List<string>();
            
            [JsonProperty(PropertyName = "Default blueprints")]
            public List<string> defaultBlueprints = new List<string>();
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                blacklist = new List<string>
                {
                    "explosive.timed",
                    "rocket.launcher",
                    "ammo.rocket.basic",
                    "ammo.rocket.fire",
                    "ammo.rocket.hv",
                    "ammo.rifle.explosive"
                },
                defaultBlueprints = new List<string>
                {
                    "pistol.revolver",
                    "pistol.semiauto",
                    "pickaxe",
                    "hatchet"
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                
                timer.Every(10f, () =>
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                });
                LoadDefaultConfig();
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion
        
        #region Localization 1.1.1
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Usage", "Usage:\n" +
                          " * blueprintmanager reset 'player name or id' - resets blueprints for player\n" +
                          " * blueprintmanager unlock 'player name or id' 'item shortname' - unlock specified blueprint for player\n" +
                          " * blueprintmanager unlockall 'player name or id'- unlocks all blueprints for player"},
                {"Permission", "You don't have permission to use that!"},
                
                {"No Players", "There are no players with that Name or steamID! ({0})"},
                {"Multiple Players", "There are many players with that Name:\n{0}"},
                {"Success", "Your action was done successfully for '{0}'!"}
            }, this);
        }
        
        private string GetMessage(string messageKey, string playerID, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerID), args);
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
                SendReply(arg, message);
            }
        }

        #endregion
    }
}