using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SoulsFormats;
using Newtonsoft.Json;
using System.Drawing;
using System.Drawing.Text;
using System.Text.RegularExpressions;
using SoulsIds;
using static RandomizerCommon.Messages;

namespace RandomizerCommon
{
    public class FormMessages
    {
        public static readonly List<Type> WinFormsMessageTypes = new()
        {
            typeof(EldenForm), typeof(OptionsForm), typeof(HelperForm), typeof(PresetEditForm), typeof(PresetItemForm),
        };

        // Gets original text. Should only be done once at the start, to preserve English messages.
        public static Dictionary<string, string> GetFormText(Form form)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();
            void setText(string controlName, string text)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    ret[controlName] = text.Replace("\r\n", "\n");
                }
            }
            void recurseForm(Control control)
            {
                setText(control.Name, control.Text);
                foreach (Control sub in control.Controls)
                {
                    recurseForm(sub);
                }
                if (control is ToolStrip toolStrip)
                {
                    foreach (ToolStripItem sub in GetItems(toolStrip))
                    {
                        setText(sub.Name, sub.Text);
                    }
                }
            }
            recurseForm(form);
            return ret;
        }

        public static void SetFormText(Messages messages, Form form, Dictionary<string, string> original)
        {
            CultureInfo culture = Thread.CurrentThread.CurrentCulture;
            bool tryReplace(string controlName, out string replace)
            {
                replace = null;
                if (original.TryGetValue(controlName, out string originalText))
                {
                    string locName = $"{form.Name}_{controlName}";
                    if (!IgnoreFormRe.IsMatch(locName))
                    {
                        replace = messages.Get(culture, new Text(originalText, locName));
                        return true;
                    }
                }
                return false;
            }
            // Fonts from lowest to highest prioritity
            string defaultFont = "Microsoft Sans Serif";
            List<string> fontOrder = new List<string>();
            // Try to choose fonts aside from MS Gothic
            if (culture.TwoLetterISOLanguageName == "ja")
            {
                fontOrder.AddRange(new[] { "Noto Sans JP", "Yu Gothic UI", "Meiryo UI" });
            }
            else if (culture.TwoLetterISOLanguageName == "zh")
            {
                fontOrder.AddRange(new[] { "Noto Sans SC", "Microsoft YaHei UI" });
            }
            string font = defaultFont;
            FontFamily[] installed = new InstalledFontCollection().Families;
            foreach (string candidate in Enumerable.Reverse(fontOrder))
            {
                if (installed.Any(family => family.Name == candidate))
                {
                    font = candidate;
                    break;
                }
            }
            Font setFont(Font f)
            {
                if (f.Name == font) return f;
                return new Font(font, f.SizeInPoints);
            }
            void recurseForm(Control control)
            {
                control.Font = setFont(control.Font);
                if (tryReplace(control.Name, out string replace))
                {
                    control.Text = replace;
                }
                foreach (Control sub in control.Controls)
                {
                    recurseForm(sub);
                }
                if (control is ToolStrip toolStrip)
                {
                    foreach (ToolStripItem sub in GetItems(toolStrip))
                    {
                        sub.Font = setFont(sub.Font);
                        if (tryReplace(sub.Name, out replace))
                        {
                            sub.Text = replace;
                        }
                    }
                }
            }
            recurseForm(form);
        }

        private static IEnumerable<ToolStripItem> GetItems(ToolStrip strip) =>
            strip.Items.Cast<ToolStripItem>().Concat(strip.Items.Cast<ToolStripItem>().SelectMany(GetItems));

        // https://stackoverflow.com/questions/15380730/foreach-every-subitem-in-a-menustrip
        private static IEnumerable<ToolStripItem> GetItems(ToolStripItem item)
        {
            if (item is ToolStripMenuItem)
            {
                foreach (ToolStripItem tsi in (item as ToolStripMenuItem).DropDownItems)
                {
                    if (tsi is ToolStripMenuItem)
                    {
                        if ((tsi as ToolStripMenuItem).HasDropDownItems)
                        {
                            foreach (ToolStripItem subItem in GetItems((tsi as ToolStripMenuItem)))
                                yield return subItem;
                        }
                        yield return (tsi as ToolStripMenuItem);
                    }
                    else if (tsi is ToolStripSeparator)
                    {
                        yield return (tsi as ToolStripSeparator);
                    }
                }
            }
            else if (item is ToolStripSeparator)
            {
                yield return (item as ToolStripSeparator);
            }
        }

