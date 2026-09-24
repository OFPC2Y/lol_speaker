using System;
using System.Linq;
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using LolSpeaker.Constants;
using LolSpeaker.Helper;
using LolSpeaker.Views;

namespace LolSpeaker.ViewModels
{
    /// <summary>
    /// 聊天范围
    /// </summary>
    public enum ChatScope
    {
        /// <summary>
        /// 队内
        /// </summary>
        Team = 0,
        /// <summary>
        /// 所有人
        /// </summary>
        All = 1
    }

    /// <summary>
    /// 按键功能：一个按键，以及这个按键触发的功能
    /// </summary>
    public class KeyFunction : BindableBase
    {
        [JsonIgnore]
        public WordsLibrary Library { get; set; }

        private string _Key;
        /// <summary>
        /// 触发按键
        /// </summary>
        public string Key
        {
            get { return _Key; }
            set { SetProperty(ref _Key, value); }
        }

        private string _Remark;
        /// <summary>
        /// 功能备注
        /// </summary>
        public string Remark
        {
            get { return _Remark; }
            set
            {
                SetProperty(ref _Remark, value);

                RaisePropertyChanged(nameof(DisplayName));
            }
        }

        private string _CategoryName;
        /// <summary>
        /// 绑定的词库
        /// </summary>
        public string CategoryName
        {
            get { return _CategoryName; }
            set
            {
                SetProperty(ref _CategoryName, value);

                RaisePropertyChanged(nameof(DisplayName));
            }
        }

        private ChatScope _Scope = ChatScope.Team;
        /// <summary>
        /// 聊天范围开关：队内/所有人
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public ChatScope Scope
        {
            get { return _Scope; }
            set
            {
                SetProperty(ref _Scope, value);

                RaisePropertyChanged(nameof(IsAll));
                RaisePropertyChanged(nameof(IsTeam));
                RaisePropertyChanged(nameof(ScopeText));
                RaisePropertyChanged(nameof(Prefix));
            }
        }

        [JsonIgnore]
        /// <summary>
        /// 界面绑定形式：是否所有人
        /// </summary>
        public bool IsAll
        {
            get { return Scope == ChatScope.All; }
            set
            {
                Scope = value ? ChatScope.All : ChatScope.Team;

                //对话框里编辑的副本还没进词库，不用落盘
                if (Library != null && Library.KeyFunctions.Contains(this))
                {
                    JsonHelper.SerializeWordsLibrary(Library);
                }
            }
        }

        [JsonIgnore]
        /// <summary>
        /// 界面绑定形式：是否队内
        /// </summary>
        public bool IsTeam
        {
            get { return Scope == ChatScope.Team; }
            set { if (value) Scope = ChatScope.Team; }
        }

        [JsonIgnore]
        public string ScopeText => Scope == ChatScope.All ? "所有人" : "队内";

        /// <summary>
        /// 开关为所有人时随身附带的额外文本，发送时直接拼在词条前面
        /// </summary>
        [JsonIgnore]
        public string Prefix => Scope == ChatScope.All ? "/all " : "";

        [JsonIgnore]
        /// <summary>
        /// 界面显示用：优先显示备注，没填备注就显示词库名
        /// </summary>
        public string DisplayName => string.IsNullOrWhiteSpace(Remark) ? CategoryName : Remark;

        public KeyFunction Clone()
        {
            return new KeyFunction
            {
                Library = Library,
                Key = Key,
                Remark = Remark,
                CategoryName = CategoryName,
                Scope = Scope
            };
        }

        public void CopyFrom(KeyFunction other)
        {
            Key = other.Key;
            Remark = other.Remark;
            CategoryName = other.CategoryName;
            Scope = other.Scope;
        }

        #region EditCommand
        private DelegateCommand _EditCommand;
        [JsonIgnore]
        public DelegateCommand EditCommand => _EditCommand ?? (_EditCommand = new DelegateCommand(ExecuteEditCommand));
        void ExecuteEditCommand()
        {
            try
            {
                var dialogService = ContainerLocator.Container.Resolve<IDialogService>();

                //在副本上修改，取消时不影响原对象
                var draft = Clone();

                var parameters = new DialogParameters();
                parameters.Add(Params.Library, Library);
                parameters.Add(Params.KeyFunction, draft);

                IDialogResult result = null;
                dialogService.ShowDialog(nameof(KeyFunctionEdit), parameters, r => result = r);

                if (result.Result != ButtonResult.OK) return;

                CopyFrom(draft);

                JsonHelper.SerializeWordsLibrary(Library);
            }
            catch (Exception e)
            {
                e.Show();
            }
        }
        #endregion

        #region DeleteCommand
        private DelegateCommand _DeleteCommand;
        [JsonIgnore]
        public DelegateCommand DeleteCommand => _DeleteCommand ?? (_DeleteCommand = new DelegateCommand(ExecuteDeleteCommand));
        void ExecuteDeleteCommand()
        {
            try
            {
                if (!MessageHelper.Warning($"确定删除按键「{Key}」（{DisplayName}）吗？")) return;

                Library.KeyFunctions.Remove(this);

                JsonHelper.SerializeWordsLibrary(Library);
            }
            catch (Exception e)
            {
                e.Show();
            }
        }
        #endregion
    }
}
