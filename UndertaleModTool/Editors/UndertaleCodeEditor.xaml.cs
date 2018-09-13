﻿using GraphVizWrapper;
using GraphVizWrapper.Commands;
using GraphVizWrapper.Queries;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using UndertaleModLib;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;

namespace UndertaleModTool
{
    /// <summary>
    /// Logika interakcji dla klasy UndertaleCodeEditor.xaml
    /// </summary>
    public partial class UndertaleCodeEditor : UserControl
    {
        public UndertaleCode CurrentDisassembled = null;
        public UndertaleCode CurrentDecompiled = null;
        public UndertaleCode CurrentGraphed = null;

        public UndertaleCodeEditor()
        {
            InitializeComponent();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UndertaleCode code = this.DataContext as UndertaleCode;
            if (code == null)
                return;
            if (DisassemblyTab.IsSelected && code != CurrentDisassembled)
            {
                DisassembleCode(code);
            }
            if (DecompiledTab.IsSelected && code != CurrentDecompiled)
            {
                DecompileCode(code);
            }
            if (GraphTab.IsSelected && code != CurrentGraphed)
            {
                GraphCode(code);
            }
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UndertaleCode code = this.DataContext as UndertaleCode;
            if (code == null)
                return;
            if (DisassemblyTab.IsSelected && code != CurrentDisassembled)
            {
                DisassembleCode(code);
            }
            if (DecompiledTab.IsSelected && code != CurrentDecompiled)
            {
                DecompileCode(code);
            }
            if (GraphTab.IsSelected && code != CurrentGraphed)
            {
                GraphCode(code);
            }
        }

        private void DisassembleCode(UndertaleCode code)
        {
            string disasm = code.Disassembly;

            FlowDocument document = new FlowDocument();
            document.PagePadding = new Thickness(0);
            document.FontFamily = new FontFamily("Lucida Console");
            Paragraph par = new Paragraph();

            if (code.Instructions.Count > 5000)
            {
                // Disable syntax highlighting. Loading it can take a few MINUTES on large scripts.
                par.Inlines.Add(new Run(code.Disassembly));
            }
            else
            {
                Brush addressBrush = new SolidColorBrush(Color.FromRgb(50, 50, 50));
                Brush opcodeBrush = new SolidColorBrush(Color.FromRgb(0, 100, 0));
                Brush argBrush = new SolidColorBrush(Color.FromRgb(0, 0, 150));
                Brush typeBrush = new SolidColorBrush(Color.FromRgb(0, 0, 50));
                foreach (var instr in code.Instructions)
                {
                    par.Inlines.Add(new Run(instr.Address.ToString("D5") + ": ") { Foreground = addressBrush });
                    par.Inlines.Add(new Run(instr.Kind.ToString().ToLower()) { Foreground = opcodeBrush, FontWeight = FontWeights.Bold });

                    switch (UndertaleInstruction.GetInstructionType(instr.Kind))
                    {
                        case UndertaleInstruction.InstructionType.SingleTypeInstruction:
                            par.Inlines.Add(new Run("." + instr.Type1.ToOpcodeParam()) { Foreground = typeBrush });
                            par.Inlines.Add(new Run(" "));

                            if (instr.Kind == UndertaleInstruction.Opcode.Dup)
                            {
                                par.Inlines.Add(new Run(" "));
                                par.Inlines.Add(new Run(instr.DupExtra.ToString()) { Foreground = argBrush });
                            }
                            break;

                        case UndertaleInstruction.InstructionType.DoubleTypeInstruction:
                            par.Inlines.Add(new Run("." + instr.Type1.ToOpcodeParam()) { Foreground = typeBrush });
                            par.Inlines.Add(new Run("." + instr.Type2.ToOpcodeParam()) { Foreground = typeBrush });
                            par.Inlines.Add(new Run(" "));
                            break;

                        case UndertaleInstruction.InstructionType.ComparisonInstruction:
                            par.Inlines.Add(new Run("." + instr.Type1.ToOpcodeParam()) { Foreground = typeBrush });
                            par.Inlines.Add(new Run("." + instr.Type2.ToOpcodeParam()) { Foreground = typeBrush });
                            par.Inlines.Add(new Run(" "));
                            par.Inlines.Add(new Run(instr.ComparisonKind.ToString()) { Foreground = opcodeBrush });
                            break;

                        case UndertaleInstruction.InstructionType.GotoInstruction:
                            par.Inlines.Add(new Run(" "));
                            par.Inlines.Add(new Run("$" + instr.JumpOffset.ToString("+#;-#;0")) { Foreground = argBrush, ToolTip = (instr.Address + instr.JumpOffset).ToString("D5") });
                            break;

                        case UndertaleInstruction.InstructionType.PopInstruction:
                            par.Inlines.Add(new Run("." + instr.Type1.ToOpcodeParam()) { Foreground = typeBrush });
                            par.Inlines.Add(new Run("." + instr.Type2.ToOpcodeParam()) { Foreground = typeBrush });
                            par.Inlines.Add(new Run(" "));
                            Run runDest = new Run(instr.Destination.ToString()) { Foreground = argBrush, Cursor = Cursors.Hand };
                            runDest.MouseDown += (sender, e) =>
                            {
                                (Application.Current.MainWindow as MainWindow).Selected = instr.Destination;
                            };
                            par.Inlines.Add(runDest);
                            break;

                        case UndertaleInstruction.InstructionType.PushInstruction:
                            par.Inlines.Add(new Run("." + instr.Type1.ToOpcodeParam()) { Foreground = typeBrush });
                            par.Inlines.Add(new Run(" "));
                            Run valueRun = new Run(instr.Value.ToString()) { Foreground = argBrush, Cursor = (instr.Value is UndertaleObject || instr.Value is UndertaleResourceRef) ? Cursors.Hand : Cursors.Arrow };
                            if (instr.Value is UndertaleResourceRef)
                            {
                                valueRun.MouseDown += (sender, e) =>
                                {
                                    (Application.Current.MainWindow as MainWindow).Selected = (instr.Value as UndertaleResourceRef).Resource;
                                };
                            }
                            else if (instr.Value is UndertaleObject)
                            {
                                valueRun.MouseDown += (sender, e) =>
                                {
                                    (Application.Current.MainWindow as MainWindow).Selected = instr.Value;
                                };
                            }
                            par.Inlines.Add(valueRun);
                            break;

                        case UndertaleInstruction.InstructionType.CallInstruction:
                            par.Inlines.Add(new Run("." + instr.Type1.ToOpcodeParam()) { Foreground = typeBrush });
                            par.Inlines.Add(new Run(" "));
                            par.Inlines.Add(new Run(instr.Function.ToString()) { Foreground = argBrush });
                            par.Inlines.Add(new Run("(argc="));
                            par.Inlines.Add(new Run(instr.ArgumentsCount.ToString()) { Foreground = argBrush });
                            par.Inlines.Add(new Run(")"));
                            break;

                        case UndertaleInstruction.InstructionType.BreakInstruction:
                            par.Inlines.Add(new Run("." + instr.Type1.ToOpcodeParam()) { Foreground = typeBrush });
                            par.Inlines.Add(new Run(" "));
                            par.Inlines.Add(new Run(instr.Value.ToString()) { Foreground = argBrush });
                            break;
                    }

                    par.Inlines.Add(new Run("\n"));
                }
            }
            document.Blocks.Add(par);

            DisassemblyView.Document = document;

            CurrentDisassembled = code;
        }

