using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using LolSpeaker.Constants;
using LolSpeaker.Helper;

namespace LolSpeaker.ViewModels
{
    /// <summary>
    /// 按键功能编辑
    /// </summary>
    public class KeyFunctionEditViewModel : BindableBase, IDialogAware
    {
        private const int MaxRemarkLength = 50;

        /// <summary>
        /// 可选按键
        /// </summary>
        private static readonly List<string> AllKeys =
            Enumerable.Range(1, 12).Select(x => "F" + x).ToList();

        private KeyFunction _Function;
        /// <summary>
        /// 正在编辑的按键功能，取消时不影响原对象
        /// </summary>
        public KeyFunction Function
        {
            get { return _Function; }
            set { SetProperty(ref _Function, value); }
        }

        private ObservableCollection<WordsCategory> _Categories;
        /// <summary>
        /// 可绑定的词库
        /// </summary>
        public ObservableCollection<WordsCategory> Categories
        {
            get { return _Categories; }
            set { SetProperty(ref _Categories, value); }
        }

        private List<string> _Keys;
        /// <summary>
        /// 当前可以选用的按键
        /// </summary>
        public List<string> Keys
        {
            get { return _Keys; }
            set { SetProperty(ref _Keys, value); }
        }

        #region IDialogAware
        public string Title => "按键功能";

        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog() { return true; }

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            try
            {
                var library = parameters.GetValue<WordsLibrary>(Params.Library);

                Function = parameters.GetValue<KeyFunction>(Params.KeyFunction);
                Categories = library.Categories;

                //已被其他按键功能占用的按键不再可选，编辑时保留自己当前用的那个
                var takenKeys = library.KeyFunctions.Select(x => x.Key).ToList();
                Keys = AllKeys.Where(x => !takenKeys.Contains(x) || x == Function.Key).ToList();

                if (string.IsNullOrEmpty(Function.Key)) Function.Key = Keys.FirstOrDefault();
                if (string.IsNullOrEmpty(Function.CategoryName)) Function.CategoryName = Categories.FirstOrDefault()?.CategoryName;
            }
            catch (Exception e)
            {
                e.Show();
            }
        }
        #endregion IDialogAware

        #region OkCommand
        private DelegateCommand _OkCommand;
        public DelegateCommand OkCommand => _OkCommand ?? (_OkCommand = new DelegateCommand(ExecuteOkCommand));
        void ExecuteOkCommand()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Function.Key))
                {
                    throw new ArgumentOutOfRangeException(nameof(Function.Key), "没有可用的按键了，请先删掉不用的按键功能");
                }

                if (string.IsNullOrWhiteSpace(Function.CategoryName))
                {
                    throw new ArgumentOutOfRangeException(nameof(Function.CategoryName), "请选择这个按键要绑定的词库");
                }

                if (Function.Remark != null && Function.Remark.Length > MaxRemarkLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(Function.Remark), $"功能备注不能超过{MaxRemarkLength}个字");
                }

                RequestClose?.Invoke(new DialogResult(ButtonResult.OK));
            }
            catch (ArgumentOutOfRangeException e)
            {
                e.Show(showDetail: false);
            }
            catch (Exception e)
            {
                e.Show();
            }
        }
        #endregion

        #region CancelCommand
        private DelegateCommand _CancelCommand;
        public DelegateCommand CancelCommand => _CancelCommand ?? (_CancelCommand = new DelegateCommand(ExecuteCancelCommand));
        void ExecuteCancelCommand()
        {
            try
            {
                RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
            }
            catch (Exception e)
            {
                e.Show();
            }
        }
        #endregion
    }
}
