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
        <Project Sdk="Microsoft.NET.Sdk">
            <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
            </PropertyGroup>
        </Project>
        """;

    var xmlReader = XmlReader.Create(new StringReader(projectText));
    var projectRoot = ProjectRootElement.Create(xmlReader);
    Directory.CreateDirectory(Path.Join(Environment.CurrentDirectory, "test"));
    projectRoot.FullPath = Path.Join(Environment.CurrentDirectory, "test", "test.csproj");

    var buildParameters = new BuildParameters
    {
        Loggers = [new ConsoleLogger(LoggerVerbosity.Normal)],
    };
    var buildRequest = new BuildRequestData(
        ProjectInstance.FromProjectRootElement(projectRoot, new ProjectOptions()),
        targetsToBuild: ["Restore", "Build"]);
    var result = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequest);
    Console.WriteLine(result.OverallResult);
}