        private static Dictionary<string, int> gettext = null;
        private void UpdateGettext(UndertaleCode gettextCode)
        {
            gettext = new Dictionary<string, int>();
            foreach(var line in Decompiler.Decompile(gettextCode).Replace("\r\n", "\n").Split('\n'))
            {
                Match m = Regex.Match(line, "^ds_map_add\\(global.text_data_en, \"(.*)\"@([0-9]+), \"(.*)\"@([0-9]+)\\)");
                if (m.Success)
                    gettext.Add(m.Groups[1].Value, Int32.Parse(m.Groups[4].Value));
            }
        }

        private async void DecompileCode(UndertaleCode code)
        {
            LoaderDialog dialog = new LoaderDialog("Decompiling", "Decompiling, please wait... This can take a while on complex scripts");
            dialog.Owner = Window.GetWindow(this);

            FlowDocument document = new FlowDocument();
            document.PagePadding = new Thickness(0);
            document.FontFamily = new FontFamily("Lucida Console");
            Paragraph par = new Paragraph();

            UndertaleCode gettextCode = null;
            if (gettext == null)
                gettextCode = (Application.Current.MainWindow as MainWindow).Data.Code.ByName("gml_Script_textdata_en");

            Task t = Task.Run(() =>
            {
                string decompiled = null;
                Exception e = null;
                try
                {
                    decompiled = Decompiler.Decompile(code).Replace("\r\n", "\n");
                }
                catch (Exception ex)
                {
                    e = ex;
                }

                if (gettextCode != null)
                    UpdateGettext(gettextCode);

                Dispatcher.Invoke(() =>
                {
                    if (e != null)
                    {
                        Brush exceptionBrush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                        par.Inlines.Add(new Run("EXCEPTION!\n") { Foreground = exceptionBrush, FontWeight = FontWeights.Bold });
                        par.Inlines.Add(new Run(e.ToString()) { Foreground = exceptionBrush });
                    }
                    else if (decompiled != null)
                    {
                        string[] lines = decompiled.Split('\n');
                        if (lines.Length > 5000)
                        {
                            par.Inlines.Add(new Run(decompiled));
                        }
                        else
                        {
                            Brush keywordBrush = new SolidColorBrush(Color.FromRgb(0, 0, 150));
                            Brush stringBrush = new SolidColorBrush(Color.FromRgb(0, 0, 200));
                            Brush commentBrush = new SolidColorBrush(Color.FromRgb(0, 150, 0));
                            Brush funcBrush = new SolidColorBrush(Color.FromRgb(100, 100, 0));

                            Dictionary<string, UndertaleFunction> funcs = new Dictionary<string, UndertaleFunction>();
                            foreach (var x in (Application.Current.MainWindow as MainWindow).Data.Functions)
                                funcs.Add(x.Name.Content, x);

                            foreach (var line in lines)
                            {
                                char[] special = { '.', ',', ')', '(', '[', ']', '>', '<', ':', ';', '=', '"' };
                                Func<char, bool> IsSpecial = (c) => Char.IsWhiteSpace(c) || special.Contains(c);
                                List<string> split = new List<string>();
                                string tok = "";
                                bool readingString = false;
                                for (int i = 0; i < line.Length; i++)
                                {
                                    if (tok == "//")
                                    {
                                        tok += line.Substring(i);
                                        break;
                                    }
                                    if (!readingString && tok.Length > 0 && (
                                        (Char.IsWhiteSpace(line[i]) != Char.IsWhiteSpace(tok[tok.Length - 1])) ||
                                        (special.Contains(line[i]) != special.Contains(tok[tok.Length - 1])) ||
                                        (special.Contains(line[i]) && special.Contains(tok[tok.Length - 1])) ||
                                        line[i] == '"'
                                        ))
                                    {
                                        split.Add(tok);
                                        tok = "";
                                    }
                                    tok += line[i];
                                    if (line[i] == '"')
                                    {
                                        if (readingString)
                                        {
                                            split.Add(tok);
                                            tok = "";
                                        }
                                        readingString = !readingString;
                                    }
                                }
                                if (tok != "")
                                    split.Add(tok);

                                Dictionary<string, UndertaleObject> usedObjects = new Dictionary<string, UndertaleObject>();
                                for (int i = 0; i < split.Count; i++)
                                {
                                    string token = split[i];
                                    if (token == "if" || token == "else" || token == "return" || token == "break" || token == "continue" || token == "while" || token == "with")
                                        par.Inlines.Add(new Run(token) { Foreground = keywordBrush, FontWeight = FontWeights.Bold });
                                    else if (token == "self" || token == "global" || token == "local" || token == "other" || token == "noone" || token == "true" || token == "false")
                                        par.Inlines.Add(new Run(token) { Foreground = keywordBrush });
                                    else if (token.StartsWith("\""))
                                        par.Inlines.Add(new Run(token) { Foreground = stringBrush });
                                    else if (token.StartsWith("//"))
                                        par.Inlines.Add(new Run(token) { Foreground = commentBrush });
                                    else if (token.StartsWith("@"))
                                    {
                                        par.Inlines.LastInline.Cursor = Cursors.Hand;
                                        par.Inlines.LastInline.MouseDown += (sender, ev) =>
                                        {
                                            MainWindow mw = Application.Current.MainWindow as MainWindow;
                                            mw.Selected = mw.Data.Strings[Int32.Parse(token.Substring(1))];
                                        };
                                    }
                                    else if (funcs.ContainsKey(token))
                                    {
                                        par.Inlines.Add(new Run(token) { Foreground = funcBrush, Cursor = Cursors.Hand });
                                        par.Inlines.LastInline.MouseDown += (sender, ev) => (Application.Current.MainWindow as MainWindow).Selected = funcs[token];
                                        if (token == "scr_gettext" && gettext != null)
                                        {
                                            if (split[i + 1] == "(" && split[i + 2].StartsWith("\"") && split[i + 3].StartsWith("@") && split[i + 4] == ")")
                                            {
                                                string id = split[i + 2].Substring(1, split[i + 2].Length - 2);
                                                if(!usedObjects.ContainsKey(id))
                                                    usedObjects.Add(id, (Application.Current.MainWindow as MainWindow).Data.Strings[gettext[id]]);
                                            }
                                        }
                                    }
                                    else if (Char.IsDigit(token[0]))
                                    {
                                        par.Inlines.Add(new Run(token) { Cursor = Cursors.Hand });
                                        par.Inlines.LastInline.MouseDown += (sender, ev) =>
                                        {
                                            // TODO: Add type resolving to the decompiler so that this is handled mostly automatically

                                            UndertaleData data = (Application.Current.MainWindow as MainWindow).Data;
                                            int id = Int32.Parse(token);
                                            List<UndertaleObject> possibleObjects = new List<UndertaleObject>();
                                            if (id < data.Sprites.Count)
                                                possibleObjects.Add(data.Sprites[id]);
                                            if (id < data.Rooms.Count)
                                                possibleObjects.Add(data.Rooms[id]);
                                            if (id < data.GameObjects.Count)
                                                possibleObjects.Add(data.GameObjects[id]);
                                            if (id < data.Backgrounds.Count)
                                                possibleObjects.Add(data.Backgrounds[id]);
                                            if (id < data.Scripts.Count)
                                                possibleObjects.Add(data.Scripts[id]);
                                            if (id < data.Paths.Count)
                                                possibleObjects.Add(data.Paths[id]);

                                            ContextMenu contextMenu = new ContextMenu();
                                            foreach(UndertaleObject obj in possibleObjects)
                                            {
                                                MenuItem item = new MenuItem();
                                                item.Header = obj.ToString();
                                                item.Click += (sender2, ev2) => (Application.Current.MainWindow as MainWindow).Selected = obj;
                                                contextMenu.Items.Add(item);
                                            }
                                            if (id > 0x00050000)
                                            {
                                                contextMenu.Items.Add(new MenuItem() { Header = "#" + id.ToString("X6") + " (color)", IsEnabled = false });
                                            }
                                            contextMenu.Items.Add(new MenuItem() { Header = id + " (number)", IsEnabled = false });
                                            (sender as Run).ContextMenu = contextMenu;
                                            contextMenu.IsOpen = true;
                                            ev.Handled = true;
                                        };
                                    }
                                    else
                                        par.Inlines.Add(new Run(token));

                                    if (token == ".")
                                    {
                                        int id;
                                        if (Int32.TryParse(split[i - 1], out id))
                                        {
                                            if (!usedObjects.ContainsKey(split[i - 1]))
                                                usedObjects.Add(split[i - 1], (Application.Current.MainWindow as MainWindow).Data.GameObjects[id]);
                                        }
                                    }
                                }
                                foreach (var gt in usedObjects)
                                {
                                    par.Inlines.Add(new Run(" // ") { Foreground = commentBrush });
                                    par.Inlines.Add(new Run(gt.Key) { Foreground = commentBrush });
                                    par.Inlines.Add(new Run(" = ") { Foreground = commentBrush });
                                    par.Inlines.Add(new Run(gt.Value.ToString()) { Foreground = commentBrush, Cursor = Cursors.Hand });
                                    par.Inlines.LastInline.MouseDown += (sender, ev) => (Application.Current.MainWindow as MainWindow).Selected = gt.Value;
                                }
                                par.Inlines.Add(new Run("\n"));
                            }
                        }
                    }

                    document.Blocks.Add(par);
                    DecompiledView.Document = document;
                    CurrentDecompiled = code;
                    dialog.Hide();
                });
            });
            dialog.ShowDialog();
            await t;
        }

