// GitInfo（source generator）的替身。
// 原项目用 GitInfo 在编译期生成 ThisAssembly.Git.*，
// 但本机 git 信息算出的版本号非法（"0.0.0+main."），无法通过 NuGet 版本校验，
// 所以直接硬编码这几个真正被用到的值。
internal static class ThisAssembly
{
    internal static class Git
    {
        public const string Branch = "main";
        public const string Commit = "brushuproles";
        public const string Sha = "brushuproles";
        public const string BaseTag = "v26.9.20";
        public const string Tag = "v26.9.20";
        public const string Commits = "0";
        public const bool IsDirty = false;
        public const string RepositoryUrl = "https://github.com/brushuproles/brushuproles";
    }
}