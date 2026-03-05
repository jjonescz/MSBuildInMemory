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
    var loggers = new ILogger[]
    {
        new BinaryLogger { Parameters = "msbuild.binlog" },
        new ConsoleLogger(LoggerVerbosity.Quiet),
    };

    var projectCollection = new ProjectCollection(
        globalProperties: new Dictionary<string, string>
        {
            { "_BuildNonexistentProjectsByDefault", bool.TrueString },
            { "RestoreUseSkipNonexistentTargets", bool.FalseString },
        },
        loggers: loggers,
        ToolsetDefinitionLocations.Default
    );

    var projectRoot2 = createProjectRootElement("test2", """
        <Project>

            <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />

            <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
            </PropertyGroup>

            <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />

        </Project>
        """);

    var projectRoot = createProjectRootElement("test", """
        <Project>

            <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />

            <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
            </PropertyGroup>

            <ItemGroup>
                <ProjectReference Include="test2.csproj" />
            </ItemGroup>

            <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />

        </Project>
        """);

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

    ProjectRootElement createProjectRootElement(string name, string projectText)
    {
        var projectDir = Path.Join(Environment.CurrentDirectory, "test");
        Directory.CreateDirectory(projectDir);

        var projectFilePath = Path.Join(Environment.CurrentDirectory, "test", $"{name}.csproj");

        if (args.Contains("--write"))
        {
            File.WriteAllText(projectFilePath, projectText);
            return ProjectRootElement.Open(projectFilePath, projectCollection);
        }

        var xmlReader = XmlReader.Create(new StringReader(projectText));
        var projectRoot = ProjectRootElement.Create(xmlReader, projectCollection);
        projectRoot.FullPath = projectFilePath;
        return projectRoot;
    }
}
