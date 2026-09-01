using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static RandomizerCommon.ComboTextBox;
using static RandomizerCommon.Messages;
using static RandomizerCommon.ItemPreset;
using PresetTagInfo = RandomizerCommon.AnnotationData.PresetTagInfo;
using PresetItemGroup = RandomizerCommon.AnnotationData.PresetItemGroup;
using AreaSiloType = RandomizerCommon.LocationData.AreaSiloType;

namespace RandomizerCommon
{
    public partial class PresetItemForm : Form
    {
#if DEBUG
        internal static bool DebugOpen = false;
#endif

        public ItemPreset Preset;
        public ItemPreset SavedPreset;

        private readonly AnnotationData.Annotations ann;
        private readonly AnnotationData.PresetInfo presetAnn;
        private readonly Messages messages;
        private readonly Func<RandomizerOptions> getCurrentOptions;

        // UI state
        private PresetEnemyControl norandomItems;
        // Be careful, this can get out of sync
        // private List<PresetEnemyControl> classControls = new List<PresetEnemyControl>();
        // Used for TabIndex ordering within classControls (needed as global var still?)
        private int pageControlCount = 0;
        // Preventing event handlers from triggering each other
        private bool simultaneousUpdate = false;

        // Attempted dirty bit, for intercepting window close
        private bool modified_;
        private bool Modified
        {
            get => modified_;
            set
            {
                modified_ = value;
                applyButton.Enabled = value;
            }
        }

        // From display string to ItemRef
        private readonly Dictionary<string, ItemRef> toInternalItem = new();
        // From ItemRef.Group ?? ItemRef.ID to display name
        private readonly Dictionary<string, string> toDisplayItem = new();
        // From display string to LocationRef
        private readonly Dictionary<string, LocationRef> toInternalLoc = new();
        // From LocationRef.Tag to display name
        private readonly Dictionary<string, string> toDisplayLoc = new();

        // From preset config, for counts and name-checking
        private readonly Dictionary<string, List<PresetTagInfo>> tagLocations = new();
        private readonly Dictionary<string, (int, int)> itemCounts = new();

        // Based on above data, sanitized default preset with builtins still present
        private readonly ItemPreset defaultTemplate;

        // Template for all lists to use
        private readonly List<AutocompleteItem> placementItems = new();
        private readonly List<AutocompleteItem> allItems = new();
        private readonly List<AutocompleteItem> requireItems = new();
        private readonly List<AutocompleteItem> excludeItems = new();
        private readonly List<AutocompleteItem> excludeGlobalItems = new();

        public PresetItemForm(AnnotationData.Annotations ann, AnnotationData.PresetInfo presetAnn, Messages messages, ItemPreset init, Func<RandomizerOptions> getCurrentOptions)
        {
            this.ann = ann;
            this.presetAnn = presetAnn;
            this.messages = messages;
            this.getCurrentOptions = getCurrentOptions;
            InitializeComponent();
            FormMessages.SetFormText(messages, this, FormMessages.GetFormText(this));

            // Set up ComboBox mapping, and internal<->display names
            void mapItem(string display, ItemRef item)
            {
                toInternalItem[display] = item;
                toDisplayItem[item.RefID] = display;
            }
            void mapLoc(string display, LocationRef loc)
            {
                toInternalLoc[display] = loc;
                toDisplayLoc[loc.RefID] = display;
            }
            foreach (PresetTagInfo info in presetAnn.Locations)
            {
                foreach (string tag in info.Tags)
                {
                    Util.AddMulti(tagLocations, tag, info);
                }
            }
            // Preprocess config
            HashSet<string> noplaceItems = new();
            // Item groups: allItems, placementItems
            foreach ((string name, Text text) in ItemGroupNames)
            {
                string display = messages.Get(text);
                string displayCount = "";
                bool place = !NoPlacementItemGroups.Contains(name);
                if (presetAnn.ItemGroups.TryGetValue(name, out PresetItemGroup info))
                {
                    itemCounts[name] = (info.LocationCount, info.UniqueCount);
                    displayCount = $" ({info.LocationCount})";
                    if (!place)
                    {
                        foreach ((string type, List<int> ids) in info.Items)
                        {
                            foreach (int id in ids)
                            {
                                noplaceItems.Add($"{type}:{id}");
                            }
                        }
                    }
                }
                mapItem(display, new ItemRef { Group = name });
                AutocompleteItem auto = new AutocompleteItem(display, display + displayCount);
                allItems.Add(auto);
                if (place)
                {
                    placementItems.Add(auto);
                }
            }
            int groupAllCount = allItems.Count;
            int groupPlaceCount = placementItems.Count;
            // Regular items: allItems, placementItems. Make temporary separate lists so they can be sorted together
            foreach ((string type, var typeItems) in presetAnn.ItemNames)
            {
                if (!presetAnn.ItemCounts.TryGetValue(type, out SortedDictionary<int, int> totals)) totals = new();
                if (!presetAnn.UniqueCounts.TryGetValue(type, out SortedDictionary<int, int> uniques)) uniques = new();
                foreach ((int id, string name) in typeItems)
                {
                    if (!totals.TryGetValue(id, out int count)) count = 1;
                    if (!uniques.TryGetValue(id, out int uniqueCount)) uniqueCount = count;
                    string itemId = $"{type}:{id}";
                    itemCounts[itemId] = (count, uniqueCount);
                    string displayCount = count > 1 ? $" ({count})" : "";
                    mapItem(name, new ItemRef { Name = name, ID = itemId });
                    AutocompleteItem auto = new AutocompleteItem(name, name + displayCount);
                    allItems.Add(auto);
                    if (!noplaceItems.Contains(itemId))
                    {
                        placementItems.Add(auto);
                    }
                }
            }
            allItems.Sort(0, groupAllCount, null);
            allItems.Sort(groupAllCount, allItems.Count - groupAllCount, null);
            placementItems.Sort(0, groupPlaceCount, null);
            placementItems.Sort(groupPlaceCount, placementItems.Count - groupPlaceCount, null);
            foreach ((string name, Text text) in LocationGroupNames)
            {
                string display = messages.Get(text);
                mapLoc(display, new LocationRef { Tag = name });
                string displayCount = "";
                if (tagLocations.TryGetValue(name, out List<PresetTagInfo> locs))
                {
                    // Ignore RealCount for now, as this is calculated based on union/difference per-category
                    int count = locs.Select(l => l.Count).Sum();
                    displayCount = $" ({count})";
                }
                AutocompleteItem auto = new AutocompleteItem(display, display + displayCount);
                excludeItems.Add(auto);
                if (!DlcTags.Contains(name))
                {
                    excludeGlobalItems.Add(auto);
                }
                if (RequirableTags.Contains(name))
                {
                    requireItems.Add(auto);
                }
            }
            excludeItems.Sort();
            excludeGlobalItems.Sort();
            requireItems.Sort();

            defaultTemplate = ann.DefaultPreset;
            ResolveRefs(defaultTemplate);

            Preset = init == null ? GetCurrentDefault() : init.Clone();
            Modified = false;
        }

