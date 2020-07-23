using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using UnityEngine;

/*======================================================================================================================= 
*
*   
*   20th november 2018
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*   1.3.0   20181120    New maintainer (BuzZ)   added GUI button for set new tp pos (config for color, and bool for on/off)
*   1.4.0   20190609    toggle system FIX
*********************************************
*   Original author :   DaBludger on versions <1.3.0
*   Maintainer(s)   :   BuzZ since 20181116 from v1.3.0
*********************************************   
*
*=======================================================================================================================*/

/*
 * 1.4.5:
 * UI updates panel when player toggles godmode/vanish/radar
 * Requires AdminRadar 5.0.8+
 * 
 * 1.4.4:
 * Added support for AdminRadar 5.0+
 * 
 * 1.4.3:
 * Fixed `adminpanel toggle`
 * 
 * 1.4.2:
 *  Fixed issue with GUI on server startup @atope
 *  
 * 1.4.1:
 *  Fixed vanish permission
 *  Fixed NullReferenceException in console command: adminpanel
 *  Renamed deprecated hook OnPlayerDie to OnPlayerDeath
 *  Added unsubcribing and subscribing of hooks
 *  Fixed `/adminpanel show` glitching the game when AdminPanelToggleMode is true
 *  Fixed `/adminpanel show` not showing the GUI
 */

namespace Oxide.Plugins
{
    [Info("Admin Panel", "nivex", "1.4.5")]
    [Description("GUI admin panel with command buttons")]
    class AdminPanel : RustPlugin
    {
        [PluginReference]
        private Plugin AdminRadar, EnhancedBanSystem, Godmode, NTeleportation, Vanish;

        private const string permAdminPanel = "adminpanel.allowed";
        private const string permAdminRadar = "adminradar.allowed";
        private const string permGodmode = "godmode.toggle";
        private const string permVanish = "vanish.allow";

        public Dictionary<BasePlayer, string> playerCUI = new Dictionary<BasePlayer, string>();

        #region Integrations

        #region Godmode

        private bool IsGod(string UserID)
        {
            return Godmode != null && Godmode.IsLoaded && Godmode.Call<bool>("IsGod", UserID);
        }

        private void ToggleGodmode(BasePlayer player)
        {
            if (Godmode == null || !Godmode.IsLoaded) return;

            if (IsGod(player.UserIDString))
                Godmode.Call("DisableGodmode", player.IPlayer);
            else
                Godmode.Call("EnableGodmode", player.IPlayer);

            AdminGui(player);
        }

        private void OnGodmodeToggle(string playerId, bool state)
        {
            var player = RustCore.FindPlayerByIdString(playerId);

            if (player.IsValid() && player.IsConnected && IsAllowed(player, permAdminPanel))
            {
                AdminGui(player);
            }
        }

        #endregion Godmode

        #region Vanish

        private bool IsInvisible(BasePlayer player)
        {
            return Vanish != null && Vanish.IsLoaded && Vanish.Call<bool>("IsInvisible", player);
        }

        private void ToggleVanish(BasePlayer player)
        {
            if (Vanish == null || !Vanish.IsLoaded) return;

            if (!IsInvisible(player))
                Vanish.Call("Disappear", player);
            else
                Vanish.Call("Reappear", player);

            AdminGui(player);
        }

        private void OnVanishDisappear(BasePlayer player)
        {
            if (player.IsValid() && player.IsConnected && IsAllowed(player, permAdminPanel))
            {
                AdminGui(player);
            }
        }

        private void OnVanishReappear(BasePlayer player)
        {
            if (player.IsValid() && player.IsConnected && IsAllowed(player, permAdminPanel))
            {
                AdminGui(player);
            }
        }

        #endregion Vanish

        #region Admin Radar

        private bool IsRadar(string id)
        {
            return AdminRadar != null && AdminRadar.IsLoaded && AdminRadar.Call<bool>("IsRadar", id);
        }

        private void ToggleRadar(BasePlayer player)
        {
            if (AdminRadar == null || !AdminRadar.IsLoaded) return;

            if (AdminRadar.Version < new Core.VersionNumber(5, 0, 0)) AdminRadar.Call("cmdESP", player, "radar", new string[0]);
            else if (player.IPlayer != null) AdminRadar.Call("RadarCommand", player.IPlayer, "radar", new string[0]);
            AdminGui(player);
        }

        private void OnRadarActivated(BasePlayer player)
        {
            if (player.IsValid() && player.IsConnected && IsAllowed(player, permAdminPanel))
            {
                AdminGui(player);
            }
        }

        private void OnRadarDeactivated(BasePlayer player)
        {
            if (player.IsValid() && player.IsConnected && IsAllowed(player, permAdminPanel))
            {
                AdminGui(player);
            }
        }

        #endregion Admin Radar

        #endregion Integrations

        private void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(permAdminPanel, this);
            Unsubscribe(nameof(OnPlayerSleepEnded));
            Unsubscribe(nameof(OnPlayerDeath));
        }

        #region Configuration

        private bool ToggleMode;
        private bool newtp;
        private string PanelPosMax;
        private string PanelPosMin;
        private string adminZoneCords;
        private string btnActColor;
        private string btnInactColor;
        private string btnNewtpColor;

