using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
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
using System.Management.Automation;
using System.Collections.ObjectModel;
using System.Threading;
using NetFwTypeLib;
using System.Net;
using MahApps.Metro.Controls;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Destiny2SoloMode.Properties;

namespace Destiny2SoloMode
{
    // TODO: Windows 7 Support
    public partial class MainWindow : MetroWindow
    {
        private FirewallManager firewallManager = new FirewallManager();
        private static RichTextBox OutputConsole;
        private static bool IsRunning = false;
        private static bool PortsUpdated = false;
        private static readonly string[] defaultLocalPorts = new string[]{"", ""}; //"1935,3097-3098,3478-3480"
        private static readonly string[] defaultRemotePorts = new string[]{"27000-27100", "27000-27100" };

        public MainWindow()
        {
            InitializeComponent();
        }

        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach(var ip in host.AddressList)
            {
                if(ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("An error occurred whilst trying to get your local IP address!");
        }

        public static void AddMessageToOutputConsole(string message, SolidColorBrush messageColour)
        {
            App.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, (ThreadStart)delegate ()
            {
                Paragraph para = new Paragraph(new Run(message));
                para.Margin = new Thickness(0);
                para.Foreground = messageColour;
                OutputConsole.Document.Blocks.Add(para);
            });
        }

        private static void ClearOutputConsole()
        {
            App.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, (ThreadStart)delegate ()
            {
                OutputConsole.Document.Blocks.Clear();
            });
        }

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Make a static reference of rtbOutputConsole
            OutputConsole = rtbOutputConsole;
            // Prevent Default Tooltip Timeout
            ToolTipService.ShowDurationProperty.OverrideMetadata(
                typeof(DependencyObject), new FrameworkPropertyMetadata(Int32.MaxValue));
            // Check if User is an Admin (probably redundant since the program requires admin privileges anyway)
            /*
            bool isAdmin = false;
            using(WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            */

            if(!String.IsNullOrWhiteSpace(Settings.Default.TCPPorts))
            {
                tbTCPPorts.Text = Settings.Default.TCPPorts;
            } else {
                tbTCPPorts.Text = defaultLocalPorts[0];
            }

            if (!String.IsNullOrWhiteSpace(Settings.Default.UDPPorts))
            {
                tbUDPPorts.Text = Settings.Default.UDPPorts;
            } else {
                tbUDPPorts.Text = defaultLocalPorts[1];
            }

            if(!String.IsNullOrWhiteSpace(Settings.Default.TCPRemote))
            {
                tbTCPPortsRemote.Text = Settings.Default.TCPRemote;
            } else {
                tbUDPPorts.Text = defaultRemotePorts[0];
            }

            if(!String.IsNullOrWhiteSpace(Settings.Default.UDPRemote))
            {
                tbUDPPortsRemote.Text = Settings.Default.UDPRemote;
            } else {
                tbUDPPorts.Text = defaultRemotePorts[1];
            }

