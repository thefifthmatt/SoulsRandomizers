namespace RandomizerCommon
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            dlc2 = new System.Windows.Forms.CheckBox();
            dlc1 = new System.Windows.Forms.CheckBox();
            mergemods = new System.Windows.Forms.CheckBox();
            statusStrip1 = new System.Windows.Forms.StatusStrip();
            statusL = new System.Windows.Forms.ToolStripStatusLabel();
            fixedseed = new System.Windows.Forms.TextBox();
            randomize = new System.Windows.Forms.Button();
            label8 = new System.Windows.Forms.Label();
            warningL = new System.Windows.Forms.Label();
            defaultReroll = new System.Windows.Forms.CheckBox();
            optionwindow = new System.Windows.Forms.Button();
            enemy = new System.Windows.Forms.CheckBox();
            item = new System.Windows.Forms.CheckBox();
            edittext = new System.Windows.Forms.CheckBox();
            enemyPage = new System.Windows.Forms.TabPage();
            defaultRerollEnemy = new System.Windows.Forms.CheckBox();
            label4 = new System.Windows.Forms.Label();
            enemyseed = new System.Windows.Forms.TextBox();
            presetL = new System.Windows.Forms.Label();
            preset = new System.Windows.Forms.Button();
            miscGroup2 = new System.Windows.Forms.GroupBox();
            supermimic = new System.Windows.Forms.CheckBox();
            yhormruler = new System.Windows.Forms.CheckBox();
            chests = new System.Windows.Forms.CheckBox();
            enemyRandomGroup = new System.Windows.Forms.GroupBox();
            reducepassive = new System.Windows.Forms.CheckBox();
            lizards = new System.Windows.Forms.CheckBox();
            mimics = new System.Windows.Forms.CheckBox();
            enemyProgressGroup = new System.Windows.Forms.GroupBox();
            scale = new System.Windows.Forms.CheckBox();
            earlyreq = new System.Windows.Forms.CheckBox();
            itemPage = new System.Windows.Forms.TabPage();
            biasGroup = new System.Windows.Forms.GroupBox();
            difficultyAmtL = new System.Windows.Forms.Label();
            difficultyL = new System.Windows.Forms.Label();
            difficulty = new System.Windows.Forms.TrackBar();
            label14 = new System.Windows.Forms.Label();
            dlcGroup = new System.Windows.Forms.GroupBox();
            earlydlcL = new System.Windows.Forms.Label();
            label3 = new System.Windows.Forms.Label();
            earlydlc = new System.Windows.Forms.CheckBox();
            dlc2fromdlc1 = new System.Windows.Forms.CheckBox();
            lothricGroup = new System.Windows.Forms.GroupBox();
            earlylothricL = new System.Windows.Forms.Label();
            earlylothric = new System.Windows.Forms.RadioButton();
            middancer = new System.Windows.Forms.RadioButton();
            regdancer = new System.Windows.Forms.RadioButton();
            skipsgroup = new System.Windows.Forms.GroupBox();
            label5 = new System.Windows.Forms.Label();
            label7 = new System.Windows.Forms.Label();
            vilhelmskip = new System.Windows.Forms.CheckBox();
            treeskip = new System.Windows.Forms.CheckBox();
            keyGroup = new System.Windows.Forms.GroupBox();
            label13 = new System.Windows.Forms.Label();
            label12 = new System.Windows.Forms.Label();
            label11 = new System.Windows.Forms.Label();
            label10 = new System.Windows.Forms.Label();
            label9 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            label1 = new System.Windows.Forms.Label();
            raceloc_ring = new System.Windows.Forms.CheckBox();
            raceloc_lizard = new System.Windows.Forms.CheckBox();
            raceloc_miniboss = new System.Windows.Forms.CheckBox();
            raceloc_ashes = new System.Windows.Forms.CheckBox();
            raceloc_chest = new System.Windows.Forms.CheckBox();
            defaultD = new System.Windows.Forms.CheckBox();
            defaultC = new System.Windows.Forms.CheckBox();
            racemode = new System.Windows.Forms.RadioButton();
            norandom = new System.Windows.Forms.RadioButton();
            defaultKey = new System.Windows.Forms.RadioButton();
            healthGroup = new System.Windows.Forms.GroupBox();
            racemode_health = new System.Windows.Forms.RadioButton();
            estusprogressionL = new System.Windows.Forms.Label();
            norandom_health = new System.Windows.Forms.RadioButton();
            defaultHealth = new System.Windows.Forms.RadioButton();
            estusprogression = new System.Windows.Forms.CheckBox();
            progressionGroup = new System.Windows.Forms.GroupBox();
            soulsprogressionL = new System.Windows.Forms.Label();
            weaponprogressionL = new System.Windows.Forms.Label();
            soulsprogression = new System.Windows.Forms.CheckBox();
            weaponprogression = new System.Windows.Forms.CheckBox();
            miscGroup = new System.Windows.Forms.GroupBox();
            onehand = new System.Windows.Forms.CheckBox();
            ngplusrings = new System.Windows.Forms.CheckBox();
            tabControl1 = new System.Windows.Forms.TabControl();
            archipelagoButton = new System.Windows.Forms.Button();
            statusStrip1.SuspendLayout();
            enemyPage.SuspendLayout();
            miscGroup2.SuspendLayout();
            enemyRandomGroup.SuspendLayout();
            enemyProgressGroup.SuspendLayout();
            itemPage.SuspendLayout();
            biasGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)difficulty).BeginInit();
            dlcGroup.SuspendLayout();
            lothricGroup.SuspendLayout();
            skipsgroup.SuspendLayout();
            keyGroup.SuspendLayout();
            healthGroup.SuspendLayout();
            progressionGroup.SuspendLayout();
            miscGroup.SuspendLayout();
            tabControl1.SuspendLayout();
            SuspendLayout();
            // 
            // dlc2
            // 
            dlc2.AutoSize = true;
            dlc2.Checked = true;
            dlc2.CheckState = System.Windows.Forms.CheckState.Checked;
            dlc2.Location = new System.Drawing.Point(165, 592);
            dlc2.Name = "dlc2";
            dlc2.Size = new System.Drawing.Size(164, 24);
            dlc2.TabIndex = 4;
            dlc2.Text = "Randomize DLC2";
            dlc2.UseVisualStyleBackColor = true;
            dlc2.CheckedChanged += option_CheckedChanged;
            // 
            // dlc1
            // 
            dlc1.AutoSize = true;
            dlc1.Checked = true;
            dlc1.CheckState = System.Windows.Forms.CheckState.Checked;
            dlc1.Location = new System.Drawing.Point(28, 591);
            dlc1.Name = "dlc1";
            dlc1.Size = new System.Drawing.Size(164, 24);
            dlc1.TabIndex = 3;
            dlc1.Text = "Randomize DLC1";
            dlc1.UseVisualStyleBackColor = true;
            dlc1.CheckedChanged += option_CheckedChanged;
            // 
            // mergemods
            // 
            mergemods.AutoSize = true;
            mergemods.Location = new System.Drawing.Point(28, 610);
            mergemods.Name = "mergemods";
            mergemods.Size = new System.Drawing.Size(334, 24);
            mergemods.TabIndex = 5;
            mergemods.Text = "Merge mods from normal 'mod' directory";
            mergemods.UseVisualStyleBackColor = true;
            mergemods.CheckedChanged += option_CheckedChanged;
            // 
            // statusStrip1
            // 
            statusStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { statusL });
            statusStrip1.Location = new System.Drawing.Point(0, 675);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new System.Drawing.Size(1181, 22);
            statusStrip1.SizingGrip = false;
            statusStrip1.TabIndex = 13;
            statusStrip1.Text = "statusStrip1";
            // 
            // statusL
            // 
            statusL.Name = "statusL";
            statusL.Size = new System.Drawing.Size(0, 16);
            // 
            // fixedseed
            // 
            fixedseed.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            fixedseed.Location = new System.Drawing.Point(641, 589);
            fixedseed.Name = "fixedseed";
            fixedseed.Size = new System.Drawing.Size(188, 30);
            fixedseed.TabIndex = 8;
            fixedseed.TextChanged += fixedseed_TextChanged;
            // 
            // randomize
            // 
            randomize.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            randomize.Location = new System.Drawing.Point(956, 588);
            randomize.Name = "randomize";
            randomize.Size = new System.Drawing.Size(217, 29);
            randomize.TabIndex = 10;
            randomize.Text = "Randomize new run!";
            randomize.UseVisualStyleBackColor = true;
            randomize.Click += randomize_Click;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label8.Location = new System.Drawing.Point(534, 592);
            label8.Name = "label8";
            label8.Size = new System.Drawing.Size(128, 25);
            label8.TabIndex = 7;
            label8.Text = "Overall seed:";
            // 
            // warningL
            // 
            warningL.AutoSize = true;
            warningL.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            warningL.ForeColor = System.Drawing.Color.DarkRed;
            warningL.Location = new System.Drawing.Point(311, 622);
            warningL.Name = "warningL";
            warningL.Size = new System.Drawing.Size(611, 40);
            warningL.TabIndex = 12;
            warningL.Text = "Error Error Error Error Error Error Error Error Error Error Error Error Error Error \r\nError Error Error Error Error Error Error Error Error Error Error Error Error Error";
            warningL.Visible = false;
            // 
            // defaultReroll
            // 
            defaultReroll.AutoSize = true;
            defaultReroll.Checked = true;
            defaultReroll.CheckState = System.Windows.Forms.CheckState.Checked;
            defaultReroll.Enabled = false;
            defaultReroll.Location = new System.Drawing.Point(844, 592);
            defaultReroll.Name = "defaultReroll";
            defaultReroll.Size = new System.Drawing.Size(116, 24);
            defaultReroll.TabIndex = 9;
            defaultReroll.Text = "Reroll seed";
            defaultReroll.UseVisualStyleBackColor = true;
            defaultReroll.CheckedChanged += reroll_CheckedChanged;
            // 
            // optionwindow
            // 
            optionwindow.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            optionwindow.Location = new System.Drawing.Point(956, 622);
            optionwindow.Name = "optionwindow";
            optionwindow.Size = new System.Drawing.Size(217, 29);
            optionwindow.TabIndex = 11;
            optionwindow.Text = "Set options from string...";
            optionwindow.UseVisualStyleBackColor = true;
            optionwindow.Click += optionwindow_Click;
            // 
            // enemy
            // 
            enemy.AutoSize = true;
            enemy.Checked = true;
            enemy.CheckState = System.Windows.Forms.CheckState.Checked;
            enemy.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            enemy.Location = new System.Drawing.Point(201, 22);
            enemy.Name = "enemy";
            enemy.Size = new System.Drawing.Size(18, 17);
            enemy.TabIndex = 1;
            enemy.UseVisualStyleBackColor = true;
            enemy.CheckedChanged += option_CheckedChanged;
            // 
            // item
            // 
            item.AutoSize = true;
            item.Checked = true;
            item.CheckState = System.Windows.Forms.CheckState.Checked;
            item.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            item.Location = new System.Drawing.Point(27, 22);
            item.Name = "item";
            item.Size = new System.Drawing.Size(18, 17);
            item.TabIndex = 0;
            item.UseVisualStyleBackColor = true;
            item.CheckedChanged += option_CheckedChanged;
            // 
            // edittext
            // 
            edittext.AutoSize = true;
            edittext.Checked = true;
            edittext.CheckState = System.Windows.Forms.CheckState.Checked;
            edittext.Location = new System.Drawing.Point(28, 630);
            edittext.Name = "edittext";
            edittext.Size = new System.Drawing.Size(157, 24);
            edittext.TabIndex = 6;
            edittext.Text = "Edit in-game text";
            edittext.UseVisualStyleBackColor = true;
            edittext.CheckedChanged += option_CheckedChanged;
            // 
            // enemyPage
            // 
            enemyPage.BackColor = System.Drawing.SystemColors.Control;
            enemyPage.Controls.Add(defaultRerollEnemy);
            enemyPage.Controls.Add(label4);
            enemyPage.Controls.Add(enemyseed);
            enemyPage.Controls.Add(presetL);
            enemyPage.Controls.Add(preset);
            enemyPage.Controls.Add(miscGroup2);
            enemyPage.Controls.Add(enemyRandomGroup);
            enemyPage.Controls.Add(enemyProgressGroup);
            enemyPage.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            enemyPage.Location = new System.Drawing.Point(4, 38);
            enemyPage.Name = "enemyPage";
            enemyPage.Padding = new System.Windows.Forms.Padding(3);
            enemyPage.Size = new System.Drawing.Size(1148, 531);
            enemyPage.TabIndex = 0;
            enemyPage.Text = "    Enemy Randomizer";
            // 
            // defaultRerollEnemy
            // 
            defaultRerollEnemy.AutoSize = true;
            defaultRerollEnemy.Enabled = false;
            defaultRerollEnemy.Location = new System.Drawing.Point(823, 507);
            defaultRerollEnemy.Name = "defaultRerollEnemy";
            defaultRerollEnemy.Size = new System.Drawing.Size(252, 24);
            defaultRerollEnemy.TabIndex = 30;
            defaultRerollEnemy.Text = "Reroll enemy seed seperately";
            defaultRerollEnemy.UseVisualStyleBackColor = true;
            defaultRerollEnemy.CheckedChanged += reroll_CheckedChanged;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label4.Location = new System.Drawing.Point(444, 507);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(210, 25);
            label4.TabIndex = 28;
            label4.Text = "Separate enemy seed:";
            // 
            // enemyseed
            // 
            enemyseed.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            enemyseed.Location = new System.Drawing.Point(620, 504);
            enemyseed.Name = "enemyseed";
            enemyseed.Size = new System.Drawing.Size(188, 30);
            enemyseed.TabIndex = 29;
            enemyseed.TextChanged += enemyseed_TextChanged;
            // 
            // presetL
            // 
            presetL.AutoSize = true;
            presetL.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            presetL.Location = new System.Drawing.Point(6, 346);
            presetL.Name = "presetL";
            presetL.Size = new System.Drawing.Size(384, 20);
            presetL.TabIndex = 27;
            presetL.Text = "Preset: Oops All Father Ariandel and Sister Friede";
            // 
            // preset
            // 
            preset.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            preset.Location = new System.Drawing.Point(7, 312);
            preset.Name = "preset";
            preset.Size = new System.Drawing.Size(246, 31);
            preset.TabIndex = 26;
            preset.Text = "Select challenge run or preset...";
            preset.UseVisualStyleBackColor = true;
            preset.Click += preset_Click;
            // 
            // miscGroup2
            // 
            miscGroup2.Controls.Add(supermimic);
            miscGroup2.Controls.Add(yhormruler);
            miscGroup2.Controls.Add(chests);
            miscGroup2.Location = new System.Drawing.Point(8, 204);
            miscGroup2.Name = "miscGroup2";
            miscGroup2.Size = new System.Drawing.Size(351, 99);
            miscGroup2.TabIndex = 2;
            miscGroup2.TabStop = false;
            miscGroup2.Text = "Misc";
            // 
            // supermimic
            // 
            supermimic.AutoSize = true;
            supermimic.Location = new System.Drawing.Point(16, 73);
            supermimic.Name = "supermimic";
            supermimic.Size = new System.Drawing.Size(158, 24);
            supermimic.TabIndex = 2;
            supermimic.Text = "Impatient mimics";
            supermimic.UseVisualStyleBackColor = true;
            supermimic.CheckedChanged += option_CheckedChanged;
            // 
            // yhormruler
            // 
            yhormruler.AutoSize = true;
            yhormruler.Location = new System.Drawing.Point(15, 21);
            yhormruler.Name = "yhormruler";
            yhormruler.Size = new System.Drawing.Size(363, 24);
            yhormruler.TabIndex = 0;
            yhormruler.Text = "Grant Storm Ruler upon encountering Yhorm";
            yhormruler.UseVisualStyleBackColor = true;
            yhormruler.CheckedChanged += option_CheckedChanged;
            // 
            // chests
            // 
            chests.AutoSize = true;
            chests.Location = new System.Drawing.Point(16, 47);
            chests.Name = "chests";
            chests.Size = new System.Drawing.Size(233, 24);
            chests.TabIndex = 1;
            chests.Text = "Turn all chests into mimics";
            chests.UseVisualStyleBackColor = true;
            chests.CheckedChanged += option_CheckedChanged;
            // 
            // enemyRandomGroup
            // 
            enemyRandomGroup.Controls.Add(reducepassive);
            enemyRandomGroup.Controls.Add(lizards);
            enemyRandomGroup.Controls.Add(mimics);
            enemyRandomGroup.Location = new System.Drawing.Point(7, 6);
            enemyRandomGroup.Name = "enemyRandomGroup";
            enemyRandomGroup.Size = new System.Drawing.Size(351, 106);
            enemyRandomGroup.TabIndex = 0;
            enemyRandomGroup.TabStop = false;
            enemyRandomGroup.Text = "Randomness";
            // 
            // reducepassive
            // 
            reducepassive.AutoSize = true;
            reducepassive.Location = new System.Drawing.Point(15, 78);
            reducepassive.Name = "reducepassive";
            reducepassive.Size = new System.Drawing.Size(339, 24);
            reducepassive.TabIndex = 5;
            reducepassive.Text = "Reduce frequency of \"harmless\" enemies";
            reducepassive.UseVisualStyleBackColor = true;
            reducepassive.CheckedChanged += option_CheckedChanged;
            // 
            // lizards
            // 
            lizards.AutoSize = true;
            lizards.Checked = true;
            lizards.CheckState = System.Windows.Forms.CheckState.Checked;
            lizards.Location = new System.Drawing.Point(15, 52);
            lizards.Name = "lizards";
            lizards.Size = new System.Drawing.Size(270, 24);
            lizards.TabIndex = 4;
            lizards.Text = "Randomize small crystal lizards";
            lizards.UseVisualStyleBackColor = true;
            lizards.CheckedChanged += option_CheckedChanged;
            // 
            // mimics
            // 
            mimics.AutoSize = true;
            mimics.Checked = true;
            mimics.CheckState = System.Windows.Forms.CheckState.Checked;
            mimics.Location = new System.Drawing.Point(15, 26);
            mimics.Name = "mimics";
            mimics.Size = new System.Drawing.Size(174, 24);
            mimics.TabIndex = 2;
            mimics.Text = "Randomize mimics";
            mimics.UseVisualStyleBackColor = true;
            mimics.CheckedChanged += option_CheckedChanged;
            // 
            // enemyProgressGroup
            // 
            enemyProgressGroup.Controls.Add(scale);
            enemyProgressGroup.Controls.Add(earlyreq);
            enemyProgressGroup.Location = new System.Drawing.Point(7, 118);
            enemyProgressGroup.Name = "enemyProgressGroup";
            enemyProgressGroup.Size = new System.Drawing.Size(351, 80);
            enemyProgressGroup.TabIndex = 1;
            enemyProgressGroup.TabStop = false;
            enemyProgressGroup.Text = "Progression";
            // 
            // scale
            // 
            scale.AutoSize = true;
            scale.Checked = true;
            scale.CheckState = System.Windows.Forms.CheckState.Checked;
            scale.Location = new System.Drawing.Point(15, 47);
            scale.Name = "scale";
            scale.Size = new System.Drawing.Size(308, 24);
            scale.TabIndex = 4;
            scale.Text = "Scale up/down enemy health/damage";
            scale.UseVisualStyleBackColor = true;
            scale.CheckedChanged += option_CheckedChanged;
            // 
            // earlyreq
            // 
            earlyreq.AutoSize = true;
            earlyreq.Checked = true;
            earlyreq.CheckState = System.Windows.Forms.CheckState.Checked;
            earlyreq.Location = new System.Drawing.Point(15, 21);
            earlyreq.Name = "earlyreq";
            earlyreq.Size = new System.Drawing.Size(391, 24);
            earlyreq.TabIndex = 2;
            earlyreq.Text = "Simple Gundyr and Vordt (no super late bosses)";
            earlyreq.UseVisualStyleBackColor = true;
            earlyreq.CheckedChanged += option_CheckedChanged;
            // 
            // itemPage
            // 
            itemPage.BackColor = System.Drawing.SystemColors.Control;
            itemPage.Controls.Add(biasGroup);
            itemPage.Controls.Add(label14);
            itemPage.Controls.Add(dlcGroup);
            itemPage.Controls.Add(lothricGroup);
            itemPage.Controls.Add(skipsgroup);
            itemPage.Controls.Add(keyGroup);
            itemPage.Controls.Add(healthGroup);
            itemPage.Controls.Add(progressionGroup);
            itemPage.Controls.Add(miscGroup);
            itemPage.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            itemPage.Location = new System.Drawing.Point(4, 38);
            itemPage.Name = "itemPage";
            itemPage.Padding = new System.Windows.Forms.Padding(3);
            itemPage.Size = new System.Drawing.Size(1148, 531);
            itemPage.TabIndex = 1;
            itemPage.Text = "    Item Randomizer";
            // 
            // biasGroup
            // 
            biasGroup.Controls.Add(difficultyAmtL);
            biasGroup.Controls.Add(difficultyL);
            biasGroup.Controls.Add(difficulty);
            biasGroup.Location = new System.Drawing.Point(7, 6);
            biasGroup.Name = "biasGroup";
            biasGroup.Size = new System.Drawing.Size(1131, 101);
            biasGroup.TabIndex = 0;
            biasGroup.TabStop = false;
            biasGroup.Text = "Bias";
            // 
            // difficultyAmtL
            // 
            difficultyAmtL.AutoSize = true;
            difficultyAmtL.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            difficultyAmtL.Location = new System.Drawing.Point(1073, 56);
            difficultyAmtL.Name = "difficultyAmtL";
            difficultyAmtL.Size = new System.Drawing.Size(41, 25);
            difficultyAmtL.TabIndex = 5;
            difficultyAmtL.Text = "0%";
            difficultyAmtL.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // difficultyL
            // 
            difficultyL.AutoSize = true;
            difficultyL.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            difficultyL.Location = new System.Drawing.Point(22, 56);
            difficultyL.Name = "difficultyL";
            difficultyL.Size = new System.Drawing.Size(366, 40);
            difficultyL.TabIndex = 4;
            difficultyL.Text = "All possible locations for items are equally likely\r\nKey items may depend on each other";
            // 
            // difficulty
            // 
            difficulty.LargeChange = 10;
            difficulty.Location = new System.Drawing.Point(17, 21);
            difficulty.Maximum = 100;
            difficulty.Name = "difficulty";
            difficulty.Size = new System.Drawing.Size(1088, 56);
            difficulty.TabIndex = 3;
            difficulty.Scroll += difficulty_Scroll;
            // 
            // label14
            // 
            label14.AutoSize = true;
            label14.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label14.Location = new System.Drawing.Point(790, 455);
            label14.Name = "label14";
            label14.Size = new System.Drawing.Size(423, 40);
            label14.TabIndex = 8;
            label14.Text = "Check the mod page or README for a full list of all key\r\nitems, special item locations, and various other details.";
            // 
            // dlcGroup
            // 
            dlcGroup.Controls.Add(earlydlcL);
            dlcGroup.Controls.Add(label3);
            dlcGroup.Controls.Add(earlydlc);
            dlcGroup.Controls.Add(dlc2fromdlc1);
            dlcGroup.Location = new System.Drawing.Point(7, 113);
            dlcGroup.Name = "dlcGroup";
            dlcGroup.Size = new System.Drawing.Size(351, 118);
            dlcGroup.TabIndex = 1;
            dlcGroup.TabStop = false;
            dlcGroup.Text = "DLC";
            // 
            // earlydlcL
            // 
            earlydlcL.AutoSize = true;
            earlydlcL.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            earlydlcL.Location = new System.Drawing.Point(22, 88);
            earlydlcL.Name = "earlydlcL";
            earlydlcL.Size = new System.Drawing.Size(80, 17);
            earlydlcL.TabIndex = 6;
            earlydlcL.Text = "Late Friede";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label3.Location = new System.Drawing.Point(22, 49);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(211, 17);
            label3.TabIndex = 1;
            label3.Text = "Allow Cinders to appear in DLC2";
            // 
            // earlydlc
            // 
            earlydlc.AutoSize = true;
            earlydlc.Location = new System.Drawing.Point(6, 65);
            earlydlc.Name = "earlydlc";
            earlydlc.Size = new System.Drawing.Size(292, 24);
            earlydlc.TabIndex = 4;
            earlydlc.Text = "DLC may be required before Irithyll";
            earlydlc.UseVisualStyleBackColor = true;
            earlydlc.CheckedChanged += option_CheckedChanged;
            // 
            // dlc2fromdlc1
            // 
            dlc2fromdlc1.AutoSize = true;
            dlc2fromdlc1.Checked = true;
            dlc2fromdlc1.CheckState = System.Windows.Forms.CheckState.Checked;
            dlc2fromdlc1.Location = new System.Drawing.Point(6, 26);
            dlc2fromdlc1.Name = "dlc2fromdlc1";
            dlc2fromdlc1.Size = new System.Drawing.Size(285, 24);
            dlc2fromdlc1.TabIndex = 2;
            dlc2fromdlc1.Text = "Use DLC1→DLC2 routing in logic";
            dlc2fromdlc1.UseVisualStyleBackColor = true;
            dlc2fromdlc1.CheckedChanged += option_CheckedChanged;
            // 
            // lothricGroup
            // 
            lothricGroup.Controls.Add(earlylothricL);
            lothricGroup.Controls.Add(earlylothric);
            lothricGroup.Controls.Add(middancer);
            lothricGroup.Controls.Add(regdancer);
            lothricGroup.Location = new System.Drawing.Point(7, 237);
            lothricGroup.Name = "lothricGroup";
            lothricGroup.Size = new System.Drawing.Size(351, 107);
            lothricGroup.TabIndex = 2;
            lothricGroup.TabStop = false;
            lothricGroup.Text = "Lothric Castle";
            // 
            // earlylothricL
            // 
            earlylothricL.AutoSize = true;
            earlylothricL.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            earlylothricL.Location = new System.Drawing.Point(23, 90);
            earlylothricL.Name = "earlylothricL";
            earlylothricL.Size = new System.Drawing.Size(86, 17);
            earlylothricL.TabIndex = 5;
            earlylothricL.Text = "Late Dancer";
            // 
            // earlylothric
            // 
            earlylothric.AutoSize = true;
            earlylothric.Location = new System.Drawing.Point(6, 67);
            earlylothric.Name = "earlylothric";
            earlylothric.Size = new System.Drawing.Size(397, 24);
            earlylothric.TabIndex = 2;
            earlylothric.TabStop = true;
            earlylothric.Text = "Lothric Castle may be required before Settlement";
            earlylothric.UseVisualStyleBackColor = true;
            earlylothric.CheckedChanged += option_CheckedChanged;
            // 
            // middancer
            // 
            middancer.AutoSize = true;
            middancer.Location = new System.Drawing.Point(6, 44);
            middancer.Name = "middancer";
            middancer.Size = new System.Drawing.Size(361, 24);
            middancer.TabIndex = 1;
            middancer.TabStop = true;
            middancer.Text = "Lothric Castle may be required before Irithyll";
            middancer.UseVisualStyleBackColor = true;
            middancer.CheckedChanged += option_CheckedChanged;
            // 
            // regdancer
            // 
            regdancer.AutoSize = true;
            regdancer.Checked = true;
            regdancer.Location = new System.Drawing.Point(6, 21);
            regdancer.Name = "regdancer";
            regdancer.Size = new System.Drawing.Size(270, 24);
            regdancer.TabIndex = 0;
            regdancer.TabStop = true;
            regdancer.Text = "Lothric Castle not required early";
            regdancer.UseVisualStyleBackColor = true;
            regdancer.CheckedChanged += option_CheckedChanged;
            // 
            // skipsgroup
            // 
            skipsgroup.Controls.Add(label5);
            skipsgroup.Controls.Add(label7);
            skipsgroup.Controls.Add(vilhelmskip);
            skipsgroup.Controls.Add(treeskip);
            skipsgroup.Location = new System.Drawing.Point(7, 350);
            skipsgroup.Name = "skipsgroup";
            skipsgroup.Size = new System.Drawing.Size(351, 118);
            skipsgroup.TabIndex = 3;
            skipsgroup.TabStop = false;
            skipsgroup.Text = "Skips";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label5.Location = new System.Drawing.Point(22, 85);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(366, 17);
            label5.TabIndex = 8;
            label5.Text = "Extremely difficult. Spook quitout with long deathcam run.";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label7.Location = new System.Drawing.Point(22, 44);
            label7.Name = "label7";
            label7.Size = new System.Drawing.Size(398, 17);
            label7.TabIndex = 12;
            label7.Text = "Fairly easy. Access lower Firelink Roof by jumping from a tree.";
            // 
            // vilhelmskip
            // 
            vilhelmskip.AutoSize = true;
            vilhelmskip.Location = new System.Drawing.Point(6, 62);
            vilhelmskip.Name = "vilhelmskip";
            vilhelmskip.Size = new System.Drawing.Size(223, 24);
            vilhelmskip.TabIndex = 10;
            vilhelmskip.Text = "Doll skip and Vilhelm skip";
            vilhelmskip.UseVisualStyleBackColor = true;
            vilhelmskip.CheckedChanged += option_CheckedChanged;
            // 
            // treeskip
            // 
            treeskip.AutoSize = true;
            treeskip.Location = new System.Drawing.Point(6, 21);
            treeskip.Name = "treeskip";
            treeskip.Size = new System.Drawing.Size(230, 24);
            treeskip.TabIndex = 7;
            treeskip.Text = "Tree skip in Firelink Shrine";
            treeskip.UseVisualStyleBackColor = true;
            treeskip.CheckedChanged += option_CheckedChanged;
            // 
            // keyGroup
            // 
            keyGroup.Controls.Add(label13);
            keyGroup.Controls.Add(label12);
            keyGroup.Controls.Add(label11);
            keyGroup.Controls.Add(label10);
            keyGroup.Controls.Add(label9);
            keyGroup.Controls.Add(label2);
            keyGroup.Controls.Add(label1);
            keyGroup.Controls.Add(raceloc_ring);
            keyGroup.Controls.Add(raceloc_lizard);
            keyGroup.Controls.Add(raceloc_miniboss);
            keyGroup.Controls.Add(raceloc_ashes);
            keyGroup.Controls.Add(raceloc_chest);
            keyGroup.Controls.Add(defaultD);
            keyGroup.Controls.Add(defaultC);
            keyGroup.Controls.Add(racemode);
            keyGroup.Controls.Add(norandom);
            keyGroup.Controls.Add(defaultKey);
            keyGroup.Location = new System.Drawing.Point(373, 122);
            keyGroup.Name = "keyGroup";
            keyGroup.Size = new System.Drawing.Size(408, 403);
            keyGroup.TabIndex = 4;
            keyGroup.TabStop = false;
            keyGroup.Text = "Key item placement";
            // 
            // label13
            // 
            label13.AutoSize = true;
            label13.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label13.Location = new System.Drawing.Point(41, 359);
            label13.Name = "label13";
            label13.Size = new System.Drawing.Size(463, 34);
            label13.TabIndex = 20;
            label13.Text = "Approx 50 checks. Excludes rings covered by other categories and rings\r\nwhich only appear in later game cycles.";
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label12.Location = new System.Drawing.Point(41, 320);
            label12.Name = "label12";
            label12.Size = new System.Drawing.Size(477, 17);
            label12.TabIndex = 19;
            label12.Text = "Approx 45 checks. Scurrying lizards, or their replacements in enemy rando";
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label11.Location = new System.Drawing.Point(41, 268);
            label11.Name = "label11";
            label11.Size = new System.Drawing.Size(449, 34);
            label11.TabIndex = 18;
            label11.Text = "Approx 45 checks. Includes most hostile non-respawning enemies and\r\nalso the High Wall Darkwraith, or their replacements in enemy rando";
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label10.Location = new System.Drawing.Point(41, 230);
            label10.Name = "label10";
            label10.Size = new System.Drawing.Size(437, 17);
            label10.TabIndex = 17;
            label10.Text = "Approx 30 checks. Includes items spawning in handmaid's inventory.";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label9.Location = new System.Drawing.Point(41, 191);
            label9.Name = "label9";
            label9.Size = new System.Drawing.Size(280, 17);
            label9.TabIndex = 16;
            label9.Text = "Approx 30 checks. Does not include mimics";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label2.Location = new System.Drawing.Point(41, 153);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(307, 17);
            label2.TabIndex = 15;
            label2.Text = "Always included in this mode! Approx 40 checks";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label1.Location = new System.Drawing.Point(41, 113);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(307, 17);
            label1.TabIndex = 7;
            label1.Text = "Always included in this mode! Approx 25 checks";
            // 
            // raceloc_ring
            // 
            raceloc_ring.AutoSize = true;
            raceloc_ring.Location = new System.Drawing.Point(25, 337);
            raceloc_ring.Name = "raceloc_ring";
            raceloc_ring.Size = new System.Drawing.Size(222, 24);
            raceloc_ring.TabIndex = 14;
            raceloc_ring.Text = "Original locations of rings";
            raceloc_ring.UseVisualStyleBackColor = true;
            raceloc_ring.CheckedChanged += option_CheckedChanged;
            // 
            // raceloc_lizard
            // 
            raceloc_lizard.AutoSize = true;
            raceloc_lizard.Location = new System.Drawing.Point(25, 298);
            raceloc_lizard.Name = "raceloc_lizard";
            raceloc_lizard.Size = new System.Drawing.Size(183, 24);
            raceloc_lizard.TabIndex = 13;
            raceloc_lizard.Text = "Small crystal lizards";
            raceloc_lizard.UseVisualStyleBackColor = true;
            raceloc_lizard.CheckedChanged += option_CheckedChanged;
            // 
            // raceloc_miniboss
            // 
            raceloc_miniboss.AutoSize = true;
            raceloc_miniboss.Checked = true;
            raceloc_miniboss.CheckState = System.Windows.Forms.CheckState.Checked;
            raceloc_miniboss.Location = new System.Drawing.Point(25, 246);
            raceloc_miniboss.Name = "raceloc_miniboss";
            raceloc_miniboss.Size = new System.Drawing.Size(238, 24);
            raceloc_miniboss.TabIndex = 12;
            raceloc_miniboss.Text = "Powerful non-boss enemies";
            raceloc_miniboss.UseVisualStyleBackColor = true;
            raceloc_miniboss.CheckedChanged += option_CheckedChanged;
            // 
            // raceloc_ashes
            // 
            raceloc_ashes.AutoSize = true;
            raceloc_ashes.Checked = true;
            raceloc_ashes.CheckState = System.Windows.Forms.CheckState.Checked;
            raceloc_ashes.Location = new System.Drawing.Point(25, 207);
            raceloc_ashes.Name = "raceloc_ashes";
            raceloc_ashes.Size = new System.Drawing.Size(271, 24);
            raceloc_ashes.TabIndex = 11;
            raceloc_ashes.Text = "NPC shops and non-NPC ashes";
            raceloc_ashes.UseVisualStyleBackColor = true;
            raceloc_ashes.CheckedChanged += option_CheckedChanged;
            // 
            // raceloc_chest
            // 
            raceloc_chest.AutoSize = true;
            raceloc_chest.Checked = true;
            raceloc_chest.CheckState = System.Windows.Forms.CheckState.Checked;
            raceloc_chest.Location = new System.Drawing.Point(25, 170);
            raceloc_chest.Name = "raceloc_chest";
            raceloc_chest.Size = new System.Drawing.Size(84, 24);
            raceloc_chest.TabIndex = 10;
            raceloc_chest.Text = "Chests";
            raceloc_chest.UseVisualStyleBackColor = true;
            raceloc_chest.CheckedChanged += option_CheckedChanged;
            // 
            // defaultD
            // 
            defaultD.AutoSize = true;
            defaultD.Checked = true;
            defaultD.CheckState = System.Windows.Forms.CheckState.Checked;
            defaultD.Location = new System.Drawing.Point(25, 130);
            defaultD.Name = "defaultD";
            defaultD.Size = new System.Drawing.Size(465, 24);
            defaultD.TabIndex = 9;
            defaultD.Text = "Vanilla locations of key items, coals, and healing upgrades";
            defaultD.UseVisualStyleBackColor = true;
            defaultD.CheckedChanged += option_alwaysEnable;
            // 
            // defaultC
            // 
            defaultC.AutoSize = true;
            defaultC.Checked = true;
            defaultC.CheckState = System.Windows.Forms.CheckState.Checked;
            defaultC.Location = new System.Drawing.Point(25, 92);
            defaultC.Name = "defaultC";
            defaultC.Size = new System.Drawing.Size(117, 24);
            defaultC.TabIndex = 8;
            defaultC.Text = "Boss drops";
            defaultC.UseVisualStyleBackColor = true;
            defaultC.CheckedChanged += option_alwaysEnable;
            // 
            // racemode
            // 
            racemode.AutoSize = true;
            racemode.Checked = true;
            racemode.Location = new System.Drawing.Point(7, 69);
            racemode.Name = "racemode";
            racemode.Size = new System.Drawing.Size(343, 24);
            racemode.TabIndex = 3;
            racemode.TabStop = true;
            racemode.Text = "Randomize to the following locations only:";
            racemode.UseVisualStyleBackColor = true;
            racemode.CheckedChanged += option_CheckedChanged;
            // 
            // norandom
            // 
            norandom.AutoSize = true;
            norandom.Location = new System.Drawing.Point(7, 23);
            norandom.Name = "norandom";
            norandom.Size = new System.Drawing.Size(148, 24);
            norandom.TabIndex = 1;
            norandom.Text = "Not randomized";
            norandom.UseVisualStyleBackColor = true;
            norandom.CheckedChanged += option_CheckedChanged;
            // 
            // defaultKey
            // 
            defaultKey.AutoSize = true;
            defaultKey.Location = new System.Drawing.Point(7, 46);
            defaultKey.Name = "defaultKey";
            defaultKey.Size = new System.Drawing.Size(357, 24);
            defaultKey.TabIndex = 2;
            defaultKey.Text = "Randomize to anywhere (over 1000 checks)";
            defaultKey.UseVisualStyleBackColor = true;
            defaultKey.CheckedChanged += option_CheckedChanged;
            // 
            // healthGroup
            // 
            healthGroup.Controls.Add(racemode_health);
            healthGroup.Controls.Add(estusprogressionL);
            healthGroup.Controls.Add(norandom_health);
            healthGroup.Controls.Add(defaultHealth);
            healthGroup.Controls.Add(estusprogression);
            healthGroup.Location = new System.Drawing.Point(788, 122);
            healthGroup.Name = "healthGroup";
            healthGroup.Size = new System.Drawing.Size(351, 141);
            healthGroup.TabIndex = 5;
            healthGroup.TabStop = false;
            healthGroup.Text = "Healing upgrade item placement";
            // 
            // racemode_health
            // 
            racemode_health.AutoSize = true;
            racemode_health.Checked = true;
            racemode_health.Location = new System.Drawing.Point(6, 69);
            racemode_health.Name = "racemode_health";
            racemode_health.Size = new System.Drawing.Size(244, 24);
            racemode_health.TabIndex = 23;
            racemode_health.TabStop = true;
            racemode_health.Text = "Randomized in key item pool";
            racemode_health.UseVisualStyleBackColor = true;
            racemode_health.CheckedChanged += option_CheckedChanged;
            // 
            // estusprogressionL
            // 
            estusprogressionL.AutoSize = true;
            estusprogressionL.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            estusprogressionL.Location = new System.Drawing.Point(22, 118);
            estusprogressionL.Name = "estusprogressionL";
            estusprogressionL.Size = new System.Drawing.Size(139, 17);
            estusprogressionL.TabIndex = 13;
            estusprogressionL.Text = "Comparable difficulty";
            // 
            // norandom_health
            // 
            norandom_health.AutoSize = true;
            norandom_health.Location = new System.Drawing.Point(6, 23);
            norandom_health.Name = "norandom_health";
            norandom_health.Size = new System.Drawing.Size(148, 24);
            norandom_health.TabIndex = 20;
            norandom_health.Text = "Not randomized";
            norandom_health.UseVisualStyleBackColor = true;
            norandom_health.CheckedChanged += option_CheckedChanged;
            // 
            // defaultHealth
            // 
            defaultHealth.AutoSize = true;
            defaultHealth.Location = new System.Drawing.Point(6, 46);
            defaultHealth.Name = "defaultHealth";
            defaultHealth.Size = new System.Drawing.Size(209, 24);
            defaultHealth.TabIndex = 21;
            defaultHealth.Text = "Randomize to anywhere";
            defaultHealth.UseVisualStyleBackColor = true;
            defaultHealth.CheckedChanged += option_CheckedChanged;
            // 
            // estusprogression
            // 
            estusprogression.AutoSize = true;
            estusprogression.Checked = true;
            estusprogression.CheckState = System.Windows.Forms.CheckState.Checked;
            estusprogression.Location = new System.Drawing.Point(6, 95);
            estusprogression.Name = "estusprogression";
            estusprogression.Size = new System.Drawing.Size(382, 24);
            estusprogression.TabIndex = 24;
            estusprogression.Text = "Estus upgrade availability similar to base game";
            estusprogression.UseVisualStyleBackColor = true;
            estusprogression.CheckedChanged += option_CheckedChanged;
            // 
            // progressionGroup
            // 
            progressionGroup.Controls.Add(soulsprogressionL);
            progressionGroup.Controls.Add(weaponprogressionL);
            progressionGroup.Controls.Add(soulsprogression);
            progressionGroup.Controls.Add(weaponprogression);
            progressionGroup.Location = new System.Drawing.Point(788, 269);
            progressionGroup.Name = "progressionGroup";
            progressionGroup.Size = new System.Drawing.Size(351, 106);
            progressionGroup.TabIndex = 6;
            progressionGroup.TabStop = false;
            progressionGroup.Text = "Other item progression";
            // 
            // soulsprogressionL
            // 
            soulsprogressionL.AutoSize = true;
            soulsprogressionL.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            soulsprogressionL.Location = new System.Drawing.Point(22, 84);
            soulsprogressionL.Name = "soulsprogressionL";
            soulsprogressionL.Size = new System.Drawing.Size(139, 17);
            soulsprogressionL.TabIndex = 8;
            soulsprogressionL.Text = "Comparable difficulty";
            // 
            // weaponprogressionL
            // 
            weaponprogressionL.AutoSize = true;
            weaponprogressionL.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            weaponprogressionL.Location = new System.Drawing.Point(22, 44);
            weaponprogressionL.Name = "weaponprogressionL";
            weaponprogressionL.Size = new System.Drawing.Size(139, 17);
            weaponprogressionL.TabIndex = 12;
            weaponprogressionL.Text = "Comparable difficulty";
            // 
            // soulsprogression
            // 
            soulsprogression.AutoSize = true;
            soulsprogression.Checked = true;
            soulsprogression.CheckState = System.Windows.Forms.CheckState.Checked;
            soulsprogression.Location = new System.Drawing.Point(6, 61);
            soulsprogression.Name = "soulsprogression";
            soulsprogression.Size = new System.Drawing.Size(344, 24);
            soulsprogression.TabIndex = 10;
            soulsprogression.Text = "Soul item availability similar to base game";
            soulsprogression.UseVisualStyleBackColor = true;
            soulsprogression.CheckedChanged += option_CheckedChanged;
            // 
            // weaponprogression
            // 
            weaponprogression.AutoSize = true;
            weaponprogression.Checked = true;
            weaponprogression.CheckState = System.Windows.Forms.CheckState.Checked;
            weaponprogression.Location = new System.Drawing.Point(6, 21);
            weaponprogression.Name = "weaponprogression";
            weaponprogression.Size = new System.Drawing.Size(400, 24);
            weaponprogression.TabIndex = 7;
            weaponprogression.Text = "Weapon upgrade availability similar to base game";
            weaponprogression.UseVisualStyleBackColor = true;
            weaponprogression.CheckedChanged += option_CheckedChanged;
            // 
            // miscGroup
            // 
            miscGroup.Controls.Add(onehand);
            miscGroup.Controls.Add(ngplusrings);
            miscGroup.Location = new System.Drawing.Point(787, 381);
            miscGroup.Name = "miscGroup";
            miscGroup.Size = new System.Drawing.Size(351, 67);
            miscGroup.TabIndex = 7;
            miscGroup.TabStop = false;
            miscGroup.Text = "Misc";
            // 
            // onehand
            // 
            onehand.AutoSize = true;
            onehand.Location = new System.Drawing.Point(7, 39);
            onehand.Name = "onehand";
            onehand.Size = new System.Drawing.Size(392, 24);
            onehand.TabIndex = 10;
            onehand.Text = "Disallow starting weapons requiring two-handing";
            onehand.UseVisualStyleBackColor = true;
            onehand.CheckedChanged += option_CheckedChanged;
            // 
            // ngplusrings
            // 
            ngplusrings.AutoSize = true;
            ngplusrings.Location = new System.Drawing.Point(6, 17);
            ngplusrings.Name = "ngplusrings";
            ngplusrings.Size = new System.Drawing.Size(279, 24);
            ngplusrings.TabIndex = 9;
            ngplusrings.Text = "Add NG+ rings and ring locations";
            ngplusrings.UseVisualStyleBackColor = true;
            ngplusrings.CheckedChanged += option_CheckedChanged;
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(itemPage);
            tabControl1.Controls.Add(enemyPage);
            tabControl1.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            tabControl1.Location = new System.Drawing.Point(17, 12);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new System.Drawing.Size(1156, 573);
            tabControl1.TabIndex = 2;
            // 
            // archipelagoButton
            // 
            archipelagoButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            archipelagoButton.Location = new System.Drawing.Point(956, 657);
            archipelagoButton.Name = "archipelagoButton";
            archipelagoButton.Size = new System.Drawing.Size(217, 29);
            archipelagoButton.TabIndex = 14;
            archipelagoButton.Text = "Load Archipelago run";
            archipelagoButton.UseVisualStyleBackColor = true;
            archipelagoButton.Click += archipelagoButton_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.SystemColors.Control;
            ClientSize = new System.Drawing.Size(1181, 697);
            Controls.Add(archipelagoButton);
            Controls.Add(edittext);
            Controls.Add(item);
            Controls.Add(enemy);
            Controls.Add(tabControl1);
            Controls.Add(mergemods);
            Controls.Add(optionwindow);
            Controls.Add(defaultReroll);
            Controls.Add(dlc2);
            Controls.Add(dlc1);
            Controls.Add(warningL);
            Controls.Add(label8);
            Controls.Add(randomize);
            Controls.Add(fixedseed);
            Controls.Add(statusStrip1);
            Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Name = "MainForm";
            Text = "DS3 Static Item and Enemy Randomizer v0.3";
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            enemyPage.ResumeLayout(false);
            enemyPage.PerformLayout();
            miscGroup2.ResumeLayout(false);
            miscGroup2.PerformLayout();
            enemyRandomGroup.ResumeLayout(false);
            enemyRandomGroup.PerformLayout();
            enemyProgressGroup.ResumeLayout(false);
            enemyProgressGroup.PerformLayout();
            itemPage.ResumeLayout(false);
            itemPage.PerformLayout();
            biasGroup.ResumeLayout(false);
            biasGroup.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)difficulty).EndInit();
            dlcGroup.ResumeLayout(false);
            dlcGroup.PerformLayout();
            lothricGroup.ResumeLayout(false);
            lothricGroup.PerformLayout();
            skipsgroup.ResumeLayout(false);
            skipsgroup.PerformLayout();
            keyGroup.ResumeLayout(false);
            keyGroup.PerformLayout();
            healthGroup.ResumeLayout(false);
            healthGroup.PerformLayout();
            progressionGroup.ResumeLayout(false);
            progressionGroup.PerformLayout();
            miscGroup.ResumeLayout(false);
            miscGroup.PerformLayout();
            tabControl1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private System.Windows.Forms.CheckBox dlc2;
        private System.Windows.Forms.CheckBox dlc1;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel statusL;
        private System.Windows.Forms.TextBox fixedseed;
        private System.Windows.Forms.Button randomize;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label warningL;
        private System.Windows.Forms.CheckBox mergemods;
        private System.Windows.Forms.CheckBox defaultReroll;
        private System.Windows.Forms.Button optionwindow;
        private System.Windows.Forms.CheckBox enemy;
        private System.Windows.Forms.CheckBox item;
        private System.Windows.Forms.CheckBox edittext;
        private System.Windows.Forms.TabPage enemyPage;
        private System.Windows.Forms.CheckBox defaultRerollEnemy;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox enemyseed;
        private System.Windows.Forms.Label presetL;
        private System.Windows.Forms.Button preset;
        private System.Windows.Forms.GroupBox miscGroup2;
        private System.Windows.Forms.CheckBox supermimic;
        private System.Windows.Forms.CheckBox yhormruler;
        private System.Windows.Forms.CheckBox chests;
        private System.Windows.Forms.GroupBox enemyRandomGroup;
        private System.Windows.Forms.CheckBox reducepassive;
        private System.Windows.Forms.CheckBox lizards;
        private System.Windows.Forms.CheckBox mimics;
        private System.Windows.Forms.GroupBox enemyProgressGroup;
        private System.Windows.Forms.CheckBox scale;
        private System.Windows.Forms.CheckBox earlyreq;
        private System.Windows.Forms.TabPage itemPage;
        private System.Windows.Forms.GroupBox biasGroup;
        private System.Windows.Forms.Label difficultyAmtL;
        private System.Windows.Forms.Label difficultyL;
        private System.Windows.Forms.TrackBar difficulty;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.GroupBox dlcGroup;
        private System.Windows.Forms.Label earlydlcL;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckBox earlydlc;
        private System.Windows.Forms.CheckBox dlc2fromdlc1;
        private System.Windows.Forms.GroupBox lothricGroup;
        private System.Windows.Forms.Label earlylothricL;
        private System.Windows.Forms.RadioButton earlylothric;
        private System.Windows.Forms.RadioButton middancer;
        private System.Windows.Forms.RadioButton regdancer;
        private System.Windows.Forms.GroupBox skipsgroup;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.CheckBox vilhelmskip;
        private System.Windows.Forms.CheckBox treeskip;
        private System.Windows.Forms.GroupBox keyGroup;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox raceloc_ring;
        private System.Windows.Forms.CheckBox raceloc_lizard;
        private System.Windows.Forms.CheckBox raceloc_miniboss;
        private System.Windows.Forms.CheckBox raceloc_ashes;
        private System.Windows.Forms.CheckBox raceloc_chest;
        private System.Windows.Forms.CheckBox defaultD;
        private System.Windows.Forms.CheckBox defaultC;
        private System.Windows.Forms.RadioButton racemode;
        private System.Windows.Forms.RadioButton norandom;
        private System.Windows.Forms.RadioButton defaultKey;
        private System.Windows.Forms.GroupBox healthGroup;
        private System.Windows.Forms.RadioButton racemode_health;
        private System.Windows.Forms.Label estusprogressionL;
        private System.Windows.Forms.RadioButton norandom_health;
        private System.Windows.Forms.RadioButton defaultHealth;
        private System.Windows.Forms.CheckBox estusprogression;
        private System.Windows.Forms.GroupBox progressionGroup;
        private System.Windows.Forms.Label soulsprogressionL;
        private System.Windows.Forms.Label weaponprogressionL;
        private System.Windows.Forms.CheckBox soulsprogression;
        private System.Windows.Forms.CheckBox weaponprogression;
        private System.Windows.Forms.GroupBox miscGroup;
        private System.Windows.Forms.CheckBox onehand;
        private System.Windows.Forms.CheckBox ngplusrings;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.Button archipelagoButton;
    }
}

