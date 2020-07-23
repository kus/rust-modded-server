using Oxide.Core;   
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Spawn Mini", "SpooksAU", "2.6.3"), Description("Spawn a mini!")]
    class SpawnMini : RustPlugin
    {
        private SaveData _data;
        private PluginConfig _config;

        /* EDIT PERMISSIONS HERE */
        private readonly string _spawnMini = "spawnmini.mini";
        private readonly string _noCooldown = "spawnmini.nocd";
        private readonly string _noMini = "spawnmini.nomini";
        private readonly string _fetchMini = "spawnmini.fmini";
        private readonly string _noFuel = "spawnmini.unlimitedfuel";
        private readonly string _noDecay = "spawnmini.nodecay";

        #region Hooks

        private void Loaded() 
        {
            _config = Config.ReadObject<PluginConfig>();

            permission.RegisterPermission(_spawnMini, this);
            permission.RegisterPermission(_noCooldown, this);
            permission.RegisterPermission(_noMini, this);
            permission.RegisterPermission(_fetchMini, this);
            permission.RegisterPermission(_noFuel, this);
            permission.RegisterPermission(_noDecay, this);

            foreach (var perm in _config.cooldowns)
                permission.RegisterPermission(perm.Key, this);

            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("SpawnMini"))
                Interface.Oxide.DataFileSystem.GetDatafile("SpawnMini").Save();

            _data = Interface.Oxide.DataFileSystem.ReadObject<SaveData>("SpawnMini");

            if (!_config.ownerOnly)
                Unsubscribe(nameof(CanMountEntity));
        }

        void Unload() 
            => Interface.Oxide.DataFileSystem.WriteObject("SpawnMini", _data);

        void OnEntityKill(MiniCopter mini)
        {
            if (_data.playerMini.ContainsValue(mini.net.ID))
            {
                string key = _data.playerMini.FirstOrDefault(x => x.Value == mini.net.ID).Key;

                ulong result;
                ulong.TryParse(key, out result);
                BasePlayer player = BasePlayer.FindByID(result);

                if (player != null)
                    player.ChatMessage(lang.GetMessage("mini_destroyed", this, player.UserIDString));

                _data.playerMini.Remove(key);
            }
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null || entity.OwnerID == 0)
                return null;

            if (_data.playerMini.ContainsValue(entity.net.ID))
                if (permission.UserHasPermission(entity.OwnerID.ToString(), _noDecay) && info.damageTypes.Has(Rust.DamageType.Decay))
                    return false;

            return null;
        }

        object CanMountEntity(BasePlayer player, BaseMountable entity)
        {
            if (player == null || entity == null || entity.OwnerID == 0)
                return null;

            if (entity.OwnerID != player.userID)
            {
                if (player.Team != null && player.Team.members.Contains(entity.OwnerID))
                    return null;

                player.ChatMessage(lang.GetMessage("mini_canmount", this, player.UserIDString));
                return false;
            }
            return null;
        }

        #endregion

        #region Commands

        [ChatCommand("mymini")]
        private void GiveMinicopter(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _spawnMini))
            {
                player.ChatMessage(lang.GetMessage("mini_perm", this, player.UserIDString));
            }
            else
            {
                if (_data.playerMini.ContainsKey(player.UserIDString))
                {
                    // Should fix the bug that someone posted on the forums, should look for a better fix later though
                    // https://umod.org/community/spawn-mini/21188-unable-to-spawn-mini
                    if (BaseNetworkable.serverEntities.Find(_data.playerMini[player.UserIDString]) == null)
                    {
                        _data.playerMini.Remove(player.UserIDString);
                        player.ChatMessage(lang.GetMessage("mini_error", this, player.UserIDString));
                    }
                    else
                    {
                        player.ChatMessage(lang.GetMessage("mini_current", this, player.UserIDString));
                    } 
                }
                else if (_data.cooldown.ContainsKey(player.UserIDString) && !permission.UserHasPermission(player.UserIDString, _noCooldown))
                {
                    DateTime cooldown = _data.cooldown[player.UserIDString];
                    if (cooldown > DateTime.Now)
                    {
                        player.ChatMessage(string.Format(lang.GetMessage("mini_timeleft", this, player.UserIDString),
                            Math.Round((cooldown - DateTime.Now).TotalMinutes, 2)));
                    }
                    else
                    {
                        _data.cooldown.Remove(player.UserIDString);
                        SpawnMinicopter(player);
                    }
                }
                else if (!_data.playerMini.ContainsKey(player.UserIDString))
                {
                    SpawnMinicopter(player);
                }
            }
        }        

        [ChatCommand("fmini")]
        private void FetchMinicopter(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _fetchMini))
            {
                player.ChatMessage(lang.GetMessage("mini_perm", this, player.UserIDString));
            }
            else
            {
                if (!_data.playerMini.ContainsKey(player.UserIDString))
                {
                    player.ChatMessage(lang.GetMessage("mini_notcurrent", this, player.UserIDString));
                }
                else if (_data.playerMini.ContainsKey(player.UserIDString))
                {
                    MiniCopter mini = BaseNetworkable.serverEntities.Find(_data.playerMini[player.UserIDString]) as MiniCopter;

                    if (mini == null)
                        return;

                    if (!mini.AnyMounted() && GetDistance(player, mini) < _config.noMiniDistance)
                    {
                        Puts(GetDistance(player, mini).ToString());
                        Vector3 destination = new Vector3(player.transform.position.x, player.transform.position.y + 3.5f, player.transform.position.z + 2);
                        mini.transform.position = destination;
                    }
                    else if (GetDistance(player, mini) > _config.noMiniDistance)
                    {
                        player.ChatMessage(lang.GetMessage("mini_distance", this, player.UserIDString));
                    }
                    else if (mini.AnyMounted())
                    {
                        player.ChatMessage(lang.GetMessage("mini_mounted", this, player.UserIDString));
                    }
                }
            }
        }
        
        [ChatCommand("nomini")]
        private void NoMinicopter(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _noMini))
            {
                player.ChatMessage(lang.GetMessage("mini_perm", this, player.UserIDString));
            }
            else
            {
                if (!_data.playerMini.ContainsKey(player.UserIDString))
                {
                    player.ChatMessage(lang.GetMessage("mini_notcurrent", this, player.UserIDString));
                }
                else if (_data.playerMini.ContainsKey(player.UserIDString))
                {
                    MiniCopter mini = BaseNetworkable.serverEntities.Find(_data.playerMini[player.UserIDString]) as MiniCopter;

                    if (mini == null)
                        return;

                    if (!mini.AnyMounted() && GetDistance(player, mini) < _config.noMiniDistance)
                    {
                        BaseNetworkable.serverEntities.Find(_data.playerMini[player.UserIDString])?.Kill();
                    }
                    else if (GetDistance(player, mini) > _config.noMiniDistance)
                    {
                        player.ChatMessage(lang.GetMessage("mini_distance", this, player.UserIDString));
                    }
                    else if (mini.AnyMounted())
                    {
                        player.ChatMessage(lang.GetMessage("mini_mounted", this, player.UserIDString));
                    }
                }
            }
        }

        [ConsoleCommand("spawnmini.give")]
        private void GiveMiniConsole(ConsoleSystem.Arg arg)
        {
            if (arg.IsClientside)
                return;

            if (arg.Args == null)
            {
                Puts("Please put a valid steam64 id");
                return;
            }
            else if (arg.Args.Length == 1 && arg.IsRcon)
            {
                SpawnMinicopter(arg.Args[0]);
            }
        }

        #endregion

        #region Helpers/Functions

        private void SpawnMinicopter(BasePlayer player)
        {
            if (!player.IsBuildingBlocked() || _config.canSpawnBuildingBlocked)
            {
                RaycastHit hit;
                if (Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity, 
                    LayerMask.GetMask("Construction", "Default", "Deployed", "Resource", "Terrain", "Water", "World")))
                {
                    if (hit.distance > _config.maxSpawnDistance)
                    {
                        player.ChatMessage(lang.GetMessage("mini_sdistance", this, player.UserIDString));
                    }
                    else
                    {
                        Vector3 position = hit.point + Vector3.up * 2f;
                        BaseVehicle miniEntity = (BaseVehicle)GameManager.server.CreateEntity(_config.assetPrefab, position, new Quaternion());
                        miniEntity.OwnerID = player.userID;
                        miniEntity.health = _config.spawnHealth;
                        miniEntity.Spawn();

                        // Credit Original MyMinicopter Plugin
                        if (permission.UserHasPermission(player.UserIDString, _noFuel))
                        {
                            MiniCopter minicopter = miniEntity as MiniCopter;

                            if (minicopter == null)
                                return;

                            minicopter.fuelPerSec = 0f;

                            StorageContainer fuelContainer = minicopter.GetFuelSystem().GetFuelContainer();
                            ItemManager.CreateByItemID(-946369541, 1)?.MoveToContainer(fuelContainer.inventory);
                            fuelContainer.SetFlag(BaseEntity.Flags.Locked, true);
                        }

                        _data.playerMini.Add(player.UserIDString, miniEntity.net.ID);

                        if (!permission.UserHasPermission(player.UserIDString, _noCooldown))
                        {
                            

                            // Sloppy but works
                            // TODO: Improve later
                            Dictionary<string, float> perms = new Dictionary<string, float>();
                            foreach (var perm in _config.cooldowns)
                                if (permission.UserHasPermission(player.UserIDString, perm.Key))
                                    perms.Add(perm.Key, perm.Value);

                            if (!perms.IsEmpty())
                                _data.cooldown.Add(player.UserIDString, DateTime.Now.AddSeconds(perms.Aggregate((l, r) => l.Value < r.Value ? l : r).Value));

                            // Incase players don't have any cooldown permission default to one day
                            if (!_data.cooldown.ContainsKey(player.UserIDString))
                                _data.cooldown.Add(player.UserIDString, DateTime.Now.AddDays(1));
                        }
                    }
                }
                else
                {
                    player.ChatMessage(lang.GetMessage("mini_terrain", this, player.UserIDString));
                }
            }
            else
            {
                player.ChatMessage(lang.GetMessage("mini_priv", this, player.UserIDString));
            }
        }

        private void SpawnMinicopter(string id)
        {
            // Use find incase they put their username
            BasePlayer player = BasePlayer.Find(id);

            if (player == null)
                return;

            if (_data.playerMini.ContainsKey(player.UserIDString))
            {
                player.ChatMessage(lang.GetMessage("mini_current", this, player.UserIDString));
                return;
            }

            // Credit Original MyMinicopter Plugin
            Quaternion rotation = player.GetNetworkRotation();
            Vector3 forward = rotation * Vector3.forward;
            Vector3 straight = Vector3.Cross(Vector3.Cross(Vector3.up, forward), Vector3.up).normalized;
            Vector3 position = player.transform.position + straight * 5f;
            position.y = player.transform.position.y + 2f;

            if (position == null) return;
            BaseVehicle vehicleMini = (BaseVehicle)GameManager.server.CreateEntity(_config.assetPrefab, position, new Quaternion());
            if (vehicleMini == null) return;
            vehicleMini.OwnerID = player.userID;
            vehicleMini.Spawn();
            // End

            _data.playerMini.Add(player.UserIDString, vehicleMini.net.ID);

            if (permission.UserHasPermission(player.UserIDString, _noFuel))
            {
                MiniCopter minicopter = vehicleMini as MiniCopter;

                if (minicopter == null)
                    return;

                minicopter.fuelPerSec = 0f;

                StorageContainer fuelContainer = minicopter.GetFuelSystem().GetFuelContainer();
                ItemManager.CreateByItemID(-946369541, 1)?.MoveToContainer(fuelContainer.inventory);
                fuelContainer.SetFlag(BaseEntity.Flags.Locked, true);
            }
        }

        private float GetDistance(BasePlayer player, MiniCopter mini)
        {
            float distance = Vector3.Distance(player.transform.position, mini.transform.position);
            return distance;
        }

        private bool IsPlayerOwned(MiniCopter mini)
        {
            if (mini != null && _data.playerMini.ContainsValue(mini.net.ID))
                return true;

            return false; 
        }

        #endregion

        #region Classes And Overrides

        class SaveData
        {
            public Dictionary<string, uint> playerMini = new Dictionary<string, uint>();
            public Dictionary<string, DateTime> cooldown = new Dictionary<string, DateTime>();
        }

        class PluginConfig
        {
            [JsonProperty("AssetPrefab")]
            public string assetPrefab { get; set; }

            [JsonProperty("MaxSpawnDistance")]
            public float maxSpawnDistance { get; set; }

            [JsonProperty("MaxNoMiniDistance")]
            public float noMiniDistance { get; set; }

            [JsonProperty("SpawnHealth")]
            public float spawnHealth { get; set; }

            [JsonProperty("CanSpawnBuildingBlocked")]
            public bool canSpawnBuildingBlocked { get; set; }

            [JsonProperty("PermissionCooldowns")]
            public Dictionary<string, float> cooldowns { get; set; }

            [JsonProperty("OwnerAndTeamCanMount")]
            public bool ownerOnly { get; set; }
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                maxSpawnDistance = 5f,
                spawnHealth = 750f,
                noMiniDistance = 300f,
                assetPrefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab",
                canSpawnBuildingBlocked = false,
                ownerOnly = false,
                cooldowns = new Dictionary<string, float>()
                {
                    ["spawnmini.tier1"] = 86400f,
                    ["spawnmini.tier2"] = 43200f,
                    ["spawnmini.tier3"] = 21600f,
                }
            };
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["mini_destroyed"] = "Your minicopter has been destroyed!",
                ["mini_perm"] = "You do not have permission to use this command!",
                ["mini_current"] = "You already have a minicopter!",
                ["mini_notcurrent"] = "You do not have a minicopter!",
                ["mini_priv"] = "Cannot spawn a minicopter because you're building blocked!",
                ["mini_timeleft"] = "You have {0} minutes until your cooldown ends",
                ["mini_sdistance"] = "You're trying to spawn the minicopter too far away!",
                ["mini_terrain"] = "Trying to spawn minicopter outside of terrain!",
                ["mini_mounted"] = "A player is currenty mounted on the minicopter!",
                ["mini_distance"] = "The minicopter is too far!",
                ["mini_error"] = "Unknown error occured, try again!",
                ["mini_rcon"] = "This command can only be run from RCON!",
                ["mini_canmount"] = "You are not the owner of this Minicopter or in the owner's team!"
            }, this, "en");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["mini_destroyed"] = "Ваш миникоптер был уничтожен!",
                ["mini_perm"] = "У вас нет разрешения на использование этой команды!",
                ["mini_current"] = "У вас уже есть мини-вертолет!",
                ["mini_notcurrent"] = "У вас нет мини-вертолета!",
                ["mini_priv"] = "Невозможно вызвать мини-вертолет, потому что ваше здание заблокировано!",
                ["mini_timeleft"] = "У вас есть {0} минута, пока ваше время восстановления не закончится.",
                ["mini_sdistance"] = "Вы пытаетесь породить миникоптер слишком далеко!",
                ["mini_terrain"] = "Попытка породить мини-вертолет вне местности!",
                ["mini_mounted"] = "Игрок в данный момент сидит на миникоптере или это слишком далеко!",
                ["mini_distance"] = "Мини-вертолет слишком далеко!",
                ["mini_error"] = "Произошла неизвестная ошибка, попробуйте еще раз!",
                ["mini_rcon"] = "Эту команду можно запустить только из RCON!",
                ["mini_canmount"] = "Вы не являетесь владельцем этого Minicopter или в команде владельца!"
            }, this, "ru");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["mini_destroyed"] = "Ihr minikopter wurde zerstört!",
                ["mini_perm"] = "Du habe keine keine berechtigung diesen befehl zu verwenden!",
                ["mini_current"] = "Du hast bereits einen minikopter!",
                ["mini_notcurrent"] = "Du hast keine minikopter!",
                ["mini_priv"] = "Ein minikopter kann nicht hervorgebracht, da das bauwerk ist verstopft!",
                ["mini_timeleft"] = "Du hast {0} minuten, bis ihre abklingzeit ende",
                ["mini_sdistance"] = "Du bist versuchen den minikopter zu weit weg zu spawnen!",
                ["mini_terrain"] = "Du versucht laichen einen minikopter außerhalb des geländes!",
                ["mini_mounted"] = "Ein Spieler ist gerade am Minikopter montiert oder es ist zu weit!",
                ["mini_distance"] = "Der Minikopter ist zu weit!",
                ["mini_error"] = "Unbekannter Fehler aufgetreten, versuchen Sie es erneut!",
                ["mini_rcon"] = "Dieser Befehl kann nur von RCON ausgeführt werden!",
                ["mini_canmount"] = "Sie sind nicht der Besitzer dieses Minicopters oder im Team des Besitzers!"
            }, this, "de");
        }

        #endregion
    }
}
