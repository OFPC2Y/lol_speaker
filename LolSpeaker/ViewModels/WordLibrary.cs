using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using Prism.Mvvm;
using Prism.Ioc;
using Prism.Events;
using Prism.Commands;
using LolSpeaker.Helper;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Prism.Services.Dialogs;
using LolSpeaker.Views;
using LolSpeaker.Constants;
using LolSpeaker.Constants.Events;
using System.Windows;

namespace LolSpeaker.ViewModels
{
    /// <summary>
    /// 词库，包括多类词条
    /// </summary>
    public class WordsLibrary : BindableBase
    {
        private ObservableCollection<WordsCategory> _Categories;
        public ObservableCollection<WordsCategory> Categories
        {
            get { return _Categories; }
            set { SetProperty(ref _Categories, value); }
        }

        private ObservableCollection<KeyFunction> _KeyFunctions;
        /// <summary>
        /// 按键功能，每个按键对应一个功能
        /// </summary>
        public ObservableCollection<KeyFunction> KeyFunctions
        {
            get { return _KeyFunctions; }
            set { SetProperty(ref _KeyFunctions, value); }
        }
    }

    /// <summary>
    /// 词库的发送方式
    /// </summary>
    public enum SendMode
    {
        /// <summary>
        /// 随机发送
        /// </summary>
        Random = 0,
        /// <summary>
        /// 顺序发送
        /// </summary>
        Sequential = 1,
        /// <summary>
        /// 一次性发送，按下按键后把整个词库逐条发出去
        /// </summary>
        OneShot = 2
    }

    /// <summary>
    /// 词条类
    /// </summary>
    public class WordsCategory : BindableBase
    {
        [JsonIgnore]
        public WordsLibrary Library { get; set; }

        private ObservableCollection<Word> _Words;
        public ObservableCollection<Word> Words
        {
            get { return _Words; }
            set { SetProperty(ref _Words, value); }
        }

        private List<WordsCategory> _TargetCategories;
        [JsonIgnore]
        /// <summary>
        /// 其他类别
        /// </summary>
        public List<WordsCategory> TargetCategories
        {
            get { return _TargetCategories; }
            set { SetProperty(ref _TargetCategories, value); }
        }

        private string _CategoryName;
        /// <summary>
        /// 词库名，改名后页签和“复制到”菜单都要跟着变
        /// </summary>
        public string CategoryName
        {
            get { return _CategoryName; }
            set { SetProperty(ref _CategoryName, value); }
        }

        private SendMode _SendMode = SendMode.Random;
        /// <summary>
        /// 发送方式：随机发送/顺序发送，两者互斥
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public SendMode SendMode
        {
            get { return _SendMode; }
            set
            {
                SetProperty(ref _SendMode, value);
                RaisePropertyChanged(nameof(IsRandomSend));
                RaisePropertyChanged(nameof(IsSequentialSend));
                RaisePropertyChanged(nameof(IsOneShotSend));
            }
        }

        [JsonIgnore]
        /// <summary>
        /// 界面绑定形式：是否随机发送
        /// </summary>
        public bool IsRandomSend
        {
            get { return SendMode == SendMode.Random; }
            set { if (value) SendMode = SendMode.Random; }
        }

        [JsonIgnore]
        /// <summary>
        /// 界面绑定形式：是否顺序发送
        /// </summary>
        public bool IsSequentialSend
        {
            get { return SendMode == SendMode.Sequential; }
            set { if (value) SendMode = SendMode.Sequential; }
        }

        private bool _IsPerWord;
        /// <summary>
        /// 是否逐字发送
        /// </summary>
        public bool IsPerWord
        {
            get { return _IsPerWord; }
            set { SetProperty(ref _IsPerWord, value); }
        }

        /// <summary>
        /// 一次性发送最多支持的词条数
        /// </summary>
        public const int MaxOneShotWordCount = 30;

        /// <summary>
        /// 发送间隔的默认值（毫秒）
        /// </summary>
        public const int DefaultSendInterval = 100;

        /// <summary>
        /// 发送间隔的上限（毫秒），再大界面会长时间没响应
        /// </summary>
        private const int MaxSendInterval = 60000;

        [JsonIgnore]
        /// <summary>
        /// 界面绑定形式：是否一次性发送，词条太多时拒绝开启
        /// </summary>
        public bool IsOneShotSend
        {
            get { return SendMode == SendMode.OneShot; }
            set
            {
                if (!value) return;

                if (Words != null && Words.Count > MaxOneShotWordCount)
                {
                    MessageHelper.Error($"一次性发送要求词库不超过{MaxOneShotWordCount}条，当前词库有{Words.Count}条");

                    //没改成，把发送方式的三个单选按钮刷回当前状态
                    RaisePropertyChanged(nameof(IsRandomSend));
                    RaisePropertyChanged(nameof(IsSequentialSend));
                    RaisePropertyChanged(nameof(IsOneShotSend));
                    return;
                }

                SendMode = SendMode.OneShot;
            }
        }

