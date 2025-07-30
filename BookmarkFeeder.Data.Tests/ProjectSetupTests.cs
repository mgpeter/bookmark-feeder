using Shouldly;
using Xunit;
using System.Reflection;

namespace BookmarkFeeder.Data.Tests;

public class ProjectSetupTests
{
    [Fact]
    public void DataProject_ShouldHaveCorrectAssemblyName()
    {
        // Arrange & Act
        var assembly = Assembly.GetAssembly(typeof(BookmarkFeeder.Data.DataAssemblyReference));
        
        // Assert
        assembly.ShouldNotBeNull();
        assembly.GetName().Name.ShouldBe("BookmarkFeeder.Data");
    }
    
    [Fact]
    public void DataProject_ShouldTargetNet90()
    {
        // Arrange & Act
        var assembly = Assembly.GetAssembly(typeof(BookmarkFeeder.Data.DataAssemblyReference));
        var targetFramework = assembly?.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();
        
        // Assert
        targetFramework.ShouldNotBeNull();
        targetFramework.FrameworkName.ShouldContain(".NETCoreApp,Version=v9.0");
    }
}