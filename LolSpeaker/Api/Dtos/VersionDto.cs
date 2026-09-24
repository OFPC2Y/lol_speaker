namespace LolSpeaker.Api.Dtos
{
    /// <summary>
    /// 一个 release 里我们关心的东西
    /// </summary>
    public class VersionDto
    {
        /// <summary>
        /// 安装包下载地址
        /// </summary>
        public string Url { get; set; } = "";

        /// <summary>
        /// 安装包文件名
        /// </summary>
        public string FileName { get; set; } = "";

        /// <summary>
        /// 版本号，形如 x.y.z
        /// </summary>
        public string VersionName { get; set; } = "";
    }
}
