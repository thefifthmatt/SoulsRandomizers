using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SoulsIds;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Tomlyn;
using Tomlyn.Model;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Util;
using static SoulsIds.GameSpec;

namespace RandomizerCommon
{
    public partial class ArchipelagoForm : Form
    {

        /// <summary>
        /// The location of the file in which data about this AP session is saved.
        /// </summary>
        private static readonly string ConfigFileLocation = "..\\apconfig.json";

        /// <summary>
        /// The location of the file in which ME3's configuration is stored.
        /// </summary>
        private static readonly string ME3ConfigFileLocation = "..\\me3-config.me3";

        /// <summary>
        /// The Archipelago configuration data that was already saved in this directory, or an
        /// empty object if there wasn't any data.
        /// </summary>
        private readonly JObject configData;

        /// <summary>
        /// The ModEngine3 configuration that was saved in this directory, or null if none was
        /// found.
        /// </summary>
        private readonly TomlTable me3ConfigData;

        private readonly Timer blinkTimer;

        public ArchipelagoForm()
        {
            InitializeComponent();

            MinimumSize = Size;

            blinkTimer = new()
            {
                Interval = 500 // 0.5 seconds
            };
            blinkTimer.Tick += BlinkTimer_Tick;

            try
            {
                configData = JsonConvert.DeserializeObject<JObject>(
                    File.ReadAllText(ConfigFileLocation)
                );
            }
            catch (FileNotFoundException)
            {
                configData = new JObject();
            }
            catch (JsonException)
            {
                MessageBox.Show(
                    $"Failed to load {Path.GetFileName(ConfigFileLocation)}. Running the " + 
                    "randomizer may overwrite existing save data.",
                    "Archipelago Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }

            try
            {
                me3ConfigData = Toml.ToModel(File.ReadAllText(ME3ConfigFileLocation));
            }
            catch (Exception)
            {
                // Allow the config to be null, we just won't customize the save location.
            }

            if (configData.Value<string>("url") is string savedUrl) url.Text = savedUrl;
            if (configData.Value<string>("slot") is string savedSlot) name.Text = savedSlot;
            if (configData.Value<string>("password") is string savedPassword) password.Text = savedPassword;
        }

        private static SemanticVersioning.Version Version {
            get
            {
                return Assembly.GetCallingAssembly()
                    .GetCustomAttribute<VersionAttribute>()
                    .Version;
            }
        }

        private async void submit_Click(object sender, EventArgs e)
        {
            foreach (Control control in Controls)
            {
                if (control.Name != "status")
                {
                    control.Enabled = false;
                }
            }

            Cursor = Cursors.WaitCursor;

            SetStatusText("Connecting...", System.Drawing.Color.Blue);
            status.Visible = true;
            status.Refresh();

            if (url.Text.Length == 0)
            {
                ShowFailure("Missing Archipelago URL");
                return;
            }

            if (name.Text.Length == 0)
            {
                ShowFailure("Missing player name");
                return;
            }

            ArchipelagoSession session;
            try
            {
                session = ArchipelagoSessionFactory.CreateSession(url.Text);
            }
            catch (System.UriFormatException ex)
            {
                ShowFailure(ex.Message);
                return;
            }

            LoginResult result;
            try
            {
                result = session.TryConnectAndLogin(
                    "Dark Souls III",
                    name.Text,
                    Archipelago.MultiClient.Net.Enums.ItemsHandlingFlags.NoItems,
                    password: password.Text.Length == 0 ? null : password.Text,
                    version: new System.Version(0, 6, 1),
                    requestSlotData: false
                );
            }
            catch (Exception exception)
            {
                result = new LoginFailure(exception.GetBaseException().Message);
            }

            if (!result.Successful)
            {
                var failure = (LoginFailure)result;
                var errorMessage = "Failed to connect:";
                foreach (string error in failure.Errors)
                {
                    errorMessage += $"\n    {error}";
                }
                foreach (ConnectionRefusedError error in failure.ErrorCodes)
                {
                    errorMessage += $"\n    {error}";
                }
                ShowFailure(errorMessage);
                return;
            }

#if !DEBUG
            try
            {
#endif
            await Task.Run(() => RandomizeForArchipelago(session));
#if !DEBUG

            }
            catch (Exception ex)
            {
                ShowFailure(ex.Message);
                return;
            }
#endif

            MessageBox.Show("Archipelago config loaded successfully!");

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// Runs the randomizer and saves its results.
        /// </summary>
        /// <returns>True if randomization succeeded, false if it was canceled.</returns>
        private void RandomizeForArchipelago(ArchipelagoSession session)
        {
            SetStatusText("Downloading item data...");
            var locations = session.Locations
                .ScoutLocationsAsync(session.Locations.AllLocations.ToArray())
                .Result
                .Values
                .OrderBy(location => location.LocationId)
                .ToList();
            var slotData = session.DataStorage.GetSlotData();
            var apIdsToItemIds = ((JObject)slotData["apIdsToItemIds"]).ToObject<Dictionary<string, int>>()
                .ToDictionary(entry => long.Parse(entry.Key), entry => entry.Value);
            CheckVersionRange(slotData);
            var options = (JObject)slotData["options"];
            if (disableEnemyRandomizerCheckbox.Checked) options["randomize_enemies"] = false;

            var opt = ConvertRandomizerOptions(options);
            var itemCounts = ((JObject)slotData["itemCounts"]).ToObject<Dictionary<string, uint>>()
                .ToDictionary(entry => long.Parse(entry.Key), entry => entry.Value);

            SetStatusText("Loading game data...");

            var distBasename = "dist";
#if DEBUG
            // In debug mode, always use the data files from the local repository rather than those
            // in the directory we're randomizing to. This ensures we don't accidentally end up
            // testing against the data that shipped with whichever release copy of the Archipelago
            // client we downloaded.
            var distDir = Path.Join(Application.StartupPath, $@"..\..\..\..\..\{distBasename}");
#else
            var distDir = distBasename;
#endif
            if (!Directory.Exists(distDir))
            {
                throw new Exception("Missing data directory");
            }
            var game = new GameData(distDir, FromGame.DS3);
            game.Load();
            var scraper = new LocationDataScraper(logUnused: false);
            var data = scraper.FindItems(game);
            var ann = new AnnotationData(game, data);
            ann.Load(opt);
            var events = new Events($@"{game.Dir}\Base\ds3-common.emedf.json", darkScriptMode: true);
            var writer = new PermutationWriter(
                game, data, ann, events, game.ParseYaml<EventConfig>("itemevents.yaml"), opt);
            var permutation = new Permutation(game, data, ann, new Messages(null));
            var apLocationsToScopes = ArchipelagoLocations(session, ann, locations);

            // The Archipelago API doesn't guarantee that the seed is a number, so we hash it so
            // that we can use it as a seed for C#'s RNG. Add the current player's slot number so
            // that multiple DS3 instances in the same multiworld have different local seeds.
            var seed = HashStringToInt(session.RoomState.Seed) + session.ConnectionInfo.Slot;
            opt.Seed = (uint)seed;
            var random = new Random(seed);

            // Randomize starting loadout *before* adding a bunch of synthetic weapons and armor to
            // the pool that we don't want shoved into shops.
            if (options["random_starting_loadout"].ToObject<bool>())
            {
                var characters = new CharacterWriter(game, data);
                characters.Write(random, opt);
            }

            // A map from locations in the game where items can appear to the list of items that
            // should appear in those locations.
            var items = new Dictionary<SlotKey, List<SlotKey>>();

            // A map from items in the game that should be removed to locations where those items
            // would normally appear, or null if those items should remain in-game (likely because
            // they're assigned elsewhere).
            var itemsToRemove = new Dictionary<SlotKey, List<SlotKey>>();

            foreach (var info in locations)
            {
                var targetScope = apLocationsToScopes[info.LocationId];
                var targetSlotKey = FindMatchingSlotKey(
                    session, game, data.Location(targetScope), info);
                AddMulti(itemsToRemove, targetSlotKey, FindMatchingSlotKey(
                    session, game, data.Locations[targetScope], info));

                var targetSlot = ann.Slots[targetScope];
                var player = session.Players.Players[session.ConnectionInfo.Team]
                    .First(player => player.Slot == info.Player);

                if (info.Player != session.ConnectionInfo.Slot)
                {
                    // Create a fake key item for each item from another world.
                    var item = writer.AddSyntheticItem(
                        SyntheticItemName(info),
                        $"An object from a mysterious world known only as \"{player.Game}\".",
                        // Custom Archipelago icon.
                        iconId: 6020,
                        // The highest in-game sortId is 133,100, so for foreign items we start
                        // from 200,000 to sort them after in-game key items. From there we add
                        // the player ID as the primary sort, followed by the item ID (mod 10k
                        // because Archipelago puts all item IDs in a single 54-bit numberspace).
                        // This means that in shops, foreign items will be grouped first by player
                        // and then by item.
                        sortId: 200000 + (uint)info.Player.Slot * 10000 +
                            (uint)(info.ItemId % 10000),
                        archipelagoLocationId: info.LocationId);
                    AddMulti(items, targetSlotKey, item);
                }
                else if (info.ItemName == "Path of the Dragon")
                {
                    AddMulti(items, targetSlotKey, writer.AddSyntheticItem(
                        $"Path of the Dragon",
                        "A gesture of meditation channeling the eternal essence of the ancient dragons",
                        "The path to ascendence can be achieved only by the most resolute of seekers. Proper utilization of this technique can grant deep inner focus.",
                        iconId: 7039,
                        archipelagoLocationId: info.LocationId));
                }
                else if (targetScope.ShopIds.Count == 0 && !(targetSlot.Tags?.Contains("crow") ?? false))
                {
                    // The Archipelago mod can't replace items that appear in shops or are dropped
                    // by the crow, so we put more realistic items there. Everywhere else, we
                    // replace with placeholders, so we can notify the Archipelago server when
                    // they're checked. We can't do this with items in shops because we don't have
                    // a good way to replace them on pickup.
                    //
                    // We can't make _all_ items realistic like we do for shops because that can't
                    // represent bundles of multiple items.
                    AddMulti(items, targetSlotKey, writer.AddSyntheticItem(
                        $"[Placeholder] {info.ItemName}",
                        "If you can see this your Archipelago mod isn't working.",
                        archipelagoLocationId: info.LocationId,
                        replaceWithInArchipelago: new ItemKey(apIdsToItemIds[info.ItemId]),
                        replaceWithQuantity: itemCounts.GetValueOrDefault(info.ItemId, 1U)));
                }
                else
                {
                    var original = new ItemKey(apIdsToItemIds[info.ItemId]);
                    var (copy, _) = writer.AddSyntheticCopy(
                        original,
                        archipelagoLocationId: info.LocationId,
                        replaceWithInArchipelago: original,
                        replaceWithQuantity: 1
                    );
                    AddMulti(items, targetSlotKey, copy);
                }
            }

            SetStatusText("Randomizing locations...");

            permutation.Forced(items, remove: itemsToRemove);

            permutation.Logic(random, opt, null, new List<Permutation.RandomSilo> {
                Permutation.RandomSilo.INFINITE,
                Permutation.RandomSilo.INFINITE_SHOP,
                Permutation.RandomSilo.INFINITE_GEAR,
                Permutation.RandomSilo.INFINITE_CERTAIN,
                Permutation.RandomSilo.MIXED
            });

            writer.Write(random, permutation, alwaysReplacePathOfTheDragon: true);

            if (options["no_weapon_requirements"].ToObject<bool>())
            {
                RemoveWeaponRequirements(game);
            }

            if (options["no_spell_requirements"].ToObject<bool>())
            {
                RemoveSpellRequirements(game);
            }

            if (options["no_equip_load"].ToObject<bool>())
            {
                RemoveEquipLoad(game);
            }

            if (options["randomize_enemies"].ToObject<bool>()) {
                // Serializing this only to parse it again is silly, but YamlDotNet doesn't have
                // any way to deserialize from an object graph
                var presetYaml = (string)slotData["random_enemy_preset"];
                Preset preset;
                try
                {
                    preset = Preset.ParsePreset("archipelago", presetYaml);
                }
                catch (YamlException)
                {
                    DisplayYamlParseError(presetYaml);
                    throw new Exception("Failed to parse enemy preset");
                }
                preset.RemoveSource = preset.RemoveSource == null
                    ? "Yhorm the Giant"
                    : preset.RemoveSource + ";Yhorm the Giant";
                preset.Enemies ??= new Dictionary<string, string>();
                preset.Enemies[(string)slotData["yhorm"]] = "Yhorm the Giant";
                new EnemyRandomizer(game, events, game.ParseYaml<EventConfig>("events.yaml"))
                    .Run(opt, preset);
            }

            // Sort these params because there are technically debug rows above them, and the game
            // (as well as fromsoftware-rs) expects rows to be ordered by ID. We don't need to sort
            // accessories or goods because they don't have debug entries.
            MiscSetup.SortParams(game, new[] { "EquipParamProtector", "EquipParamWeapon" });
            MiscSetup.DS3CommonPass(game, events, opt);
            MiscSetup.InjectUncompressed(game);

            SetStatusText("Writing game files...");
            game.SaveDS3(Directory.GetCurrentDirectory(), true);

            SetStatusText("Writing client save file...");
            WriteConfigFiles(slotData);

            SetStatusText("Finished!", System.Drawing.Color.Green);
        }

        /// <summary>
        /// Show a dialog visually indicating the location of a parse error in the given YAML
        /// preset. This reformats the preset first, since we're confident that it's syntactically
        /// valid YAML.
        /// </summary>
        private static void DisplayYamlParseError(string presetYaml)
        {
            var obj = new DeserializerBuilder()
                .WithAttemptingUnquotedStringTypeDeserialization()
                .Build()
                .Deserialize(new StringReader(presetYaml));
            var serializer = new SerializerBuilder().WithQuotingNecessaryStrings().Build();
            var formattedYaml = serializer.Serialize(obj);

            try
            {
                Preset.ParsePreset("archipelago", formattedYaml);
            }
            catch (YamlException ex)
            {
                new PresetErrorDialog(formattedYaml, ex).ShowDialog();
            }
        }

        /// <summary>
        /// Returns the SlotKey in candidates whose base item name matches the item name in info.
        /// </summary>
        private static SlotKey FindMatchingSlotKey(ArchipelagoSession session, GameData game, List<SlotKey> candidates, ScoutedItemInfo info)
        {
            if (candidates.Count == 1) return candidates.First();

            var apLocation = session.Locations.GetLocationNameFromId(info.LocationId);
            var defaultItemName = ItemNameForLocation(apLocation);
            var match = candidates.FirstOrDefault(candidate => game.BaseName(candidate.Item) == defaultItemName);
            if (match != null) return match;
            throw new Exception($"Multiple possible locations for {apLocation}: {string.Join(", ", candidates)}");
        }

        /// <summary>
        /// Writes or edits the config file for the current Archipelago run.
        /// </summary>
        private void WriteConfigFiles(Dictionary<string, object> slotData)
        { 
            var seed = (string)slotData["seed"];
            configData["url"] = url.Text;
            configData["slot"] = name.Text;
            configData["seed"] = seed;
            configData["client_version"] = Version?.ToString();
            if (savePasswordCheckbox.Checked && password.Text.Length > 0)
            {
                configData["password"] = password.Text;
            }
            else
            {
                configData.Remove("password");
            }
            File.WriteAllText(ConfigFileLocation, JsonConvert.SerializeObject(configData));

            if (me3ConfigData != null)
            {
                me3ConfigData["savefile"] = $"ap-{seed}.sl2";
                if (me3ConfigData.PropertiesMetadata.TryGetProperty("profileVersion", out var metadata))
                {
                    me3ConfigData.PropertiesMetadata.SetProperty("savefile", metadata);
                    me3ConfigData.PropertiesMetadata.SetProperty("profileVersion", new());
                }

                File.WriteAllText(ME3ConfigFileLocation, Toml.FromModel(me3ConfigData).ReplaceLineEndings());
            }
        }

        /// <returns>A human-readable name for a foreign item.</returns>
        private static string SyntheticItemName(ScoutedItemInfo info)
        {
            // Use the player's entire name, if it fits.
            var name = $"{info.Player.Alias}'s {info.ItemName}";
            if (name.Length <= ItemNameLimit) return name;

            // If the player's name doesn't fit, trim it. Don't trim below four characters in case
            // it becomes unrecognizable. This may still result in a string longer than the maximum,
            // but in that case the item name will automatically get trimmed by the game as
            // necessary.
            var charactersToTrim = name.Length - ItemNameLimit;
            var trimmedPlayerName = info.Player.Alias[
                ..Math.Min(
                    info.Player.Alias.Length,
                    Math.Max(info.Player.Alias.Length - charactersToTrim, 4)
                )
            ];
            return $"{trimmedPlayerName} {info.ItemName}";
        }

        /// <summary>
        /// Converts Archipelago options into options for this randomizer.
        /// </summary>
        private static RandomizerOptions ConvertRandomizerOptions(JObject archiOptions)
        {
            var opt = new RandomizerOptions(FromGame.DS3);
            opt["archipelago"] = true;
            opt["safequest"] = archiOptions["unmissable_quests"].ToObject<bool>();
            opt["phantomhunters"] = archiOptions["unmissable_invasions"].ToObject<bool>();
            opt["bosssoulshop"] = archiOptions["unmissable_transpositions"].ToObject<bool>();
            opt["onehand"] = archiOptions["require_one_handed_starting_weapons"].ToObject<bool>();
            opt["ngplusrings"] = archiOptions["enable_ngp"].ToObject<bool>();
            opt["nongplusrings"] = !archiOptions["enable_ngp"].ToObject<bool>();
            opt["nooutfits"] = true; // Don't randomize NPC equipment. We should add this option
                                     // when we add enemizer support.
            // Used for infinite items from shops and enemy drops
            opt["weaponprogression"] = archiOptions["smooth_upgrade_locations"].ToObject<bool>();
            opt["soulsprogression"] = archiOptions["smooth_soul_locations"].ToObject<bool>();
            switch (archiOptions["anri_gender"].ToObject<string>())
            {
                case "same":
                    opt["anrisamegender"] = true;
                    break;
                case "female":
                    opt["anrifemale"] = true;
                    break;
                case "male":
                    opt["anrimale"] = true;
                    break;
            }

            if (archiOptions["randomize_enemies"].ToObject<bool>())
            {
                opt["bosses"] = true;
                opt["enemies"] = true;
                opt["edittext"] = true;
                opt["mimics"] = archiOptions["randomize_mimics_with_enemies"].ToObject<bool>();
                opt["lizards"] = archiOptions["randomize_small_crystal_lizards_with_enemies"]
                    .ToObject<bool>();
                opt["reducepassive"] = archiOptions["reduce_harmless_enemies"].ToObject<bool>();
                opt["earlyreq"] = archiOptions["simple_early_bosses"].ToObject<bool>();
                opt["scale"] = archiOptions["scale_enemies"].ToObject<bool>();
                opt["chests"] = archiOptions["all_chests_are_mimics"].ToObject<bool>();
                opt["supermimics"] = archiOptions["impatient_mimics"].ToObject<bool>();
            }

            if (archiOptions["enable_dlc"].ToObject<bool>())
            {
                opt["dlc1"] = true;
                opt["dlc2"] = true;
                opt["dlc2fromdlc1"] = true;
            }
            else
            {
                opt["omitdlc"] = true;
            }

            // These options aren't actually used, but they're necessary to run the offlien item
            // randomizer for infinite items.
            opt.Difficulty = 50;

            return opt;
        }

        /// <summary>
        /// Computes a stable hash of the given string and reduces it to a single integer.
        /// </summary>
        private static int HashStringToInt(string str)
        {
            using (var hash = SHA256.Create())
            {
                return BitConverter.ToInt32(hash.ComputeHash(Encoding.UTF8.GetBytes(str)), 0);
            }
        }

        /// <summary>
        /// Returns a map from Archipelago location IDs to the corresponding location scopes.
        /// </summary>
        private static Dictionary<long, LocationScope> ArchipelagoLocations(
            ArchipelagoSession session, AnnotationData ann, List<ScoutedItemInfo> locations)
        {
            var slotData = session.DataStorage.GetSlotData();
            var apIdsToKeys = ((JObject)slotData["locationIdsToKeys"])
                .ToObject<Dictionary<string, string>>()
                .ToDictionary(entry => long.Parse(entry.Key), entry => entry.Value);

            // A map from item names to all the slots that correspond to those names.
            var itemNameToSlots = new Dictionary<string, List<AnnotationData.SlotAnnotation>>();

            // A map from (Archipelago region abbreviation, item name) pairs to all the slots that
            // could correspond to those pairs.
            var locationToSlots = new Dictionary<(string, string), Queue<AnnotationData.SlotAnnotation>>();
            foreach (var slot in ann.SlotsByAnnotationsKey.Values)
            {
                var area = ann.Areas[slot.Area].Archipelago;
                if (area == null) continue;

                foreach (var text in slot.DebugText)
                {
                    var itemKey = text.Split(" - ")[0];
                    itemNameToSlots.TryAdd(itemKey, new());
                    itemNameToSlots[itemKey].Add(slot);

                    var locationKey = (area, itemKey);
                    locationToSlots.TryAdd(locationKey, new());
                    locationToSlots[locationKey].Enqueue(slot);
                }
            }

            var locationToCounts =
                locationToSlots.ToDictionary(pair => pair.Key, pair => pair.Value.Count);

            var scopesToLocations = new Dictionary<LocationScope, List<ScoutedItemInfo>>();
            var result = new Dictionary<long, LocationScope>();

            // Adds [location] and [slot] to [result]. Throws an error if [slot] has been added for
            // more locations than it has in the static randomizer.
            void add(ScoutedItemInfo location, AnnotationData.SlotAnnotation slot)
            {
                if (!scopesToLocations.TryGetValue(slot.LocationScope, out var locs))
                {
                    locs = new();
                    scopesToLocations[slot.LocationScope] = locs;
                }
                locs.Add(location);

                if (locs.Count > slot.DebugText.Count)
                {
                    var builder = new StringBuilder(
                        $"[BUG] Too many Archipelago locations map to the slot " +
                        $"{slot.LocationScope} ({slot.Text}):\n\n");
                    foreach (var loc in locs)
                    {
                        builder.AppendLine($"* {loc.LocationDisplayName}");
                    }
                    builder.AppendLine();
                    builder.AppendLine("Expected:\n");
                    foreach (var line in slot.DebugText)
                    {
                        builder.AppendLine($"* {line}");
                    }
                    throw new Exception(builder.ToString());
                }

                 result[location.LocationId] = slot.LocationScope;
            }

            foreach (var location in locations)
            {
                if (apIdsToKeys.TryGetValue(location.LocationId, out var key))
                {
                    add(location, ann.SlotsByAnnotationsKey[key]);
                    continue;
                }

                var apName = location.LocationName;
                if (apName == null)
                {
                    throw new Exception(
                        $"Can't find a name for location ID {location.LocationId}. This " +
                        "probably indicates a server bug. Try regenerating your Multiworld.");
                }

                // https://github.com/ArchipelagoMW/Archipelago.MultiClient.Net/issues/83
                apName = apName.Replace("Siegbr��u", "Siegbräu");
                var apKey = ParseArchipelagoLocation(apName);
                var (apRegion, itemName) = apKey;
                if (locationToSlots.TryGetValue(apKey, out var locationSlots))
                {
                    if (locationSlots.TryDequeue(out var slot))
                    {
                        add(location, slot);
                        continue;
                    }
                    else
                    {
                        throw new Exception(
                            $"There are only {locationToCounts[apKey]} locations in the offline " +
                            $"randomizer matching \"{apRegion}: {itemName}\", but there are more " +
                            "in Archipelago.");
                    }
                }

                if (itemNameToSlots.TryGetValue(itemName, out var itemSlots) && itemSlots.Count == 1)
                {
                    add(location, itemSlots.First());
                    continue;
                }

                throw new Exception($"Couldn't find a slot that corresponds to Archipelago location \"{apName}\".");
            }
            return result;
        }

        /// <summary>
        /// Parses a full Archipelago location name into its region code and item name.
        /// </summary>
        private static (string, string) ParseArchipelagoLocation(string locationName)
        {
            var rx = new Regex(@"^([A-Z0-9]+): (.*?)(?: - .*)?$", RegexOptions.Compiled);
            var match = rx.Match(locationName);
            if (!match.Success)
            {
                throw new Exception($"Unknown Archipelago location format \"{locationName}\".");
            }

            return (match.Groups[1].Value, match.Groups[2].Value);
        }

        /// <summary>Sets all weapon stat requirements to 0.</summary>
        private static void RemoveWeaponRequirements(GameData game)
        {
            foreach (var row in game.Params["EquipParamWeapon"].Rows)
            {
                foreach (var stat in new[] { "Strength", "Agility", "Magic", "Faith" })
                {
                    row[$"proper{stat}"].Value = 0;
                }
            }
        }

        /// <summary>Sets all spell stat requirements to 0.</summary>
        private static void RemoveSpellRequirements(GameData game)
        {
            foreach (var row in game.Params["Magic"].Rows)
            {
                row["requirementIntellect"].Value = 0;
                row["requirementFaith"].Value = 0;
            }
        }

        /// <summary>Sets the equip burden of all items to 0.</summary>
        private static void RemoveEquipLoad(GameData game)
        {
            foreach (var type in new ItemType[] {
                ItemType.WEAPON, ItemType.ARMOR, ItemType.RING, ItemType.GOOD
            })
            {
                foreach (var row in game.Param(type).Rows)
                {
                    row["weight"].Value = 0;
                }
            }
        }

        private static readonly Regex ApLocationRe = new(@"^[^:]+: (.*?)( - .*)?$");

        /// <summary>The maximum number of characters in a DS3 item's name.</summary>
        private const int ItemNameLimit = 32;

        /// <summary>
        /// Gets the name of the default item from an Archipelago location name.
        /// </summary>
        private static String ItemNameForLocation(string location)
        {
            var match = ApLocationRe.Match(location);
            if (!match.Success)
            {
                throw new Exception($"Unexpect Archipelago location format \"{location}\"");
            }

            return match.Groups[1].Value;
        }

        /// <summary>
        /// Throws an error if the current DS3 Archipelago version doesn't match the server's
        /// version range.
        /// </summary>
        private static void CheckVersionRange(Dictionary<string, object> slotData)
        {
            if (!slotData.ContainsKey("versions"))
            {
                throw new Exception(
                    "The server's version of the DS3 apworld doesn't include any version " +
                    "information, which means it's not compatible with this static randomizer." +
                    (Version?.IsPreRelease ?? false
                        ? " Make sure you use the apworld that comes with this version to " +
                          "generate the multiworld."
                        : "")
                );
            }
            var range = new SemanticVersioning.Range((string)slotData["versions"]);

            // This should only be the case during development.
            if (Version == null) return;

            // Until we actually make server-side changes for v4, declare ourselves compatible with
            // the 3.x.x branch.
            var compatibleVersion = new SemanticVersioning.Version("3.0.13");
            if (range.IsSatisfied(Version, includePrerelease: true) ||
                range.IsSatisfied(compatibleVersion, includePrerelease: true))
            {
                return;
            }


            throw new Exception(
                $"The server's version of the DS3 apworld supports DS3 AP versions {range}, " +
                $"but this static randomizer is version {Version}."
            );
        }

        /// <summary>
        /// Sets the status text and color on the UI thread. If called from a background thread, it marshals.
        /// If the message ends with "...", it will start blinking.
        /// </summary>
        private void SetStatusText(string message, System.Drawing.Color color = default(System.Drawing.Color))
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetStatusText(message, color)));
            }
            else
            {
                blinkTimer.Stop();
                status.Text = message;
                if (color != default(System.Drawing.Color))
                {
                    status.ForeColor = color;
                }
                if (message.EndsWith("..."))
                {
                    blinkTimer.Start();
                }
                status.Refresh();
            }
        }

