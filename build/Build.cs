using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.Globbing;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using System.Collections.Generic;
using Serilog;

partial class Build : NukeBuild
{
    [Solution(GenerateProjects = true)]
    readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion] readonly GitVersion GitVersion;

    [Parameter] [Secret] readonly string NugetApiKey;
    [Parameter] [Secret] readonly string NugetApiIntKey;
    [Parameter] static bool DryRun = true;

    readonly string NugetApiUrl = DryRun ?
        "https://apiint.nugettest.org/v3/index.json" :
        "https://api.nuget.org/v3/index.json";

    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath NugetDirectory => RootDirectory / "artifacts";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            NugetDirectory.DeleteDirectory();
            DotNetClean(_ => _
                .SetProject(Solution.StepWise_ProseMirror));
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(_ => _
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(_ => _
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetTest(_ => _
                .SetConfiguration(Configuration.Debug));
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .Requires(() => Configuration.Equals(Configuration.Release))
        .Executes(() =>
        {
            Log.Information($"Dry run: {DryRun}");
            DotNetPack(s => s
                .SetProject(Solution.StepWise_ProseMirror)
                .SetConfiguration(Configuration)
                .SetProperties(new Dictionary<string, object>() {
                    ["RepositoryCommit"] = GitVersion.Sha,
                    ["RepositoryBranch"] = GitVersion.BranchName
                })
                .SetVersion(GitVersion.NuGetVersionV2)
                .SetOutputDirectory("artifacts"));
        });

    Target Push => _ => _
        .DependsOn(Clean, Pack, Test)
        .Requires(() => Configuration.Equals(Configuration.Release))
        .Executes(() =>
        {
            Log.Information($"Dry run: {DryRun}");
            GlobFiles(NugetDirectory, "*.nupkg")
               .Where(x => !x.EndsWith("symbols.nupkg"))
               .ForEach(x =>
               {
                   DotNetNuGetPush(s => s
                       .SetTargetPath(x)
                       .SetApiKey(DryRun ? NugetApiIntKey : NugetApiKey)
                       .SetSource(NugetApiUrl)
                   );
               });
        });

}