        private string ToDisplayItem(string val) => val != null && toDisplayItem.TryGetValue(val, out string res) ? res : val;
        private string ToInternalItem(string val) => val != null && toInternalItem.TryGetValue(val, out ItemRef res) ? res.RefID : val;
        private string ToDisplayLoc(string val) => val != null && toDisplayLoc.TryGetValue(val, out string res) ? res : val;
        private string ToInternalLoc(string val) => val != null && toInternalLoc.TryGetValue(val, out LocationRef res) ? res.RefID : val;

        // Preprocess presets to ensure RefID is valid for all items, without needing to dynamically look up and filter in each individual case
        private void ResolveRefs(ItemPreset preset)
        {
            bool deleteItem(ItemRef item)
            {
                if (item.RefID != null)
                {
                    return !toDisplayItem.ContainsKey(item.RefID);
                }
                if (item.Name != null && toInternalItem.TryGetValue(item.Name, out ItemRef other))
                {
                    item.ID = other.ID;
                    return false;
                }
                return true;
            }
            preset.NorandomItems.RemoveAll(deleteItem);
            preset.Placements.ForEach(p =>
            {
                p.Items.RemoveAll(deleteItem);
            });
        }

        private ItemPreset GetCurrentDefault()
        {
            ItemPreset processed = ann.DefaultPreset.ProcessBuiltins(getCurrentOptions());
            // TODO: Also perhaps resolve things and error check, but this requires expanding itemAnn etc
            return processed;
        }

        private void PresetItemForm_Load(object sender, EventArgs e)
        {
            tabControl.Appearance = TabAppearance.FlatButtons;
            tabControl.ItemSize = new Size(0, 1);
            tabControl.SizeMode = TabSizeMode.Fixed;
            tabControl.TabStop = false;

            LoadPreset(Preset);
        }

        private void applyButton_Click(object sender, EventArgs e)
        {
            try
            {
                SaveUI();
                Preset.SavePreset();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    messages.Get(FormText.SaveFailureText, FileName, ex), messages.Get(FormText.ErrorTitleText),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            SavedPreset = Preset.Clone();
            Modified = false;
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            applyButton_Click(sender, e);
            // TODO: Unconditional close seems bad?
            Close();
        }

        private void resetButton_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                messages.Get(FormText.ConfirmResetPresetText), messages.Get(FormText.ConfirmTitleText), MessageBoxButtons.OKCancel);
            if (result != DialogResult.OK)
            {
                return;
            }
            ItemPreset replace = GetCurrentDefault();
            Modified = true;
            LoadPreset(replace);
        }

        private void PresetItemForm_FormClosing(object sender, FormClosingEventArgs e)
        {
#if DEBUG
            if (DebugOpen)
            {
                Application.Exit();
                return;
            }
#endif
            if (!ContinueAfterSave())
            {
                e.Cancel = true;
            }
        }

