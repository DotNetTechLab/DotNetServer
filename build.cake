#tool "nuget:?package=GitReleaseNotes"
#tool "nuget:?package=GitVersion.CommandLine"
#tool "nuget:?package=gitlink"

var target = Argument("target", "Default");
var outputDir = "./artifacts/";
var solutionPath = "./DotNetServer.sln";

Task("Clean")
    .Does(() => {
        if (DirectoryExists(outputDir))
        {
            DeleteDirectory(outputDir, recursive:true);
        }
        CreateDirectory(outputDir);
    });

Task("Restore")
    .Does(() => {
        DotNetCoreRestore(".");
    });

GitVersion versionInfo = null;
Task("Version")
    .Does(() => {
        GitVersion(new GitVersionSettings{
            UpdateAssemblyInfo = true,
            OutputType = GitVersionOutput.BuildServer
        });
        versionInfo = GitVersion(new GitVersionSettings{ OutputType = GitVersionOutput.Json });

		var file = "./src/DotNetServer.Core.cs";
		CreateAssemblyInfo(file, new AssemblyInfoSettings {
			Product = "DotNetServer.Core",
			Version = versionInfo.NuGetVersionV2,
			FileVersion = versionInfo.AssemblySemFileVer,
			InformationalVersion = versionInfo.InformationalVersion,
			Copyright = "Copyright (c) Contoso 2017 - " + DateTime.Now.Year
		});
    });

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Version")
    .IsDependentOn("Restore")
    .Does(() => {
        MSBuild(solutionPath);
    });

Task("Test")
    .IsDependentOn("Build")
    .Does(() => {
        DotNetCoreTest("./test/DotNetServer.Core.Test");
    });

Task("Package")
    .IsDependentOn("Test")
    .Does(() => {
        GenerateReleaseNotes();

        if (AppVeyor.IsRunningOnAppVeyor)
        {
            foreach (var file in GetFiles(outputDir + "**/*"))
            {
                AppVeyor.UploadArtifact(file.FullPath);
            }
        }
    });

private void PackageProject(string projectName, string projectJsonPath)
{
    var settings = new DotNetCorePackSettings
        {
            OutputDirectory = outputDir,
            NoBuild = true
        };

    DotNetCorePack(projectJsonPath, settings);

    System.IO.File.WriteAllLines(outputDir + "artifacts", new[]{
        "nuget:" + projectName + "." + versionInfo.NuGetVersion + ".nupkg",
        "nugetSymbols:" + projectName + "." + versionInfo.NuGetVersion + ".symbols.nupkg",
        "releaseNotes:releasenotes.md"
    });
}

private void GenerateReleaseNotes()
{
    // GitReleaseNotes("./artifacts/releasenotes.md", new GitReleaseNotesSettings(){
    //     WorkingDirectory = "."
    // });

    // if (string.IsNullOrEmpty(System.IO.File.ReadAllText("./artifacts/releasenotes.md")))
    // {
    //     System.IO.File.WriteAllText("./artifacts/releasenotes.md", "No issues closed since last release");
    // }
}

Task("Default")
    .IsDependentOn("Package");

RunTarget(target);
