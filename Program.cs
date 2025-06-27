using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;
using Microsoft.Build.Logging;

MSBuildLocator.RegisterDefaults();

BuildInMemoryProject();

void BuildInMemoryProject()
{
    var projectText = """
        <Project>

            <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />

            <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
            </PropertyGroup>

            <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />

            <!-- Override targets which don't work with project files that are not present on disk. -->

            <Target Name="_FilterRestoreGraphProjectInputItems"
                    DependsOnTargets="_LoadRestoreGraphEntryPoints">
                <!-- No-op, the original output is not needed by the overwritten targets. -->
            </Target>

            <Target Name="_GetAllRestoreProjectPathItems"
                    DependsOnTargets="_FilterRestoreGraphProjectInputItems;_GenerateRestoreProjectPathWalk"
                    Returns="@(_RestoreProjectPathItems)">
                <!-- Output from dependency _GenerateRestoreProjectPathWalk. -->
            </Target>

            <Target Name="_GenerateRestoreGraph"
                    DependsOnTargets="_FilterRestoreGraphProjectInputItems;_GetAllRestoreProjectPathItems;_GenerateRestoreGraphProjectEntry;_GenerateProjectRestoreGraph"
                    Returns="@(_RestoreGraphEntry)">
                <!-- Output partly from dependency _GenerateRestoreGraphProjectEntry and _GenerateProjectRestoreGraph. -->

                <ItemGroup>
                    <_GenerateRestoreGraphProjectEntryInput Include="@(_RestoreProjectPathItems)" Exclude="$(MSBuildProjectFullPath)" />
                </ItemGroup>

                <MSBuild
                    BuildInParallel="$(RestoreBuildInParallel)"
                    Projects="@(_GenerateRestoreGraphProjectEntryInput)"
                    Targets="_GenerateRestoreGraphProjectEntry"
                    Properties="$(_GenerateRestoreGraphProjectEntryInputProperties)">

                <Output
                    TaskParameter="TargetOutputs"
                    ItemName="_RestoreGraphEntry" />
                </MSBuild>

                <MSBuild
                    BuildInParallel="$(RestoreBuildInParallel)"
                    Projects="@(_GenerateRestoreGraphProjectEntryInput)"
                    Targets="_GenerateProjectRestoreGraph"
                    Properties="$(_GenerateRestoreGraphProjectEntryInputProperties)">

                <Output
                    TaskParameter="TargetOutputs"
                    ItemName="_RestoreGraphEntry" />
                </MSBuild>
            </Target>

        </Project>
        """;

    var loggers = new ILogger[]
    {
        new BinaryLogger { Parameters = "msbuild.binlog" },
        new ConsoleLogger(LoggerVerbosity.Quiet),
    };

    var projectCollection = new ProjectCollection(
        globalProperties: new Dictionary<string, string>(),
        loggers: loggers,
        ToolsetDefinitionLocations.Default
    );

    ProjectRootElement projectRoot;
    var projectDir = Path.Join(Environment.CurrentDirectory, "test");
    Directory.CreateDirectory(projectDir);
    var projectFilePath = Path.Join(Environment.CurrentDirectory, "test", "test.csproj");
    if (args.Contains("--write"))
    {
        File.WriteAllText(projectFilePath, projectText);
        projectRoot = ProjectRootElement.Open(projectFilePath, projectCollection);
    }
    else
    {
        var xmlReader = XmlReader.Create(new StringReader(projectText));
        projectRoot = ProjectRootElement.Create(xmlReader, projectCollection);
        projectRoot.FullPath = projectFilePath;
    }

    var buildParameters = new BuildParameters(projectCollection)
    {
        Loggers = projectCollection.Loggers,
        LogTaskInputs = true,
        LogInitialPropertiesAndItems = true,
        DetailedSummary = true,
        OnlyLogCriticalEvents = false,
    };
    BuildManager.DefaultBuildManager.BeginBuild(buildParameters);

    // Restore
    {
        var buildRequest = new BuildRequestData(
            ProjectInstance.FromProjectRootElement(projectRoot, new ProjectOptions
            {
                LoadSettings = ProjectLoadSettings.RecordEvaluatedItemElements,
                ProjectCollection = projectCollection,
                GlobalProperties = new Dictionary<string, string>(projectCollection.GlobalProperties, StringComparer.OrdinalIgnoreCase)
                {
                    ["MSBuildRestoreSessionId"] = Guid.NewGuid().ToString("D"),
                    ["MSBuildIsRestoring"] = bool.TrueString,
                },
            }),
            targetsToBuild: ["Restore"],
            hostServices: null,
            BuildRequestDataFlags.ClearCachesAfterBuild | BuildRequestDataFlags.SkipNonexistentTargets | BuildRequestDataFlags.IgnoreMissingEmptyAndInvalidImports | BuildRequestDataFlags.FailOnUnresolvedSdk);
        var result = BuildManager.DefaultBuildManager.BuildRequest(buildRequest);
        Console.WriteLine($"Restore result: {result.OverallResult}");
        if (result.OverallResult != BuildResultCode.Success)
        {
            return;
        }
    }

    // Build
    {
        var buildRequest = new BuildRequestData(
            ProjectInstance.FromProjectRootElement(projectRoot, new ProjectOptions
            {
                LoadSettings = ProjectLoadSettings.RecordEvaluatedItemElements,
                ProjectCollection = projectCollection,
                GlobalProperties = new Dictionary<string, string>(projectCollection.GlobalProperties, StringComparer.OrdinalIgnoreCase),
            }),
            targetsToBuild: ["Build"]);
        var result = BuildManager.DefaultBuildManager.BuildRequest(buildRequest);
        Console.WriteLine($"Build result: {result.OverallResult}");
        if (result.OverallResult != BuildResultCode.Success)
        {
            return;
        }
    }

    BuildManager.DefaultBuildManager.EndBuild();
    projectCollection.Dispose();
}
