//If debug is defined it will add a stopwatch to the paste and copydata which can be used to profile copying and pasting.
//#define DEBUG

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using ProtoBuf;
using UnityEngine;
using Graphics = System.Drawing.Graphics;

/*
 * CREDITS
 *
 * Orange - Saving ContainerIOEntity
 * UIP88 - Turrets fix
 * bsdinis - Wire fix
 * nivex - Ownership option
 * 
 */

namespace Oxide.Plugins
{
    [Info("Copy Paste", "Reneb & MiRror & Misstake & misticos", "4.1.22")]
    [Description("Copy and paste buildings to save them or move them")]

    public class CopyPaste : RustPlugin
    {
        private int _copyLayer =
                LayerMask.GetMask("Construction", "Prevent Building", "Construction Trigger", "Trigger", "Deployed",
                    "Default", "Ragdoll"),
            _groundLayer = LayerMask.GetMask("Terrain", "Default"),
            _rayCopy = LayerMask.GetMask("Construction", "Deployed", "Tree", "Resource", "Prevent Building"),
            _rayPaste = LayerMask.GetMask("Construction", "Deployed", "Tree", "Terrain", "World", "Water",
                "Prevent Building");

        private string _copyPermission = "copypaste.copy",
            _listPermission = "copypaste.list",
            _pastePermission = "copypaste.paste",
            _pastebackPermission = "copypaste.pasteback",
            _undoPermission = "copypaste.undo",
            _serverId = "Server",
            _subDirectory = "copypaste/";

        private Dictionary<string, Stack<List<BaseEntity>>> _lastPastes =
            new Dictionary<string, Stack<List<BaseEntity>>>();

        private Dictionary<string, SignSize> _signSizes = new Dictionary<string, SignSize>
        {
            //{"spinner.wheel.deployed", new SignSize(512, 512)},
            {"sign.pictureframe.landscape", new SignSize(256, 128)},
            {"sign.pictureframe.tall", new SignSize(128, 512)},
            {"sign.pictureframe.portrait", new SignSize(128, 256)},
            {"sign.pictureframe.xxl", new SignSize(1024, 512)},
            {"sign.pictureframe.xl", new SignSize(512, 512)},
            {"sign.small.wood", new SignSize(128, 64)},
            {"sign.medium.wood", new SignSize(256, 128)},
            {"sign.large.wood", new SignSize(256, 128)},
            {"sign.huge.wood", new SignSize(512, 128)},
            {"sign.hanging.banner.large", new SignSize(64, 256)},
            {"sign.pole.banner.large", new SignSize(64, 256)},
            {"sign.post.single", new SignSize(128, 64)},
            {"sign.post.double", new SignSize(256, 256)},
            {"sign.post.town", new SignSize(256, 128)},
            {"sign.post.town.roof", new SignSize(256, 128)},
            {"sign.hanging", new SignSize(128, 256)},
            {"sign.hanging.ornate", new SignSize(256, 128)}
        };

        private List<BaseEntity.Slot> _checkSlots = new List<BaseEntity.Slot>
        {
            BaseEntity.Slot.Lock,
            BaseEntity.Slot.UpperModifier,
            BaseEntity.Slot.MiddleModifier,
            BaseEntity.Slot.LowerModifier
        };

        public enum CopyMechanics
        {
            Building,
            Proximity
        }

        private class SignSize
        {
            public int Width;
            public int Height;

            public SignSize(int width, int height)
            {
                Width = width;
                Height = height;
            }
        }

        //Config

        private ConfigData _config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Copy Options")]
            public CopyOptions Copy { get; set; }

            [JsonProperty(PropertyName = "Paste Options")]
            public PasteOptions Paste { get; set; }

            [JsonProperty(PropertyName =
                "Amount of entities to paste per batch. Use to tweak performance impact of pasting")]
            [DefaultValue(15)]
            public int PasteBatchSize = 15;

            [JsonProperty(PropertyName =
                "Amount of entities to copy per batch. Use to tweak performance impact of copying")]
            [DefaultValue(100)]
            public int CopyBatchSize = 100;

            [JsonProperty(PropertyName =
                "Amount of entities to undo per batch. Use to tweak performance impact of undoing")]
            [DefaultValue(15)]
            public int UndoBatchSize = 15;

            [JsonProperty(PropertyName = "Enable data saving feature")]
            [DefaultValue(true)]
            public bool DataSaving = true;

            public class CopyOptions
            {
                [JsonProperty(PropertyName = "Check radius from each entity (true/false)")]
                [DefaultValue(true)]
                public bool EachToEach { get; set; } = true;

                [JsonProperty(PropertyName = "Share (true/false)")]
                [DefaultValue(false)]
                public bool Share { get; set; } = false;

                [JsonProperty(PropertyName = "Tree (true/false)")]
                [DefaultValue(false)]
                public bool Tree { get; set; } = false;

                [JsonProperty(PropertyName = "Default radius to look for entities from block")]
                [DefaultValue(3.0f)]
                public float Radius { get; set; } = 3.0f;
            }

            public class PasteOptions
            {
                [JsonProperty(PropertyName = "Auth (true/false)")]
                [DefaultValue(false)]
                public bool Auth { get; set; } = false;

                [JsonProperty(PropertyName = "Deployables (true/false)")]
                [DefaultValue(true)]
                public bool Deployables { get; set; } = true;

                [JsonProperty(PropertyName = "Inventories (true/false)")]
                [DefaultValue(true)]
                public bool Inventories { get; set; } = true;

                [JsonProperty(PropertyName = "Vending Machines (true/false)")]
                [DefaultValue(true)]
                public bool VendingMachines { get; set; } = true;

                [JsonProperty(PropertyName = "Stability (true/false)")]
                [DefaultValue(true)]
                public bool Stability { get; set; } = true;

                [JsonProperty(PropertyName = "EntityOwner (true/false)")]
                [DefaultValue(true)]
                public bool EntityOwner { get; set; } = true;
            }
        }

        private void LoadVariables()
        {
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;

            _config = Config.ReadObject<ConfigData>();

            Config.WriteObject(_config, true);
        }

        protected override void LoadDefaultConfig()
        {
            var configData = new ConfigData
            {
                Copy = new ConfigData.CopyOptions(),
                Paste = new ConfigData.PasteOptions()
            };

            Config.WriteObject(configData, true);
        }

        //Hooks

        private void Init()
        {
            permission.RegisterPermission(_copyPermission, this);
            permission.RegisterPermission(_listPermission, this);
            permission.RegisterPermission(_pastePermission, this);
            permission.RegisterPermission(_pastebackPermission, this);
            permission.RegisterPermission(_undoPermission, this);

            var compiledLangs = new Dictionary<string, Dictionary<string, string>>();

            foreach (var line in _messages)
            {
                foreach (var translate in line.Value)
                {
                    if (!compiledLangs.ContainsKey(translate.Key))
                        compiledLangs[translate.Key] = new Dictionary<string, string>();

                    compiledLangs[translate.Key][line.Key] = translate.Value;
                }
            }

            foreach (var cLangs in compiledLangs)
            {
                lang.RegisterMessages(cLangs.Value, this, cLangs.Key);
            }
        }

        private void OnServerInitialized()
        {
            LoadVariables();

            Vis.colBuffer = new Collider[8192 * 16];

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
        }

        #region API

        private object TryCopyFromSteamId(ulong userId, string filename, string[] args, Action callback = null)
        {
            var player = BasePlayer.FindByID(userId);

            if (player == null)
                return Lang("NOT_FOUND_PLAYER", userId.ToString());

