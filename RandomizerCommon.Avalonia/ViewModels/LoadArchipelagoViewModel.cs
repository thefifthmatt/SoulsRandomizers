using Avalonia.Controls;
using RandomizerCommon.Localization;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using static RandomizerCommon.Messages;
using RandomizerCommon.Models;

#if ARCHIPELAGO
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
#endif

namespace RandomizerCommon.ViewModels
{
    public partial class LoadArchipelagoViewModel : ViewModelBase
    {
        [Reactive]
        private string _host = "";
        [Reactive]
        private string _player = "";
        [Reactive]
        private string _password = "";

        [Reactive]
        private bool _working = false;
        [Reactive]
        private string _error = "";

        [Localize]
        private static readonly Text titleText = new Text("Connect to Archipelago", "LoadArchipelagoForm_title");
        public IObservable<string> Title { get; } = AvaloniaMessages.FromText(titleText);
        [Localize]
        private static readonly Text hostText = new Text("Room URL (archipelago.gg:12345)", "LoadArchipelagoForm_hostPlaceholder");
        public IObservable<string> HostPlaceholder { get; } = AvaloniaMessages.FromText(hostText);
        [Localize]
        private static readonly Text playerText = new Text("Player name", "LoadArchipelagoForm_playerPlaceholder");
        public IObservable<string> PlayerPlaceholder { get; } = AvaloniaMessages.FromText(playerText);
        [Localize]
        private static readonly Text passwordText = new Text("Password", "LoadArchipelagoForm_passwordPlaceholder");
        public IObservable<string> PasswordPlaceholder { get; } = AvaloniaMessages.FromText(passwordText);

        [ReactiveCommand]
        public ArchipelagoData Close(ArchipelagoData pass)
        {
            return pass;
        }

        // Errors are not localized for the time being
        private async void SetError(string error)
        {
            Error = error;
            await Modal(MessageBoxManager.GetMessageBoxStandard(
                "Could not load data",
                error,
                ButtonEnum.Ok, Icon.Error, WindowStartupLocation.CenterOwner));
            Working = false;
        }

        [ReactiveCommand]
        public async void Load()
        {
#if ARCHIPELAGO
            if (Working)
            {
                return;
            }
            Error = "";
            Working = true;
            ArchipelagoSession session;
            try
            {
                session = ArchipelagoSessionFactory.CreateSession(Host);
            }
            catch (Exception ex)
            {
                SetError($"Invalid URL: {ex}");
                return;
            }

            using DisposeSession disposeSession = new(session);
            try
            {
                // This seems to cause connection issues in server logs but LoginAsync seems to require it.
                await session.ConnectAsync();
            }
            catch (TaskCanceledException)
            {
                // Clearer error message
                SetError($"Failed to connect: network timeout");
                return;
            }
            catch (Exception ex)
            {
                SetError($"Failed to connect: {ex}");
                return;
            }
            try
            {
                LoginResult result = await session.LoginAsync(
                    "EldenRing",
                    Player,
                    ItemsHandlingFlags.NoItems,
                    tags: ["StaticRandomizer"],
                    password: Password,
                    version: new Version(0, 6, 7),
                    requestSlotData: false);
                if (result is LoginFailure failure)
                {
                    SetError($"Failed to log in: {string.Join(", ", failure.ErrorCodes)}" + string.Join("", failure.Errors.Select(e => "\n- " + e)));
                    return;
                }
            }
            catch (Exception ex)
            {
                SetError($"Failed to connect: {ex}");
                return;
            }
            // Does parallelizing this help/hurt/matter at all?
            Task<Dictionary<string, object>> slotDataTask = session.DataStorage.GetSlotDataAsync();
            Task<Dictionary<long, ScoutedItemInfo>> locationsTask = session.Locations.ScoutLocationsAsync(session.Locations.AllLocations.ToArray());
            Dictionary<string, object> slotData = await slotDataTask;
            Dictionary<long, ScoutedItemInfo> locations = await locationsTask;

            try
            {
                string rangeStr = slotData["versions"].ToString()!;
                SemanticVersioning.Range range = new(rangeStr);
                SemanticVersioning.Version ownVersion = new(Randomizer.EldenApVersion);
                if (!range.IsSatisfied(ownVersion, includePrerelease: true))
                {
                    SetError($"The server's apworld requires client versions {range} but this randomizer is version {ownVersion}");
                    return;
                }
                // Get randomizer inputs
                RandomizerOptionsFile options = ExternalItemPreset.MakeEldenRingOptions(slotData);
                string teamInfo = session.ConnectionInfo.Team > 0 ? $"team {session.ConnectionInfo.Team} " : "";
                ExternalItemPreset externalPreset = ExternalItemPreset.FromArchipelago(
                    $"Randomizing seed {session.RoomState.Seed} with client v{Randomizer.EldenApVersion}, connected to Archipelago v{session.RoomState.Version} as {session.Players.ActivePlayer} in {teamInfo}slot {session.ConnectionInfo.Slot}",
                    session.ConnectionInfo.Slot,
                    slotData,
                    locations);
                string runId = $"{session.RoomState.Seed}-{session.ConnectionInfo.Slot}";
                ClientArchipelagoConfig config = new()
                {
                    Host = Host,
                    Player = Player,
                    Seed = session.RoomState.Seed,
                    Password = string.IsNullOrEmpty(Password) ? null : Password,
                    DeathLinkEnabled = ExternalItemPreset.DeathLinkEnabled(slotData),
                };
                ArchipelagoData model = new(runId, options, externalPreset, config);
                CloseCommand.Execute(model).Subscribe();
            }
            catch (Exception ex)
            {
                SetError($"Failed to load data: {ex}");
            }
            Working = false;
#endif
        }

        private record DisposeSession(ArchipelagoSession Session) : IDisposable
        {
            public void Dispose()
            {
                // Fire and forget is fine. Otherwise this is done when randomizer program is closed.
                Session.Socket.DisconnectAsync();
            }
        }
    }
}