#if DEBUG
        public static void DumpEnglishMessages(List<Form> forms, List<Type> types)
        {
            ExplainBuilder explain = new();
            forms.ForEach(form => AddExplainForm(explain, form));
            types.ForEach(type => AddExplainType(explain, type));
            explain.Write();
            Console.WriteLine("Wrote explain.json");
        }

        public static void AddExplainForms(ExplainBuilder explain, params Form[] forms)
        {
            foreach (Form form in forms)
            {
                AddExplainForm(explain, form);
            }
        }

        public static void AddExplainForm(ExplainBuilder explain, Form form)
        {
            void setText(string controlName, string text)
            {
                if (!string.IsNullOrWhiteSpace(text) && text != "{0}")
                {
                    string name = $"{form.Name}_{controlName}";
                    if (!IgnoreFormRe.IsMatch(name))
                    {
                        text = text.Replace("\r\n", "\n");
                        explain.AddMessage(name, text);
                    }
                }
            }
            void recurseForm(Control control)
            {
                if (control is not ComboBox)
                {
                    setText(control.Name, control.Text);
                }
                foreach (Control sub in control.Controls)
                {
                    recurseForm(sub);
                }
                if (control is ToolStrip toolStrip)
                {
                    foreach (ToolStripItem sub in GetItems(toolStrip))
                    {
                        setText(sub.Name, sub.Text);
                    }
                }
            }
            recurseForm(form);
        }

        public static void FormToAxaml(Form form)
        {
            string dataName(string s) => s[0].ToString().ToUpperInvariant() + s.Substring(1).ToString();
            string keyName(string controlName) => $"{form.Name}_{controlName}";
            // Make this super basic. If necessary, make text a child or use JSON scaping
            string escape(string s) => s.Replace("\"", "\\\"");

            string attr(Control c) => $@"Key=""{keyName(c.Name)}"" Text=""{escape(c.Text)}""";
            string sp = "    ";
            void recurseForm(Control control, string indent, HashSet<string> shown)
            {
                if (!shown.Add(control.Name))
                {
                    return;
                }

                List<string> lines = new();
                List<string> after = new();
                string subsp = sp;
                if (control is CheckBox or RadioButton)
                {
                    Control label = control.Parent.Controls[control.Name + "L"];
                    string input = control.GetType().Name;
                    if (label is null)
                    {
                        lines.Add($@"<{input} IsChecked=""{{Binding {dataName(control.Name)}}}"">");
                        lines.Add($@"{sp}<loc:Message {attr(control)} />");
                        lines.Add($@"</{input}>");
                    }
                    else
                    {
                        // TODO: This no longer uses SubMessage
                        lines.Add($@"<{input} IsChecked=""{{Binding {dataName(control.Name)}}}"">");
                        lines.Add($@"{sp}<TextBlock>");
                        lines.Add($@"{sp}{sp}<loc:SubMessage {attr(control)} /><LineBreak />");
                        lines.Add($@"{sp}{sp}<loc:SubMessage Classes=""moreinfo"" {attr(label)} />");                        
                        lines.Add($@"{sp}</TextBlock>");
                        lines.Add($@"</{input}>");
                        shown.Add(label.Name);
                    }
                }
                else if (control is GroupBox)
                {
                    lines.Add($@"<HeaderedContentControl Classes=""groupbox"">");
                    lines.Add($@"{sp}<HeaderedContentControl.Header>");
                    lines.Add($@"{sp}{sp}<loc:Message {attr(control)} />");
                    lines.Add($@"{sp}</HeaderedContentControl.Header>");
                    lines.Add($@"{sp}<StackPanel>");
                    after.Add($@"{sp}</StackPanel>");
                    after.Add($@"</HeaderedContentControl>");
                    subsp += sp;
                }
                else if (control is Button)
                {
                    lines.Add($@"<Button Command=""{{Binding TodoCommand}}"">");
                    lines.Add($@"{sp}<loc:Message {attr(control)} />");
                    lines.Add($@"</Button>");
                }
                else if (control is Label)
                {
                    lines.Add($@"<loc:Message {attr(control)} />");
                }
                else if (control is ToolStrip toolStrip)
                {
                    lines.Add($@"<Menu>");
                    // Just do this here, since they're not children
                    // This has been flattened so the hierarchy is not correct either
                    foreach (ToolStripItem sub in GetItems(toolStrip))
                    {
                        lines.Add($@"{sp}<MenuItem Command=""{{Binding TodoCommand}}"">");
                        lines.Add($@"{sp}{sp}<MenuItem.Header><loc:Message Key=""{keyName(sub.Name)}"" Text=""{escape(sub.Text)}"" /></MenuItem.Header>");
                        lines.Add($@"{sp}</MenuItem>");
                    }
                    after.Add($@"</Menu>");
                }
                else
                {
                    // Includes YaTabPage, LinkLabel, TextBox
                    lines.Add(@$"<!-- {control.GetType().Name} {control.Name} - '{control.Text}' -->");
                    if (!string.IsNullOrEmpty(control.Text))
                    {
                        // Add message any if there's text
                        lines.Add($@"<loc:Message {attr(control)} />");
                    }
                }

                foreach (string line in lines) Console.WriteLine($"{indent}{line}");
                IEnumerable<Control> subs = control.Controls.Cast<Control>();
                if (control.Name != "tabControl")
                {
                    subs = subs.OrderBy(c => c.TabIndex);
                }
                foreach (Control sub in subs)
                {
                    recurseForm(sub, indent + subsp, shown);
                }
                foreach (string line in after) Console.WriteLine($"{indent}{line}");
            }
            recurseForm(form, "", new());
        }
#endif
    }
}
