using NetFwTypeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Destiny2SoloMode
{
    public class FirewallManager
    {
        private List<string> customRuleNames = new List<string>(new string[]{"Destiny 2 Inbound TCP", "Destiny 2 Inbound UDP", "Destiny 2 Outbound TCP", "Destiny 2 Outbound UDP"});

        public bool RuleExists(string name)
        {
            INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
            foreach (INetFwRule firewallRule in firewallPolicy.Rules)
            {
                if (firewallRule.Name != null && firewallRule.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public bool RuleIsEnabled(string name)
        {
            INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));

            foreach (INetFwRule firewallRule in firewallPolicy.Rules)
            {
                if (firewallRule.Name != null && firewallRule.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return firewallRule.Enabled;
                }
            }
            return false;
        }

        public bool IsFirewallEnabled()
        {
            INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
            if (firewallPolicy != null)
            {
                NET_FW_PROFILE_TYPE2_ fwCurrentProfileTypes = (NET_FW_PROFILE_TYPE2_)firewallPolicy.CurrentProfileTypes;
                bool isEnabled = firewallPolicy.FirewallEnabled[fwCurrentProfileTypes];
                return isEnabled;
            }
            throw new Exception("Error! Unable to fetch the Firewall Policies! [IsFirewallEnabled]");
        }

        public void ToggleFirewall(bool newState)
        {
            INetFwPolicy2 firewallPolicy = GetFirewallPolicy();

            if (firewallPolicy != null)
            {
                NET_FW_PROFILE_TYPE2_ fwCurrentProfileTypes2 = (NET_FW_PROFILE_TYPE2_)firewallPolicy.CurrentProfileTypes;
                firewallPolicy.FirewallEnabled[fwCurrentProfileTypes2] = newState;
                SendMessageToOutputConsole($"Changing Firewall Enabled to [{newState}]...", Brushes.Azure);
            } else {
                throw new Exception("Error! Unable to fetch the Firewall Policies! [ToggleFirewall]");
            }
        }

        private INetFwPolicy2 GetFirewallPolicy()
        {
            INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
            if(firewallPolicy != null)
            {
                return firewallPolicy;
            }
            throw new Exception("Error! Unable to fetch the Firewall Policies!");
        }

        private INetFwRule GetFirewallRule(string ruleName)
        {
            if(!String.IsNullOrWhiteSpace(ruleName))
            {
                if(RuleExists(ruleName))
                {
                    INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
                    return firewallPolicy.Rules.Item(ruleName);
                }
            }
            throw new Exception($"Error! Unable to find rule \"{ruleName}\"!");
        }

        public bool DoesCustomRulesExist()
        {
            foreach (string ruleName in customRuleNames)
            {
                if(!RuleExists(ruleName))
                {
                    return false;
                }
            }
            return true;
        }

        public bool DoesDefaultRulesExist()
        {
            INetFwPolicy2 firewallPolicy = GetFirewallPolicy();
            bool tcpFound = false;
            bool udpFound = false;

            foreach (INetFwRule firewallRule in firewallPolicy.Rules)
            {
                if (firewallRule.Name != null && firewallRule.Name.Equals("Destiny 2", StringComparison.OrdinalIgnoreCase))
                {
                    if(firewallRule.Protocol == 6)
                    {
                        tcpFound = true;
                    } else if(firewallRule.Protocol == 17) {
                        udpFound = true;
                    }
                }
            }

            if(tcpFound || udpFound)
            {
                return true;
            } else {
                return false;
            }
        }

        private INetFwRule GetDefaultTCPRule()
        {
            INetFwPolicy2 firewallPolicy = GetFirewallPolicy();

            foreach(INetFwRule firewallRule in firewallPolicy.Rules)
            {
                if (firewallRule.Name != null && firewallRule.Name.Equals("Destiny 2", StringComparison.OrdinalIgnoreCase))
                {
                    if (firewallRule.Protocol == 6)
                    {
                        return firewallRule;
                    }
                }
            }

            throw new Exception($"Error! Unable to find the default Destiny 2 rule for inbound TCP traffic!");
        }

        private INetFwRule GetDefaultUDPRule()
        {
            INetFwPolicy2 firewallPolicy = GetFirewallPolicy();

            foreach (INetFwRule firewallRule in firewallPolicy.Rules)
            {
                if (firewallRule.Name != null && firewallRule.Name.Equals("Destiny 2", StringComparison.OrdinalIgnoreCase))
                {
                    if (firewallRule.Protocol == 17)
                    {
                        return firewallRule;
                    }
                }
            }

            throw new Exception($"Error! Unable to find the default Destiny 2 rule for inbound UDP traffic!");
        }

        public bool IsDefaultTCPRuleEnabled()
        {
            INetFwRule tcpRule = GetDefaultTCPRule();
            return tcpRule.Enabled;
        }

        public bool IsDefaultUDPRuleEnabled()
        {
            INetFwRule udpRule = GetDefaultUDPRule();
            return udpRule.Enabled;
        }

        public bool AreCustomRulesEnabled()
        {
            foreach(string ruleName in customRuleNames)
            {
                if(!RuleExists(ruleName) || !RuleIsEnabled(ruleName))
                {
                    return false;
                }
            }
            return true;
        }

        private static void SendMessageToOutputConsole(string message, SolidColorBrush messageColour)
        {
            App.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, (ThreadStart)delegate ()
            {
                Paragraph para = new Paragraph(new Run(message));
                para.Margin = new Thickness(0);
                para.Foreground = messageColour;
                ((MainWindow)System.Windows.Application.Current.MainWindow).rtbOutputConsole.Document.Blocks.Add(para);
            });
        }

        public void RestoreLocalFirewallDefaults()
        {
            INetFwPolicy2 firewallPolicy = GetFirewallPolicy();

            firewallPolicy.RestoreLocalFirewallDefaults();
        }

        public void RemoveCustomRules()
        {
            INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));

            List<string> rulesToDelete = new List<string>();
            foreach (string ruleName in customRuleNames)
            {
                if (RuleExists(ruleName))
                {
                    rulesToDelete.Add(ruleName);
                }
            }
            if (rulesToDelete.Count > 0)
            {
                foreach (string ruleToDelete in rulesToDelete)
                {
                    firewallPolicy.Rules.Remove(ruleToDelete);
                    SendMessageToOutputConsole($"Deleting Custom Rule: {ruleToDelete}", Brushes.Azure);
                }
            }
            // TODO: Make sure the rules were actually added?
            //((MainWindow)System.Windows.Application.Current.MainWindow).gCustomRules.Background = Brushes.Red;
            //((MainWindow)System.Windows.Application.Current.MainWindow).gToggleMatchmaking.Background = Brushes.Red;
        }

        public void AddCustomRules(string[] localPorts, string[] remotePorts)
        {
            // Clear Any Existing Rules
            RemoveCustomRules();

            INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));

            foreach (string ruleName in customRuleNames)
            {
                INetFwRule firewallRule = (INetFwRule)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule"));

                firewallRule.Name = ruleName;
                firewallRule.Description = "Destiny 2 Solo Mode - Attempts to Prevent Matchmaking";
                firewallRule.Action = NET_FW_ACTION_.NET_FW_ACTION_BLOCK;
                firewallRule.Direction = (ruleName.Contains("Inbound")) ? NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN : NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_OUT;
                firewallRule.InterfaceTypes = "All";
                firewallRule.Protocol = (ruleName.Contains("TCP")) ? 6 : 17; // 6 = TCP, 17 = UDP
                firewallRule.LocalPorts = (ruleName.Contains("TCP")) ? localPorts[0] : localPorts[1];
                firewallRule.RemotePorts = (ruleName.Contains("TCP")) ? remotePorts[0] : remotePorts[1];
                firewallRule.Enabled = false;

                firewallPolicy.Rules.Add(firewallRule);
                SendMessageToOutputConsole($"Added Custom Rule: {ruleName}", Brushes.Azure);
            }
        }
        /*
        public void AddCustomRules(string tcpPorts, string udpPorts)
        {
            // Clear Any Existing Rules
            RemoveCustomRules();

            INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));

            foreach (string ruleName in customRuleNames)
            {
                INetFwRule firewallRule = (INetFwRule)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule"));

                firewallRule.Name = ruleName;
                firewallRule.Description = "Destiny 2 Solo Mode - Attempts to Prevent Matchmaking";
                firewallRule.Action = NET_FW_ACTION_.NET_FW_ACTION_BLOCK;
                firewallRule.Direction = (ruleName.Contains("Inbound")) ? NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN : NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_OUT;
                firewallRule.InterfaceTypes = "All";
                firewallRule.Protocol = (ruleName.Contains("TCP")) ? 6 : 17; // 6 = TCP, 17 = UDP
                firewallRule.LocalPorts = (ruleName.Contains("TCP")) ? tcpPorts : udpPorts;
                firewallRule.Enabled = false;

                firewallPolicy.Rules.Add(firewallRule);
                SendMessageToOutputConsole($"Added Custom Rule: {ruleName}", Brushes.Azure);
            }
        }
        */

        public bool? ToggleCustomRules(bool newState)
        {
            if(DoesCustomRulesExist())
            {
                foreach(string ruleName in customRuleNames)
                {
                    INetFwRule rule = GetFirewallRule(ruleName);
                    if(rule != null)
                    {
                        rule.Enabled = newState;
                    } else {
                        SendMessageToOutputConsole($"Error! Unable to fetch rule \"{ruleName}\" - returned null!", Brushes.Red);
                        return null;
                    }
                }
                if(AreCustomRulesEnabled())
                {
                    SendMessageToOutputConsole("Changed Custom Rules Enabled to [True]...", Brushes.Azure);
                    return true;
                } else {
                    SendMessageToOutputConsole("Changed Custom Rules Enabled to [False]...", Brushes.Azure);
                    return false;
                }
            } else {
                SendMessageToOutputConsole("Error! Unable to find the custom Destiny 2 rules.", Brushes.Red);
                return null;
            }
        }

        public bool? ToggleDefaultRules(bool newState)
        {
            if (DoesDefaultRulesExist())
            {
                INetFwRule tcpRule = GetDefaultTCPRule();
                INetFwRule udpRule = GetDefaultUDPRule();

                if(tcpRule != null && udpRule != null)
                {
                    tcpRule.Enabled = newState;
                    SendMessageToOutputConsole($"Changing TCP Rule \"Destiny 2\" Enabled to [{newState}]...", Brushes.Azure);
                    udpRule.Enabled = newState;
                    SendMessageToOutputConsole($"Changing UDP Rule \"Destiny 2\" Enabled to [{newState}]...", Brushes.Azure);

                    if(newState)
                    {
                        return true;
                    } else {
                        return false;
                    }
                } else {
                    SendMessageToOutputConsole("Error! Unable to fetch one of the two default Destiny 2 rules!", Brushes.Red);
                    return null;
                }
            } else {
                SendMessageToOutputConsole("Error! Unable to find the default Destiny 2 rules.", Brushes.Red);
                return null;
            }
        }
    }
}