        private int _SendInterval = DefaultSendInterval;
        /// <summary>
        /// 发送间隔，单位毫秒，逐字发送和一次性发送都按它来
        /// </summary>
        public int SendInterval
        {
            get { return _SendInterval; }
            set
            {
                if (value < 1 || value > MaxSendInterval)
                {
                    //非法值还原成当前值
                    RaisePropertyChanged();
                    return;
                }

                SetProperty(ref _SendInterval, value);
            }
        }

        /// <summary>
        /// 按发送方式取一条词条
        /// </summary>
        public Word GetWord()
        {
            if (Words == null || Words.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Words), "词库为空");
            }

            if (SendMode == SendMode.Sequential)
            {
                return Words[SendStateHelper.NextIndex(CategoryName, Words.Count)];
            }

            var random = new Random((int)DateTime.Now.Ticks);
            return Words[random.Next(0, Words.Count)];
        }

        private string _RawText;
        [JsonIgnore]
        /// <summary>
        /// 词库内容的编辑文本，一行一条词条；没编辑过时按 Words 生成
        /// </summary>
        public string WordsText
        {
            get
            {
                if (_RawText != null) return _RawText;
                if (Words == null) return "";

                return string.Join(Environment.NewLine, Words.Select(x => x.Content));
            }
            set { _RawText = value; }
        }

        /// <summary>
        /// 丢掉编辑文本，下次读 WordsText 时重新按 Words 生成
        /// </summary>
        public void InvalidateWordsText()
        {
            _RawText = null;
            RaisePropertyChanged(nameof(WordsText));
        }

        /// <summary>
        /// 把编辑文本写回 Words，返回第一个超长行的行号（从 1 开始，0 表示没问题）
        /// </summary>
        public int MaterializeWords()
        {
            if (_RawText == null) return 0;

            var lines = _RawText.Replace("\r", "").Split('\n');

            var words = new ObservableCollection<Word>();
            for (int i = 0; i < lines.Length; i++)
            {
                var content = lines[i].Trim();

                //空行不算一条
                if (string.IsNullOrWhiteSpace(content)) continue;

                if (content.Length > WordsHelper.MaxContentLength) return i + 1;

                words.Add(new Word { Content = content, Category = this });
            }

            Words = words;

            return 0;
        }

        #region 命令

        #region CopyToCommand
        private DelegateCommand<string> _CopyToCommand;
        public DelegateCommand<string> CopyToCommand => _CopyToCommand ?? (_CopyToCommand = new DelegateCommand<string>(ExecuteCopyToCommand));
        void ExecuteCopyToCommand(string parameter)
        {
            try
            {
                var target = Library.GetCategory(parameter);
                if (target == null)
                {
                    MessageHelper.Error($"没找到词库「{parameter}」");
                    return;
                }

                //先把编辑框里的内容写回，不然复制过去的是改动之前的
                var badLine = MaterializeWords();
                if (badLine > 0)
                {
                    MessageHelper.Error($"第 {badLine} 行超过 {WordsHelper.MaxContentLength} 个字，先改短一点再复制");
                    return;
                }

                //已经在目标词库里的就不再加，免得连点几次复制出一堆重复词条
                var existing = new HashSet<string>(target.Words.Select(x => x.Content));
                foreach (var word in Words)
                {
                    if (!existing.Add(word.Content)) continue;

                    target.Words.Add(new Word { Content = word.Content, Category = target });
                }

                target.InvalidateWordsText();
            }
            catch (Exception e)
            {
                e.Show();
            }
        }
        #endregion

        #endregion 命令

    }

    /// <summary>
    /// 词条
    /// </summary>
    public class Word : BindableBase
    {
        [JsonIgnore]
        public WordsCategory Category { get; set; }

        private string _Content;
        /// <summary>
        /// 内容
        /// </summary>
        public string Content
        {
            get { return _Content; }
            set
            {
                value = value.Replace('\r', char.MinValue);

                WordsHelper.EnsureValidContent(value);

                SetProperty(ref _Content, value);
            }
        }
    }

    public static class WordsLibraryExtension
    {
        /// <summary>
        /// 按名称取词库
        /// </summary>
        public static WordsCategory GetCategory(this WordsLibrary library, string categoryName)
        {
            return library.Categories.FirstOrDefault(x => x.CategoryName == categoryName);
        }
    }
}
