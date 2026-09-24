using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using LolSpeaker.ViewModels;

namespace LolSpeaker.Helper
{
    /// <summary>
    /// json持久化工具类
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// 反序列化词库
        /// </summary>
        /// <returns></returns>
        public static WordsLibrary DeserializeWordsLibrary()
        {
            using (var stream = new FileStream(LocalConfigHelper.WordsLibraryPath, FileMode.Open))
            {
                #region 给word的category导航属性赋值
                var libray = DeserializeStream<WordsLibrary>(stream);
                foreach (var category in libray.Categories)
                {
                    category.TargetCategories = libray.Categories.Where(x => x != category).ToList();

                    category.Library = libray;

                    foreach (var word in category.Words)
                    {
                        word.Category = category;
                    }
                }

                #endregion 给word的category导航属性赋值

                #region 给按键功能赋值
                //老版本词库里没有按键功能，补上默认的 F2/F3
                if (libray.KeyFunctions == null)
                {
                    libray.KeyFunctions = new ObservableCollection<KeyFunction>
                    {
                        new KeyFunction { Key = "F2", Remark = "默认词库", CategoryName = "默认词库" },
                        new KeyFunction { Key = "F3", Remark = "自定义词库", CategoryName = "自定义词库" }
                    };
                }

                foreach (var function in libray.KeyFunctions)
                {
                    function.Library = libray;
                }
                #endregion 给按键功能赋值

                return libray;
            }
        }

        /// <summary>
        /// 序列化词库
        /// </summary>
        /// <param name="library"></param>
        public static void SerializeWordsLibrary(WordsLibrary library)
        {
            if (library == null) return;

            var tempfile = Path.GetTempFileName();
            using (var streamWritter = File.CreateText(tempfile))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(streamWritter, library);
            }

            File.Copy(tempfile, LocalConfigHelper.WordsLibraryPath, true);
        }

        private static T DeserializeStream<T>(Stream stream)
        {
            StreamReader reader = new StreamReader(stream);
            JsonSerializer serializer = new JsonSerializer();
            return (T)serializer.Deserialize(reader, typeof(T));
        }
    }
}
