using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaleWorlds.MountAndBlade.DedicatedCustomServer;
using Microsoft.AspNetCore;
using System.Reflection.PortableExecutable;
using NetworkMessages.FromClient;
using OCEAdmin.Panel.Handlers;
using OCEAdmin.Shared.Network;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using System.IO;

namespace OCEAdmin.Panel
{
    public class SubModule : MBSubModuleBase
    {
        public static string dir;
        public static Dictionary<string, List<string>> customMaps = new Dictionary<string, List<string>>();

        protected override async void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            ModuleInfo moduleInfo = ModuleHelper.GetModuleInfo("OCEAdminPanel");
            dir = moduleInfo.FolderPath;

            LoadCustomMaps();

            Task.Run(() =>
            {
                while(!OCEAdminSubModule.Instance.IsLoaded()) { }

                PacketRegistry.LoadNetworkPackets();

                if (OCEAdmin.Config.Get().SteamAPI != null)
                {
                    CreateHostBuilder().Build().Run();
                }
                else
                {
                    MPUtil.WriteToConsole("Admin panel extension was not launched.");
                    MPUtil.WriteToConsole("Steam API is missing from OCEAdmin's config.xml");
                }
            });
        }

        public static void LoadCustomMaps()
        {
            string yamlFilePath = dir + "/custom_maps.yml";

            if (!File.Exists(yamlFilePath))
            {
                // Create the YAML content
                var maps = new Dictionary<string, List<string>>
                {
                    { "Battle", new List<string> { "custom_map_001",  } },
                    { "Captain", new List<string> { "custom_map_002" } }
                };

                // Serialize the maps dictionary to YAML
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                var content = serializer.Serialize(maps);

                // Write the YAML content to the file
                File.WriteAllText(yamlFilePath, content);
            }

            // Read the YAML file
            string yamlContent = File.ReadAllText(yamlFilePath);

            // Parse the YAML content
            var deserializer = new DeserializerBuilder().Build();
            var yamlData = deserializer.Deserialize<Dictionary<string, List<string>>>(yamlContent);

            // Use the parsed data to populate your dictionary
            customMaps = new Dictionary<string, List<string>>(yamlData);

            foreach (var kvp in customMaps)
            {
                string key = kvp.Key;
                List<string> values = kvp.Value;

                string output = $"Panel loading maps for gamemode '{key}': {string.Join(", ", values)}";
                MPUtil.WriteToConsole(output);
            }
        }

        public override void OnMultiplayerGameStart(Game game, object starterObject)
        {
            try
            {
                MPUtil.WriteToConsole("Loading panel game handlers...");

                game.AddGameHandler<AdminPanelGameHandler>();
            }
            catch (Exception ex)
            {
                MPUtil.WriteToConsole(ex.ToString(), true);
            }
        }

        public override void OnGameEnd(Game game)
        {
            try
            {
                game.RemoveGameHandler<AdminPanelGameHandler>();
            }
            catch (Exception ex)
            {
                MPUtil.WriteToConsole(ex.ToString(), true);
            }
        }

        public static IHostBuilder CreateHostBuilder() =>
            Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls("http://*:" + (Module.CurrentModule.StartupInfo.ServerPort + 100)  + "/");
                });
    }
}
