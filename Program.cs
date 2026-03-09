using Microsoft.Build.Construction;
using Microsoft.Build.Locator;

MSBuildLocator.RegisterDefaults();

Test();

void Test()
{
    var projectRootElement = ProjectRootElement.Create("C:/test.proj");
    projectRootElement.FullPath = "C:/test2.proj";
    projectRootElement.FullPath = "C:/test.proj";
    projectRootElement.FullPath = "C:/test2.proj";
    projectRootElement = ProjectRootElement.Create("C:/test.proj");
    projectRootElement.FullPath = "C:/test2.proj";
    projectRootElement.FullPath = "C:/test.proj";
    projectRootElement.FullPath = "C:/test2.proj";
}
