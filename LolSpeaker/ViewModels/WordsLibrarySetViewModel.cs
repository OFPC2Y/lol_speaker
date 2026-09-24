using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using LolSpeaker.Constants;
using LolSpeaker.Helper;
using LolSpeaker.Views;

namespace LolSpeaker.ViewModels
{
    /// <summary>
    /// 词库
    /// </summary>
    /// <remarks>后期可考虑分为视图模型和模型</remarks>
    public class WordsLibrarySetViewModel : BindableBase, IDialogAware
    {
        private WordsLibrary _Library;
        /// <summary>
        /// 词库
        /// </summary>
        public WordsLibrary Library
        {
            get { return _Library; }
            set { SetProperty(ref _Library, value); }
        }

        private WordsCategory _SelectedCategory;
        /// <summary>
        /// 当前选中的那个页签，改名和删除词库都作用在它上面
        /// </summary>
        public WordsCategory SelectedCategory
        {
            get { return _SelectedCategory; }
            set { SetProperty(ref _SelectedCategory, value); }
        }

        #region IDialogAware
        public string Title => "词库设置";

        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog() { return true; }

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            try
            {
                Library = parameters.GetValue<WordsLibrary>(Params.Library);
            }
            catch (Exception e)
            {
                e.Show();
            }
        }
        #endregion IDialogAware

        #region AddCategoryCommand
        private DelegateCommand _AddCategoryCommand;
        public DelegateCommand AddCategoryCommand => _AddCategoryCommand ?? (_AddCategoryCommand = new DelegateCommand(ExecuteAddCategoryCommand));
        void ExecuteAddCategoryCommand()
        {
            try
            {
                //借用词条对话框输入词库名
                var dialogService = ContainerLocator.Container.Resolve<IDialogService>();

                var parameters = new DialogParameters();
                parameters.Add(Params.WordContent, "");
                parameters.Add(Params.Title, "新增词库");

                IDialogResult result = null;
                dialogService.ShowDialog(nameof(WordEdit), parameters, r => result = r);

                if (result.Result != ButtonResult.OK) return;

                var name = result.Parameters.GetValue<string>(Params.WordContent);

                if (Library.Categories.Any(x => x.CategoryName == name))
                {
                    MessageHelper.Error($"词库「{name}」已经存在了");
                    return;
                }

                var category = new WordsCategory
                {
                    CategoryName = name,
                    Library = Library,
                    Words = new ObservableCollection<Word>()
                };
                Library.Categories.Add(category);

                RefreshTargetCategories();

                SelectedCategory = category;
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

        #region RenameCategoryCommand
        private DelegateCommand _RenameCategoryCommand;
        public DelegateCommand RenameCategoryCommand => _RenameCategoryCommand ?? (_RenameCategoryCommand = new DelegateCommand(ExecuteRenameCategoryCommand));
        void ExecuteRenameCategoryCommand()
        {
            try
            {
                var category = SelectedCategory;
                if (category == null) return;

                //借用词条对话框输入新名字
                var dialogService = ContainerLocator.Container.Resolve<IDialogService>();

                var parameters = new DialogParameters();
                parameters.Add(Params.WordContent, category.CategoryName);
                parameters.Add(Params.Title, "修改词库名称");

                IDialogResult result = null;
                dialogService.ShowDialog(nameof(WordEdit), parameters, r => result = r);

                if (result.Result != ButtonResult.OK) return;

                var newName = result.Parameters.GetValue<string>(Params.WordContent);
                if (newName == category.CategoryName) return;

                if (Library.Categories.Any(x => x != category && x.CategoryName == newName))
                {
                    MessageHelper.Error($"词库「{newName}」已经存在了");
                    return;
                }

                var oldName = category.CategoryName;

                category.CategoryName = newName;

                //绑定过这个词库的按键功能跟着改名，不然发送时会找不到词库
                foreach (var function in Library.KeyFunctions.Where(x => x.CategoryName == oldName))
                {
                    function.CategoryName = newName;
                }
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

        #region DeleteCategoryCommand
        private DelegateCommand _DeleteCategoryCommand;
        public DelegateCommand DeleteCategoryCommand => _DeleteCategoryCommand ?? (_DeleteCategoryCommand = new DelegateCommand(ExecuteDeleteCategoryCommand));
        void ExecuteDeleteCategoryCommand()
        {
            try
            {
                var category = SelectedCategory;
                if (category == null) return;

                if (Library.Categories.Count <= 1)
                {
                    MessageHelper.Error("至少要保留一个词库");
                    return;
                }

                var boundFunctions = Library.KeyFunctions
                    .Where(x => x.CategoryName == category.CategoryName).ToList();

                var msg = $"确定删除词库「{category.CategoryName}」吗？";
                if (boundFunctions.Count > 0)
                {
                    msg += $"\n\n有 {boundFunctions.Count} 个按键功能绑定了这个词库，会一起删掉：" +
                           string.Join("、", boundFunctions.Select(x => x.Key));
                }

                if (!MessageHelper.Warning(msg)) return;

                foreach (var function in boundFunctions)
                {
                    Library.KeyFunctions.Remove(function);
                }

                Library.Categories.Remove(category);

                RefreshTargetCategories();

                SelectedCategory = Library.Categories.FirstOrDefault();
            }
            catch (Exception e)
            {
                e.Show();
            }
        }
        #endregion

        /// <summary>
        /// 词库增删之后刷新每个词库的“复制到”列表
        /// </summary>
        private void RefreshTargetCategories()
        {
            foreach (var category in Library.Categories)
            {
                category.TargetCategories = Library.Categories.Where(x => x != category).ToList();
                category.Library = Library;
            }
        }

        #region SaveCommand
        private DelegateCommand _SaveCommand;
        public DelegateCommand SaveCommand => _SaveCommand ?? (_SaveCommand = new DelegateCommand(ExecuteSaveCommand));
        void ExecuteSaveCommand()
        {
            try
            {
                //先把每个词库编辑框里的文本写回词条
                foreach (var category in Library.Categories)
                {
                    var badLine = category.MaterializeWords();
                    if (badLine > 0)
                    {
                        MessageHelper.Error($"词库「{category.CategoryName}」第 {badLine} 行超过 {WordsHelper.MaxContentLength} 个字，改短一点再保存");
                        return;
                    }
                }

                JsonHelper.SerializeWordsLibrary(Library);

                //保存完不关窗口，方便接着改
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
