using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    [Info("Welcomer", "Tricky", "1.5.1")]
    [Description("Provides welcome and join/leave messages")]
    public class Welcomer : RustPlugin
    {
        #region Fields
        private const string perm = "welcomer.bypass";
        #endregion

        #region Config
        Configuration config;

        class Configuration
        {
            [JsonProperty(PropertyName = "Enable: Welcome Message")]
            public bool WelcomeMessage = true;

            [JsonProperty(PropertyName = "Enable: Join Messages")]
            public bool JoinMessages = true;

            [JsonProperty(PropertyName = "Enable: Leave Messages")]
            public bool LeaveMessages = true;

            [JsonProperty(PropertyName = "Chat Icon (SteamID64)")]
            public ulong ChatIcon = 0;

            [JsonProperty(PropertyName = "Display Steam Avatar of Player - Join/Leave")]
            public bool SteamAvatar = true;

            [JsonProperty(PropertyName = "Print To Console - Join/Leave")]
            public bool PrintToConsole = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Registering Permissions
        private void OnServerInitialized()
        {
            permission.RegisterPermission(perm, this);
        }
        #endregion

        #region API Class
        class Response
        {
            [JsonProperty("country")]
            public string Country { get; set; }
        }
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Welcome"] = "<size=17>Welcome to the <color=#0099CC>Server</color></size>\n--------------------------------------------\n<color=#0099CC>•</color> Type <color=#0099CC>/info</color> for all available commands\n<color=#0099CC>•</color> Read the server rules by typing <color=#0099CC>/info</color>\n<color=#0099CC>•</color> Have fun and respect other players",
                ["Joined"] = "<color=#37BC61>✔</color> {0} <color=#37BC61>joined the server</color> from <color=#37BC61>{1}</color>",
                ["JoinedUnknown"] = "<color=#37BC61>✔</color> {0} <color=#37BC61>joined the server</color>",
                ["Left"] = "<color=#FF4040>☒</color> {0} <color=#FF4040>left the server</color> ({1})"
            }, this);
        }
        #endregion

        #region Collection
        List<ulong> connected = new List<ulong>();
        #endregion

        #region OnPlayerHooks
        private void OnPlayerConnected(BasePlayer player)
        {
            if (config.WelcomeMessage)
            {
                if (HasPermission(player))
                    return;

                if (!connected.Contains(player.userID))
                    connected.Add(player.userID);
            }

            if (config.JoinMessages)
            {
                if (HasPermission(player))
                    return;

                var playerAddress = player.net.connection.ipaddress.Split(':')[0];

                webrequest.Enqueue("http://ip-api.com/json/" + playerAddress, null, (code, response) =>
                {
                    if (code != 200 || response == null)
                    {
                        Broadcast(Lang("JoinedUnknown", null, player.displayName), player.userID);

                        if (config.PrintToConsole)
                            Puts(StripRichText(Lang("JoinedUnknown", null, player.displayName)));

                        return;
                    }

                    var country = JsonConvert.DeserializeObject<Response>(response).Country;

                    Broadcast(Lang("Joined", null, player.displayName, country), player.userID);

                    if (config.PrintToConsole)
                        Puts(StripRichText(Lang("Joined", null, player.displayName, country)));

                }, this);
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!config.WelcomeMessage)
                return;

            if (!connected.Contains(player.userID))
                return;

            Message(player, Lang("Welcome", player.UserIDString));
            connected.Remove(player.userID);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!config.LeaveMessages)
                return;

            if (HasPermission(player))
                return;

            Broadcast(Lang("Left", null, player.displayName, reason), player.userID);

            if (config.PrintToConsole)
                Puts(StripRichText(Lang("Left", null, player.displayName, reason)));
        }
        #endregion

        #region Helpers
        private void Broadcast(string message, ulong playerId)
        {
            Server.Broadcast(message, config.SteamAvatar ? playerId : config.ChatIcon);
        }

        private void Message(BasePlayer player, string message)
        {
            Player.Message(player, message, config.ChatIcon);
        }

        private bool HasPermission(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, perm);
        }

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        private string StripRichText(string text)
        {
            var stringReplacements = new string[]
            {
                "<b>", "</b>",
                "<i>", "</i>",
                "</size>",
                "</color>"
            };

            var regexReplacements = new Regex[]
            {
                new Regex(@"<color=.+?>"),
                new Regex(@"<size=.+?>"),
            };

            foreach (var replacement in stringReplacements)
                text = text.Replace(replacement, string.Empty);

            foreach (var replacement in regexReplacements)
                text = replacement.Replace(text, string.Empty);

            return Formatter.ToPlaintext(text);
        }
        #endregion
    }
}
