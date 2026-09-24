using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using WindowsInput;
using LolSpeaker.Api;
using LolSpeaker.Constants;
using LolSpeaker.Helper;
using LolSpeaker.Views;

namespace LolSpeaker.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        private readonly IDialogService _dialogService;
        private readonly GitHubApi _api = GitHubApi.GetInstance();
        GlobalKeyboardHook hook;

        /// <summary>
        /// 是否正在一次性发送，连按两次会让两轮发送混在一起
        /// </summary>
        private volatile bool IsOneShotSending;

        private WordsLibrary _Library;
        public WordsLibrary Library
        {
            get { return _Library; }
            set { SetProperty(ref _Library, value); }
        }

        #region 绑定属性
        private bool _IsNotifyIconBlink;
        /// <summary>
        /// 托盘图标是否闪烁
        /// </summary>
        public bool IsNotifyIconBlink
        {
            get { return _IsNotifyIconBlink; }
            set { SetProperty(ref _IsNotifyIconBlink, value); }
        }

        private bool _IsNotifyIconShow = true;
        /// <summary>
        /// 托盘图标是否显示
        /// </summary>
        public bool IsNotifyIconShow
        {
            get { return _IsNotifyIconShow; }
            set { SetProperty(ref _IsNotifyIconShow, value); }
        }

        private string _Version;
        /// <summary>
        /// 程序版本号
        /// </summary>
        public string Version
        {
            get { return _Version; }
            set { SetProperty(ref _Version, value); }
        }

        private bool _NeedUpdate;
        /// <summary>
        /// 是否需要更新
        /// </summary>
        public bool NeedUpdate
        {
            get { return _NeedUpdate; }
            set { SetProperty(ref _NeedUpdate, value); }
        }

        #endregion 绑定属性

        public MainWindowViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        #region 命令
        #region LoadedCommand
        private DelegateCommand _LoadedCommand;
        public DelegateCommand LoadedCommand => _LoadedCommand ?? (_LoadedCommand = new DelegateCommand(ExecuteLoadedCommand));
        async void ExecuteLoadedCommand()
        {
            try
            {
                LoadWordsLibrary();

                HookKeys();

                Version = "v" + System.Windows.Application.ResourceAssembly.GetName().Version.ToString(3);

                await InitNeedUpdate();
            }
            catch (Exception e)
            {
                e.Show();
            }
        }

        private async Task InitNeedUpdate()
        {
            var latestVersion = await VersionHelper.GetLatestVersion();
            NeedUpdate = !VersionHelper.IsNewestVersion(latestVersion);
        }

        private void LoadWordsLibrary()
        {
            try
            {
                //第一试用本软件没有本地词库，用资源清单的词库
                if (!File.Exists(LocalConfigHelper.WordsLibraryPath))
                {
                    var dir = Path.GetDirectoryName(LocalConfigHelper.WordsLibraryPath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    var manifestStream = ManifestHelper.GetManifestStream("wordsLibrary.json");
                    using (var stream = File.Create(LocalConfigHelper.WordsLibraryPath))
                    {
                        manifestStream.CopyTo(stream);
                    }
                }

                Library = JsonHelper.DeserializeWordsLibrary();//反序列化本地词库
            }
            catch (Exception e)
            {
                e.Show("读取词库失败！");
                File.Delete(LocalConfigHelper.WordsLibraryPath);
                App.Current.Shutdown();
            }
        }

        #endregion

        #region 访问GitHub
        private DelegateCommand _VisitGitHubCommand;
        public DelegateCommand VisitGitHubCommand => _VisitGitHubCommand ?? (_VisitGitHubCommand = new DelegateCommand(ExecuteVisitGitHubCommand));
        void ExecuteVisitGitHubCommand()
        {
            try
            {
                Process.Start(GitHubApi.RepoWebUrl);
            }
            catch (Exception e)
            {
                e.Show();
            }
        }
        #endregion

        #region SetCommand
        private DelegateCommand _SetCommand;

        public DelegateCommand SetCommand => _SetCommand ?? (_SetCommand = new DelegateCommand(ExecuteSetCommand));
        void ExecuteSetCommand()
        {
            try
            {
                var parameters = new DialogParameters();
                parameters.Add(Params.Library, Library);

                _dialogService.ShowDialog(nameof(WordsLibrarySet), parameters, result => { });

                //词库是在对话框里保存的，关掉之后重新读一遍磁盘，丢掉没保存的改动
                LoadWordsLibrary();
            }
            catch (Exception e)
            {
                e.Show();
            }
        }
        #endregion

        #region AddKeyFunctionCommand
        private DelegateCommand _AddKeyFunctionCommand;
        public DelegateCommand AddKeyFunctionCommand => _AddKeyFunctionCommand ?? (_AddKeyFunctionCommand = new DelegateCommand(ExecuteAddKeyFunctionCommand));
        void ExecuteAddKeyFunctionCommand()
        {
            try
            {
                //先建一个副本让用户配置，确定后才加进词库
                var function = new KeyFunction { Library = Library };

                var parameters = new DialogParameters();
                parameters.Add(Params.Library, Library);
                parameters.Add(Params.KeyFunction, function);

                IDialogResult r = null;
                _dialogService.ShowDialog(nameof(KeyFunctionEdit), parameters, result => r = result);

                if (r.Result != ButtonResult.OK) return;

                Library.KeyFunctions.Add(function);

                JsonHelper.SerializeWordsLibrary(Library);
            }
            catch (Exception e)
            {
                e.Show();
            }
        }
        #endregion

        #region SelectedKeyFunction
        private KeyFunction _SelectedKeyFunction;
        /// <summary>
        /// 主界面上选中那一行的按键功能
        /// </summary>
        public KeyFunction SelectedKeyFunction
        {
            get { return _SelectedKeyFunction; }
            set
            {
                SetProperty(ref _SelectedKeyFunction, value);

                HasSelectedKeyFunction = value != null;
            }
        }

        private bool _HasSelectedKeyFunction;
        /// <summary>
        /// 当前是否有选中的行，修改和删除按钮靠它决定能不能点
        /// </summary>
        public bool HasSelectedKeyFunction
        {
            get { return _HasSelectedKeyFunction; }
            set { SetProperty(ref _HasSelectedKeyFunction, value); }
        }
        #endregion

        #region EditKeyFunctionCommand
        private DelegateCommand _EditKeyFunctionCommand;
        public DelegateCommand EditKeyFunctionCommand => _EditKeyFunctionCommand ?? (_EditKeyFunctionCommand = new DelegateCommand(ExecuteEditKeyFunctionCommand).ObservesCanExecute(() => HasSelectedKeyFunction));
        void ExecuteEditKeyFunctionCommand()
        {
            SelectedKeyFunction?.EditCommand.Execute();
        }
        #endregion

        #region DeleteKeyFunctionCommand
        private DelegateCommand _DeleteKeyFunctionCommand;
        public DelegateCommand DeleteKeyFunctionCommand => _DeleteKeyFunctionCommand ?? (_DeleteKeyFunctionCommand = new DelegateCommand(ExecuteDeleteKeyFunctionCommand).ObservesCanExecute(() => HasSelectedKeyFunction));
        void ExecuteDeleteKeyFunctionCommand()
        {
            SelectedKeyFunction?.DeleteCommand.Execute();
        }
        #endregion

        #region UpdateCommand
        private bool _UpdateEnabled = true;
        public bool UpdateEnabled
        {
            get { return _UpdateEnabled; }
            set { SetProperty(ref _UpdateEnabled, value); }
        }
        private DelegateCommand _UpdateCommand;
        public DelegateCommand UpdateCommand => _UpdateCommand ?? (_UpdateCommand = new DelegateCommand(ExecuteUpdateCommand).ObservesCanExecute(() => UpdateEnabled));
        async void ExecuteUpdateCommand()
        {
            try
            {
                UpdateEnabled = false;

                var latestVersion = await VersionHelper.GetLatestVersion();
                if (latestVersion == null)
                {
                    MessageHelper.Error($"没有获取到版本信息。可能是 GitHub 上还没发布过 Release，或者网络不通。\n\n{GitHubApi.ReleasesUrl}");
                    return;
                }

                if (VersionHelper.IsNewestVersion(latestVersion))
                {
                    MessageHelper.Info($"当前版本已经是最新版本");
                    return;
                }

                var result = MessageHelper.Question($"当前版本为{VersionHelper.GetCurrentVersionName()}，最新版本为{latestVersion.VersionName}，是否更新？");
                if (!result) return;

                if (string.IsNullOrWhiteSpace(latestVersion.Url))
                {
                    //这个 release 没挂安装包，只能自己去下载
                    MessageHelper.Info($"这个版本没有提供安装包，请到 Releases 页面手动下载");
                    Process.Start(GitHubApi.ReleasesUrl);
                    return;
                }

                //先下载新版本到临时目录
                var file = _api.DownloadFile(Path.GetTempPath(), latestVersion.FileName, latestVersion.Url);

                var currentExe = Assembly.GetExecutingAssembly().Location;

                //拿到更新程序，交给它去结束当前进程、替换文件、重新启动
                var updater = GetUpdaterPath();
                if (updater == null)
                {
                    MessageHelper.Error("没找到更新程序 LolSpeakerUpdate.exe");
                    return;
                }

                var startInfo = new ProcessStartInfo(updater, $"\"{file}\" \"{currentExe}\"");
                //设置不在新窗口中启动新的进程
                startInfo.CreateNoWindow = true;
                //不使用操作系统使用的shell启动进程
                startInfo.UseShellExecute = false;
                //将输出信息重定向
                startInfo.RedirectStandardOutput = true;
                Process.Start(startInfo);
            }
            catch (Exception e)
            {
                e.Show();
            }
            finally
            {
                UpdateEnabled = true;
            }
        }
        #endregion


        #endregion

        /// <summary>
        /// 按键勾子
        /// </summary>
        private void HookKeys()
        {
            hook = new GlobalKeyboardHook();
            hook.KeyUp += Hook_KeyUp;
            //F1~F12 全部挂钩，具体哪个键触发哪个功能由词库里的“按键功能”决定
            hook.HookedKeys.AddRange(new[]
            {
                Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6,
                Keys.F7, Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.F12
            });
            hook.hook();
        }

        /// <summary>
        /// 勾子事件处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Hook_KeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                var function = Library.KeyFunctions.FirstOrDefault(x => x.Key == e.KeyCode.ToString());
                if (function == null) return;

                var category = Library.GetCategory(function.CategoryName);
                if (category == null)
                {
                    MessageHelper.Error($"按键「{function.Key}」绑定的词库「{function.CategoryName}」不存在");
                    return;
                }

                if (category.SendMode == SendMode.OneShot)
                {
                    SendWholeCategory(function, category);
                    return;
                }

                string word = category.GetWord().Content;
                var interval = category.SendInterval;

                var builder = Simulate.Events();
                if (category.IsPerWord)
                {
                    //逐字发送：每个字单独发一条，两条之间的间隔按设置来
                    foreach (var item in word)
                    {
                        builder = builder.
                            Click(WindowsInput.Events.KeyCode.Enter).Wait(100).
                            Click(function.Prefix + item).Wait(100).
                            Click(WindowsInput.Events.KeyCode.Enter).Wait(interval);

                    }
                }
                else
                {
                    builder = builder.
                        Click(WindowsInput.Events.KeyCode.Enter).Wait(100).
                        Click(function.Prefix + word).Wait(100).
                        Click(WindowsInput.Events.KeyCode.Enter).Wait(100);
                }
                builder.Invoke();
            }
            catch (ArgumentOutOfRangeException)
            {
                MessageHelper.Error($"词库为空");
            }
            catch (Exception ex)
            {
                ex.Show();
            }
        }

        /// <summary>
        /// 一次性发送：按下按键后把这个词库逐条发出去
        /// </summary>
        private void SendWholeCategory(KeyFunction function, WordsCategory category)
        {
            //一次发送要跑挺久，中途再按一次会跟正在发的混在一起，直接忽略
            if (IsOneShotSending) return;

            var words = category.Words.Select(x => x.Content).ToList();
            if (words.Count == 0)
            {
                MessageHelper.Error($"词库为空");
                return;
            }

            IsOneShotSending = true;

            var prefix = function.Prefix;
            var interval = category.SendInterval;

            //发完整个词库可能要几十秒，放后台线程，不然界面一直没响应
            Task.Run(() =>
            {
                try
                {
                    var builder = Simulate.Events();
                    foreach (var word in words)
                    {
                        builder = builder.
                            Click(WindowsInput.Events.KeyCode.Enter).Wait(100).
                            Click(prefix + word).Wait(100).
                            Click(WindowsInput.Events.KeyCode.Enter).Wait(interval);
                    }
                    builder.Invoke();
                }
                catch (Exception ex)
                {
                    App.Current.Dispatcher.Invoke(() => ex.Show());
                }
                finally
                {
                    IsOneShotSending = false;
                }
            });
        }

        /// <summary>
        /// 取更新程序的可执行文件路径，找不到返回 null。
        /// 单文件打包时更新程序是主程序的内嵌资源，得先释放到临时目录；
        /// 普通编译时它就在程序旁边。
        /// </summary>
        private string GetUpdaterPath()
        {
            var assembly = Assembly.GetExecutingAssembly();

            var embedded = assembly.GetManifestResourceNames()
                .FirstOrDefault(x => x.EndsWith("lolspeakerupdate.exe", StringComparison.OrdinalIgnoreCase));

            if (embedded != null)
            {
                var tempPath = Path.Combine(Path.GetTempPath(), "lolspeakerupdate.exe");

                using (var stream = assembly.GetManifestResourceStream(embedded))
                using (var file = File.Create(tempPath))
                {
                    stream.CopyTo(file);
                }

                return tempPath;
            }

            var sibling = Path.Combine(Path.GetDirectoryName(assembly.Location), "LolSpeakerUpdate.exe");

            return File.Exists(sibling) ? sibling : null;
        }

    }
}
