using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;
using Microsoft.Build.Logging;

MSBuildLocator.RegisterDefaults();

BuildInMemoryProject();

static void BuildInMemoryProject()
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

    var xmlReader = XmlReader.Create(new StringReader(projectText));
    var projectRoot = ProjectRootElement.Create(xmlReader);
    Directory.CreateDirectory(Path.Join(Environment.CurrentDirectory, "test"));
    projectRoot.FullPath = Path.Join(Environment.CurrentDirectory, "test", "test.csproj");

    var buildParameters = new BuildParameters
    {
        Loggers =
        [
            new BinaryLogger { Parameters = "msbuild.binlog" },
            new ConsoleLogger(LoggerVerbosity.Quiet),
        ],
    };
    var buildRequest = new BuildRequestData(
        ProjectInstance.FromProjectRootElement(projectRoot, new ProjectOptions()),
        targetsToBuild: ["Restore", "Build"]);
    var result = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequest);
    Console.WriteLine(result.OverallResult);
}
