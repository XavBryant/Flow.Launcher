using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedCommands;
using Application = System.Windows.Application;
using Control = System.Windows.Controls.Control;
using FormsApplication = System.Windows.Forms.Application;
using MessageBox = System.Windows.MessageBox;

namespace Flow.Launcher.Plugin.Sys
{
    public class Main : IPlugin, ISettingProvider, IPluginI18n
    {
        private PluginInitContext context;

        #region DllImport

        internal const int EWX_LOGOFF = 0x00000000;
        internal const int EWX_SHUTDOWN = 0x00000001;
        internal const int EWX_REBOOT = 0x00000002;
        internal const int EWX_FORCE = 0x00000004;
        internal const int EWX_POWEROFF = 0x00000008;
        internal const int EWX_FORCEIFHUNG = 0x00000010;

        [DllImport("user32")]
        private static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

        [DllImport("user32")]
        private static extern void LockWorkStation();

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hWnd, uint dwFlags);

        // http://www.pinvoke.net/default.aspx/Enums/HRESULT.html
        private enum HRESULT : uint
        {
            S_FALSE = 0x0001,
            S_OK = 0x0000
        }

        #endregion

        public Control CreateSettingPanel()
        {
            var results = Commands();
            return new SysSettings(results);
        }

        public List<Result> Query(Query query)
        {
            var commands = Commands();
            var results = new List<Result>();
            foreach (var c in commands)
            {
                var titleMatch = StringMatcher.FuzzySearch(query.Search, c.Title);
                var subTitleMatch = StringMatcher.FuzzySearch(query.Search, c.SubTitle);

                var score = Math.Max(titleMatch.Score, subTitleMatch.Score);
                if (score > 0)
                {
                    c.Score = score;
                    if (score == titleMatch.Score)
                    {
                        c.TitleHighlightData = titleMatch.MatchData;
                    }
                    else
                    {
                        c.SubTitleHighlightData = subTitleMatch.MatchData;
                    }

                    results.Add(c);
                }
            }

            return results;
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
        }

