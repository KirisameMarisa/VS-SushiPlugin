using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Design;
using EnvDTE80;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using System.ComponentModel;
using System.Diagnostics;

namespace CommandToolbar
{
    public class SushiPlugin
    {
        static int TIME_HISTORY_SIZE = 60;
        static bool _isCancel = false;
        static bool _isRun = false;

        uint[] _timeHistory = new uint[TIME_HISTORY_SIZE];
        uint _idealFrameInterval = 1000 / 30;
        uint _previousFrameTime;
        uint _previousFrameInterval;
        
        public SushiPlugin()
        {
            uint currentTime = time();
            for (int i = 0; i < TIME_HISTORY_SIZE; ++i)
            {
                _timeHistory[i] = currentTime;
            }
        }

        public System.Threading.Tasks.Task Run(IVsStatusbar statusBar)
        {
            return new System.Threading.Tasks.Task(() =>
            {
                int count = 0;
                string label = "";
                while (true)
                {
                    //!< 実行中
                    _isRun = true;

                    //!< フレームレート調整
                    FrameRateAdjustment();
                    
                    //!< 🍣 
                    if (count % 2 == 0)
                    {
                        label += "     ";
                    }
                    else if (count % 2 == 1)
                    {
                        label += "🍣 ";
                    }

                    label = label.Remove(0, 1);
                    statusBar.SetText(label);

                    ++count;
                    // キャンセルされてないか定期的にチェック
                    if (_isCancel)
                    {
                        _isRun = false;
                        _isCancel = false;
                        statusBar.SetText("🍣 Finish...");
                        break;
                    }
                }
            });
        }

        private void FrameRateAdjustment()
        {
            uint currentTime = time();
            while ((currentTime - _previousFrameTime) < _idealFrameInterval)
            {
                System.Threading.Thread.Sleep(1);
                currentTime = time();
            }
            _previousFrameTime = currentTime;

            //フレーム更新
            _previousFrameInterval = currentTime - _timeHistory[TIME_HISTORY_SIZE - 1];
            uint frameIntervalSum = currentTime - _timeHistory[0];
            //履歴更新
            for (int i = 0; i < TIME_HISTORY_SIZE - 1; ++i)
            {
                _timeHistory[i] = _timeHistory[i + 1];
            }
            _timeHistory[TIME_HISTORY_SIZE - 1] = currentTime;
        }

        public void Cancel()
        {
            if(_isRun)
            {
                _isCancel = true;
            }     
        }

        public uint time()
        {
            return (uint)Environment.TickCount;
        }
    }

    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [Guid(SushiPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class SushiPackage : Package
    {
        private const string PackageGuidString = "4d39cbb8-8aad-4b40-b867-22082c266766";
        private const int CommandId = 0x0100;
        private static readonly Guid CommandSet = new Guid("8b033dfa-ab64-4106-9b87-116a5fc2a9d8");
        private BuildEvents _buildEvents;
        private IVsStatusbar _statusBar;
        private SushiPlugin _plugin;
        
        public SushiPackage()
        {
            _plugin = new SushiPlugin();
        }

        protected override void Initialize()
        {
            base.Initialize();

            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs)
            {
                // Create the command for the menu item.
                CommandID menuCommandID = new CommandID(CommandSet, CommandId);
                OleMenuCommand menuItem = new OleMenuCommand(MenuItemCallback, menuCommandID);
                mcs.AddCommand(menuItem);
            }
       }

        private void MenuItemCallback(object sender, EventArgs e)
        {
            var myCommand = sender as OleMenuCommand;
            if (null != myCommand)
            {
                myCommand.Checked = !myCommand.Checked;
            }

            _statusBar = (IVsStatusbar)GetService(typeof(SVsStatusbar));
            DTE2 dte = (DTE2)GetService(typeof(DTE));
            _buildEvents = dte.Events.BuildEvents;
            if (myCommand.Checked)
            {
                _buildEvents.OnBuildBegin += onBuildBegin;
                _buildEvents.OnBuildDone += onBuildFinish;
            }
            else
            {
                _buildEvents.OnBuildBegin -= onBuildBegin;
                _buildEvents.OnBuildDone -= onBuildFinish;
            }
        }

        private void onBuildBegin(vsBuildScope Scope, vsBuildAction Action)
        {
            _plugin.Run(_statusBar).Start();
        }

        private void onBuildFinish(vsBuildScope Scope, vsBuildAction Action)
        {
            _plugin.Cancel();
        }
    }
}
