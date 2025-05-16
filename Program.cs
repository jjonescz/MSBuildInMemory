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
                    DependsOnTargets="_LoadRestoreGraphEntryPoints"
                    Returns="@(FilteredRestoreGraphProjectInputItems)">
                <ItemGroup>
                    <FilteredRestoreGraphProjectInputItems Include="@(RestoreGraphProjectInputItems)" />
                </ItemGroup>
            </Target>

            <Target Name="_GetAllRestoreProjectPathItems"
                    DependsOnTargets="_FilterRestoreGraphProjectInputItems"
                    Returns="@(_RestoreProjectPathItems)">
                <ItemGroup>
                    <_RestoreProjectPathItems Include="@(FilteredRestoreGraphProjectInputItems)" />
                </ItemGroup>
            </Target>

            <Target Name="_GenerateRestoreGraph"
                    DependsOnTargets="_FilterRestoreGraphProjectInputItems;_GetAllRestoreProjectPathItems;_GenerateRestoreGraphProjectEntry;_GenerateProjectRestoreGraph"
                    Returns="@(_RestoreGraphEntry)">
                <!-- Output from dependency _GenerateRestoreGraphProjectEntry and _GenerateProjectRestoreGraph -->
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
    var buildRequest = new BuildRequestData(
        ProjectInstance.FromProjectRootElement(projectRoot, new ProjectOptions
        {
            LoadSettings = ProjectLoadSettings.RecordEvaluatedItemElements,
            ProjectCollection = projectCollection,
        }),
        targetsToBuild: ["Restore", "Build"]);
    var result = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequest);
    Console.WriteLine(result.OverallResult);

    projectCollection.Dispose();
}