        private bool ContinueAfterSave()
        {
            if (Modified)
            {
                DialogResult result = MessageBox.Show(
                    messages.Get(FormText.ConfirmLeaveText), messages.Get(FormText.ConfirmTitleText), MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.Yes)
                {
                    try
                    {
                        SaveUI();
                        Preset.SavePreset();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            messages.Get(FormText.SaveFailureText, FileName, ex), messages.Get(FormText.ErrorTitleText),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                    SavedPreset = Preset;
                    // In theory, immediately exiting, but just in case
                    Modified = false;
                }
                else if (result == DialogResult.No)
                {
                    // Nothing to do
                }
                else
                {
                    return false;
                }
            }
            return true;
        }


        private enum SiloPanelType
        {
            Overall,
            KeyItem
        }
        private readonly Dictionary<(SiloPanelType, AreaSiloType), RadioButton> siloButtons = new();

        private void LoadPreset(ItemPreset preset)
        {
            InitializeTabs();

            Preset = preset;
            simultaneousUpdate = true;
            // TODO: Initialize silos from values in preset (check/uncheck)

            if (siloButtons.Count == 0)
            {
                void addSiloGroup(SiloPanelType type, Text labelText, bool flowBreak)
                {
                    FlowLayoutPanel siloPanel = new FlowLayoutPanel() { AutoSize = true };
                    Label label = new Label { Text = messages.Get(labelText), AutoSize = true };
                    label.Margin = new Padding(0, 4, 2, 0);
                    siloPanel.Controls.Add(label);
                    void addButton(AreaSiloType areaType)
                    {
                        RadioButton radio = new RadioButton { AutoSize = true, Text = messages.Get(AreaSiloTypeNames[areaType]) };
                        radio.CheckedChanged += silo_CheckChanged;
                        siloPanel.Controls.Add(radio);
                        siloButtons[(type, areaType)] = radio;
                    }
                    addButton(AreaSiloType.None);
                    addButton(AreaSiloType.DLC);
                    globalPanel.Controls.Add(siloPanel);
                    if (flowBreak) globalPanel.SetFlowBreak(siloPanel, true);
                }
                addSiloGroup(SiloPanelType.Overall, OverallSiloText, true);
                addSiloGroup(SiloPanelType.KeyItem, KeyItemSiloText, false);
            }
            RandomizerOptions opt = getCurrentOptions();
            if (opt["dlc"] && !opt["dlcsilo"])
            {
                // Any silos can be selected freely here, set up the current state
                UpdateSilos(opt["dlcstart"]);
            }
            else
            {
                bool forceSilo = opt["dlc"] && opt["dlcsilo"];
                foreach (((SiloPanelType panelType, AreaSiloType siloType), RadioButton radio) in siloButtons)
                {
                    if ((siloType == AreaSiloType.DLC) == forceSilo)
                    {
                        radio.Checked = true;
                    }
                    else
                    {
                        radio.Enabled = false;
                    }
                }
            }

            simultaneousUpdate = false;

            if (norandomItems != null) RemoveEntryInput(globalPanel, norandomItems);
            norandomItems = AddEntryInput(globalPanel, NorandomItemsText, InputType.Item, null, preset.NorandomItems, defaultTemplate.NorandomItems, 1);

            if (classesView.Nodes.Count > 0)
            {
                // TODO: Initialize first class
                simultaneousUpdate = true;
                // currentClass = ann.Classes[0].Class;

                classesView.SelectedNode = classesView.Nodes[0];
                simultaneousUpdate = false;
                // This has its own simultaneousUpdate
                // It would nice to do viewmodel updates in a coherent way

                // InitializeClass(currentClass);
            }
        }

        private void silo_CheckChanged(object sender, EventArgs e)
        {
            if (simultaneousUpdate) return;
            Modified = true;
            // Overall
            bool oldDlc = Preset.OverallSilo == AreaSiloType.DLC;
            Preset.OverallSilo = siloButtons[(SiloPanelType.Overall, AreaSiloType.DLC)].Checked ? AreaSiloType.DLC : AreaSiloType.None;
            bool newDlc = Preset.OverallSilo == AreaSiloType.DLC;
            // Key
            Preset.KeyItemSilo = siloButtons[(SiloPanelType.KeyItem, AreaSiloType.DLC)].Checked ? AreaSiloType.DLC : AreaSiloType.None;
            RandomizerOptions opt = getCurrentOptions();
            bool dlcKeySilo = opt["dlcstart"];
            if (oldDlc && !newDlc)
            {
                // This logic is awful. Need to actually make truth tables when adding regionlocking
                // This restores the original setting since it was not previously editable.
                Preset.KeyItemSilo = Preset.OverallSilo;
            }
            if (dlcKeySilo)
            {
                Preset.KeyItemSilo = AreaSiloType.DLC;
            }
            // Also awful
            simultaneousUpdate = true;
            UpdateSilos(dlcKeySilo);
            simultaneousUpdate = false;
        }

        private void UpdateSilos(bool dlcKeySilo)
        {
            if (Preset.OverallSilo == AreaSiloType.DLC)
            {
                Preset.KeyItemSilo = Preset.OverallSilo;
            }
            foreach (((SiloPanelType panelType, AreaSiloType siloType), RadioButton radio) in siloButtons)
            {
                if (panelType == SiloPanelType.Overall)
                {
                    radio.Checked = Preset.OverallSilo == siloType;
                }
                else
                {
                    if (siloType == AreaSiloType.None)
                    {
                        radio.Enabled = Preset.OverallSilo == AreaSiloType.None && !dlcKeySilo;
                    }
                    radio.Checked = Preset.KeyItemSilo == siloType;
                }
            }
        }

        // Don't access before InitializeTabs
        private List<PageControls> Pages { get; set; }
        private class PageControls
        {
            public string Name { get; set; }
            public PageKey Key { get; set; }
            public TabPage Tab { get; set; }
            public FlowLayoutPanel Panel { get; set; }
            public PresetEnemyControl Items { get; set; }
            public PresetEnemyControl Requires { get; set; }
            public PresetEnemyControl Excludes { get; set; }
            public Dictionary<PlacementMode, RadioButton> Modes { get; set; } = new();
            public LinkLabel InfoLabel { get; set; }

            // Kept up to date. This is null when there are no required locations
            public List<PresetTagInfo> Locations { get; set; }

            // Static metadata
            public PlacementMeta Meta { get; set; }
            // This can be split into subclasses if needed. These have all switchs still intact, so don't depend on current options
            public Placement Default { get; set; }
            public LocationGroup DefaultLoc { get; set; }
        }
        private record PageKey(TabType Tab, PlacementType Placement = PlacementType.Unspecified)
        {
            public static PageKey For(PlacementType type) => new PageKey(TabType.Placement, type);
        }
        private PageKey CurrentPage;

        // private PageControls GetPage(PlacementType type) => GetPage(PageKey.For(type));
        private PageControls GetPage(PageKey key) => Pages.Find(p => p.Key.Equals(key));

        private enum TabType
        {
            // Don't recognize parents for now, clicking them does nothing
            Placement,
            Important
        }

        [Localize]
        private static readonly Text parentPlacementsText = new Text("Item placements", "PresetItemForm_parentPlacements");
        [Localize]
        private static readonly Text parentLocationsText = new Text("Location groups", "PresetItemForm_parentLocations");

        private void InitializeTabs()
        {
            // Initialize tree view
            classesView.BeginUpdate();
            classesView.Nodes.Clear();

            Pages = new();
            tabControl.Controls.Clear();
            PageControls addClass(TreeNodeCollection parent, PageKey key, Text text)
            {
                if (GetPage(key) != null) return GetPage(key);
                string name = messages.Get(text);
                TreeNode node = new TreeNode(name);
                node.Tag = key;
                parent.Add(node);
                TabPage tabPage = new();
                tabControl.Controls.Add(tabPage);
                tabControl.Text = name;
                PageControls page = new()
                {
                    Name = name,
                    Key = key,
                    Tab = tabPage,
                };
                Pages.Add(page);
                return page;
            }
            TreeNode placementParent = new TreeNode(messages.Get(parentPlacementsText));
            classesView.Nodes.Add(placementParent);
            TreeNode locationParent = new TreeNode(messages.Get(parentLocationsText));
            classesView.Nodes.Add(locationParent);
            // TODO eventually use preset itself to generate this, but right now all presets are subset of default. Assume it's sane here
            ItemPreset def = ann.DefaultPreset;
            foreach (Placement place in def.Placements)
            {
                // TODO: Actual text
                PageControls page = addClass(placementParent.Nodes, PageKey.For(place.Type), PlacementTypeNames[place.Type]);
                page.Meta = PlacementMetas[place.Type];
                page.Default = place;
            }
            {
                PageControls page = addClass(locationParent.Nodes, new PageKey(TabType.Important), PlacementModeNames[PlacementMode.Important]);
                page.Meta = ImportantMeta;
                page.DefaultLoc = def.ImportantGroup;
            }

            classesView.ExpandAll();
            classesView.EndUpdate();
            // LoadPreset will then set tab state
        }

        private void classesView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            // Avoid SavePool accidentally using a currentClass of empty
            // TODO: ?
            if (simultaneousUpdate) return;
            UpdatePool("tab_prechange", null);
            // Can happen with parents
            if (e.Node.Tag is not PageKey key) return;
            InitializeClass(key);
        }