            IsRunning = true;
        }

        private void bClearOutput_Click(object sender, RoutedEventArgs e)
        {
            ClearOutputConsole();
        }

        private void HighlightOutputText(string text, Color highlightColor, bool bold, bool italic)
        {
            rtbOutputConsole.SelectAll();

            Regex trueRegEx = new Regex(text.Trim(), RegexOptions.Compiled | RegexOptions.IgnoreCase);
            TextPointer position = rtbOutputConsole.Document.ContentStart;
            List<TextRange> ranges = new List<TextRange>();

            while (position != null)
            {
                if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string currentText = position.GetTextInRun(LogicalDirection.Forward);
                    var matches = trueRegEx.Matches(currentText);

                    foreach (Match match in matches)
                    {
                        TextPointer start = position.GetPositionAtOffset(match.Index);
                        TextPointer end = start.GetPositionAtOffset(text.Trim().Length);

                        TextRange textRange = new TextRange(start, end);
                        ranges.Add(textRange);
                    }
                }
                position = position.GetNextContextPosition(LogicalDirection.Forward);
            }

            foreach (TextRange range in ranges)
            {
                range.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(highlightColor));

                if(bold)
                    range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);

                if(italic)
                    range.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
            }
        }

        private void rtbOutputConsole_TextChanged(object sender, TextChangedEventArgs e)
        {
            HighlightOutputText("true", Colors.Chartreuse, true, false);
            HighlightOutputText("false", Colors.Red, true, false);
            HighlightOutputText("ALLOWED", Colors.Chartreuse, true, false);
            HighlightOutputText("BLOCKED", Colors.Red, true, false);
            string[] customRuleNames = new string[]{"Destiny 2 Inbound TCP", "Destiny 2 Inbound UDP", "Destiny 2 Outbound TCP", "Destiny 2 Outbound UDP"};
            foreach(string ruleName in customRuleNames)
            {
                HighlightOutputText(ruleName, Colors.Aquamarine, false, true);
            }
        }


        private void GridButton_MouseEnter(object sender, MouseEventArgs e)
        {
            Grid grid = (Grid)sender;

            if(grid != null)
            {
                if(grid == gFirewallEnabled)
                {
                    if (gFirewallEnabled.Background == Brushes.Gray)
                        gFirewallEnabled.Background = Brushes.DarkGray;
                    if (gFirewallEnabled.Background == Brushes.Green)
                        gFirewallEnabled.Background = Brushes.DarkGreen;
                    if (gFirewallEnabled.Background == Brushes.Red)
                        gFirewallEnabled.Background = Brushes.DarkRed;
                } else if(grid == gDefaultRules) {
                    if(gFirewallEnabled.Background == Brushes.Green)
                    {
                        if (gDefaultRules.Background == Brushes.Gray)
                            gDefaultRules.Background = Brushes.DarkGray;
                        if (gDefaultRules.Background == Brushes.Green)
                            gDefaultRules.Background = Brushes.DarkGreen;
                        if (gDefaultRules.Background == Brushes.Red)
                            gDefaultRules.Background = Brushes.DarkRed;
                    }
                } else if(grid == gCustomRules) {
                    if(gFirewallEnabled.Background == Brushes.Green)
                    {
                        if (gCustomRules.Background == Brushes.Gray)
                            gCustomRules.Background = Brushes.DarkGray;
                        if (gCustomRules.Background == Brushes.Green)
                            gCustomRules.Background = Brushes.DarkGreen;
                        if (gCustomRules.Background == Brushes.Red)
                            gCustomRules.Background = Brushes.DarkRed;
                    }
                } else if(grid == gToggleMatchmaking) {
                    if (gFirewallEnabled.Background == Brushes.Green && gDefaultRules.Background == Brushes.Green && gCustomRules.Background == Brushes.Green)
                    {
                        if (gToggleMatchmaking.Background == Brushes.Gray)
                            gToggleMatchmaking.Background = Brushes.DarkGray;
                        if (gToggleMatchmaking.Background == Brushes.Green)
                            gToggleMatchmaking.Background = Brushes.DarkGreen;
                        if (gToggleMatchmaking.Background == Brushes.Red)
                            gToggleMatchmaking.Background = Brushes.DarkRed;
                        if (gToggleMatchmaking.Background == Brushes.Orange)
                            gToggleMatchmaking.Background = Brushes.DarkOrange;
                    }
                }
            }
        }

        private void GridButton_MouseLeave(object sender, MouseEventArgs e)
        {
            Grid grid = (Grid)sender;

            if (grid != null)
            {
                if (grid == gFirewallEnabled)
                {
                    if (gFirewallEnabled.Background == Brushes.DarkGray)
                        gFirewallEnabled.Background = Brushes.Gray;
                    if (gFirewallEnabled.Background == Brushes.DarkGreen)
                        gFirewallEnabled.Background = Brushes.Green;
                    if (gFirewallEnabled.Background == Brushes.DarkRed)
                        gFirewallEnabled.Background = Brushes.Red;
                } else if (grid == gDefaultRules) {
                    if (gDefaultRules.Background == Brushes.DarkGray)
                        gDefaultRules.Background = Brushes.Gray;
                    if (gDefaultRules.Background == Brushes.DarkGreen)
                        gDefaultRules.Background = Brushes.Green;
                    if (gDefaultRules.Background == Brushes.DarkRed)
                        gDefaultRules.Background = Brushes.Red;
                } else if (grid == gCustomRules) {
                    if (gCustomRules.Background == Brushes.DarkGray)
                        gCustomRules.Background = Brushes.Gray;
                    if (gCustomRules.Background == Brushes.DarkGreen)
                        gCustomRules.Background = Brushes.Green;
                    if (gCustomRules.Background == Brushes.DarkRed)
                        gCustomRules.Background = Brushes.Red;
                } else if (grid == gToggleMatchmaking) {
                    if (gDefaultRules.Background != Brushes.Red || gCustomRules.Background != Brushes.Red)
                    {
                        if (gToggleMatchmaking.Background == Brushes.DarkGray)
                            gToggleMatchmaking.Background = Brushes.Gray;
                        if (gToggleMatchmaking.Background == Brushes.DarkGreen)
                            gToggleMatchmaking.Background = Brushes.Green;
                        if (gToggleMatchmaking.Background == Brushes.DarkRed)
                            gToggleMatchmaking.Background = Brushes.Red;
                        if (gToggleMatchmaking.Background == Brushes.DarkOrange)
                            gToggleMatchmaking.Background = Brushes.Orange;
                    }
                }
            }
        }

        private void GridButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Grid grid = (Grid)sender;

            if(grid != null)
            {
                // Toggle Firewall Button
                if(grid == gFirewallEnabled)
                {
                    if(firewallManager.IsFirewallEnabled())
                    {
                        firewallManager.ToggleFirewall(false);
                        gFirewallEnabled.Background = Brushes.Red;
                        tbFirewallEnabled.Text = "Firewall Disabled";
                    } else {
                        firewallManager.ToggleFirewall(true);
                        gFirewallEnabled.Background = Brushes.Green;
                        tbFirewallEnabled.Text = "Firewall Enabled";
                    }
                }
                // Default Rules Button
                if(grid == gDefaultRules)
                {
                    if(gFirewallEnabled.Background == Brushes.Green)
                    {
                        bool enableOrDisable = !(firewallManager.IsDefaultTCPRuleEnabled() && firewallManager.IsDefaultUDPRuleEnabled());
                        bool? rulesChanged = firewallManager.ToggleDefaultRules(enableOrDisable);

                        if (rulesChanged.HasValue)
                        {
                            if (rulesChanged.Value)
                            {
                                gDefaultRules.Background = Brushes.Red;
                                tbDefaultRules.Text = "Disable Default Rules";
                            } else {
                                gDefaultRules.Background = Brushes.Green;
                                tbDefaultRules.Text = "Default Rules Disabled";
                            }
                        } else {
                            AddMessageToOutputConsole("Error - ToggleDefaultRules() returned null!", Brushes.Red);
                        }
                    } else {
                        //AddMessageToOutputConsole("To operate this button, enable the firewall you must.", Brushes.Azure);
                    }
                }
                // Custom Rules Button
                if(grid == gCustomRules)
                {
                    if (gFirewallEnabled.Background == Brushes.Green)
                    {
                        if (firewallManager.DoesCustomRulesExist())
                        {
                            firewallManager.RemoveCustomRules();
                            gCustomRules.Background = Brushes.Red;
                            gToggleMatchmaking.Background = Brushes.Gray;
                            tbCustomRules.Text = "Add Custom Rules";
                        } else {
                            // TODO: Verify ports before doing this!
                            string[] localPorts = new string[]{tbTCPPorts.Text, tbUDPPorts.Text};
                            string[] remotePorts = new string[]{tbTCPPortsRemote.Text, tbUDPPortsRemote.Text};

                            firewallManager.AddCustomRules(localPorts, remotePorts);
                            PortsUpdated = false;
                            gCustomRules.Background = Brushes.Green;
                            tbCustomRules.Text = "Custom Rules Added";
                            if(gDefaultRules.Background == Brushes.Green)
                            {
                                gToggleMatchmaking.Background = Brushes.Orange;
                            }
                        }
                    } else {
                        //AddMessageToOutputConsole("To operate this button, disable the default rules first you must.", Brushes.Azure);
                    }
                }
                // Toggle Matchmaking Buttons
                if(grid == gToggleMatchmaking)
                {
                    if(gFirewallEnabled.Background == Brushes.Green && gDefaultRules.Background == Brushes.Green && gCustomRules.Background == Brushes.Green && firewallManager.DoesCustomRulesExist())
                    {
                        bool enableOrDisable = !firewallManager.AreCustomRulesEnabled();

                        // This is a mess, haven't been arsed to refactor it yet
                        if(enableOrDisable)
                        {
                            if(!IsDestiny2Running())
                            {
                                // Trying to set the custom rules to true without Destiny 2 already running
                                if (MessageBox.Show("Destiny 2 was not found to be running, blocking the matchmaking now may result in you not being able to play Destiny 2. It is recommended that you block it whilst ingame AND in orbit.\n\nDo you wish to proceed anyway?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                                {
                                    if (firewallManager.ToggleCustomRules(enableOrDisable).Value == true)
                                    {
                                        svgMatchmakingStatus.Icon = FontAwesome5.EFontAwesomeIcon.Solid_Lock;
                                        gToggleMatchmaking.Background = Brushes.Green;
                                        tbToggleMatchmaking.Text = "Matchmaking Blocked";
                                        AddMessageToOutputConsole($"Matchmaking is currently BLOCKED.", Brushes.Orange);
                                    } else {
                                        svgMatchmakingStatus.Icon = FontAwesome5.EFontAwesomeIcon.Solid_LockOpen;
                                        gToggleMatchmaking.Background = Brushes.Orange;
                                        tbToggleMatchmaking.Text = "Matchmaking Allowed";
                                        AddMessageToOutputConsole($"Matchmaking is currently ALLOWED.", Brushes.Orange);
                                    }
                                }
                            } else {
                                if (firewallManager.ToggleCustomRules(enableOrDisable).Value == true)
                                {
                                    svgMatchmakingStatus.Icon = FontAwesome5.EFontAwesomeIcon.Solid_Lock;
                                    gToggleMatchmaking.Background = Brushes.Green;
                                    tbToggleMatchmaking.Text = "Matchmaking Blocked";
                                    AddMessageToOutputConsole($"Matchmaking is currently BLOCKED.", Brushes.Orange);
                                } else {
                                    svgMatchmakingStatus.Icon = FontAwesome5.EFontAwesomeIcon.Solid_LockOpen;
                                    gToggleMatchmaking.Background = Brushes.Orange;
                                    tbToggleMatchmaking.Text = "Matchmaking Allowed";
                                    AddMessageToOutputConsole($"Matchmaking is currently ALLOWED.", Brushes.Orange);
                                }
                            }
                        } else {
                            if (firewallManager.ToggleCustomRules(enableOrDisable).Value == true)
                            {
                                svgMatchmakingStatus.Icon = FontAwesome5.EFontAwesomeIcon.Solid_Lock;
                                gToggleMatchmaking.Background = Brushes.Green;
                                tbToggleMatchmaking.Text = "Matchmaking Blocked";
                                AddMessageToOutputConsole($"Matchmaking is currently BLOCKED.", Brushes.Orange);
                            } else {
                                svgMatchmakingStatus.Icon = FontAwesome5.EFontAwesomeIcon.Solid_LockOpen;
                                gToggleMatchmaking.Background = Brushes.Orange;
                                tbToggleMatchmaking.Text = "Matchmaking Allowed";
                                AddMessageToOutputConsole($"Matchmaking is currently ALLOWED.", Brushes.Orange);
                            }
                        }
                    } else {
                        //AddMessageToOutputConsole("To operate this button, need to add the custom rules first you do.", Brushes.Azure);
                    }
                }
            }
        }

        private bool IsDestiny2Running()
        {
            Process process = Process.GetProcessesByName("destiny2").FirstOrDefault();
            if(process != null)
            {
                return true;
            } else {
                return false;
            }
        }

        private void MetroWindow_ContentRendered(object sender, EventArgs e)
        {
            #region Firewall Enabled
            bool firewallEnabled = firewallManager.IsFirewallEnabled();
            AddMessageToOutputConsole($"Windows Firewall Enabled: {firewallEnabled}", Brushes.Azure);
            if (!firewallEnabled)
            {
                if (MessageBox.Show("In order to prevent matchmaking in Destiny 2 you need to enable the Windows Firewall.\n\nDo you want me to try and enable it for you?\n\nIf you choose No, you'll have to do it manually.", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    firewallManager.ToggleFirewall(true);
                    if(firewallManager.IsFirewallEnabled())
                    {
                        gFirewallEnabled.Background = Brushes.Green;
                        tbFirewallEnabled.Text = "Firewall Enabled";
                        MessageBox.Show("Successfully enabled the Windows Firewall!", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                } else {
                    gFirewallEnabled.Background = Brushes.Red;
                    tbFirewallEnabled.Text = "Firewall Disabled";
                }
            } else {
                gFirewallEnabled.Background = Brushes.Green;
                tbFirewallEnabled.Text = "Firewall Enabled";
            }
            #endregion

            #region Default Destiny 2 Rules
            bool defaultRulesExist = firewallManager.DoesDefaultRulesExist();
            AddMessageToOutputConsole($"Default Destiny 2 Rules Exist: {defaultRulesExist}", Brushes.Azure);

            if (defaultRulesExist)
            {
                bool defaultTCPRuleEnabled = firewallManager.IsDefaultTCPRuleEnabled();
                bool defaultUDPRuleEnabled = firewallManager.IsDefaultUDPRuleEnabled();
                AddMessageToOutputConsole($"Default Destiny 2 TCP Rule Enabled: {defaultTCPRuleEnabled}", Brushes.Azure);
                AddMessageToOutputConsole($"Default Destiny 2 UDP Rule Enabled: {defaultUDPRuleEnabled}", Brushes.Azure);
                if(defaultTCPRuleEnabled && defaultUDPRuleEnabled)
                {
                    // Default rules are enabled
                    gDefaultRules.Background = Brushes.Red;
                    tbDefaultRules.Text = "Disable Default Rules";
                } else if(!defaultTCPRuleEnabled && !defaultUDPRuleEnabled) {
                    // Default rules are disabled
                    gDefaultRules.Background = Brushes.Green;
                    tbDefaultRules.Text = "Default Rules Disabled";
                } else {
                    // One or the other is enabled, weird - try and disable?
                    firewallManager.ToggleDefaultRules(false);
                    gDefaultRules.Background = Brushes.Green;
                    tbDefaultRules.Text = "Default Rules Disabled";
                }
            } else {
                gDefaultRules.Background = Brushes.Green;
                tbDefaultRules.Text = "No Default Rules Found";
            }
            #endregion

            #region Custom Destiny 2 Rules & Matchmaking
            bool customRulesExist = firewallManager.DoesCustomRulesExist();
            AddMessageToOutputConsole($"Custom Destiny 2 Rules Exist: {customRulesExist}", Brushes.Azure);

            if (customRulesExist)
            {
                gCustomRules.Background = Brushes.Green;
                tbCustomRules.Text = "Custom Rules Added";
            } else {
                gCustomRules.Background = Brushes.Red;
                tbCustomRules.Text = "Add Custom Rules";
            }
            #endregion

            #region Toggle Matchmaking
            if(customRulesExist)
            {
                bool customRulesEnabled = firewallManager.AreCustomRulesEnabled();
                AddMessageToOutputConsole($"Custom Destiny 2 Rules Enabled: {customRulesEnabled}", Brushes.Azure);
                if (customRulesEnabled)
                {
                    bool destinyRunning = IsDestiny2Running();
                    AddMessageToOutputConsole($"Destiny 2 Running: {destinyRunning}", Brushes.Azure);

                    if(destinyRunning)
                    {
                        // Rules Enabled + Destiny 2 Running = Good
                        gToggleMatchmaking.Background = Brushes.Green;
                        tbToggleMatchmaking.Text = "Matchmaking Blocked";
                    } else {
                        // Rules Enabled + Destiny 2 NOT Running = Bad, may prevent being able to play
                        firewallManager.ToggleCustomRules(false);
                        gToggleMatchmaking.Background = Brushes.Orange;
                        tbToggleMatchmaking.Text = "Matchmaking Allowed";
                        MessageBox.Show("Blocking matchmaking before Destiny 2 is running may result in you being unable to play.\n\nThe custom rules have been automatically disabled.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                } else {
                    // Rules Disabled
                    gToggleMatchmaking.Background = Brushes.Orange;
                    tbToggleMatchmaking.Text = "Matchmaking Allowed";
                }
            } else {
                gToggleMatchmaking.Background = Brushes.Gray;
                tbToggleMatchmaking.Text = "Toggle Matchmaking";
            }
            #endregion
        }

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(tbTCPPorts.Text != Settings.Default.TCPPorts)
            {
                Settings.Default.TCPPorts = tbTCPPorts.Text;
                Settings.Default.Save();
            }
            if(tbUDPPorts.Text != Settings.Default.UDPPorts)
            {
                Settings.Default.UDPPorts = tbUDPPorts.Text;
                Settings.Default.Save();
            }
        }

        private void TabItem_GotFocus(object sender, RoutedEventArgs e)
        {
            if(PortsUpdated && firewallManager.DoesCustomRulesExist())
            {
                if(MessageBox.Show("Ports have been changed but not yet updated. Would you like to update them now? You can do this manually later by re-applying the custom rules.", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    string[] localPorts = new string[]{tbTCPPorts.Text, tbUDPPorts.Text};
                    string[] remotePorts = new string[]{tbTCPPortsRemote.Text, tbUDPPortsRemote.Text};

                    firewallManager.AddCustomRules(localPorts, remotePorts);
                    PortsUpdated = false;
                }
            }
        }

        private static bool ArePortsValid(string portString)
        {
            if(String.IsNullOrWhiteSpace(portString))
            {
                return false;
            }

            Regex rx = new Regex(@"[0-9\-0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            MatchCollection rxMatches = rx.Matches(portString);
            //Console.WriteLine(rxMatches. .ToString());

            if(rxMatches.Count > 0)
            {
                foreach(Match match in rxMatches)
                {
                    if(match.Value.Contains("-"))
                    {
                        // Port Range
                        string[] values = match.Value.Split('-');
                        foreach(string value in values)
                        {
                            try
                            {
                                if(Convert.ToInt32(value) < 65536)
                                {
                                    //Continue
                                } else {
                                    return false;
                                }
                            } catch (OverflowException) {
                                return false;
                            }
                        }
                    } else {
                        // Single Port
                        if (Convert.ToInt32(match.Value) < 65536)
                        {
                            //Continue
                        } else {
                            return false;
                        }
                    }
                }
                return true;
            } else {
                return false;
            }

        }

        #region Button Managers
        private void ToolButton_Manager(object sender, RoutedEventArgs args)
        {
            // Advanced Firewall Policies
            if(sender == bOpenFirewallSettings)
            {
                Process.Start("wf.msc");
            }
            // Resource Monitor
            if(sender == bOpenResMon)
            {
                Process.Start("resmon.exe");
            }
            // Reset Settings
            if(sender == bResetSettings)
            {
                if(MessageBox.Show("Warning!\n\nThis will reset the program's settings to their default values. Are you sure you want to proceed?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    Settings.Default.Reset();
                    Settings.Default.Save();
                    if(firewallManager.AreCustomRulesEnabled())
                    {
                        firewallManager.RemoveCustomRules();
                    }
                    if(firewallManager.DoesDefaultRulesExist())
                    {
                        firewallManager.ToggleDefaultRules(true);
                    }
                }
            }
            // Firewall Defaults
            if(sender == bRestoreLocals)
            {
                if(MessageBox.Show("This will restore your firewall policies to their default values. This is unharmful, some applications may ask for network access after doing this.\n\nDo you want to proceed?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    firewallManager.RestoreLocalFirewallDefaults();
                }
            }
        }
        private void ResetButton_Manager(object sender, RoutedEventArgs args)
        {
            bool somethingChanged = false;
            // Local Ports, TCP & UDP
            if(sender == bResetTCPPorts)
            {
                if(bResetTCPPorts.Content.ToString() == "Reset")
                {
                    tbTCPPorts.Text = defaultLocalPorts[0];
                    Settings.Default.TCPPorts = tbTCPPorts.Text;
                    somethingChanged = true;
                    PortsUpdated = true;
                } else if(bResetTCPPorts.Content.ToString() == "Save") {
                    // TODO: Verify ports!
                    Settings.Default.TCPPorts = tbTCPPorts.Text;
                    bResetTCPPorts.Content = "Reset";
                    somethingChanged = true;
                    PortsUpdated = true;
                }
            }
            if(sender == bResetUDPPorts)
            {
                if (bResetUDPPorts.Content.ToString() == "Reset")
                {
                    tbUDPPorts.Text = defaultLocalPorts[1];
                    Settings.Default.UDPPorts = tbUDPPorts.Text;
                    somethingChanged = true;
                    PortsUpdated = true;
                } else if (bResetUDPPorts.Content.ToString() == "Save") {
                    // TODO: Verify ports!
                    Settings.Default.UDPPorts = tbUDPPorts.Text;
                    bResetUDPPorts.Content = "Reset";
                    somethingChanged = true;
                    PortsUpdated = true;
                }
            }
            // Remote Ports, TCP & UDP
            if(sender == bResetTCPRemote)
            {
                if (bResetTCPRemote.Content.ToString() == "Reset")
                {
                    tbTCPPortsRemote.Text = defaultRemotePorts[0];
                    Settings.Default.TCPRemote = tbTCPPortsRemote.Text;
                    somethingChanged = true;
                    PortsUpdated = true;
                } else if (bResetTCPRemote.Content.ToString() == "Save") {
                    // TODO: Verify ports!
                    Settings.Default.TCPRemote = tbTCPPortsRemote.Text;
                    bResetTCPRemote.Content = "Reset";
                    somethingChanged = true;
                    PortsUpdated = true;
                }
            }
            if(sender == bResetUDPRemote)
            {
                if (bResetUDPRemote.Content.ToString() == "Reset")
                {
                    tbUDPPortsRemote.Text = defaultRemotePorts[1];
                    Settings.Default.UDPRemote = tbUDPPortsRemote.Text;
                    somethingChanged = true;
                    PortsUpdated = true;
                } else if (bResetUDPRemote.Content.ToString() == "Save") {
                    // TODO: Verify ports!
                    Settings.Default.UDPRemote = tbUDPPortsRemote.Text;
                    bResetUDPRemote.Content = "Reset";
                    somethingChanged = true;
                    PortsUpdated = true;
                }
            }

            if(somethingChanged)
            {
                Settings.Default.Save();
            }
        }
        #endregion

        private void TextFields_Changed(object sender, TextChangedEventArgs e)
        {
            if (IsRunning)
            {
                // Local Ports
                if (sender == tbTCPPorts)
                {
                    if (tbTCPPorts.Text != Settings.Default.TCPPorts)
                    {
                        bResetTCPPorts.Content = "Save";
                    } else {
                        bResetTCPPorts.Content = "Reset";
                    }
                }
                if (sender == tbUDPPorts)
                {
                    if (tbUDPPorts.Text != Settings.Default.UDPPorts)
                    {
                        bResetUDPPorts.Content = "Save";
                    } else {
                        bResetUDPPorts.Content = "Reset";
                    }
                }
                // Remote Ports
                if (sender == tbTCPPortsRemote)
                {
                    if (tbTCPPortsRemote.Text != Settings.Default.TCPRemote)
                    {
                        bResetTCPRemote.Content = "Save";
                    } else {
                        bResetTCPRemote.Content = "Reset";
                    }
                }
                if (sender == tbUDPPortsRemote)
                {
                    if (tbUDPPortsRemote.Text != Settings.Default.UDPRemote)
                    {
                        bResetUDPRemote.Content = "Save";
                    } else {
                        bResetUDPRemote.Content = "Reset";
                    }
                }
            }
        }
    }
}