            RaycastHit hit;

            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 1000f, _rayCopy))
                return Lang("NO_ENTITY_RAY", player.UserIDString);

            return TryCopy(hit.point, hit.GetEntity().GetNetworkRotation().eulerAngles, filename,
                DegreeToRadian(player.GetNetworkRotation().eulerAngles.y), args, player, callback);
        }

        private object TryPasteFromSteamId(ulong userId, string filename, string[] args, Action callback = null)
        {
            var player = BasePlayer.FindByID(userId);

            if (player == null)
                return Lang("NOT_FOUND_PLAYER", player.UserIDString);

            RaycastHit hit;

            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 1000f, _rayPaste))
                return Lang("NO_ENTITY_RAY", player.UserIDString);

            return TryPaste(hit.point, filename, player, DegreeToRadian(player.GetNetworkRotation().eulerAngles.y),
                args, callback: callback);
        }

        private object TryPasteFromVector3(Vector3 pos, float rotationCorrection, string filename, string[] args,
            Action callback = null)
        {
            return TryPaste(pos, filename, null, rotationCorrection, args, callback: callback);
        }

        #endregion

        //Other methods

        private object CheckCollision(HashSet<Dictionary<string, object>> entities, Vector3 startPos, float radius)
        {
            foreach (var entityobj in entities)
            {
                if (Physics.CheckSphere((Vector3) entityobj["position"], radius, _copyLayer))
                    return Lang("BLOCKING_PASTE");
            }

            return true;
        }

        private bool CheckPlaced(string prefabname, Vector3 pos, Quaternion rot)
        {
            const float maxDiff = 0.01f;

            var ents = new List<BaseEntity>();
            Vis.Entities(pos, maxDiff, ents);

            foreach (var ent in ents)
            {
                if (ent.PrefabName != prefabname)
                    continue;

                if (Vector3.Distance(ent.transform.position, pos) > maxDiff)
                {
                    continue;
                }

                if (Vector3.Distance(ent.transform.rotation.eulerAngles, rot.eulerAngles) > maxDiff)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private object CmdPasteBack(BasePlayer player, string[] args)
        {
            var userIdString = (player == null) ? _serverId : player.UserIDString;

            if (args.Length < 1)
                return Lang("SYNTAX_PASTEBACK", userIdString);

            var success = TryPasteBack(args[0], player, args.Skip(1).ToArray());

            if (success is string)
                return (string) success;

            return true;
        }

        private object CmdUndo(string userIdString, string[] args)
        {
            var player = BasePlayer.Find(userIdString);
            if (!_lastPastes.ContainsKey(userIdString))
                return Lang("NO_PASTED_STRUCTURE", userIdString);

            var entities = new HashSet<BaseEntity>(_lastPastes[userIdString].Pop().ToList());

            UndoLoop(entities, player);

            return true;
        }

        private void UndoLoop(HashSet<BaseEntity> entities, BasePlayer player, int count = 0)
        {
            foreach (var storageContainer in entities.OfType<StorageContainer>().Where(x => !x.IsDestroyed))
            {
                storageContainer.Kill();
            }

            // Take an amount of entities from the entity list (defined in config) and kill them. Will be repeated for every tick until there are no entities left.
            entities
                .Take(_config.UndoBatchSize)
                .ToList()
                .ForEach(p =>
                {
                    entities.Remove(p);

                    // Cleanup the hotspot beloning to the node.
                    var ore = p as OreResourceEntity;
                    if (ore != null)
                    {
                        ore.CleanupBonus();
                    }

                    if (p != null && !p.IsDestroyed)
                        p.Kill();
                });

            // If it gets stuck in infinite loop break the loop.
            if (count != 0 && entities.Count != 0 && entities.Count == count)
            {
                if (player != null)
                    SendReply(player, "Undo cancelled because of infinite loop.");
                else
                    Puts("Undo cancelled because of infinite loop.");
                return;
            }

            if (entities.Count > 0)
                NextTick(() => UndoLoop(entities, player, entities.Count));
            else
            {
                if (player != null)
                    SendReply(player, Lang("UNDO_SUCCESS", player.UserIDString));
                else
                    Puts(Lang("UNDO_SUCCESS"));

                if (_lastPastes[player?.UserIDString ?? _serverId].Count == 0)
                    _lastPastes.Remove(player?.UserIDString ?? _serverId);
            }
        }

        private void Copy(Vector3 sourcePos, Vector3 sourceRot, string filename, float rotationCorrection,
            CopyMechanics copyMechanics, float range, bool saveTree, bool saveShare, bool eachToEach, BasePlayer player,
            Action callback)
        {
            var currentLayer = _copyLayer;

            if (saveTree)
                currentLayer |= LayerMask.GetMask("Tree");

            var copyData = new CopyData
            {
                FileName = filename,
                CurrentLayer = currentLayer,
                RotCor = rotationCorrection,
                Range = range,
                SaveShare = saveShare,
                SaveTree = saveTree,
                CopyMechanics = copyMechanics,
                EachToEach = eachToEach,
                SourcePos = sourcePos,
                SourceRot = sourceRot,
                Player = player,
                Callback = callback
            };

            copyData.CheckFrom.Push(sourcePos);

            NextTick(() => CopyLoop(copyData));
            ;
        }

        // Main loop for copy, will fetch all the data needed. Is called every tick untill copy is done (can't find any entities)
        private void CopyLoop(CopyData copyData)
        {
            var checkFrom = copyData.CheckFrom;
            var houseList = copyData.HouseList;
            var buildingId = copyData.BuildingId;
            var copyMechanics = copyData.CopyMechanics;
            var batchSize = checkFrom.Count < _config.CopyBatchSize ? checkFrom.Count : _config.CopyBatchSize;

            for (var i = 0; i < batchSize; i++)
            {
                if (checkFrom.Count == 0)
                    break;

                var list = Pool.GetList<BaseEntity>();
                Vis.Entities(checkFrom.Pop(), copyData.Range, list, copyData.CurrentLayer);

                foreach (var entity in list)
                {
                    if (!houseList.Add(entity))
                        continue;

                    if (copyMechanics == CopyMechanics.Building)
                    {
                        var buildingBlock = entity.GetComponentInParent<BuildingBlock>();

                        if (buildingBlock != null)
                        {
                            if (buildingId == 0)
                                buildingId = buildingBlock.buildingID;

                            if (buildingId != buildingBlock.buildingID)
                                continue;
                        }
                    }

                    if (copyData.EachToEach)
                        checkFrom.Push(entity.transform.position);
                    if (entity.GetComponent<BaseLock>() != null)
                        continue;
                    copyData.RawData.Add(EntityData(entity, entity.transform.position,
                        entity.transform.rotation.eulerAngles / 57.29578f, copyData));
                }

                copyData.BuildingId = buildingId;
            }

            if (checkFrom.Count > 0)
            {
                NextTick(() => CopyLoop(copyData));
            }
            else
            {
                var path = _subDirectory + copyData.FileName;
                var datafile = Interface.Oxide.DataFileSystem.GetDatafile(path);

                datafile.Clear();

                var sourcePos = copyData.SourcePos;

                datafile["default"] = new Dictionary<string, object>
                {
                    {
                        "position", new Dictionary<string, object>
                        {
                            {"x", sourcePos.x.ToString()},
                            {"y", sourcePos.y.ToString()},
                            {"z", sourcePos.z.ToString()}
                        }
                    },
                    {"rotationy", copyData.SourceRot.y.ToString()},
                    {"rotationdiff", copyData.RotCor.ToString()}
                };

                datafile["entities"] = copyData.RawData;
                datafile["protocol"] = new Dictionary<string, object>
                {
                    {"items", 2},
                    {"version", Version}
                };

                Interface.Oxide.DataFileSystem.SaveDatafile(path);

                SendReply(copyData.Player, Lang("COPY_SUCCESS", copyData.Player.UserIDString, copyData.FileName));

                copyData.Callback?.Invoke();

                Interface.CallHook("OnCopyFinished", copyData.RawData);
            }
        }

        private float DegreeToRadian(float angle)
        {
            return (float) (Math.PI * angle / 180.0f);
        }

        private Dictionary<string, object> EntityData(BaseEntity entity, Vector3 entPos, Vector3 entRot,
            CopyData copyData)
        {
            var normalizedPos = NormalizePosition(copyData.SourcePos, entPos, copyData.RotCor);

            entRot.y -= copyData.RotCor;

            var data = new Dictionary<string, object>
            {
                {"prefabname", entity.PrefabName},
                {"skinid", entity.skinID},
                {"flags", TryCopyFlags(entity)},
                {
                    "pos", new Dictionary<string, object>
                    {
                        {"x", normalizedPos.x.ToString()},
                        {"y", normalizedPos.y.ToString()},
                        {"z", normalizedPos.z.ToString()}
                    }
                },
                {
                    "rot", new Dictionary<string, object>
                    {
                        {"x", entRot.x.ToString()},
                        {"y", entRot.y.ToString()},
                        {"z", entRot.z.ToString()}
                    }
                },
                {"ownerid", entity.OwnerID}
            };

            TryCopySlots(entity, data, copyData.SaveShare);

            var buildingblock = entity as BuildingBlock;

            if (buildingblock != null)
            {
                data.Add("grade", buildingblock.grade);
            }

            var box = entity as StorageContainer;
            if (box?.inventory != null)
            {
                var itemlist = new List<object>();

                foreach (var item in box.inventory.itemList)
                {
                    var itemdata = new Dictionary<string, object>
                    {
                        {"condition", item.condition.ToString()},
                        {"id", item.info.itemid},
                        {"amount", item.amount},
                        {"skinid", item.skin},
                        {"position", item.position},
                        {"blueprintTarget", item.blueprintTarget}
                    };

                    if (!string.IsNullOrEmpty(item.text))
                        itemdata["text"] = item.text;

                    var heldEnt = item.GetHeldEntity();

                    if (heldEnt != null)
                    {
                        var projectiles = heldEnt.GetComponent<BaseProjectile>();

                        if (projectiles != null)
                        {
                            var magazine = projectiles.primaryMagazine;

                            if (magazine != null)
                            {
                                itemdata.Add("magazine", new Dictionary<string, object>
                                {
                                    {magazine.ammoType.itemid.ToString(), magazine.contents}
                                });
                            }
                        }
                    }

                    if (item?.contents?.itemList != null)
                    {
                        var contents = new List<object>();

                        foreach (var itemContains in item.contents.itemList)
                        {
                            contents.Add(new Dictionary<string, object>
                            {
                                {"id", itemContains.info.itemid},
                                {"amount", itemContains.amount}
                            });
                        }

                        itemdata["items"] = contents;
                    }

                    itemlist.Add(itemdata);
                }

                data.Add("items", itemlist);
            }

            var box2 = entity as ContainerIOEntity;
            if (box2 != null)
            {
                var itemlist = new List<object>();

                foreach (var item in box2.inventory.itemList)
                {
                    var itemdata = new Dictionary<string, object>
                    {
                        {"condition", item.condition.ToString()},
                        {"id", item.info.itemid},
                        {"amount", item.amount},
                        {"skinid", item.skin},
                        {"position", item.position},
                        {"blueprintTarget", item.blueprintTarget}
                    };

                    if (!string.IsNullOrEmpty(item.text))
                        itemdata["text"] = item.text;

                    var heldEnt = item.GetHeldEntity();

                    if (heldEnt != null)
                    {
                        var projectiles = heldEnt.GetComponent<BaseProjectile>();

                        if (projectiles != null)
                        {
                            var magazine = projectiles.primaryMagazine;

                            if (magazine != null)
                            {
                                itemdata.Add("magazine", new Dictionary<string, object>
                                {
                                    {magazine.ammoType.itemid.ToString(), magazine.contents}
                                });
                            }
                        }
                    }

                    if (item?.contents?.itemList != null)
                    {
                        var contents = new List<object>();

                        foreach (var itemContains in item.contents.itemList)
                        {
                            contents.Add(new Dictionary<string, object>
                            {
                                {"id", itemContains.info.itemid},
                                {"amount", itemContains.amount}
                            });
                        }

                        itemdata["items"] = contents;
                    }

                    itemlist.Add(itemdata);
                }

                data.Add("items", itemlist);
            }

            var sign = entity as Signage;
            if (sign != null)
            {
                var imageByte = FileStorage.server.Get(sign.textureID, FileStorage.Type.png, sign.net.ID);

                data.Add("sign", new Dictionary<string, object>
                {
                    {"locked", sign.IsLocked()}
                });

                if (sign.textureID > 0 && imageByte != null)
                    ((Dictionary<string, object>) data["sign"]).Add("texture", Convert.ToBase64String(imageByte));
            }

            if (copyData.SaveShare)
            {
                var sleepingBag = entity as SleepingBag;

                if (sleepingBag != null)
                {
                    data.Add("sleepingbag", new Dictionary<string, object>
                    {
                        {"niceName", sleepingBag.niceName},
                        {"deployerUserID", sleepingBag.deployerUserID},
                        {"isPublic", sleepingBag.IsPublic()}
                    });
                }

                var cupboard = entity as BuildingPrivlidge;

                if (cupboard != null)
                {
                    data.Add("cupboard", new Dictionary<string, object>
                    {
                        {"authorizedPlayers", cupboard.authorizedPlayers.Select(y => y.userid).ToList()}
                    });
                }

                var autoTurret = entity as AutoTurret;

                if (autoTurret != null)
                {
                    data.Add("autoturret", new Dictionary<string, object>
                    {
                        {"authorizedPlayers", autoTurret.authorizedPlayers.Select(p => p.userid).ToList()}
                    });
                }
            }

            var vendingMachine = entity as VendingMachine;

            if (vendingMachine != null)
            {
                var sellOrders = new List<object>();

                foreach (var vendItem in vendingMachine.sellOrders.sellOrders)
                {
                    sellOrders.Add(new Dictionary<string, object>
                    {
                        {"itemToSellID", vendItem.itemToSellID},
                        {"itemToSellAmount", vendItem.itemToSellAmount},
                        {"currencyID", vendItem.currencyID},
                        {"currencyAmountPerItem", vendItem.currencyAmountPerItem},
                        {"inStock", vendItem.inStock},
                        {"currencyIsBP", vendItem.currencyIsBP},
                        {"itemToSellIsBP", vendItem.itemToSellIsBP}
                    });
                }

                data.Add("vendingmachine", new Dictionary<string, object>
                {
                    {"shopName", vendingMachine.shopName},
                    {"isBroadcasting", vendingMachine.IsBroadcasting()},
                    {"sellOrders", sellOrders}
                });
            }

            var ioEntity = entity as IOEntity;

            if (ioEntity != null)
            {
                var ioData = new Dictionary<string, object>();
                var inputs = ioEntity.inputs.Select(input => new Dictionary<string, object>
                    {
                        {"connectedID", input.connectedTo.entityRef.uid},
                        {"connectedToSlot", input.connectedToSlot},
                        {"niceName", input.niceName},
                        {"type", (int) input.type}
                    })
                    .Cast<object>()
                    .ToList();

                ioData.Add("inputs", inputs);

                var outputs = new List<object>();
                foreach (var output in ioEntity.outputs)
                {
                    var ioConnection = new Dictionary<string, object>
                    {
                        {"connectedID", output.connectedTo.entityRef.uid},
                        {"connectedToSlot", output.connectedToSlot},
                        {"niceName", output.niceName},
                        {"type", (int) output.type},
                        {"linePoints", output.linePoints?.ToList() ?? new List<Vector3>()}
                    };

                    outputs.Add(ioConnection);
                }

                ioData.Add("outputs", outputs);
                ioData.Add("oldID", ioEntity.net.ID);
                var electricalBranch = ioEntity as ElectricalBranch;
                if (electricalBranch != null)
                {
                    ioData.Add("branchAmount", electricalBranch.branchAmount);
                }

                /*var counter = ioEntity.GetComponent<PowerCounter>();
                if (counter != null)
                {
                    ioData.Add("targetNumber", counter.GetTarget());
                }*/

                var timerSwitch = ioEntity as TimerSwitch;
                if (timerSwitch != null)
                {
                    ioData.Add("timerLength", timerSwitch.timerLength);
                }

                var rfBroadcaster = ioEntity as IRFObject;
                if (rfBroadcaster != null)
                {
                    ioData.Add("frequency", rfBroadcaster.GetFrequency());
                }

                data.Add("IOEntity", ioData);
            }

            return data;
        }

        private object FindBestHeight(HashSet<Dictionary<string, object>> entities, Vector3 startPos)
        {
            var maxHeight = 0f;

            foreach (var entity in entities)
            {
                if (((string) entity["prefabname"]).Contains("/foundation/"))
                {
                    var foundHeight = GetGround((Vector3) entity["position"]);

                    if (foundHeight != null)
                    {
                        var height = (Vector3) foundHeight;

                        if (height.y > maxHeight)
                            maxHeight = height.y;
                    }
                }
            }

            maxHeight += 1f;

            return maxHeight;
        }

        private bool FindRayEntity(Vector3 sourcePos, Vector3 sourceDir, out Vector3 point, out BaseEntity entity,
            int rayLayer)
        {
            RaycastHit hitinfo;
            entity = null;
            point = Vector3.zero;

            if (!Physics.Raycast(sourcePos, sourceDir, out hitinfo, 1000f, rayLayer))
                return false;

            entity = hitinfo.GetEntity();
            point = hitinfo.point;

            return true;
        }

        private void FixSignage(Signage sign, byte[] imageBytes)
        {
            if (!_signSizes.ContainsKey(sign.ShortPrefabName))
                return;

            var resizedImage = ImageResize(imageBytes, _signSizes[sign.ShortPrefabName].Width,
                _signSizes[sign.ShortPrefabName].Height);

            sign.textureID = FileStorage.server.Store(resizedImage, FileStorage.Type.png, sign.net.ID);
        }

        private object GetGround(Vector3 pos)
        {
            RaycastHit hitInfo;
            pos += new Vector3(0, 100, 0);

            if (Physics.Raycast(pos, Vector3.down, out hitInfo, 200, _groundLayer))
                return hitInfo.point;

            return null;
        }

        private int GetItemId(int itemId)
        {
            if (ReplaceItemId.ContainsKey(itemId))
                return ReplaceItemId[itemId];

            return itemId;
        }

        private bool HasAccess(BasePlayer player, string permName)
        {
            return player.IsAdmin || permission.UserHasPermission(player.UserIDString, permName);
        }

        private byte[] ImageResize(byte[] imageBytes, int width, int height)
        {
            Bitmap resizedImage = new Bitmap(width, height),
                sourceImage = new Bitmap(new MemoryStream(imageBytes));

            Graphics.FromImage(resizedImage).DrawImage(sourceImage, new Rectangle(0, 0, width, height),
                new Rectangle(0, 0, sourceImage.Width, sourceImage.Height), GraphicsUnit.Pixel);

            var ms = new MemoryStream();
            resizedImage.Save(ms, ImageFormat.Png);

            return ms.ToArray();
        }

        private string Lang(string key, string userId = null, params object[] args) =>
            string.Format(lang.GetMessage(key, this, userId), args);

        private Vector3 NormalizePosition(Vector3 initialPos, Vector3 currentPos, float diffRot)
        {
            var transformedPos = currentPos - initialPos;
            var newX = (transformedPos.x * (float) Math.Cos(-diffRot)) +
                       (transformedPos.z * (float) Math.Sin(-diffRot));
            var newZ = (transformedPos.z * (float) Math.Cos(-diffRot)) -
                       (transformedPos.x * (float) Math.Sin(-diffRot));

            transformedPos.x = newX;
            transformedPos.z = newZ;

            return transformedPos;
        }

        private void Paste(ICollection<Dictionary<string, object>> entities, Dictionary<string, object> protocol,
            bool ownership, Vector3 startPos, BasePlayer player, bool stability, float rotationCorrection,
            float heightAdj, bool auth, Action callback)
        {

            var ioEntities = new Dictionary<uint, Dictionary<string, object>>();
            uint buildingId = 0;

            //Settings

            var isItemReplace = !protocol.ContainsKey("items");

            var eulerRotation = new Vector3(0f, rotationCorrection * 57.2958f, 0f);
            var quaternionRotation = Quaternion.Euler(eulerRotation);

            var pasteData = new PasteData
            {
                HeightAdj = heightAdj,
                IsItemReplace = isItemReplace,
                Entities = entities,
                Player = player,
                QuaternionRotation = quaternionRotation,
                StartPos = startPos,
                Stability = stability,
                Auth = auth,
                Ownership = ownership,
                Callback = callback
            };

            NextTick(() => PasteLoop(pasteData));
        }

        private void PasteLoop(PasteData pasteData)
        {
            var entities = pasteData.Entities;
            var todo = entities.Take(_config.PasteBatchSize).ToArray();
            var player = pasteData.Player;

            foreach (var data in todo)
            {
                entities.Remove(data);
                var prefabname = (string) data["prefabname"];
                var skinid = ulong.Parse(data["skinid"].ToString());
                var pos = (Vector3) data["position"];
                var rot = (Quaternion) data["rotation"];

                var ownerId = player?.userID ?? 0;
                if (data.ContainsKey("ownerid"))
                {
                    ownerId = Convert.ToUInt64(data["ownerid"]);
                }

                if (CheckPlaced(prefabname, pos, rot))
                    continue;

                if (prefabname.Contains("pillar"))
                    continue;

                // Used to copy locks for no reason in previous versions (is included in the slots info so no need to copy locks) so just skipping them.
                if (prefabname.Contains("locks"))
                    continue;

                var entity = GameManager.server.CreateEntity(prefabname, pos, rot);

                if (entity == null)
                    continue;

                entity.transform.position = pos;
                entity.transform.rotation = rot;

                if (player != null)
                    entity.SendMessage("SetDeployedBy", player, SendMessageOptions.DontRequireReceiver);

                if (pasteData.Ownership)
                    entity.OwnerID = ownerId;

                var buildingBlock = entity as BuildingBlock;

                if (buildingBlock != null)
                {
                    buildingBlock.blockDefinition = PrefabAttribute.server.Find<Construction>(buildingBlock.prefabID);
                    buildingBlock.SetGrade((BuildingGrade.Enum) data["grade"]);
                    if (!pasteData.Stability)
                        buildingBlock.grounded = true;

                }

                var decayEntity = entity as DecayEntity;

                if (decayEntity != null)
                {
                    if (pasteData.BuildingId == 0)
                        pasteData.BuildingId = BuildingManager.server.NewBuildingID();

                    decayEntity.AttachToBuilding(pasteData.BuildingId);
                }

                var stabilityEntity = entity as StabilityEntity;

                if (stabilityEntity != null)
                {
                    if (!stabilityEntity.grounded)
                    {
                        stabilityEntity.grounded = true;
                        pasteData.StabilityEntities.Add(stabilityEntity);
                    }
                }

                entity.skinID = skinid;
                entity.Spawn();

                var baseCombat = entity as BaseCombatEntity;

                if (baseCombat != null)
                    baseCombat.SetHealth(baseCombat.MaxHealth());

                pasteData.PastedEntities.AddRange(TryPasteSlots(entity, data, pasteData));

                var box = entity as StorageContainer;
                if (box != null)
                {
                    box.inventory.Clear();

                    var items = new List<object>();

                    if (data.ContainsKey("items"))
                        items = data["items"] as List<object>;

                    foreach (var itemDef in items)
                    {
                        var item = itemDef as Dictionary<string, object>;
                        var itemid = Convert.ToInt32(item["id"]);
                        var itemamount = Convert.ToInt32(item["amount"]);
                        var itemskin = ulong.Parse(item["skinid"].ToString());
                        var itemcondition = Convert.ToSingle(item["condition"]);

                        if (pasteData.IsItemReplace)
                            itemid = GetItemId(itemid);

                        var i = ItemManager.CreateByItemID(itemid, itemamount, itemskin);

                        if (i != null)
                        {
                            i.condition = itemcondition;

                            if (item.ContainsKey("text"))
                                i.text = item["text"].ToString();

                            if (item.ContainsKey("blueprintTarget"))
                            {
                                var blueprintTarget = Convert.ToInt32(item["blueprintTarget"]);

                                if (pasteData.IsItemReplace)
                                    blueprintTarget = GetItemId(blueprintTarget);

                                i.blueprintTarget = blueprintTarget;
                            }

                            if (item.ContainsKey("magazine"))
                            {
                                var heldent = i.GetHeldEntity();

                                if (heldent != null)
                                {
                                    var projectiles = heldent.GetComponent<BaseProjectile>();

                                    if (projectiles != null)
                                    {
                                        var magazine = item["magazine"] as Dictionary<string, object>;
                                        var ammotype = int.Parse(magazine.Keys.ToArray()[0]);
                                        var ammoamount = int.Parse(magazine[ammotype.ToString()].ToString());

                                        if (pasteData.IsItemReplace)
                                            ammotype = GetItemId(ammotype);

                                        projectiles.primaryMagazine.ammoType = ItemManager.FindItemDefinition(ammotype);
                                        projectiles.primaryMagazine.contents = ammoamount;
                                    }

                                    //TODO Doesn't add water to some containers

                                    if (item.ContainsKey("items"))
                                    {
                                        var itemContainsList = item["items"] as List<object>;

                                        foreach (var itemContains in itemContainsList)
                                        {
                                            var contents = itemContains as Dictionary<string, object>;

                                            var contentsItemId = Convert.ToInt32(contents["id"]);

                                            if (pasteData.IsItemReplace)
                                                contentsItemId = GetItemId(contentsItemId);

                                            i.contents.AddItem(ItemManager.FindItemDefinition(contentsItemId),
                                                Convert.ToInt32(contents["amount"]));
                                        }
                                    }
                                }
                            }

                            var targetPos = -1;

                            if (item.ContainsKey("position"))
                                targetPos = Convert.ToInt32(item["position"]);

                            i.position = targetPos;
                            box.inventory.Insert(i);
                        }
                    }
                }

                var autoTurret = entity as AutoTurret;
                if (autoTurret != null)
                {
                    var authorizedPlayers = new List<ulong>();

                    if (data.ContainsKey("autoturret"))
                    {
                        var autoTurretData = data["autoturret"] as Dictionary<string, object>;
                        authorizedPlayers = (autoTurretData["authorizedPlayers"] as List<object>)
                            .Select(Convert.ToUInt64).ToList();
                    }

                    if (player != null && !authorizedPlayers.Contains(player.userID) && pasteData.Auth)
                        authorizedPlayers.Add(player.userID);

                    foreach (var userId in authorizedPlayers)
                    {
                        autoTurret.authorizedPlayers.Add(new PlayerNameID
                        {
                            userid = Convert.ToUInt64(userId),
                            username = "Player"
                        });
                    }

                    autoTurret.SendNetworkUpdate();
                }

                var containerIo = entity as ContainerIOEntity;
                if (containerIo != null)
                {
                    containerIo.inventory.Clear();

                    var items = new List<object>();

                    if (data.ContainsKey("items"))
                        items = data["items"] as List<object>;

                    foreach (var itemDef in items)
                    {
                        var itemJson = itemDef as Dictionary<string, object>;
                        var itemid = Convert.ToInt32(itemJson["id"]);
                        var itemamount = Convert.ToInt32(itemJson["amount"]);
                        var itemskin = ulong.Parse(itemJson["skinid"].ToString());
                        var itemcondition = Convert.ToSingle(itemJson["condition"]);

                        if (pasteData.IsItemReplace)
                            itemid = GetItemId(itemid);

                        var item = ItemManager.CreateByItemID(itemid, itemamount, itemskin);

                        if (item != null)
                        {
                            item.condition = itemcondition;

                            if (itemJson.ContainsKey("text"))
                                item.text = itemJson["text"].ToString();

                            if (itemJson.ContainsKey("blueprintTarget"))
                            {
                                var blueprintTarget = Convert.ToInt32(itemJson["blueprintTarget"]);

                                if (pasteData.IsItemReplace)
                                    blueprintTarget = GetItemId(blueprintTarget);

                                item.blueprintTarget = blueprintTarget;
                            }

                            if (itemJson.ContainsKey("magazine"))
                            {
                                var heldent = item.GetHeldEntity();

                                if (heldent != null)
                                {
                                    var projectiles = heldent.GetComponent<BaseProjectile>();

                                    if (projectiles != null)
                                    {
                                        var magazine = itemJson["magazine"] as Dictionary<string, object>;
                                        var ammotype = int.Parse(magazine.Keys.ToArray()[0]);
                                        var ammoamount = int.Parse(magazine[ammotype.ToString()].ToString());

                                        if (pasteData.IsItemReplace)
                                            ammotype = GetItemId(ammotype);

                                        projectiles.primaryMagazine.ammoType = ItemManager.FindItemDefinition(ammotype);
                                        projectiles.primaryMagazine.contents = ammoamount;
                                    }

                                    //TODO Doesn't add water to some containers

                                    if (itemJson.ContainsKey("items"))
                                    {
                                        var itemContainsList = itemJson["items"] as List<object>;

                                        foreach (var itemContains in itemContainsList)
                                        {
                                            var contents = itemContains as Dictionary<string, object>;

                                            var contentsItemId = Convert.ToInt32(contents["id"]);

                                            if (pasteData.IsItemReplace)
                                                contentsItemId = GetItemId(contentsItemId);

                                            item.contents.AddItem(ItemManager.FindItemDefinition(contentsItemId),
                                                Convert.ToInt32(contents["amount"]));
                                        }
                                    }
                                }
                            }

                            var targetPos = -1;
                            if (itemJson.ContainsKey("position"))
                                targetPos = Convert.ToInt32(itemJson["position"]);

                            item.position = targetPos;
                            containerIo.inventory.Insert(item);
                        }
                    }

                    if (autoTurret != null)
                    {
                        autoTurret.Invoke(autoTurret.UpdateAttachedWeapon, 0.5f);
                    }

                    containerIo.SendNetworkUpdate();
                }

                var sign = entity as Signage;
                if (sign != null && data.ContainsKey("sign"))
                {
                    var signData = data["sign"] as Dictionary<string, object>;

                    if (signData.ContainsKey("texture"))
                    {
                        var imageBytes = Convert.FromBase64String(signData["texture"].ToString());

                        FixSignage(sign, imageBytes);
                    }

                    if (Convert.ToBoolean(signData["locked"]))
                        sign.SetFlag(BaseEntity.Flags.Locked, true);

                    sign.SendNetworkUpdate();
                }

                var sleepingBag = entity as SleepingBag;
                if (sleepingBag != null && data.ContainsKey("sleepingbag"))
                {
                    var bagData = data["sleepingbag"] as Dictionary<string, object>;

                    sleepingBag.niceName = bagData["niceName"].ToString();
                    sleepingBag.deployerUserID = ulong.Parse(bagData["deployerUserID"].ToString());
                    sleepingBag.SetPublic(Convert.ToBoolean(bagData["isPublic"]));
                }

                var cupboard = entity as BuildingPrivlidge;
                if (cupboard != null)
                {
                    var authorizedPlayers = new List<ulong>();

                    if (data.ContainsKey("cupboard"))
                    {
                        var cupboardData = data["cupboard"] as Dictionary<string, object>;
                        authorizedPlayers = (cupboardData["authorizedPlayers"] as List<object>).Select(Convert.ToUInt64)
                            .ToList();
                    }

                    if (player != null && !authorizedPlayers.Contains(player.userID) && pasteData.Auth)
                        authorizedPlayers.Add(player.userID);

                    foreach (var userId in authorizedPlayers)
                    {
                        cupboard.authorizedPlayers.Add(new PlayerNameID
                        {
                            userid = Convert.ToUInt64(userId),
                            username = "Player"
                        });
                    }

                    cupboard.SendNetworkUpdate();
                }

                var vendingMachine = entity as VendingMachine;
                if (vendingMachine != null && data.ContainsKey("vendingmachine"))
                {
                    var vendingData = data["vendingmachine"] as Dictionary<string, object>;

                    vendingMachine.shopName = vendingData["shopName"].ToString();
                    vendingMachine.SetFlag(BaseEntity.Flags.Reserved4,
                        Convert.ToBoolean(vendingData["isBroadcasting"]));

                    var sellOrders = vendingData["sellOrders"] as List<object>;

                    foreach (var orderPreInfo in sellOrders)
                    {
                        var orderInfo = orderPreInfo as Dictionary<string, object>;

                        if (!orderInfo.ContainsKey("inStock"))
                        {
                            orderInfo["inStock"] = 0;
                            orderInfo["currencyIsBP"] = false;
                            orderInfo["itemToSellIsBP"] = false;
                        }

                        int itemToSellId = Convert.ToInt32(orderInfo["itemToSellID"]),
                            currencyId = Convert.ToInt32(orderInfo["currencyID"]);

                        if (pasteData.IsItemReplace)
                        {
                            itemToSellId = GetItemId(itemToSellId);
                            currencyId = GetItemId(currencyId);
                        }

                        vendingMachine.sellOrders.sellOrders.Add(new ProtoBuf.VendingMachine.SellOrder
                        {
                            ShouldPool = false,
                            itemToSellID = itemToSellId,
                            itemToSellAmount = Convert.ToInt32(orderInfo["itemToSellAmount"]),
                            currencyID = currencyId,
                            currencyAmountPerItem = Convert.ToInt32(orderInfo["currencyAmountPerItem"]),
                            inStock = Convert.ToInt32(orderInfo["inStock"]),
                            currencyIsBP = Convert.ToBoolean(orderInfo["currencyIsBP"]),
                            itemToSellIsBP = Convert.ToBoolean(orderInfo["itemToSellIsBP"])
                        });
                    }

                    vendingMachine.FullUpdate();
                }

                var ioEntity = entity as IOEntity;

                if (ioEntity != null)
                {
                    var ioData = new Dictionary<string, object>();

                    if (data.ContainsKey("IOEntity"))
                    {
                        ioData = data["IOEntity"] as Dictionary<string, object> ?? new Dictionary<string, object>();
                    }

                    ioData.Add("entity", ioEntity);
                    ioData.Add("newId", ioEntity.net.ID);

                    object oldIdObject = 0;
                    if (ioData.TryGetValue("oldID", out oldIdObject))
                    {
                        var oldId = Convert.ToUInt32(oldIdObject);
                        pasteData.IoEntities.Add(oldId, ioData);
                    }
                }

                var flagsData = new Dictionary<string, object>();

                if (data.ContainsKey("flags"))
                    flagsData = data["flags"] as Dictionary<string, object>;

                var flags = new Dictionary<BaseEntity.Flags, bool>();

                foreach (var flagData in flagsData)
                {
                    BaseEntity.Flags baseFlag;
                    if (Enum.TryParse(flagData.Key, out baseFlag))
                        flags.Add(baseFlag, Convert.ToBoolean(flagData.Value));
                }

                foreach (var flag in flags)
                {
                    entity.SetFlag(flag.Key, flag.Value);
                }

                pasteData.PastedEntities.Add(entity);
            }

            if (entities.Count > 0)
                NextTick(() => PasteLoop(pasteData));
            else
            {
                foreach (var ioData in pasteData.IoEntities.Values.ToArray())
                {
                    if (!ioData.ContainsKey("entity"))
                        continue;


                    var ioEntity = ioData["entity"] as IOEntity;

                    List<object> inputs = null;
                    if (ioData.ContainsKey("inputs"))
                        inputs = ioData["inputs"] as List<object>;

                    var electricalBranch = ioEntity as ElectricalBranch;
                    if (electricalBranch != null && ioData.ContainsKey("branchAmount"))
                    {
                        electricalBranch.branchAmount = Convert.ToInt32(ioData["branchAmount"]);
                    }

                    // Realized counter.targetCounterNumber is private, leaving it in in case signature changes.
                    /*var counter = ioEntity.GetComponentInParent<PowerCounter>();
                    if (counter != null)
                    {
                        counter.targetCounterNumber = Convert.ToInt32(ioData["targetNumber"]);
                    }*/

                    var timer = ioEntity as TimerSwitch;
                    if (timer != null && ioData.ContainsKey("timerLength"))
                    {
                        timer.timerLength = Convert.ToInt32(ioData["timerLength"]);
                    }

                    var rfBroadcaster = ioEntity as RFBroadcaster;
                    if (rfBroadcaster != null && ioData.ContainsKey("frequency"))
                    {
                        rfBroadcaster.frequency = Convert.ToInt32(ioData["frequency"]);
                    }

                    var rfReceiver = ioEntity as RFReceiver;
                    if (rfReceiver != null && ioData.ContainsKey("frequency"))
                    {
                        rfReceiver.frequency = Convert.ToInt32(ioData["frequency"]);
                    }

                    var doorManipulator = ioEntity as CustomDoorManipulator;
                    if (doorManipulator != null)
                    {
                        var door = doorManipulator.FindDoor();
                        doorManipulator.SetTargetDoor(door);
                    }

                    if (inputs != null && inputs.Count > 0)
                    {
                        for (var index = 0; index < inputs.Count; index++)
                        {
                            var input = inputs[index] as Dictionary<string, object>;
                            object oldIdObject;
                            if (!input.TryGetValue("connectedID", out oldIdObject))
                                continue;

                            var oldId = Convert.ToUInt32(oldIdObject);

                            if (oldId != 0 && pasteData.IoEntities.ContainsKey(oldId))
                            {
                                if (ioEntity.inputs[index] == null)
                                    ioEntity.inputs[index] = new IOEntity.IOSlot();

                                var ioConnection = pasteData.IoEntities[oldId];

                                object temp;

                                if (ioConnection.ContainsKey("newId"))
                                {
                                    ioEntity.inputs[index].connectedTo.entityRef.uid =
                                        Convert.ToUInt32(ioConnection["newId"]);
                                }
                            }
                        }
                    }

                    List<object> outputs = null;
                    if (ioData.ContainsKey("outputs"))
                        outputs = ioData["outputs"] as List<object>;

                    if (outputs != null && outputs.Count > 0)
                    {
                        for (var index = 0; index < outputs.Count; index++)
                        {
                            var output = outputs[index] as Dictionary<string, object>;
                            var oldId = Convert.ToUInt32(output["connectedID"]);

                            if (oldId != 0 && pasteData.IoEntities.ContainsKey(oldId))
                            {
                                if (ioEntity.outputs[index] == null)
                                    ioEntity.outputs[index] = new IOEntity.IOSlot();

                                var ioConnection = pasteData.IoEntities[oldId];

                                if (ioConnection.ContainsKey("newId"))
                                {
                                    var ioEntity2 = ioConnection["entity"] as IOEntity;
                                    var connectedToSlot = Convert.ToInt32(output["connectedToSlot"]);
                                    var ioOutput = ioEntity.outputs[index];

                                    ioOutput.connectedTo = new IOEntity.IORef();
                                    ioOutput.connectedTo.Set(ioEntity2);
                                    ioOutput.connectedToSlot = connectedToSlot;
                                    ioOutput.connectedTo.Init();

                                    ioEntity2.inputs[connectedToSlot].connectedTo = new IOEntity.IORef();
                                    ioEntity2.inputs[connectedToSlot].connectedTo.Set(ioEntity);
                                    ioEntity2.inputs[connectedToSlot].connectedToSlot = index;
                                    ioEntity2.inputs[connectedToSlot].connectedTo.Init();

                                    ioOutput.niceName = output["niceName"] as string;

                                    ioOutput.type = (IOEntity.IOType) Convert.ToInt32(output["type"]);
                                }

                                if (output.ContainsKey("linePoints"))
                                {
                                    var linePoints = output["linePoints"] as List<object>;
                                    if (linePoints != null)
                                    {
                                        var lineList = new List<Vector3>();
                                        foreach (var point in linePoints)
                                        {
                                            var linePoint = point as Dictionary<string, object>;
                                            lineList.Add(new Vector3(
                                                Convert.ToSingle(linePoint["x"]),
                                                Convert.ToSingle(linePoint["y"]),
                                                Convert.ToSingle(linePoint["z"])));
                                        }

                                        ioEntity.outputs[index].linePoints = lineList.ToArray();
                                    }
                                }
                            }
                        }
                    }

                    ioEntity.MarkDirtyForceUpdateOutputs();
                    ioEntity.SendNetworkUpdate();
                }

                foreach (var entity in pasteData.StabilityEntities)
                {
                    entity.grounded = false;
                    entity.InitializeSupports();
                    entity.UpdateStability();
                }

                if (player != null)
                {
                    SendReply(player, Lang("PASTE_SUCCESS", player.UserIDString));
#if DEBUG
                    SendReply(player, $"Stopwatch took: {pasteData.Sw.Elapsed.TotalMilliseconds} ms");
#endif
                }
                else
                {
                    Puts(Lang("PASTE_SUCCESS"));
                }

                if (!_lastPastes.ContainsKey(player?.UserIDString ?? _serverId))
                    _lastPastes[player?.UserIDString ?? _serverId] = new Stack<List<BaseEntity>>();

                _lastPastes[player?.UserIDString ?? _serverId].Push(pasteData.PastedEntities);

                pasteData.Callback?.Invoke();

                Interface.CallHook("OnPasteFinished", pasteData.PastedEntities);
            }
        }

        private HashSet<Dictionary<string, object>> PreLoadData(List<object> entities, Vector3 startPos,
            float rotationCorrection, bool deployables, bool inventories, bool auth, bool vending)
        {
            var eulerRotation = new Vector3(0f, rotationCorrection, 0f);
            var quaternionRotation = Quaternion.EulerRotation(eulerRotation);
            var preloaddata = new HashSet<Dictionary<string, object>>();

            foreach (var entity in entities)
            {
                var data = entity as Dictionary<string, object>;

                if (!deployables && !data.ContainsKey("grade"))
                    continue;

                var pos = (Dictionary<string, object>) data["pos"];
                var rot = (Dictionary<string, object>) data["rot"];

                data.Add("position",
                    quaternionRotation * (new Vector3(Convert.ToSingle(pos["x"]), Convert.ToSingle(pos["y"]),
                        Convert.ToSingle(pos["z"]))) + startPos);
                data.Add("rotation",
                    Quaternion.EulerRotation(eulerRotation + new Vector3(Convert.ToSingle(rot["x"]),
                        Convert.ToSingle(rot["y"]), Convert.ToSingle(rot["z"]))));

                if (!inventories && data.ContainsKey("items"))
                    data["items"] = new List<object>();

                if (!vending && data["prefabname"].ToString().Contains("vendingmachine"))
                    data.Remove("vendingmachine");

                preloaddata.Add(data);
            }

            return preloaddata;
        }

        private object TryCopy(Vector3 sourcePos, Vector3 sourceRot, string filename, float rotationCorrection,
            string[] args, BasePlayer player, Action callback)
        {
            bool saveShare = _config.Copy.Share, saveTree = _config.Copy.Tree, eachToEach = _config.Copy.EachToEach;
            var copyMechanics = CopyMechanics.Proximity;
            var radius = _config.Copy.Radius;

            for (var i = 0;; i = i + 2)
            {
                if (i >= args.Length)
                    break;

                var valueIndex = i + 1;

                if (valueIndex >= args.Length)
                    return Lang("SYNTAX_COPY");

                var param = args[i].ToLower();

                switch (param)
                {
                    case "e":
                    case "each":
                        if (!bool.TryParse(args[valueIndex], out eachToEach))
                            return Lang("SYNTAX_BOOL", null, param);

                        break;

                    case "m":
                    case "method":
                        switch (args[valueIndex].ToLower())
                        {
                            case "b":
                            case "building":
                                copyMechanics = CopyMechanics.Building;
                                break;

                            case "p":
                            case "proximity":
                                copyMechanics = CopyMechanics.Proximity;
                                break;
                        }

                        break;

                    case "r":
                    case "radius":
                        if (!float.TryParse(args[valueIndex], out radius))
                            return Lang("SYNTAX_RADIUS");

                        break;

                    case "s":
                    case "share":
                        if (!bool.TryParse(args[valueIndex], out saveShare))
                            return Lang("SYNTAX_BOOL", null, param);

                        break;

                    case "t":
                    case "tree":
                        if (!bool.TryParse(args[valueIndex], out saveTree))
                            return Lang("SYNTAX_BOOL", null, param);

                        break;

                    default:
                        return Lang("SYNTAX_COPY");
                }
            }

            Copy(sourcePos, sourceRot, filename, rotationCorrection, copyMechanics, radius, saveTree, saveShare,
                eachToEach, player, callback);

            return true;
        }

        private void TryCopySlots(BaseEntity ent, IDictionary<string, object> housedata, bool saveShare)
        {
            foreach (var slot in _checkSlots)
            {
                if (!ent.HasSlot(slot))
                    continue;

                var slotEntity = ent.GetSlot(slot);

                if (slotEntity == null)
                    continue;

                var codedata = new Dictionary<string, object>
                {
                    {"prefabname", slotEntity.PrefabName},
                    {"flags", TryCopyFlags(ent)}
                };

                if (slotEntity.GetComponent<CodeLock>())
                {
                    var codeLock = slotEntity.GetComponent<CodeLock>();

                    codedata.Add("code", codeLock.code);

                    if (saveShare)
                        codedata.Add("whitelistPlayers", codeLock.whitelistPlayers);

                    if (codeLock.guestCode != null && codeLock.guestCode.Length == 4)
                    {
                        codedata.Add("guestCode", codeLock.guestCode);

                        if (saveShare)
                            codedata.Add("guestPlayers", codeLock.guestPlayers);
                    }
                }
                else if (slotEntity.GetComponent<KeyLock>())
                {
                    var keyLock = slotEntity.GetComponent<KeyLock>();
                    var code = keyLock.keyCode;

                    if (keyLock.firstKeyCreated)
                        code |= 0x80;

                    codedata.Add("ownerId", keyLock.OwnerID.ToString());
                    codedata.Add("code", code.ToString());
                }

                var slotName = slot.ToString().ToLower();

                housedata.Add(slotName, codedata);
            }
        }

        private Dictionary<string, object> TryCopyFlags(BaseEntity entity)
        {
            var flags = new Dictionary<string, object>();

            foreach (BaseEntity.Flags flag in Enum.GetValues(typeof(BaseEntity.Flags)))
            {
                if (!_config.DataSaving || entity.HasFlag(flag))
                    flags.Add(flag.ToString(), entity.HasFlag(flag));
            }

            return flags;
        }

        private object TryPaste(Vector3 startPos, string filename, BasePlayer player, float rotationCorrection,
            string[] args, bool autoHeight = true, Action callback = null)
        {
            var userId = player?.UserIDString;

            var path = _subDirectory + filename;

            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(path))
                return Lang("FILE_NOT_EXISTS", userId);

            var data = Interface.Oxide.DataFileSystem.GetDatafile(path);

            if (data["default"] == null || data["entities"] == null)
                return Lang("FILE_BROKEN", userId);

            float heightAdj = 0f, blockCollision = 0f;
            bool auth = _config.Paste.Auth,
                inventories = _config.Paste.Inventories,
                deployables = _config.Paste.Deployables,
                vending = _config.Paste.VendingMachines,
                stability = _config.Paste.Stability,
                ownership = _config.Paste.EntityOwner;

            for (var i = 0;; i = i + 2)
            {
                if (i >= args.Length)
                    break;

                var valueIndex = i + 1;

                if (valueIndex >= args.Length)
                    return Lang("SYNTAX_PASTE_OR_PASTEBACK", userId);

                var param = args[i].ToLower();

                switch (param)
                {
                    case "a":
                    case "auth":
                        if (!bool.TryParse(args[valueIndex], out auth))
                            return Lang("SYNTAX_BOOL", userId, param);

                        break;

                    case "b":
                    case "blockcollision":
                        if (!float.TryParse(args[valueIndex], out blockCollision))
                            return Lang("SYNTAX_BLOCKCOLLISION", userId);

                        break;

                    case "d":
                    case "deployables":
                        if (!bool.TryParse(args[valueIndex], out deployables))
                            return Lang("SYNTAX_BOOL", userId, param);

                        break;

                    case "h":
                    case "height":
                        if (!float.TryParse(args[valueIndex], out heightAdj))
                            return Lang("SYNTAX_HEIGHT", userId);

                        break;

                    case "i":
                    case "inventories":
                        if (!bool.TryParse(args[valueIndex], out inventories))
                            return Lang("SYNTAX_BOOL", userId, param);

                        break;

                    case "s":
                    case "stability":
                        if (!bool.TryParse(args[valueIndex], out stability))
                            return Lang("SYNTAX_BOOL", userId, param);

                        break;

                    case "v":
                    case "vending":
                        if (!bool.TryParse(args[valueIndex], out vending))
                            return Lang("SYNTAX_BOOL", userId, param);

                        break;

                    case "o":
                    case "entityowner":
                        if (!bool.TryParse(args[valueIndex], out ownership))
                            return Lang("SYNTAX_BOOL", userId, param);

                        break;

                    case "autoheight":
                        if (!bool.TryParse(args[valueIndex], out autoHeight))
                            return Lang("SYNTAX_BOOL", userId, param);

                        break;

                    default:
                        return Lang("SYNTAX_PASTE_OR_PASTEBACK", userId);
                }
            }

            startPos.y += heightAdj;

            var preloadData = PreLoadData(data["entities"] as List<object>, startPos, rotationCorrection, deployables,
                inventories, auth, vending);

            if (autoHeight)
            {
                var bestHeight = FindBestHeight(preloadData, startPos);

                if (bestHeight is string)
                    return bestHeight;

                heightAdj += ((float) bestHeight - startPos.y);

                foreach (var entity in preloadData)
                {
                    var pos = ((Vector3) entity["position"]);
                    pos.y += heightAdj;

                    entity["position"] = pos;
                }
            }

            if (blockCollision > 0f)
            {
                var collision = CheckCollision(preloadData, startPos, blockCollision);

                if (collision is string)
                    return collision;
            }

            var protocol = new Dictionary<string, object>();

            if (data["protocol"] != null)
                protocol = data["protocol"] as Dictionary<string, object>;

            Paste(preloadData, protocol, ownership, startPos, player, stability, rotationCorrection,
                autoHeight ? heightAdj : 0, auth, callback);
            return true;
        }

        private List<BaseEntity> TryPasteSlots(BaseEntity ent, Dictionary<string, object> structure,
            PasteData pasteData)
        {
            var entitySlots = new List<BaseEntity>();

            foreach (var slot in _checkSlots)
            {
                var slotName = slot.ToString().ToLower();

                if (!ent.HasSlot(slot) || !structure.ContainsKey(slotName))
                    continue;

                var slotData = structure[slotName] as Dictionary<string, object>;
                var slotEntity = GameManager.server.CreateEntity((string) slotData["prefabname"], Vector3.zero);

                if (slotEntity == null)
                    continue;

                slotEntity.gameObject.Identity();
                slotEntity.SetParent(ent, slotName);
                slotEntity.OnDeployed(ent);
                slotEntity.Spawn();

                ent.SetSlot(slot, slotEntity);

                entitySlots.Add(slotEntity);

                if (slotName != "lock" || !slotData.ContainsKey("code"))
                    continue;

                if (slotEntity.GetComponent<CodeLock>())
                {
                    var code = (string) slotData["code"];

                    if (!string.IsNullOrEmpty(code))
                    {
                        var codeLock = slotEntity.GetComponent<CodeLock>();
                        codeLock.code = code;
                        codeLock.hasCode = true;

                        if (pasteData.Auth && pasteData.Player != null)
                            codeLock.whitelistPlayers.Add(pasteData.Player.userID);

                        if (slotData.ContainsKey("whitelistPlayers"))
                        {
                            foreach (var userId in slotData["whitelistPlayers"] as List<object>)
                            {
                                codeLock.whitelistPlayers.Add(Convert.ToUInt64(userId));
                            }
                        }

                        if (slotData.ContainsKey("guestCode"))
                        {
                            var guestCode = (string) slotData["guestCode"];

                            codeLock.guestCode = guestCode;
                            codeLock.hasGuestCode = true;

                            if (slotData.ContainsKey("guestPlayers"))
                            {
                                foreach (var userId in slotData["guestPlayers"] as List<object>)
                                {
                                    codeLock.guestPlayers.Add(Convert.ToUInt64(userId));
                                }
                            }
                        }

                        codeLock.SetFlag(BaseEntity.Flags.Locked, true);
                    }
                }
                else if (slotEntity.GetComponent<KeyLock>())
                {
                    var code = Convert.ToInt32(slotData["code"]);
                    var keyLock = slotEntity.GetComponent<KeyLock>();

                    if ((code & 0x80) != 0)
                    {
                        keyLock.keyCode = (code & 0x7F);
                        keyLock.firstKeyCreated = true;
                        keyLock.SetFlag(BaseEntity.Flags.Locked, true);
                    }

                    if (pasteData.Ownership && slotData.ContainsKey("ownerId"))
                    {
                        keyLock.OwnerID = Convert.ToUInt64(slotData["ownerId"]);
                    }
                }
            }

            return entitySlots;
        }

        private object TryPasteBack(string filename, BasePlayer player, string[] args)
        {
            var path = _subDirectory + filename;

            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(path))
                return Lang("FILE_NOT_EXISTS", player?.UserIDString);

            var data = Interface.Oxide.DataFileSystem.GetDatafile(path);

            if (data["default"] == null || data["entities"] == null)
                return Lang("FILE_BROKEN", player?.UserIDString);

            var defaultdata = data["default"] as Dictionary<string, object>;
            var pos = defaultdata["position"] as Dictionary<string, object>;
            var rotationCorrection = Convert.ToSingle(defaultdata["rotationdiff"]);
            var startPos = new Vector3(Convert.ToSingle(pos["x"]), Convert.ToSingle(pos["y"]),
                Convert.ToSingle(pos["z"]));

            return TryPaste(startPos, filename, player, rotationCorrection, args, autoHeight: false);
        }

        //Сhat commands

        [ChatCommand("copy")]
        private void CmdChatCopy(BasePlayer player, string command, string[] args)
        {
            if (!HasAccess(player, _copyPermission))
            {
                SendReply(player, Lang("NO_ACCESS", player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, Lang("SYNTAX_COPY", player.UserIDString));
                return;
            }

            var savename = args[0];
            var success = TryCopyFromSteamId(player.userID, savename, args.Skip(1).ToArray());

            if (success is string)
            {
                SendReply(player, (string) success);
            }
        }

        [ChatCommand("paste")]
        private void CmdChatPaste(BasePlayer player, string command, string[] args)
        {
            if (!HasAccess(player, _pastePermission))
            {
                SendReply(player, Lang("NO_ACCESS", player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, Lang("SYNTAX_PASTE_OR_PASTEBACK", player.UserIDString));
                return;
            }

            var success = TryPasteFromSteamId(player.userID, args[0], args.Skip(1).ToArray());

            if (success is string)
            {
                SendReply(player, (string) success);
            }
        }

        [ChatCommand("copylist")]
        private void CmdChatList(BasePlayer player, string command, string[] args)
        {
            if (!HasAccess(player, _listPermission))
            {
                SendReply(player, Lang("NO_ACCESS", player.UserIDString));
                return;
            }

            var files = Interface.Oxide.DataFileSystem.GetFiles(_subDirectory);

            var fileList = new List<string>();

            foreach (var file in files)
            {
                var strFileParts = file.Split('/');
                var justfile = strFileParts[strFileParts.Length - 1].Replace(".json", "");
                fileList.Add(justfile);
            }

            SendReply(player, Lang("AVAILABLE_STRUCTURES", player.UserIDString));
            SendReply(player, string.Join(", ", fileList.ToArray()));
        }

        [ChatCommand("pasteback")]
        private void CmdChatPasteBack(BasePlayer player, string command, string[] args)
        {
            if (!HasAccess(player, _pastebackPermission))
            {
                SendReply(player, Lang("NO_ACCESS", player.UserIDString));
                return;
            }

            var result = CmdPasteBack(player, args);

            if (result is string)
                SendReply(player, (string) result);
        }

        [ChatCommand("undo")]
        private void CmdChatUndo(BasePlayer player, string command, string[] args)
        {
            if (!HasAccess(player, _undoPermission))
            {
                SendReply(player, Lang("NO_ACCESS", player.UserIDString));
                return;
            }

            CmdUndo(player.UserIDString, args);
        }

        //Console commands [From Server]

        [ConsoleCommand("pasteback")]
        private void CmdConsolePasteBack(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
                return;

            var result = CmdPasteBack(arg.Player(), arg.Args);

            if (result is string)
                SendReply(arg, (string) result);
        }

        [ConsoleCommand("undo")]
        private void CmdConsoleUndo(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
                return;

            var player = arg.Player();

            CmdUndo(player == null ? _serverId : player.UserIDString, arg.Args);
        }

        //Replace between old ItemID to new ItemID

        private static readonly Dictionary<int, int> ReplaceItemId = new Dictionary<int, int>
        {
            {-1461508848, 1545779598},
            {2115555558, 588596902},
            {-533875561, 785728077},
            {1621541165, 51984655},
            {-422893115, -1691396643},
            {815896488, -1211166256},
            {805088543, -1321651331},
            {449771810, 605467368},
            {1152393492, 1712070256},
            {1578894260, -742865266},
            {1436532208, 1638322904},
            {542276424, -1841918730},
            {1594947829, -17123659},
            {-1035059994, -1685290200},
            {1818890814, -1036635990},
            {1819281075, -727717969},
            {1685058759, -1432674913},
            {93029210, 1548091822},
            {-1565095136, 352130972},
            {-1775362679, 215754713},
            {-1775249157, 14241751},
            {-1280058093, -1023065463},
            {-420273765, -1234735557},
            {563023711, -2139580305},
            {790921853, -262590403},
            {-337261910, -2072273936},
            {498312426, -1950721390},
            {504904386, 1655650836},
            {-1221200300, -559599960},
            {510887968, 15388698},
            {-814689390, 866889860},
            {1024486167, 1382263453},
            {2021568998, 609049394},
            {97329, 1099314009},
            {1046072789, -582782051},
            {97409, -1273339005},
            {-1480119738, -1262185308},
            {1611480185, 1931713481},
            {-1386464949, 1553078977},
            {93832698, 1776460938},
            {-1063412582, -586342290},
            {-1887162396, -996920608},
            {-55660037, 1588298435},
            {919780768, 1711033574},
            {-365801095, 1719978075},
            {68998734, 613961768},
            {-853695669, 1443579727},
            {271534758, 833533164},
            {-770311783, -180129657},
            {-1192532973, 1424075905},
            {-307490664, 1525520776},
            {707427396, 602741290},
            {707432758, -761829530},
            {-2079677721, 1783512007},
            {-1342405573, -1316706473},
            {-139769801, 1946219319},
            {-1043746011, -700591459},
            {2080339268, 1655979682},
            {-171664558, -1941646328},
            {1050986417, -1557377697},
            {-1693683664, 1789825282},
            {523409530, 1121925526},
            {1300054961, 634478325},
            {-2095387015, 1142993169},
            {1428021640, 1104520648},
            {94623429, 1534542921},
            {1436001773, -1938052175},
            {1711323399, 1973684065},
            {1734319168, -1848736516},
            {-1658459025, -1440987069},
            {-726947205, -751151717},
            {-341443994, 363467698},
            {1540879296, 2009734114},
            {94756378, -858312878},
            {3059095, 204391461},
            {3059624, 1367190888},
            {2045107609, -778875547},
            {583366917, 998894949},
            {2123300234, 1965232394},
            {1983936587, -321733511},
            {1257201758, -97956382},
            {-1144743963, 296519935},
            {-1144542967, -113413047},
            {-1144334585, -2022172587},
            {1066729526, -1101924344},
            {-1598790097, 1390353317},
            {-933236257, 1221063409},
            {-1575287163, -1336109173},
            {-2104481870, -2067472972},
            {-1571725662, 1353298668},
            {1456441506, 1729120840},
            {1200628767, -1112793865},
            {-778796102, 1409529282},
            {1526866730, 674734128},
            {1925723260, -1519126340},
            {1891056868, 1401987718},
            {1295154089, -1878475007},
            {498591726, 1248356124},
            {1755466030, -592016202},
            {726730162, 798638114},
            {-1034048911, -1018587433},
            {252529905, 274502203},
            {471582113, -1065444793},
            {-1138648591, 16333305},
            {305916740, 649305914},
            {305916742, 649305916},
            {305916744, 649305918},
            {1908328648, -1535621066},
            {-2078972355, 1668129151},
            {-533484654, 989925924},
            {1571660245, 1569882109},
            {1045869440, -1215753368},
            {1985408483, 528668503},
            {97513422, 304481038},
            {1496470781, -196667575},
            {1229879204, 952603248},
            {-1722829188, 936496778},
            {1849912854, 1948067030},
            {-1266285051, 1413014235},
            {-1749787215, -1000573653},
            {28178745, -946369541},
            {-505639592, -1999722522},
            {1598149413, -1992717673},
            {-1779401418, -691113464},
            {-57285700, -335089230},
            {98228420, 479143914},
            {1422845239, 999690781},
            {277631078, -1819763926},
            {115739308, 1366282552},
            {-522149009, -690276911},
            {3175989, -1899491405},
            {718197703, -746030907},
            {384204160, 1840822026},
            {-1308622549, 143803535},
            {-217113639, -2124352573},
            {-1580059655, -265876753},
            {-1832205789, 1070894649},
            {305916741, 649305917},
            {936777834, 3222790},
            {-1224598842, 200773292},
            {-1976561211, -1506397857},
            {-1406876421, 1675639563},
            {-1397343301, -23994173},
            {1260209393, 850280505},
            {-1035315940, 1877339384},
            {-1381682752, 1714496074},
            {696727039, -1022661119},
            {-2128719593, -803263829},
            {-1178289187, -1903165497},
            {1351172108, 1181207482},
            {-450738836, -1539025626},
            {-966287254, -324675402},
            {340009023, 671063303},
            {124310981, -1478212975},
            {1501403549, -2094954543},
            {698310895, -1252059217},
            {523855532, 1266491000},
            {2045246801, -886280491},
            {583506109, -237809779},
            {-148163128, 794356786},
            {-132588262, -1773144852},
            {-1666761111, 196700171},
            {-465236267, 442289265},
            {-1211618504, 1751045826},
            {2133577942, -1982036270},
            {-1014825244, -682687162},
            {-991829475, 1536610005},
            {-642008142, -1709878924},
            {661790782, 1272768630},
            {-1440143841, -1780802565},
            {569119686, 1746956556},
            {1404466285, -1102429027},
            {-1616887133, -48090175},
            {-1167640370, -1163532624},
            {-1284735799, 1242482355},
            {-1278649848, -1824943010},
            {776005741, 1814288539},
            {108061910, -316250604},
            {255101535, -1663759755},
            {-51678842, 1658229558},
            {-789202811, 254522515},
            {516382256, -132516482},
            {50834473, 1381010055},
            {-975723312, 1159991980},
            {1908195100, -850982208},
            {-1097452776, -110921842},
            {146685185, -1469578201},
            {-1716193401, -1812555177},
            {193190034, -2069578888},
            {371156815, -852563019},
            {3343606, -1966748496},
            {825308669, -1137865085},
            {830965940, -586784898},
            {1662628660, -163828118},
            {1662628661, -163828117},
            {1662628662, -163828112},
            {-1832205788, 1070894648},
            {-1832205786, 1070894646},
            {1625090418, 181590376},
            {-1269800768, -874975042},
            {429648208, -1190096326},
            {-1832205787, 1070894647},
            {-1832205785, 1070894645},
            {107868, 696029452},
            {997973965, -2012470695},
            {-46188931, -702051347},
            {-46848560, -194953424},
            {-2066726403, -989755543},
            {-2043730634, 1873897110},
            {1325935999, -1520560807},
            {-225234813, -78533081},
            {-202239044, -1509851560},
            {-322501005, 1422530437},
            {-1851058636, 1917703890},
            {-1828062867, -1162759543},
            {-1966381470, -1130350864},
            {968732481, 1391703481},
            {991728250, -242084766},
            {-253819519, 621915341},
            {-1714986849, 1827479659},
            {-1691991080, 813023040},
            {179448791, -395377963},
            {431617507, -1167031859},
            {688032252, 69511070},
            {-1059362949, -4031221},
            {1265861812, 1110385766},
            {374890416, 317398316},
            {1567404401, 1882709339},
            {-1057402571, 95950017},
            {-758925787, -1130709577},
            {-1411620422, 1052926200},
            {88869913, -542577259},
            {-2094080303, 1318558775},
            {843418712, -1962971928},
            {-1569356508, -1405508498},
            {-1569280852, 1478091698},
            {449769971, 1953903201},
            {590532217, -2097376851},
            {3387378, 1414245162},
            {1767561705, 1992974553},
            {106433500, 237239288},
            {-1334615971, -1778159885},
            {-135651869, 1722154847},
            {-1595790889, 1850456855},
            {-459156023, -1695367501},
            {106434956, -1779183908},
            {-578028723, -1302129395},
            {-586116979, 286193827},
            {-1379225193, -75944661},
            {-930579334, 649912614},
            {548699316, 818877484},
            {142147109, 1581210395},
            {148953073, 1903654061},
            {102672084, 980333378},
            {640562379, -1651220691},
            {-1732316031, -1622660759},
            {-2130280721, 756517185},
            {-1725510067, -722241321},
            {1974032895, -1673693549},
            {-225085592, -567909622},
            {509654999, 1898094925},
            {466113771, -1511285251},
            {2033918259, 1373971859},
            {2069925558, -1736356576},
            {-1026117678, 803222026},
            {1987447227, -1861522751},
            {540154065, -544317637},
            {1939428458, 176787552},
            {-288010497, -2002277461},
            {-847065290, 1199391518},
            {3506021, 963906841},
            {649603450, 442886268},
            {3506418, 1414245522},
            {569935070, -1104881824},
            {113284, -1985799200},
            {1916127949, -277057363},
            {-1775234707, -1978999529},
            {-388967316, 1326180354},
            {2007564590, -575483084},
            {-1705696613, 177226991},
            {670655301, -253079493},
            {1148128486, -1958316066},
            {-141135377, 567235583},
            {109266897, -932201673},
            {-527558546, 2087678962},
            {-1745053053, -904863145},
            {1223860752, 573926264},
            {-419069863, 1234880403},
            {-1617374968, -1994909036},
            {2057749608, 1950721418},
            {24576628, -2025184684},
            {-1659202509, 1608640313},
            {2107229499, -1549739227},
            {191795897, -765183617},
            {-1009492144, 795371088},
            {2077983581, -1367281941},
            {378365037, 352499047},
            {-529054135, -1199897169},
            {-529054134, -1199897172},
            {486166145, -1023374709},
            {1628490888, 23352662},
            {1498516223, 1205607945},
            {-632459882, -1647846966},
            {-626812403, -845557339},
            {385802761, -1370759135},
            {2117976603, 121049755},
            {1338515426, -996185386},
            {-1455694274, 98508942},
            {1579245182, 2070189026},
            {-587434450, 1521286012},
            {-163742043, 1542290441},
            {-1224714193, -1832422579},
            {644359987, 826309791},
            {-1962514734, -143132326},
            {-705305612, 1153652756},
            {-357728804, -1819233322},
            {-698499648, -1138208076},
            {1213686767, -1850571427},
            {386382445, -855748505},
            {1859976884, 553887414},
            {960793436, 996293980},
            {1001265731, 2048317869},
            {1253290621, -1754948969},
            {470729623, -1293296287},
            {1051155022, -369760990},
            {865679437, -1878764039},
            {927253046, -1039528932},
            {109552593, 1796682209},
            {-2092529553, 1230323789},
            {691633666, -363689972},
            {-2055888649, 1629293099},
            {621575320, -41440462},
            {-2118132208, 1602646136},
            {-1127699509, 1540934679},
            {-685265909, -92759291},
            {552706886, -1100422738},
            {1835797460, -1021495308},
            {-892259869, 642482233},
            {-1623330855, -465682601},
            {-1616524891, 1668858301},
            {789892804, 171931394},
            {-1289478934, -1583967946},
            {-892070738, -2099697608},
            {-891243783, -1581843485},
            {889398893, -1157596551},
            {-1625468793, 1397052267},
            {1293049486, 1975934948},
            {1369769822, 559147458},
            {586484018, 1079279582},
            {110115790, 593465182},
            {1490499512, 1523195708},
            {3552619, 2019042823},
            {1471284746, 73681876},
            {456448245, -1758372725},
            {110547964, 795236088},
            {1588977225, -1667224349},
            {918540912, -209869746},
            {-471874147, 1686524871},
            {205978836, 1723747470},
            {-1044400758, -129230242},
            {-2073307447, -1331212963},
            {435230680, 2106561762},
            {-864578046, 223891266},
            {1660607208, 935692442},
            {260214178, -1478445584},
            {-1847536522, 198438816},
            {-496055048, -967648160},
            {-1792066367, 99588025},
            {562888306, -956706906},
            {-427925529, -1429456799},
            {995306285, 1451568081},
            {-378017204, -1117626326},
            {447918618, -148794216},
            {313836902, 1516985844},
            {1175970190, -796583652},
            {525244071, -148229307},
            {-1021702157, -819720157},
            {-402507101, 671706427},
            {-1556671423, -1183726687},
            {61936445, -1614955425},
            {112903447, -1779180711},
            {1817873886, -1100168350},
            {1824679850, -132247350},
            {-1628526499, -1863559151},
            {547302405, -119235651},
            {1840561315, 2114754781},
            {-460592212, -1379835144},
            {3655341, -151838493},
            {1554697726, 418081930},
            {-1883959124, 832133926},
            {-481416622, 1524187186},
            {-481416621, -41896755},
            {-481416620, -1607980696},
            {-1151126752, 1058261682},
            {-1926458555, 794443127}
        };

        //Languages phrases

        private readonly Dictionary<string, Dictionary<string, string>> _messages =
            new Dictionary<string, Dictionary<string, string>>
            {
                {
                    "FILE_NOT_EXISTS", new Dictionary<string, string>
                    {
                        {"en", "File does not exist"},
                        {"ru", "Файл не существует"},
                        {"nl", "Bestand bestaat niet."}
                    }
                },
                {
                    "FILE_BROKEN", new Dictionary<string, string>
                    {
                        {"en", "Something went wrong during pasting because of a error in the file."},
                        {"ru", "Файл поврежден, вставка невозможна"},
                        {"nl", "Er is iets misgegaan tijdens het plakken door een beschadigd bestand."}
                    }
                },
                {
                    "NO_ACCESS", new Dictionary<string, string>
                    {
                        {"en", "You don't have the permissions to use this command"},
                        {"ru", "У вас нет прав доступа к данной команде"},
                        {"nl", "U heeft geen toestemming/permissie om dit commando te gebruiken."}
                    }
                },
                {
                    "SYNTAX_PASTEBACK", new Dictionary<string, string>
                    {
                        {
                            "en", "Syntax: /pasteback <Target Filename> <options values>\n" +
                                  "height XX - Adjust the height\n" +
                                  "vending - Information and sellings in vending machine\n" +
                                  "stability <true/false> - Wether or not to disable stability\n" +
                                  "deployables <true/false> - Wether or not to copy deployables\n" +
                                  "auth <true/false> - Wether or not to copy lock and cupboard whitelists"
                        },
                        {
                            "ru", "Синтаксис: /pasteback <Название Объекта> <опция значение>\n" +
                                  "height XX - Высота от земли\n" +
                                  "vending - Информация и товары в торговом автомате"
                        },
                        {
                            "nl", "Syntax: /pasteback <Bestandsnaam> <opties waarden>\n" +
                                  "height XX - Pas de hoogte aan \n" +
                                  "vending <true/false> - Informatie en inventaris van \"vending machines\" kopiëren\n" +
                                  "stability <true/false> - of de stabiliteit van het gebouw uitgezet moet worden\n" +
                                  "deployables <true/false> - of de \"deployables\" gekopiërd moeten worden\n" +
                                  "auth <true/false> - Of authorisatie op sloten en tool cupboards gekopiërd moet worden"
                        }
                    }
                },
                {
                    "SYNTAX_PASTE_OR_PASTEBACK", new Dictionary<string, string>
                    {
                        {
                            "en", "Syntax: /paste or /pasteback <Target Filename> <options values>\n" +
                                  "height XX - Adjust the height\n" +
                                  "autoheight true/false - sets best height, carefull of the steep\n" +
                                  "blockcollision XX - blocks the entire paste if something the new building collides with something\n" +
                                  "deployables true/false - false to remove deployables\n" +
                                  "inventories true/false - false to ignore inventories\n" +
                                  "vending - Information and sellings in vending machine\n" +
                                  "stability <true/false> - Wether or not to disable stability on the building"
                        },
                        {
                            "ru", "Синтаксис: /paste or /pasteback <Название Объекта> <опция значение>\n" +
                                  "height XX - Высота от земли\n" +
                                  "autoheight true/false - автоматически подобрать высоту от земли\n" +
                                  "blockcollision XX - блокировать вставку, если что-то этому мешает\n" +
                                  "deployables true/false - false для удаления предметов\n" +
                                  "inventories true/false - false для игнорирования копирования инвентаря\n" +
                                  "vending - Информация и товары в торговом автомате"
                        },
                        {
                            "nl", "Syntax: /paste of /pasteback <Bestandsnaam> <opties waarden>\n" +
                                  "height XX - Pas de hoogte aan \n" +
                                  "autoheight true/false - probeert de optimale hoogte te vinden om gebouw te plaatsen. Werkt optimaal op vlakke grond.\n" +
                                  "vending true/false - Informatie en inventaris van \"vending machines\" kopiëren\n" +
                                  "stability <true/false> - of de stabiliteit van het gebouw uitgezet moet worden\n" +
                                  "deployables <true/false> - of de \"deployables\" gekopiërd moeten worden\n" +
                                  "auth <true/false> - Of authorisatie op sloten en tool cupboards gekopiërd moet worden"
                        }
                    }
                },
                {
                    "PASTEBACK_SUCCESS", new Dictionary<string, string>
                    {
                        {"en", "You've successfully placed back the structure"},
                        {"ru", "Постройка успешно вставлена на старое место"},
                        {"nl", "Het gebouw is succesvol teruggeplaatst."}
                    }
                },
                {
                    "PASTE_SUCCESS", new Dictionary<string, string>
                    {
                        {"en", "You've successfully pasted the structure"},
                        {"ru", "Постройка успешно вставлена"},
                        {"nl", "Het gebouw is succesvol geplaatst."}
                    }
                },
                {
                    "SYNTAX_COPY", new Dictionary<string, string>
                    {
                        {
                            "en", "Syntax: /copy <Target Filename> <options values>\n" +
                                  "radius XX (default 3) - The radius in which to search for the next object (performs this search from every other object)\n" +
                                  "method proximity/building (default proximity) - Building only copies objects which are part of the building, proximity copies everything (within the radius)\n" +
                                  "deployables true/false (saves deployables or not) - Wether to save deployables\n" +
                                  "inventories true/false (saves inventories or not) - Wether to save inventories of found objects with inventories."
                        },
                        {
                            "ru", "Синтаксис: /copy <Название Объекта> <опция значение>\n" +
                                  "radius XX (default 3)\n" +
                                  "method proximity/building (по умолчанию proximity)\n" +
                                  "deployables true/false (сохранять предметы или нет)\n" +
                                  "inventories true/false (сохранять инвентарь или нет)"
                        },
                        {
                            "nl", "Syntax: /copy <Bestandsnaam> <opties waarden>\n" +
                                  "radius XX (standaard 3) - De radius waarin copy paste naar het volgende object zoekt\n" +
                                  "method proximity/building (standaard proximity) - Building kopieërd alleen objecten die bij het gebouw horen, proximity kopieërd alles wat gevonden is\n" +
                                  "deployables true/false (saves deployables or not) - Of de data van gevonden \"deployables\" opgeslagen moet worden\n" +
                                  "inventories true/false (saves inventories or not) - Of inventarissen van objecten (kisten, tool cupboards, etc) opgeslagen moet worden"
                        }
                    }
                },
                {
                    "NO_ENTITY_RAY", new Dictionary<string, string>
                    {
                        {"en", "Couldn't ray something valid in front of you"},
                        {"ru", "Не удалось найти какой-либо объект перед вами"},
                        {"nl", "U kijkt niet naar een geschikt object om een kopie op te starten."}
                    }
                },
                {
                    "COPY_SUCCESS", new Dictionary<string, string>
                    {
                        {"en", "The structure was successfully copied as {0}"},
                        {"ru", "Постройка успешно скопирована под названием: {0}"},
                        {"nl", "Gebouw is succesvol gekopieërd"}
                    }
                },
                {
                    "NO_PASTED_STRUCTURE", new Dictionary<string, string>
                    {
                        {"en", "You must paste structure before undoing it"},
                        {"ru", "Вы должны вставить постройку перед тем, как отменить действие"},
                        {"nl", "U moet eerst een gebouw terugplaatsen alvorens deze ongedaan gemaakt kan worden (duhh)"}
                    }
                },
                {
                    "UNDO_SUCCESS", new Dictionary<string, string>
                    {
                        {"en", "You've successfully undid what you pasted"},
                        {"ru", "Вы успешно снесли вставленную постройку"},
                        {"nl", "Laatse geplaatste gebouw is succesvol ongedaan gemaakt."}
                    }
                },
                {
                    "NOT_FOUND_PLAYER", new Dictionary<string, string>
                    {
                        {"en", "Couldn't find the player"},
                        {"ru", "Не удалось найти игрока"},
                        {"nl", "Speler niet gevonden."}
                    }
                },
                {
                    "SYNTAX_BOOL", new Dictionary<string, string>
                    {
                        {"en", "Option {0} must be true/false"},
                        {"ru", "Опция {0} принимает значения true/false"},
                        {"nl", "Optie {0} moet true of false zijn"}
                    }
                },
                {
                    "SYNTAX_HEIGHT", new Dictionary<string, string>
                    {
                        {"en", "Option height must be a number"},
                        {"ru", "Опция height принимает только числовые значения"},
                        {"nl", "De optie height accepteert alleen nummers"}
                    }
                },
                {
                    "SYNTAX_BLOCKCOLLISION", new Dictionary<string, string>
                    {
                        {"en", "Option blockcollision must be a number, 0 will deactivate the option"},
                        {
                            "ru",
                            "Опция blockcollision принимает только числовые значения, 0 позволяет отключить проверку"
                        },
                        {"nl", "Optie blockcollision accepteert alleen nummers, 0 schakelt deze functionaliteit uit"}
                    }
                },
                {
                    "SYNTAX_RADIUS", new Dictionary<string, string>
                    {
                        {"en", "Option radius must be a number"},
                        {"ru", "Опция radius принимает только числовые значения"},
                        {"nl", "Optie height accepteert alleen nummers"}
                    }
                },
                {
                    "BLOCKING_PASTE", new Dictionary<string, string>
                    {
                        {"en", "Something is blocking the paste"},
                        {"ru", "Что-то препятствует вставке"},
                        {"nl", "Iets blokkeert het plaatsen van dit gebouw"}
                    }
                },
                {
                    "AVAILABLE_STRUCTURES", new Dictionary<string, string>
                    {
                        {"ru", "<color=orange>Доступные постройки:</color>"},
                        {"en", "<color=orange>Available structures:</color>"},
                        {"nl", "Beschikbare bestanden om te plaatsen zijn:"}
                    }
                }
            };

        public class CopyData
        {
            public BasePlayer Player;
            public Stack<Vector3> CheckFrom = new Stack<Vector3>();
            public HashSet<BaseEntity> HouseList = new HashSet<BaseEntity>();
            public List<object> RawData = new List<object>();
            public Vector3 SourcePos;
            public Vector3 SourceRot;
            public Action Callback;

            public string FileName;
            public int CurrentLayer;
            public float RotCor;
            public float Range;
            public bool SaveTree;
            public bool SaveShare;
            public CopyMechanics CopyMechanics;
            public bool EachToEach;
            public uint BuildingId = 0;

#if DEBUG
            public Stopwatch Sw = new Stopwatch();
#endif
        }

        public class PasteData
        {
            public ICollection<Dictionary<string, object>> Entities;
            public List<BaseEntity> PastedEntities = new List<BaseEntity>();

            public Dictionary<uint, Dictionary<string, object>> IoEntities =
                new Dictionary<uint, Dictionary<string, object>>();

            public BasePlayer Player;
            public List<StabilityEntity> StabilityEntities = new List<StabilityEntity>();
            public Quaternion QuaternionRotation;
            public Action Callback;

            public bool Auth;
            public Vector3 StartPos;
            public float HeightAdj;
            public bool Stability;
            public bool IsItemReplace;
            public bool Ownership;

            public uint BuildingId = 0;

#if DEBUG
            public Stopwatch Sw = new Stopwatch();
#endif
        }
    }
}