using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RestSharp;
using LolSpeaker.Api.Dtos;
using LolSpeaker.Helper;

namespace LolSpeaker.Api
{
    /// <summary>
    /// 只跟 GitHub Releases 打交道：查最新版本、下载新版本的安装包
    /// </summary>
    public class GitHubApi
    {
        /// <summary>
        /// 仓库，owner/repo
        /// </summary>
        private const string Repo = "OFPC2Y/lol_speaker";

        /// <summary>
        /// 仓库的网页地址
        /// </summary>
        public const string RepoWebUrl = "https://github.com/" + Repo;

        /// <summary>
        /// releases 页面
        /// </summary>
        public const string ReleasesUrl = RepoWebUrl + "/releases";

        private static readonly string LatestReleaseApi = $"https://api.github.com/repos/{Repo}/releases/latest";

        private static readonly Regex VersionPattern = new Regex(@"^(\d{1,2})\.(\d{1,2})(?:\.(\d{1,2}))?$");

        #region 单例
        public static GitHubApi GetInstance() => instance;
        private static readonly GitHubApi instance = new GitHubApi();

        readonly RestClient _client;
        private GitHubApi()
        {
            _client = new RestClient();
            _client.Options.MaxTimeout = 120000;
            _client.Options.ThrowOnAnyError = false;

            //GitHub 的接口必须带 User-Agent
            _client.AddDefaultHeader("User-Agent", "lol_speaker");
            _client.AddDefaultHeader("Accept", "application/vnd.github+json");
        }
        #endregion

        /// <summary>
        /// 取最新的 release，查不到（没发过 release、限流、断网）返回 null
        /// </summary>
        public async Task<VersionDto> GetLatestRelease()
        {
            var response = await _client.ExecuteAsync(new RestRequest(LatestReleaseApi, Method.Get));

            if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content)) return null;

            var release = JObject.Parse(response.Content);

            var version = NormalizeVersion(release["tag_name"]?.ToString());
            if (version == null) return null;

            //优先取带 .exe 的附件，没有就当这个 release 没有可下载的东西
            var asset = (release["assets"] as JArray)?.FirstOrDefault(x =>
                (x["name"]?.ToString() ?? "").EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            return new VersionDto
            {
                VersionName = version,
                FileName = asset?["name"]?.ToString() ?? "",
                Url = asset?["browser_download_url"]?.ToString() ?? ""
            };
        }

        /// <summary>
        /// 把 tag 整成 x.y.z 三个数字，认不出来就返回 null
        /// </summary>
        private static string NormalizeVersion(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;

            var match = VersionPattern.Match(tag.Trim().TrimStart('v', 'V'));
            if (!match.Success) return null;

            var patch = match.Groups[3].Success ? match.Groups[3].Value : "0";

            return $"{match.Groups[1].Value}.{match.Groups[2].Value}.{patch}";
        }

        /// <summary>
        /// 下载文件到指定目录，返回文件全路径
        /// </summary>
        /// <param name="dir">目录</param>
        /// <param name="fileName">文件名</param>
        /// <param name="fileUrl">url</param>
        /// <returns></returns>
        public string DownloadFile(string dir, string fileName, string fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));
            if (string.IsNullOrWhiteSpace(dir)) throw new ArgumentNullException(nameof(dir));
            if (string.IsNullOrWhiteSpace(fileUrl)) throw new ArgumentNullException(nameof(fileUrl));

            fileName = Legalize(fileName);
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));

            var filePath = Path.Combine(dir, fileName);

            try
            {
                using (var writer = new ProgressFileStream(filePath, false))
                {
                    var request = new RestRequest(fileUrl, Method.Get);
                    _client.DownloadStream(request).CopyTo(writer);
                }
            }
            catch (Exception ex)
            {
                if (File.Exists(filePath)) File.Delete(filePath);
                throw new Exception($"文件下载失败！请重试\nUrl为：{fileUrl}", ex);
            }

            return filePath;
        }

        /// <summary>
        /// 使路径合法化，符合windows文件/文件夹命名规则
        /// </summary>
        private string Legalize(string path)
        {
            path = path.Replace("\\", "")
               .Replace("/", "")
               .Replace(":", "")
               .Replace("*", "")
               .Replace("?", "")
               .Replace("\"", "")
               .Replace("<", "")
               .Replace(">", "")
               .Replace("|", "");
            return path;
        }
    }
}