        protected override void LoadDefaultConfig()
        {
            Config["AdminPanelToggleMode"] = ToggleMode = GetConfig("AdminPanelToggleMode", false);
            Config["AdminPanelPosMax"] = PanelPosMax = GetConfig("AdminPanelPosMax", "0.991 0.67");
            Config["AdminPanelPosMin"] = PanelPosMin = GetConfig("AdminPanelPosMin", "0.9 0.5");
            Config["AdminZoneCoordinates"] = adminZoneCords = GetConfig("AdminZoneCoordinates", "0;0;0;");
            Config["PanelButtonActiveColor"] = btnActColor = GetConfig("PanelButtonActiveColor", "0 2.55 0 0.3");
            Config["PanelButtonInactiveColor"] = btnInactColor = GetConfig("PanelButtonInactiveColor", "2.55 0 0 0.3");
            Config["PanelButtonNewtp"] = newtp = GetConfig("PanelButtonNewtp", false);
            Config["PanelButtonNewtpColor"] = btnNewtpColor = GetConfig("PanelButtonNewtpColor", "1.0 0.65 0.85 0.3");

            SaveConfig();
        }

        #endregion Configuration

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminTP"] = "Teleport",
                ["Godmode"] = "God",
                ["Radar"] = "Radar",
                ["Vanish"] = "Vanish",
                ["NewTP"] = "NewTP"


            }, this);

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminTP"] = "Teleport",
                ["Godmode"] = "Dios",
                ["Radar"] = "Radar",
                ["Vanish"] = "Desaparecer",
                ["NewTP"] = "NewTP"

            }, this, "es");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AdminTP"] = "Teleport",
                ["Godmode"] = "Dieu",
                ["Radar"] = "Radar",
                ["Vanish"] = "Invisible",
                ["NewTP"] = "NewTP"

            }, this, "fr");

        }

        #endregion Localization

        #region Hooks

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (IsAllowed(player, permAdminPanel))
            {
                AdminGui(player);
            }
        }

        private void OnPlayerDeath(BasePlayer player)
        {
            DestroyUI(player);
        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name == "AdminRadar" || plugin.Name == "Godmode" || plugin.Name == "Vanish")
            {
                RefreshAllUI();
            }
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == "AdminRadar" || plugin.Name == "Godmode" || plugin.Name == "Vanish")
            {
                RefreshAllUI();
            }
        }

        private void OnServerInitialized()
        {
            Subscribe(nameof(OnPlayerDeath));

            if (!ToggleMode)
            {
                Subscribe(nameof(OnPlayerSleepEnded));
                RefreshAllUI();
            }
        }

        private void RefreshAllUI()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (IsAllowed(player, permAdminPanel))
                {
                    AdminGui(player);
                }
            }
        }

        #endregion Hooks

        #region Command Structure

        [ConsoleCommand("adminpanel")]
        private void ccmdAdminPanel(ConsoleSystem.Arg arg) // TODO: Make universal command
        {
            var player = arg.Player();
            if (player == null || !IsAllowed(player, permAdminPanel) || !arg.HasArgs()) return;

            switch (arg.Args[0].ToLower())
            {
                case "action":
                    {
                        if (arg.Args.Length >= 2)
                        {
                            if (arg.Args[1] == "vanish") // TODO: ToLower() args[1] here and below, use switch?
                            {
                                if (Vanish) ToggleVanish(player);
                            }
                            else if (arg.Args[1] == "admintp")
                            {
                                var pos = adminZoneCords.Split(';');
                                var loc = new Vector3(float.Parse(pos[0]), float.Parse(pos[1]), float.Parse(pos[2]));
                                covalence.Players.FindPlayer(player.UserIDString).Teleport(loc.x, loc.y, loc.z);
                            }
                            else if (arg.Args[1] == "radar")
                            {
                                if (AdminRadar) ToggleRadar(player);
                            }
                            else if (arg.Args[1] == "god")
                            {
                                if (Godmode) ToggleGodmode(player);
                            }
                            else if (arg.Args[1] == "newtp")
                            {
                                if (newtp)
                                {
                                    string[] argu = new string[1];
                                    argu[0] = "settp";
                                    ccmdAdminPanel(player, null, argu);
                                }
                            }
                            else
                            {
                                SendReply(player, "Syntax: adminpanel action vanish/admintp/radar/god/newtp");
                            }
                        }
                        else
                        {
                            SendReply(player, "Syntax: adminpanel action vanish/admintp/radar/god/newtp");
                        }

                        break;
                    }
                case "toggle":
                    {
                        if (IsAllowed(player, permAdminPanel))
                        {
                            /*if (args[1] == "True" && ToggleMode) // TODO: Convert to bool to check
                            {
                                AdminGui(player);
                            }
                            else if (args[1] == "False" && ToggleMode) // TODO: Convert to bool to check
                            {
                                PleaseDestroyUI(player);
                            }
                            // TODO: Show reply*/
                            if (ToggleMode && playerCUI.ContainsKey(player))
                                DestroyUI(player);
                            else AdminGui(player);
                        }

                        break;
                    }
                default:
                    {
                        SendReply(player, "Invalid syntax: action/toggle"); // TODO: Localization
                        break;
                    }
            }
            //Reply(player, null); // TODO: Show actual reply or not at all
        }

        [ChatCommand("adminpanel")]
        private void ccmdAdminPanel(BasePlayer player, string command, string[] args) // TODO: Make universal command
        {
            if (!IsAllowed(player, permAdminPanel))
            {
                SendReply(player, $"Unknown command: {command}"); // TODO: Localization
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, $"Usage: /{command} show/hide/settp"); // TODO: Localization
                return;
            }

            switch (args[0].ToLower())
            {
                case "hide":
                    DestroyUI(player);
                    SendReply(player, "Admin panel hidden"); // TODO: Localization
                    break;

                case "show":
                    AdminGui(player);
                    SendReply(player, "Admin panel refreshed/shown"); // TODO: Localization
                    break;

                case "settp":
                    Vector3 coord = player.transform.position + new Vector3(0, 1, 0);
                    Config["AdminZoneCoordinates"] = adminZoneCords = $"{coord.x};{coord.y};{coord.z}";
                    Config.Save();
                    SendReply(player, $"Admin zone coordinates set to current position {player.transform.position + new Vector3(0, 1, 0)}"); // TODO: Localization
                    break;

                default:
                    SendReply(player, $"Invalid syntax: /{command} {args[0]}"); // TODO: Localization
                    break;
            }
        }

        #endregion Command Structure

        #region GUI Panel

        private void AdminGui(BasePlayer player)
        {
            NextTick(() =>
            {
                // Destroy existing UI
                DestroyUI(player);

                var BTNColorVanish = btnInactColor;
                var BTNColorGod = btnInactColor;
                var BTNColorRadar = btnInactColor;
                var BTNColorNewTP = btnNewtpColor;

                if (AdminRadar) { if (IsRadar(player.UserIDString)) { BTNColorRadar = btnActColor; } }
                if (Godmode) { if (IsGod(player.UserIDString)) { BTNColorGod = btnActColor; } }
                if (Vanish) { if (IsInvisible(player)) { BTNColorVanish = btnActColor; } }

                var GUIElement = new CuiElementContainer();

                var GUIPanel = GUIElement.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "1 1 1 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = PanelPosMin,
                        AnchorMax = PanelPosMax
                    },
                    CursorEnabled = false
                }, "Hud", Name);

                if (AdminRadar && permission.UserHasPermission(player.UserIDString, permAdminRadar))
                {
                    GUIElement.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "adminpanel action radar",
                            Color = BTNColorRadar
                        },
                        Text =
                        {
                            Text = Lang("Radar", player.UserIDString),
                            FontSize = 8,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.062 0.21",
                            AnchorMax = "0.51 0.37"
                        }
                    }, GUIPanel);
                }

                GUIElement.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "adminpanel action admintp",
                        Color = "1.28 0 1.28 0.3"
                    },
                    Text =
                    {
                        Text = Lang("AdminTP", player.UserIDString),
                        FontSize = 8,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.52 0.21",
                        AnchorMax = "0.95 0.37"
                    }
                }, GUIPanel);

                if (newtp)
                {
                    GUIElement.Add(new CuiButton
                    {
                        Button =
                    {
                        Command = "adminpanel action newtp",
                        Color = BTNColorNewTP
                    },
                        Text =
                    {
                        Text = Lang("newTP", player.UserIDString),
                        FontSize = 8,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    },
                        RectTransform =
                    {
                        AnchorMin = "0.52 0.39",
                        AnchorMax = "0.95 0.47"
                    }
                    }, GUIPanel);
                }

                if (Godmode && permission.UserHasPermission(player.UserIDString, permGodmode))
                {
                    GUIElement.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "adminpanel action god",
                            Color = BTNColorGod
                        },
                        Text =
                        {
                            Text = Lang("Godmode", player.UserIDString),
                            FontSize = 8,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.52 0.02",
                            AnchorMax = "0.95 0.19"
                        }
                    }, GUIPanel);
                }

                if (Vanish && permission.UserHasPermission(player.UserIDString, permVanish))
                {
                    GUIElement.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "adminpanel action vanish",
                            Color = BTNColorVanish
                        },
                        Text =
                        {
                            Text = Lang("Vanish", player.UserIDString),
                            FontSize = 8,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.062 0.02",
                            AnchorMax = "0.51 0.19"
                        }
                    }, GUIPanel);
                }

                CuiHelper.AddUi(player, GUIElement);
                playerCUI.Add(player, GUIPanel);
            });
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        private void DestroyUI(BasePlayer player)
        {
            string cuiElement;
            if (playerCUI.TryGetValue(player, out cuiElement))
            {
                CuiHelper.DestroyUi(player, cuiElement);
                playerCUI.Remove(player);
            }
        }

        #endregion GUI Panel

        #region Helpers

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        private bool IsAllowed(BasePlayer player, string perm) => player != null && (permission.UserHasPermission(player.UserIDString, perm) || player.IsAdmin);

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion Helpers
    }
}