        private void ShowFailure(String message)
        {
            SetStatusText(message, System.Drawing.Color.DarkRed);
            Cursor = Cursors.Default;
            foreach (Control control in Controls)
            {
                control.Enabled = true;
            }
        }

        [System.AttributeUsage(System.AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
        public sealed class VersionAttribute : System.Attribute
        {
            public SemanticVersioning.Version Version { get; }
            public VersionAttribute(string version)
            {
                this.Version = version == "" ? null : new SemanticVersioning.Version(version);
            }
        }

        /// <summary>
        /// Timer tick event handler to blink the status text.
        /// </summary>
        private void BlinkTimer_Tick(object sender, EventArgs e)
        {
            status.Text = status.Text.EndsWith("...") ? status.Text.Substring(0, status.Text.Length - 2) : status.Text = status.Text + ".";
        }

        /// <summary>
        /// Whenever the password changes, ensure the "Save Password" checkbox is checked or not to
        /// match.
        /// </summary>
        private void password_TextChanged(object sender, EventArgs e)
        {
            if (password.Text.Length > 0)
            {
                savePasswordLabel.Enabled = true;
                savePasswordCheckbox.Enabled = true;
            }
            else
            {
                savePasswordLabel.Enabled = false;
                savePasswordCheckbox.Enabled = false;
            }
        }
    }
}