        private void SaveUI()
        {
            UpdatePool("save", null);
            // TODO: Save silo, not-randomized items, anything else
            // Remove Requires/Excludes as required by the mode
        }

        private void InitializeClass(PageKey key)
        {
            PageControls page = GetPage(key);
            if (page == null) return;
            simultaneousUpdate = true;
            CurrentPage = key;
            // Create the tab. Maybe the individual page should be its own control, but this would be quite a lot of coordination
            if (page.Tab.Controls.Count == 0)
            {
                FlowLayoutPanel pagePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };
                page.Panel = pagePanel;
                page.Tab.Controls.Add(pagePanel);
                void addLineBreak(FlowLayoutPanel p)
                {
                    Label dummyLabel = new Label { Width = 0, Height = 0, Margin = new Padding(0), Padding = new Padding(0) };
                    p.Controls.Add(dummyLabel);
                    p.SetFlowBreak(dummyLabel, true);
                }

                Label title = new Label { Text = page.Name, AutoSize = true };
                title.Margin = new Padding(0, 0, 0, 0);
                title.Font = new Font(title.Font.FontFamily, title.Font.Size * 1.25f, title.Font.Style | FontStyle.Bold);
                pagePanel.Controls.Add(title);
                if (!PlacementTypeDesc.TryGetValue(key.Placement, out Text descText))
                {
                    if (key.Tab == TabType.Important)
                    {
                        descText = ImportantLocationsDesc;
                    }
                }
                if (descText != null)
                {
                    addLineBreak(pagePanel);
                    Label desc = new Label { Text = messages.Get(descText), AutoSize = true };
                    desc.Margin = new Padding(0, 0, 0, 8);
                    pagePanel.Controls.Add(desc);
                }

                pageControlCount = 0;
                // TODO: Need to show items which are not currently enabled, based on what the default contains
                // TODO: Account for missing from given config? Fallback to default?
                List<ItemRef> items = null;
                List<ItemRef> itemsDef = null;
                PlacementMode? optMode = null;
                List<LocationRef> requires = null;
                List<LocationRef> requiresDef = null;
                List<LocationRef> excludes = null;
                List<LocationRef> excludesDef = null;
                if (Preset.GetPlacement(key.Placement) is Placement place)
                {
                    // All placements have items
                    items = place.Items;
                    // All locs have requires and excludes, unless it's not editable and the default doesn't have it
                    if (page.Meta.EditLocs)
                    {
                        optMode = place.Mode;
                    }
                    if (page.Meta.EditLocs || page.Default.Requires != null)
                    {
                        requires = place.Requires ?? new();
                    }
                    if (page.Meta.EditLocs || page.Default.Excludes != null)
                    {
                        excludes = place.Excludes ?? new();
                    }
                    if (defaultTemplate.GetPlacement(key.Placement) is Placement defPlace)
                    {
                        itemsDef = defPlace.Items;
                        requiresDef = defPlace.Requires ?? new();
                        excludesDef = defPlace.Excludes ?? new();
                    }
                }
                else if (key.Tab == TabType.Important)
                {
                    requires = Preset.ImportantGroup.Requires;
                    excludes = Preset.ImportantGroup.Excludes;
                    requiresDef = defaultTemplate.ImportantGroup.Requires;
                    excludesDef = defaultTemplate.ImportantGroup.Excludes;
                }
                if (items != null)
                {
                    // foreach (ItemRef item in items) Console.WriteLine($"{item}: {RefID(item)}");
                    page.Items = AddEntryInput(
                        pagePanel, PlacementItemsText, InputType.Item, key, items, itemsDef, pageControlCount++);
                }
                if (optMode is PlacementMode currentMode)
                {
                    addLineBreak(pagePanel);
                    Label locLabel = new Label { Text = messages.Get(PlacementOverallText), AutoSize = true };
                    locLabel.Margin = new Padding(0, 4, 2, 0);
                    pagePanel.Controls.Add(locLabel);
                    // TODO: Handler for changing current mode
                    void addMode(PlacementMode mode)
                    {
                        string text = messages.Get(PlacementModeNames[mode]);
                        RadioButton radio = new RadioButton { AutoSize = true, Text = text, Checked = mode == currentMode };
                        radio.CheckedChanged += placementMode_CheckChanged;
                        page.Modes[mode] = radio;
                        pagePanel.Controls.Add(radio);
                    }
                    addMode(PlacementMode.Important);
                    addMode(PlacementMode.Anywhere);
                    addMode(PlacementMode.Norandom);
                    addMode(PlacementMode.Custom);
                }
                if (requires != null)
                {
                    // List<string> locIds = requires.Select(i => i.RefID).Where(i => i != null).ToList();
                    // foreach (LocationRef item in requires) Console.WriteLine($"Req {key.Placement}: {item}: {item.RefID}");
                    page.Requires = AddEntryInput(
                        pagePanel, PlacementRequiresText, InputType.Requires, key, requires, requiresDef, pageControlCount++);
                }
                if (excludes != null)
                {
                    List<string> locIds = excludes.Select(i => i.RefID).Where(i => i != null).ToList();
                    page.Excludes = AddEntryInput(
                        pagePanel, PlacementExcludesText, InputType.Excludes, key, excludes, excludesDef, pageControlCount++);
                }
                if (requires != null)
                {
                    addLineBreak(pagePanel);
                    LinkLabel infoLabel = new LinkLabel { AutoSize = true, Text = "" };
                    // infoLabel.Links.Clear();
                    infoLabel.Margin = new Padding(0, 10, 0, 0);
                    infoLabel.LinkColor = Color.MediumBlue;
                    infoLabel.LinkBehavior = LinkBehavior.HoverUnderline;
                    pagePanel.Controls.Add(infoLabel);
                    page.InfoLabel = infoLabel;
                    page.InfoLabel.LinkClicked += InfoLabel_LinkClicked;
                }
                UpdatePlacementMode(batch: true);
                UpdatePool("tab_init", null);
            }
            // This is where to update it based on present contents, not just initialization. Probably not necessary as long as there's no cross-category mutations
            tabControl.SelectedTab = page.Tab;
            simultaneousUpdate = false;
        }

        private void placementMode_CheckChanged(object sender, EventArgs e)
        {
            // Console.WriteLine($"Mode update from {sender}, simul:{simultaneousUpdate}");
            if (simultaneousUpdate) return;
            Modified = true;
            UpdatePlacementMode();
        }

        private void UpdatePlacementMode(bool batch = false)
        {
            PageControls page = GetPage(CurrentPage);
            if (Preset.GetPlacement(CurrentPage.Placement) is not Placement place) return;
            // Assume all of them must exist for now
            if (page == null || page.Modes.Count != 4) return;
            PlacementMode newMode = PlacementMode.Unspecified;
            foreach ((PlacementMode mode, RadioButton button) in page.Modes)
            {
                if (button.Checked)
                {
                    place.Mode = newMode = mode;
                    break;
                }
            }
            if (newMode == PlacementMode.Unspecified)
            {
                // Fallback to current I guess
                bool currentUpdate = simultaneousUpdate;
                simultaneousUpdate = true;
                newMode = place.Mode == PlacementMode.Unspecified ? PlacementMode.Anywhere : place.Mode;
                page.Modes[newMode].Checked = true;
                simultaneousUpdate = currentUpdate;
            }
            // Console.WriteLine($"Update mode for {CurrentPage} to {newMode}. Requires:{page.Requires?.Tags.Count} Excludes:{page.Excludes?.Tags.Count}");
            if (newMode == PlacementMode.Custom)
            {
                page.Requires?.SetVisible(true);
                page.Excludes?.SetVisible(true);
            }
            else if (newMode == PlacementMode.Anywhere)
            {
                page.Requires?.SetVisible(false);
                page.Excludes?.SetVisible(true);
            }
            else
            {
                page.Requires?.SetVisible(false);
                page.Excludes?.SetVisible(false);
            }
            if (!batch)
            {
                UpdatePool("mode_change", null);
            }
            // Console.WriteLine($"After: {string.Join("; ", page.Panel.Controls.Cast<Control>().Select((c, i) => $"{i}:{c}"))}");
        }

        private void UpdatePool(string change, PresetEnemyControl modified)
        {
            // This happens when modifying anything, as opposed to just switching
            // Also updates stats
            if (modified != null) modified.Modified = true;
            PageControls page = GetPage(CurrentPage);
            bool modifiedItems = false;
            bool modifiedLocs = false;
            // Handling of different modes:
            // Norandom and Anywhere: Nothing
            // Important: Compare items and important locations
            // Custom: 
            bool force = modified == null;
            if (norandomItems.Modified || force)
            {
                Preset.NorandomItems = EntryToInternalList(norandomItems, toInternalItem);
            }
            if (page != null)
            {
                if (page.Items != null && (page.Items.Modified || force))
                {
                    if (Preset.GetPlacement(CurrentPage.Placement) is Placement place)
                    {
                        place.Items = EntryToInternalList(page.Items, toInternalItem);
                    }
                    modifiedItems = true;
                    page.Items.Modified = false;
                }
                if (page.Requires != null && (page.Requires.Modified || force))
                {
                    if (Preset.GetPlacement(CurrentPage.Placement) is Placement place)
                    {
                        place.Requires = EntryToInternalList(page.Requires, toInternalLoc);
                    }
                    else if (CurrentPage.Tab == TabType.Important)
                    {
                        Preset.ImportantGroup.Requires = EntryToInternalList(page.Requires, toInternalLoc);
                    }
                    modifiedLocs = true;
                    page.Requires.Modified = false;
                }
                if (page.Excludes != null && (page.Excludes.Modified || force))
                {
                    if (Preset.GetPlacement(CurrentPage.Placement) is Placement place)
                    {
                        place.Excludes = EntryToInternalList(page.Excludes, toInternalLoc);
                    }
                    else if (CurrentPage.Tab == TabType.Important)
                    {
                        Preset.ImportantGroup.Excludes = EntryToInternalList(page.Excludes, toInternalLoc);
                    }
                    modifiedLocs = true;
                    page.Excludes.Modified = false;
                }
            }
            // Attempt to update stats by previewing count. This amounts to filtering tag infos
            if (page != null && page.InfoLabel != null && modifiedLocs)
            {
                List<LocationRef> statRequires = null;
                List<LocationRef> statExcludes = null;
                if (Preset.GetPlacement(CurrentPage.Placement) is Placement place)
                {
                    if (place.Mode == PlacementMode.Custom)
                    {
                        statRequires = place.Requires;
                        statExcludes = place.Excludes;
                    }
                }
                else if (CurrentPage.Tab == TabType.Important)
                {
                    statRequires = Preset.ImportantGroup.Requires;
                    statExcludes = Preset.ImportantGroup.Excludes;
                }
                if (statRequires != null && statExcludes != null && statRequires.Count > 0)
                {
                    HashSet<string> requireTags = new(statRequires.Select(i => i.RefID));
                    HashSet<string> excludeTags = new(statExcludes.Select(i => i.RefID));
                    page.Locations = new();
                    int count = 0;
                    foreach (PresetTagInfo info in presetAnn.Locations)
                    {
                        if (info.Tags.Intersect(requireTags).Any() && !info.Tags.Intersect(excludeTags).Any())
                        {
                            page.Locations.Add(info);
                            count += info.Count;
                        }
                    }
                    string initText = messages.Get(listLocations, count);
                    bool required = CurrentPage.Placement == PlacementType.KeyItems || CurrentPage.Tab == TabType.Important;
                    page.InfoLabel.Text = $"{initText}\n{messages.Get(required ? listLocationsImportant : listLocationsRegular)}";
                    // Not enough to mutate the link, have to remove/add it, or else the text gets cut off
                    page.InfoLabel.Links.Clear();
                    page.InfoLabel.Links.Add(0, initText.Length);
                }
                else
                {
                    page.Locations = null;
                    page.InfoLabel.Text = "";
                }
            }
            // Console.WriteLine($"Received {change} for {CurrentPage}. items:{modifiedItems} locations:{modifiedLocs} force:{force}");
        }

        private void InfoLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            PageControls page = GetPage(CurrentPage);
            if (page?.Locations == null) return;
            SortedSet<(int, string)> entries = new();
            foreach (PresetTagInfo info in page.Locations)
            {
                if (info.Locations == null) continue;
                entries.UnionWith(info.Locations.Select(e => (e.Value, e.Key + "\n")));
            }
            string text = string.Join("", entries.Select(e => e.Item2));
            SearchScrollForm.Show(this, messages, text, page.Name);
        }

        [Localize]
        private static readonly Text listLocations = new Text("View all {0} selected locations", "PresetItemForm_listLocations");
        [Localize]
        private static readonly Text listLocationsRegular = new Text("If there are significantly more items than available locations, some items may be randomly placed elsewhere.", "PresetItemForm_listLocationsRegular");
        [Localize]
        private static readonly Text listLocationsImportant = new Text("If there are significantly more items than available locations, randomization will fail.", "PresetItemForm_listLocationsImportant");

        private enum InputType
        {
            Item, Requires, Excludes
        }

        // This being generic is a bit weird but it's enough to implement the logic
        private PresetEnemyControl AddEntryInput<T>(
            FlowLayoutPanel panel, Text label, InputType inputType, PageKey key, List<T> inits, List<T> defs, int tabIndex)
            where T : SwitchRef
        {
            bool allowEdit = true;
            bool includeDlc = true;
            PageControls page = GetPage(key);
            if (page?.Meta is PlacementMeta meta)
            {
                allowEdit = inputType == InputType.Item ? meta.EditItems : meta.EditLocs;
                includeDlc = !meta.GlobalLocs;
            }
            // Is it okay to not defensively copy these? I hope so
            List<AutocompleteItem> opts = inputType switch
            {
                // Lack of key means global norandom for now
                InputType.Item => key == null ? allItems : placementItems,
                InputType.Requires => requireItems,
                InputType.Excludes => includeDlc ? excludeItems : excludeGlobalItems,
                _ => new()
            };
            PresetEnemyControl input = new PresetEnemyControl(
                label == null ? "" : messages.Get(label),
                opts,
                tabIndex,
                editable: allowEdit);
            panel.SuspendLayout();
            panel.Controls.Add(input);
            // Remove and Percent are not set, so don't add them
            panel.Controls.Add(input.Pre);
            if (input.Selector != null)
            {
                panel.Controls.Add(input.Selector);
                input.Selector.SelectedValueChanged += (sender, e) =>
                {
                    AddEntry(panel, input, interactive: true);
                    Modified = true;
                };
            }
            panel.SetFlowBreak(input, true);
            panel.ResumeLayout();
            // Now, calculate how to populate this. Always include defaults.
            List<string> ids = defs.Concat(inits).Select(i => i.RefID).Distinct().ToList();
            if (ids.Count > 0)
            {
                RandomizerOptions opt = getCurrentOptions();
                foreach (string id in ids)
                {
                    T init = inits.Find(i => i.RefID == id);
                    T def = defs.Find(i => i.RefID == id);
                    T obj = init ?? def;
                    LabelTag tag = new();
                    // Casting is a bit awkward but whatever
                    if (obj is ItemRef item) tag.Text = ToDisplayItem(item.RefID);
                    else if (obj is LocationRef loc) tag.Text = ToDisplayLoc(loc.RefID);
                    else continue;
                    tag.Enabled = init != null;
                    tag.Removable = def == null;
                    if (def != null)
                    {
                        if (def.IsRequired(opt))
                        {
                            tag.Enabled = true;
                            tag.Editable = false;
                        }
                        else if (def.IsForbidden(opt))
                        {
                            tag.Enabled = false;
                            tag.Editable = false;
                        }
                    }
                    // Console.WriteLine($"For {id} have init {init}, def {def}. Tag {tag.Text}: enabled {tag.Enabled} removable {tag.Removable} editable {tag.Editable}");
                    if (!allowEdit || opts.Any(i => i.Value == tag.Text))
                    {
                        AddEntry(panel, input, tag, batch: true);
                    }
                }
                // This is moved up another level to tab creation
                // UpdateStats(input);
            }
            return input;
        }

        // Used only for the top part. All other tabs will just be removed
        private void RemoveEntryInput(FlowLayoutPanel panel, PresetEnemyControl input)
        {
            panel.SuspendLayout();
            panel.Controls.Remove(input.Pre);
            if (input.Selector != null) panel.Controls.Remove(input.Selector);
            if (input.Remove != null) panel.Controls.Remove(input.Remove);
            if (input.Percent != null) panel.Controls.Remove(input.Percent);
            foreach (LinkLabel tag in input.Tags) panel.Controls.Remove(tag);
            input.Tip.RemoveAll();
            panel.Controls.Remove(input);
            panel.ResumeLayout();
        }

        // The pure syntax way was label.Text.TrimEnd('⨉', ' ') - maybe more info can be added to tag later
        private static string TagText(LinkLabel label) => ((LabelTag)label.Tag).Text;
        private class LabelTag
        {
            public string Text { get; set; }
            // This should not change while a label exists (options assumed to be the same)
            public bool Editable { get; set; } = true;
            // Also should not change, anything listed in default config
            public bool Removable { get; set; } = true;
            // Can change when clicked
            public bool Enabled { get; set; } = true;
        }

        private void UpdateLabelAppearance(LinkLabel label)
        {
            // Respond to changes in Enabled. This is also done at the start before setting up links.
            LabelTag tag = (LabelTag)label.Tag;
            if (tag.Enabled)
            {
                label.Font = new Font(label.Font, label.Font.Style & ~FontStyle.Strikeout);
                label.ForeColor = DefaultForeColor;
            }
            else
            {
                label.Font = new Font(label.Font, label.Font.Style | FontStyle.Strikeout);
                label.ForeColor = Color.Gray;
            }
            if (tag.Editable)
            {
                label.Text = tag.Text + (tag.Enabled ? "⨉" : "＋");
            }
            else
            {
                label.Text = tag.Text;
            }
        }

        private List<T> EntryToInternalList<T>(PresetEnemyControl input, Dictionary<string, T> toInternal)
        {
            if (input == null) return null;
            List<T> ret = new();
            foreach (LinkLabel tagLabel in input.Tags)
            {
                LabelTag tag = (LabelTag)tagLabel.Tag;
                if (!tag.Enabled) continue;
                if (string.IsNullOrWhiteSpace(tag.Text) || !toInternal.TryGetValue(tag.Text, out T value)) continue;
                ret.Add(value);
            }
            return ret;
        }

        private void AddEntry(
            FlowLayoutPanel panel, PresetEnemyControl input, LabelTag tag = null,
            // Way too many parameters
            bool interactive = false, bool batch = false)
        {
            tag ??= new();
            if (tag.Text == null)
            {
                // Autoselect from adjacent dropdown
                // if (enemy.Selector.SelectedIndex == 0 || !(enemy.Selector.SelectedValue is string value))
                if (string.IsNullOrEmpty(input.Selector.SelectedValue))
                {
                    return;
                }
                // Assume this value is legitimate. It may be silently filtered during save if not
                // if (string.IsNullOrWhiteSpace(value)) return;
                tag.Text = input.Selector.SelectedValue;
                // enemy.Selector.SelectedIndex = 0;
                // enemy.Selector.Text = "";
            }
            tag.Editable &= input.Editable;
            // Have to count weirdly since Selector may be missing
            int preIndex = panel.Controls.IndexOf(input.Pre);
            if (preIndex == -1) return;
            int panelIndex = preIndex;
            panelIndex++;
            if (input.Selector != null) panelIndex++;
            int linkIndex = 0;
            while (panelIndex < panel.Controls.Count)
            {
                Control nextCon = panel.Controls[panelIndex];
                if (nextCon is LinkLabel existLabel)
                {
                    LabelTag existTag = existLabel.Tag as LabelTag;
                    if (tag.Text == existTag?.Text)
                    {
                        // Special case
                        if (!existTag.Enabled && existTag.Editable)
                        {
                            existTag.Enabled = true;
                            input.Modified = true;
                            UpdateLabelAppearance(existLabel);
                            if (!batch)
                            {
                                UpdatePool("entry_addenable", input);
                            }
                        }
                        return;
                    }
                }
                else break;
                linkIndex++;
                panelIndex++;
            }
            // string panelName(int index) => $"{index}:" + (index < panel.Controls.Count ? panel.Controls[index].ToString() : "(end)");
            // Console.WriteLine($"Adding {tag.Text}: at {linkIndex} {panelIndex}/{panel.Controls.Count}, currently {panelName(panelIndex)}. First indices are {panelName(preIndex)}, {panelName(preIndex + 1)}, {panelName(preIndex + 2)}");
            // If inacterative, can query things here. Less complicated than enemy groups, though
            LinkLabel label = new LinkLabel();
            label.Tag = tag;
            label.AutoSize = true;
            label.Font = input.Pre.Font;
            // This is awful. Presumably caused by extra character
            label.Margin = new Padding(0, tag.Editable ? 6 : 5, 0, 0);
            label.LinkBehavior = LinkBehavior.NeverUnderline;
            label.TabIndex = input.NewTabIndex();
            UpdateLabelAppearance(label);
            if (tag.Editable)
            {
                label.Links[0].Length = 1;
                label.Links[0].Start = label.Text.Length - 1;
                label.LinkClicked += (sender, e) =>
                {
                    // Flip state
                    if (tag.Enabled)
                    {
                        if (tag.Removable)
                        {
                            RemoveEntry(panel, input, label);
                        }
                        else
                        {
                            tag.Enabled = false;
                            UpdateLabelAppearance(label);
                            UpdatePool("entry_disable", input);
                        }
                    }
                    else
                    {
                        tag.Enabled = true;
                        UpdateLabelAppearance(label);
                        UpdatePool("entry_enable", input);
                    }
                    Modified = true;
                };
            }
            else
            {
                label.Links.Clear();
            }
            // Very functional, thanks
            panel.SuspendLayout();
            panel.Controls.Add(label);
            panel.Controls.SetChildIndex(label, panelIndex);
            input.Tags.Add(label);
            panel.ResumeLayout();
            input.Modified = true;
            if (!batch)
            {
                UpdatePool("entry_add", input);
            }
        }

        private void RemoveEntry(FlowLayoutPanel panel, PresetEnemyControl input, LinkLabel label, bool batch = false)
        {
            if (!panel.Controls.Contains(label)) return;
            input.Tags.Remove(label);
            panel.Controls.Remove(label);
            input.Modified = true;
            if (!batch)
            {
                UpdatePool("entry_remove", input);
            }
        }

        private void ToolTipThing(PresetEnemyControl input = null)
        {
            // input.Tip.RemoveAll();
            // input.Tip.SetToolTip(label, "plink");
            if (input != null) return;
            // Can iterate through enemy.Tags to check for consistency, once things start getting more complicated
        }
    }
}