        private List<Result> Commands()
        {
            var results = new List<Result>();
            results.AddRange(new[]
            {
                new Result
                {
                    Title = "Shutdown",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_shutdown_computer"),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe7e8"),
                    IcoPath = "Images\\shutdown.png",
                    Action = c =>
                    {
                        var reuslt = MessageBox.Show(
                            context.API.GetTranslation("flowlauncher_plugin_sys_dlgtext_shutdown_computer"),
                            context.API.GetTranslation("flowlauncher_plugin_sys_shutdown_computer"),
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (reuslt == MessageBoxResult.Yes)
                        {
                            Process.Start("shutdown", "/s /t 0");
                        }

                        return true;
                    }
                },
                new Result
                {
                    Title = "Restart",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_restart_computer"),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe777"),
                    IcoPath = "Images\\restart.png",
                    Action = c =>
                    {
                        var result = MessageBox.Show(
                            context.API.GetTranslation("flowlauncher_plugin_sys_dlgtext_restart_computer"),
                            context.API.GetTranslation("flowlauncher_plugin_sys_restart_computer"),
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.Yes)
                        {
                            Process.Start("shutdown", "/r /t 0");
                        }

                        return true;
                    }
                },
                new Result
                {
                    Title = "Restart With Advanced Boot Options",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_restart_advanced"),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xecc5"),
                    IcoPath = "Images\\restart_advanced.png",
                    Action = c =>
                    {
                        var result = MessageBox.Show(
                            context.API.GetTranslation("flowlauncher_plugin_sys_dlgtext_restart_computer_advanced"),
                            context.API.GetTranslation("flowlauncher_plugin_sys_restart_computer"),
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        
                        if (result == MessageBoxResult.Yes)
                            Process.Start("shutdown", "/r /o /t 0");

                        return true;
                    }
                },
                new Result
                {
                    Title = "Log Off",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_log_off"),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe77b"),
                    IcoPath = "Images\\logoff.png",
                    Action = c => ExitWindowsEx(EWX_LOGOFF, 0)
                },
                new Result
                {
                    Title = "Lock",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_lock"),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe72e"),
                    IcoPath = "Images\\lock.png",
                    Action = c =>
                    {
                        LockWorkStation();
                        return true;
                    }
                },
                new Result
                {
                    Title = "Sleep",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_sleep"),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xec46"),
                    IcoPath = "Images\\sleep.png",
                    Action = c => FormsApplication.SetSuspendState(PowerState.Suspend, false, false)
                },
                new Result
                {
                    Title = "Hibernate",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_hibernate"),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe945"),
                    IcoPath = "Images\\hibernate.png",
                    Action= c =>
                    {
                        var info = ShellCommand.SetProcessStartInfo("shutdown", arguments:"/h");
                        info.WindowStyle = ProcessWindowStyle.Hidden;
                        info.UseShellExecute = true;
                        
                        Process.Start(info);
                        
                        return true;
                    }
                },
                new Result
                {
                    Title = "Empty Recycle Bin",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_emptyrecyclebin"),
                    IcoPath = "Images\\recyclebin.png",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe74d"),
                    Action = c =>
                    {
                        // http://www.pinvoke.net/default.aspx/shell32/SHEmptyRecycleBin.html
                        // FYI, couldn't find documentation for this but if the recycle bin is already empty, it will return -2147418113 (0x8000FFFF (E_UNEXPECTED))
                        // 0 for nothing
                        var result = SHEmptyRecycleBin(new WindowInteropHelper(Application.Current.MainWindow).Handle, 0);
                        if (result != (uint) HRESULT.S_OK && result != (uint) 0x8000FFFF)
                        {
                            MessageBox.Show($"Error emptying recycle bin, error code: {result}\n" +
                                            "please refer to https://msdn.microsoft.com/en-us/library/windows/desktop/aa378137",
                                "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }

                        return true;
                    }
                },
                new Result
                {
                    Title = "Exit",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_exit"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        Application.Current.MainWindow.Close();
                        return true;
                    }
                },
                new Result
                {
                    Title = "Save Settings",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_save_all_settings"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        context.API.SaveAppAllSettings();
                        context.API.ShowMsg(context.API.GetTranslation("flowlauncher_plugin_sys_dlgtitle_success"),
                            context.API.GetTranslation("flowlauncher_plugin_sys_dlgtext_all_settings_saved"));
                        return true;
                    }
                },
                new Result
                {
                    Title = "Restart Flow Launcher",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_restart"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        context.API.RestartApp();
                        return false;
                    }
                },
                new Result
                {
                    Title = "Settings",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_setting"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        context.API.OpenSettingDialog();
                        return true;
                    }
                },
                new Result
                {
                    Title = "Reload Plugin Data",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_reload_plugin_data"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        // Hide the window first then show msg after done because sometimes the reload could take a while, so not to make user think it's frozen. 
                        Application.Current.MainWindow.Hide();

                        context.API.ReloadAllPluginData().ContinueWith(_ =>
                            context.API.ShowMsg(
                                context.API.GetTranslation("flowlauncher_plugin_sys_dlgtitle_success"),
                                context.API.GetTranslation(
                                    "flowlauncher_plugin_sys_dlgtext_all_applicableplugins_reloaded")));
                        
                        return true;
                    }
                },
                new Result
                {
                    Title = "Check For Update",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_check_for_update"),
                    IcoPath = "Images\\checkupdate.png",
                    Action = c =>
                    {
                        Application.Current.MainWindow.Hide();
                        context.API.CheckForNewUpdate();
                        return true;
                    }
                },
                new Result
                {
                    Title = "Open Log Location",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_open_log_location"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        var logPath = Path.Combine(DataLocation.DataDirectory(), "Logs", Constant.Version);
                        FilesFolders.OpenPath(logPath);
                        return true;
                    }
                },
                new Result
                {
                    Title = "Flow Launcher Tips",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_open_docs_tips"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        SearchWeb.NewTabInBrowser(Constant.Documentation);
                        return true;
                    }
                },
                new Result
                {
                    Title = "Flow Launcher UserData Folder",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_open_userdata_location"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        FilesFolders.OpenPath(DataLocation.DataDirectory());
                        return true;
                    }
                }
            });

            return results;
        }

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("flowlauncher_plugin_sys_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("flowlauncher_plugin_sys_plugin_description");
        }
    }
}