        private async void GraphCode(UndertaleCode code)
        {
            LoaderDialog dialog = new LoaderDialog("Generating graph", "Generating graph, please wait...");
            dialog.Owner = Window.GetWindow(this);
            Task t = Task.Run(() =>
            {
                ImageSource image = null;
                try
                {
                    var getStartProcessQuery = new GetStartProcessQuery();
                    var getProcessStartInfoQuery = new GetProcessStartInfoQuery();
                    var registerLayoutPluginCommand = new RegisterLayoutPluginCommand(getProcessStartInfoQuery, getStartProcessQuery);
                    var wrapper = new GraphGeneration(getStartProcessQuery, getProcessStartInfoQuery, registerLayoutPluginCommand);

                    var blocks = Decompiler.DecompileFlowGraph(code);
                    string dot = Decompiler.ExportFlowGraph(blocks);
                    Debug.WriteLine(dot);
                    byte[] output = wrapper.GenerateGraph(dot, Enums.GraphReturnType.Png); // TODO: Use SVG instead

                    image = new ImageSourceConverter().ConvertFrom(output) as ImageSource;
                }
                catch(Exception e)
                {
                    Debug.WriteLine(e.ToString());
                    MessageBox.Show(e.Message, "Graph generation failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                Dispatcher.Invoke(() =>
                {
                    GraphView.Source = image;
                    CurrentGraphed = code;
                    dialog.Hide();
                });
            });
            dialog.ShowDialog();
            await t;
        }
    }
}