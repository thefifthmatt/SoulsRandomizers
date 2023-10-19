using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Packets;
using Microsoft.VisualBasic.ApplicationServices;
using SoulsIds;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Util;
using static SoulsIds.GameSpec;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace RandomizerCommon
{
    public partial class ArchipelagoForm : Form
    {
        public ArchipelagoForm()
        {
            InitializeComponent();
        }

        private void submit_Click(object sender, EventArgs e)
        {
            Enabled = false;
            status.ForeColor = System.Drawing.SystemColors.GrayText;
            status.Text = "Connecting...";
            status.Visible = true;

            if (url.Text.Length == 0)
            {
                showFailure("Missing Archipelago URL");
                return;
            }

            if (name.Text.Length == 0)
            {
                showFailure("Missing player name");
                return;
            }

            var session = ArchipelagoSessionFactory.CreateSession(url.Text);

            LoginResult result;
            try
            {
                result = session.TryConnectAndLogin(
                    "Dark Souls III",
                    name.Text,
                    Archipelago.MultiClient.Net.Enums.ItemsHandlingFlags.NoItems,
                    password: password.Text.Length == 0 ? null : password.Text,
                    version: new Version(0, 4, 3),
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
                showFailure(errorMessage);
                return;
            }

            status.Text = "Downloading item data...";
            var locations = session.Locations.ScoutLocationsAsync(session.Locations.AllLocations.ToArray()).Result;

            status.Text = "Loading game data...";

            var opt = new RandomizerOptions(FromGame.DS3);
            var distDir = "dist";
            if (!Directory.Exists(distDir))
            {
                // From Release/Debug dirs
                distDir = $@"..\..\..\{distDir}";
                opt["dryrun"] = true;
            }
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
            var writer = new PermutationWriter(game, data, ann, events, null);
            var permutation = new Permutation(game, data, ann, new Messages(null));

            var random = new Random(session.RoomState.Seed.GetHashCode());

            // A map from locations in the game where items can appear to the list of items that
            // should appear in those locations.
            var items = new Dictionary<SlotKey, List<SlotKey>>();

            // A map from items in the game that should be removed to locations where those items
            // would normally appear, or null if those items should remain in-game (likely because
            // they're assigned elsewhere).
            var itemsToRemove = new Dictionary<SlotKey, SlotKey>();

            foreach (var info in locations.Locations)
            {
                var locationName = session.Locations.GetLocationNameFromId(info.Location);
                var targetSlotKey = ann.GetArchipelagoLocation(locationName);

                // Tentatively mark all items in this location as not being in the game, unless
                // we've already seen them or we see them later.
                var locationScope = ann.SlotsByArchipelagoName[locationName].LocationScope;
                foreach (var itemInLocation in data.Locations[locationScope])
                {
                    itemsToRemove.TryAdd(itemInLocation, targetSlotKey);
                }

                var itemName = session.Items.GetItemName(info.Item);
                if (info.Player != session.ConnectionInfo.Slot)
                {
                    var player = session.Players.Players[session.ConnectionInfo.Team]
                        .First(player => player.Slot == info.Player);
                    // Create a fake key item for each item from another world.
                    AddMulti(items, targetSlotKey, writer.AddSyntheticItem(
                        $"{player.Alias}'s {itemName}",
                        $"{IndefiniteArticle(itemName)} from a mysterious world known only as \"{player.Game}\".",
                        archipelagoLocationId: info.Location));
                }
                else
                {
                    // TODO: Give Archipelago a way to inject multiple copies of the same item to a
                    // given location. Maybe support an itemName like "Firebomb x3" and parse it
                    // here or in GetArchipelagoItem.
                    var slotKey = ann.GetArchipelagoItem(itemName);
                    // TODO: Once Archipelago supports items in shops, we shouldn't remove those
                    // because they'll be directly added to the player's inventory without a chance
                    // for replacement.
                    itemsToRemove[slotKey] = targetSlotKey;
                    AddMulti(items, targetSlotKey, writer.AddSyntheticItem(
                        $"[Placeholder] {itemName}",
                        "If you can see this your Archipelago mod isn't working.",
                        archipelagoLocationId: info.Location,
                        replaceWithInArchipelago: slotKey.Item));
                }
            }

            status.Text = "Randomizing locations...";

            permutation.Forced(items,
                remove: itemsToRemove
                    .Where(entry => entry.Value != null)
                    .GroupBy(entry => entry.Value)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(entry => entry.Key).ToList()));

            writer.Write(random, permutation, opt);

            MiscSetup.DS3CommonPass(game, events, opt);

            status.Text = "Writing game files...";
            game.SaveDS3(Directory.GetCurrentDirectory(), true);

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void showFailure(String message)
        {
            Enabled = true;
            status.ForeColor = Color.DarkRed;
            status.Text = message;
        }
    }
}
