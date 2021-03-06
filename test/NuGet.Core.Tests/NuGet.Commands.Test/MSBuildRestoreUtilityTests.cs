﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class MSBuildRestoreUtilityTests
    {
        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_VerifyInvalidProjectReferencesAreIgnored()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");
                var project2Root = Path.Combine(workingDir, "b");
                var project2Path = Path.Combine(project2Root, "b.csproj");
                var project3Root = Path.Combine(workingDir, "c");
                var project3Path = Path.Combine(project3Root, "c.csproj");
                var outputPath3 = Path.Combine(project3Root, "obj");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "Version", "2.0.0-rc.2+a.b.c" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46;netstandard16" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                    { "CrossTargeting", "true" },
                    { "RestoreLegacyPackagesDirectory", "true" }
                });

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "Version", "2.0.0-rc.2+a.b.c" },
                    { "ProjectName", "c" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath3 },
                    { "ProjectUniqueName", "C82C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project3Path },
                    { "TargetFrameworks", "net46;netstandard16" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                    { "CrossTargeting", "true" },
                    { "RestoreLegacyPackagesDirectory", "true" }
                });

                // A -> B
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectReference" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectReferenceUniqueName", "AA2C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project2Path },
                    { "TargetFrameworks", "net46;netstandard1.6" },
                    { "CrossTargeting", "true" },
                });

                // A -> C
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectReference" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectReferenceUniqueName", "C82C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project3Path },
                    { "TargetFrameworks", "net46;netstandard1.6" },
                    { "CrossTargeting", "true" },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var projectReferences = dgSpec.Projects.Select(e => e.RestoreMetadata)
                    .SelectMany(e => e.TargetFrameworks)
                    .SelectMany(e => e.ProjectReferences)
                    .Select(e => e.ProjectPath)
                    .Distinct()
                    .ToList();

                // Assert
                Assert.Equal(1, projectReferences.Count);
                Assert.Equal(project3Path, projectReferences[0]);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpecVersion_UAP()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project2Root = Path.Combine(workingDir, "b");

                var project1JsonPath = Path.Combine(project1Root, "project.json");
                var project2JsonPath = Path.Combine(project2Root, "project.json");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var project2Path = Path.Combine(project2Root, "b.csproj");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectJsonPath", project1JsonPath },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "ProjectJson" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                });

                var project1Json = @"
                {
                    ""version"": ""2.0.0-beta.1+build"",
                    ""description"": """",
                    ""authors"": [ ""author"" ],
                    ""tags"": [ """" ],
                    ""projectUrl"": """",
                    ""licenseUrl"": """",
                    ""frameworks"": {
                        ""net45"": {
                        }
                    }
                }";

                Directory.CreateDirectory(project1Root);
                File.WriteAllText(project1JsonPath, project1Json);

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single(e => e.Name == "a");

                // Assert
                Assert.Equal("2.0.0-beta.1+build", project1Spec.Version.ToFullString());
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_Tool()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "DotnetCliTool" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "netcoreapp1.0" },
                    { "CrossTargeting", "true" },
                });

                // Package reference
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "x" },
                    { "VersionRange", "1.0.0-beta.*" },
                    { "TargetFrameworks", "netcoreapp1.0" },
                    { "CrossTargeting", "true" },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single(e => e.Name == "a");

                // Assert
                // Dependency counts
                Assert.Equal(1, project1Spec.GetTargetFramework(NuGetFramework.Parse("netcoreapp1.0")).Dependencies.Count);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyIncludeFlags()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46" },
                    { "CrossTargeting", "true" },
                });

                // Package references
                // A net46 -> X
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "x" },
                    { "VersionRange", "1.0.0" },
                    { "TargetFrameworks", "net46" },
                    { "IncludeAssets", "build;compile" },
                    { "CrossTargeting", "true" },
                });

                // A net46 -> Y
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "y" },
                    { "VersionRange", "1.0.0" },
                    { "TargetFrameworks", "net46" },
                    { "ExcludeAssets", "build;compile" },
                });

                // A net46 -> Z
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "z" },
                    { "VersionRange", "1.0.0" },
                    { "TargetFrameworks", "net46" },
                    { "PrivateAssets", "all" },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();
                var x = project1Spec.GetTargetFramework(NuGetFramework.Parse("net46")).Dependencies.Single(e => e.Name == "x");
                var y = project1Spec.GetTargetFramework(NuGetFramework.Parse("net46")).Dependencies.Single(e => e.Name == "y");
                var z = project1Spec.GetTargetFramework(NuGetFramework.Parse("net46")).Dependencies.Single(e => e.Name == "z");

                // Assert
                // X
                Assert.Equal((LibraryIncludeFlags.Build | LibraryIncludeFlags.Compile), x.IncludeType);
                Assert.Equal((LibraryIncludeFlagUtils.DefaultSuppressParent), x.SuppressParent);

                // Y
                Assert.Equal(LibraryIncludeFlags.All & ~(LibraryIncludeFlags.Build | LibraryIncludeFlags.Compile), y.IncludeType);
                Assert.Equal((LibraryIncludeFlagUtils.DefaultSuppressParent), y.SuppressParent);

                // Z
                Assert.Equal(LibraryIncludeFlags.All, z.IncludeType);
                Assert.Equal(LibraryIncludeFlags.All, z.SuppressParent);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyBasicMetadata()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");
                var configFilePath = Path.Combine(project1Root, "nuget.config");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "Version", "2.0.0-rc.2+a.b.c" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46;netstandard16" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                    { "ConfigFilePaths", configFilePath },
                    { "CrossTargeting", "true" },
                    { "RestoreLegacyPackagesDirectory", "true" }
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                // Assert
                Assert.Equal(project1Path, project1Spec.FilePath);
                Assert.Equal("a", project1Spec.Name);
                Assert.Equal("2.0.0-rc.2+a.b.c", project1Spec.Version.ToFullString());
                Assert.Equal(ProjectStyle.PackageReference, project1Spec.RestoreMetadata.ProjectStyle);
                Assert.Equal("482C20DE-DFF9-4BD0-B90A-BD3201AA351A", project1Spec.RestoreMetadata.ProjectUniqueName);
                Assert.Equal(project1Path, project1Spec.RestoreMetadata.ProjectPath);
                Assert.Equal(0, project1Spec.RestoreMetadata.TargetFrameworks.SelectMany(e => e.ProjectReferences).Count());
                Assert.Null(project1Spec.RestoreMetadata.ProjectJsonPath);
                Assert.Equal("net46|netstandard1.6", string.Join("|", project1Spec.TargetFrameworks.Select(e => e.FrameworkName.GetShortFolderName())));
                Assert.Equal("net46|netstandard16", string.Join("|", project1Spec.RestoreMetadata.OriginalTargetFrameworks));
                Assert.Equal(outputPath1, project1Spec.RestoreMetadata.OutputPath);
                Assert.Equal("https://nuget.org/a/index.json|https://nuget.org/b/index.json", string.Join("|", project1Spec.RestoreMetadata.Sources.Select(s => s.Source)));
                Assert.Equal(fallbackFolder, string.Join("|", project1Spec.RestoreMetadata.FallbackFolders));
                Assert.Equal(packagesFolder, string.Join("|", project1Spec.RestoreMetadata.PackagesPath));
                Assert.Equal(configFilePath, string.Join("|", project1Spec.RestoreMetadata.ConfigFilePaths));
                Assert.Equal(0, project1Spec.RuntimeGraph.Runtimes.Count);
                Assert.Equal(0, project1Spec.RuntimeGraph.Supports.Count);
                Assert.True(project1Spec.RestoreMetadata.CrossTargeting);
                Assert.True(project1Spec.RestoreMetadata.LegacyPackagesDirectory);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyDefaultVersion()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");
                var configFilePath = Path.Combine(project1Root, "nuget.config");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46;netstandard16" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "ConfigFilePaths", configFilePath },
                    { "PackagesPath", packagesFolder },
                    { "CrossTargeting", "true" },
                    { "RestoreLegacyPackagesDirectory", "true" }
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                // Assert
                Assert.Equal("1.0.0", project1Spec.Version.ToFullString());
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyInvalidVersionThrowsOnParse()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");
                var configFilePath = Path.Combine(project1Root, "nuget.config");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "Version", "notaversionstring" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46;netstandard16" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "ConfigFilePaths", configFilePath },
                    { "PackagesPath", packagesFolder },
                    { "CrossTargeting", "true" },
                    { "RestoreLegacyPackagesDirectory", "true" }
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act && Assert
                Assert.Throws(typeof(ArgumentException),
                    () => MSBuildRestoreUtility.GetDependencySpec(wrappedItems));
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreNonCrossTargeting()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");
                var configFilePath = Path.Combine(project1Root, "nuget.config");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "netstandard16" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                    { "ConfigFilePaths", configFilePath },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                // Assert
                Assert.Equal(project1Path, project1Spec.FilePath);
                Assert.Equal("a", project1Spec.Name);
                Assert.Equal(ProjectStyle.PackageReference, project1Spec.RestoreMetadata.ProjectStyle);
                Assert.Equal("netstandard1.6", string.Join("|", project1Spec.TargetFrameworks.Select(e => e.FrameworkName.GetShortFolderName())));
                Assert.Equal("netstandard16", string.Join("|", project1Spec.RestoreMetadata.OriginalTargetFrameworks));
                Assert.False(project1Spec.RestoreMetadata.CrossTargeting);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreNonLegacyPackagesDirectory()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "netstandard16" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                // Assert
                Assert.Equal(project1Path, project1Spec.FilePath);
                Assert.Equal("a", project1Spec.Name);
                Assert.Equal(ProjectStyle.PackageReference, project1Spec.RestoreMetadata.ProjectStyle);
                Assert.Equal("netstandard1.6", string.Join("|", project1Spec.TargetFrameworks.Select(e => e.FrameworkName.GetShortFolderName())));
                Assert.Equal("netstandard16", string.Join("|", project1Spec.RestoreMetadata.OriginalTargetFrameworks));
                Assert.False(project1Spec.RestoreMetadata.LegacyPackagesDirectory);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyImports()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46;netstandard16" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                    { "CrossTargeting", "true" },
                });

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "TargetFrameworkInformation" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "PackageTargetFallback", "portable-net45+win8;dnxcore50;;" },
                    { "TargetFramework", "netstandard16" }
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                var nsTFM = project1Spec.GetTargetFramework(NuGetFramework.Parse("netstandard16"));
                var netTFM = project1Spec.GetTargetFramework(NuGetFramework.Parse("net46"));

                // Assert
                Assert.Equal(2, nsTFM.Imports.Count);
                Assert.Equal(0, netTFM.Imports.Count);

                Assert.Equal(NuGetFramework.Parse("portable-net45+win8"), nsTFM.Imports[0]);
                Assert.Equal(NuGetFramework.Parse("dnxcore50"), nsTFM.Imports[1]);

                // Verify fallback framework
                var fallbackFramework = (FallbackFramework)project1Spec.TargetFrameworks
                    .Single(e => e.FrameworkName.GetShortFolderName() == "netstandard1.6")
                    .FrameworkName;

                // net46 does not have imports
                var fallbackFrameworkNet45 = project1Spec.TargetFrameworks
                    .Single(e => e.FrameworkName.GetShortFolderName() == "net46")
                    .FrameworkName
                    as FallbackFramework;

                Assert.Null(fallbackFrameworkNet45);
                Assert.Equal(2, fallbackFramework.Fallback.Count);
                Assert.Equal(NuGetFramework.Parse("portable-net45+win8"), fallbackFramework.Fallback[0]);
                Assert.Equal(NuGetFramework.Parse("dnxcore50"), fallbackFramework.Fallback[1]);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyImportsEmpty()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46;netstandard16" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                    { "CrossTargeting", "true" },
                });

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "TargetFrameworkInformation" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "PackageTargetFallback", "" },
                    { "TargetFramework", "netstandard16" }
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                var nsTFM = project1Spec.GetTargetFramework(NuGetFramework.Parse("netstandard16"));
                var netTFM = project1Spec.GetTargetFramework(NuGetFramework.Parse("net46"));

                // Assert
                Assert.Equal(0, nsTFM.Imports.Count);
                Assert.Equal(0, netTFM.Imports.Count);

                // Verify no fallback frameworks
                var fallbackFrameworks = project1Spec.TargetFrameworks.Select(e => e.FrameworkName as FallbackFramework);
                Assert.True(fallbackFrameworks.All(e => e == null));
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyWhitespaceRemoved()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "  a\n  " },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "  net46  ;   netstandard16\n  " },
                    { "Sources", "https://nuget.org/a/index.json; https://nuget.org/b/index.json\n" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                    { "CrossTargeting", "true" },
                });

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "TargetFrameworkInformation" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "PackageTargetFallback", "   portable-net45+win8  ;   dnxcore50\n   ; ;  " },
                    { "TargetFramework", " netstandard16\n  " }
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                var nsTFM = project1Spec.GetTargetFramework(NuGetFramework.Parse("netstandard16"));
                var netTFM = project1Spec.GetTargetFramework(NuGetFramework.Parse("net46"));

                // Assert
                Assert.Equal("a", project1Spec.RestoreMetadata.ProjectName);
                Assert.Equal(2, nsTFM.Imports.Count);
                Assert.Equal(0, netTFM.Imports.Count);

                Assert.Equal(NuGetFramework.Parse("portable-net45+win8"), nsTFM.Imports[0]);
                Assert.Equal(NuGetFramework.Parse("dnxcore50"), nsTFM.Imports[1]);

                // Verify fallback framework
                var fallbackFramework = (FallbackFramework)project1Spec.TargetFrameworks
                    .Single(e => e.FrameworkName.GetShortFolderName() == "netstandard1.6")
                    .FrameworkName;

                // net46 does not have imports
                var fallbackFrameworkNet45 = project1Spec.TargetFrameworks
                    .Single(e => e.FrameworkName.GetShortFolderName() == "net46")
                    .FrameworkName
                    as FallbackFramework;

                Assert.Null(fallbackFrameworkNet45);
                Assert.Equal(2, fallbackFramework.Fallback.Count);
                Assert.Equal(NuGetFramework.Parse("portable-net45+win8"), fallbackFramework.Fallback[0]);
                Assert.Equal(NuGetFramework.Parse("dnxcore50"), fallbackFramework.Fallback[1]);

                // Verify original frameworks are trimmed
                Assert.Equal("net46", project1Spec.RestoreMetadata.OriginalTargetFrameworks[0]);
                Assert.Equal("netstandard16", project1Spec.RestoreMetadata.OriginalTargetFrameworks[1]);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyRuntimes()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46;netstandard16" },
                    { "RuntimeIdentifiers", "win7-x86;linux-x64" },
                    { "RuntimeSupports", "net46.app;win8.app" },
                    { "CrossTargeting", "true" },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                // Assert
                Assert.Equal(2, project1Spec.RuntimeGraph.Runtimes.Count);
                Assert.Equal(2, project1Spec.RuntimeGraph.Supports.Count);
                Assert.Equal("win7-x86", project1Spec.RuntimeGraph.Runtimes["win7-x86"].RuntimeIdentifier);
                Assert.Equal("linux-x64", project1Spec.RuntimeGraph.Runtimes["linux-x64"].RuntimeIdentifier);
                Assert.Equal("net46.app", project1Spec.RuntimeGraph.Supports["net46.app"].Name);
                Assert.Equal("win8.app", project1Spec.RuntimeGraph.Supports["win8.app"].Name);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyRuntimes_Duplicates()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46;netstandard16" },
                    { "RuntimeIdentifiers", "win7-x86;linux-x64;win7-x86;linux-x64" },
                    { "RuntimeSupports", "net46.app;win8.app;net46.app;win8.app" },
                    { "CrossTargeting", "true" },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();

                // Assert
                Assert.Equal(2, project1Spec.RuntimeGraph.Runtimes.Count);
                Assert.Equal(2, project1Spec.RuntimeGraph.Supports.Count);
                Assert.Equal("win7-x86", project1Spec.RuntimeGraph.Runtimes["win7-x86"].RuntimeIdentifier);
                Assert.Equal("linux-x64", project1Spec.RuntimeGraph.Runtimes["linux-x64"].RuntimeIdentifier);
                Assert.Equal("net46.app", project1Spec.RuntimeGraph.Supports["net46.app"].Name);
                Assert.Equal("win8.app", project1Spec.RuntimeGraph.Supports["win8.app"].Name);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCore_Conditionals()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project2Root = Path.Combine(workingDir, "b");

                var project1Path = Path.Combine(project1Root, "a.csproj");
                var project2Path = Path.Combine(project2Root, "b.csproj");

                var outputPath1 = Path.Combine(project1Root, "obj");
                var outputPath2 = Path.Combine(project2Root, "obj");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46;netstandard1.6" },
                    { "CrossTargeting", "true" },
                });

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "b" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath2 },
                    { "ProjectUniqueName", "AA2C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project2Path },
                    { "TargetFrameworks", "net45;netstandard1.0" },
                    { "CrossTargeting", "true" },
                });

                // A -> B
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectReference" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectReferenceUniqueName", "AA2C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project2Path },
                    { "TargetFrameworks", "netstandard1.6" },
                    { "CrossTargeting", "true" },
                });

                // Package references
                // A net46 -> X
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "x" },
                    { "VersionRange", "1.0.0-beta.*" },
                    { "TargetFrameworks", "net46" },
                    { "CrossTargeting", "true" },
                });

                // A netstandard1.6 -> Z
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "z" },
                    { "VersionRange", "2.0.0" },
                    { "TargetFrameworks", "netstandard1.6" },
                    { "CrossTargeting", "true" },
                });

                // B ALL -> Y
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", "AA2C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "y" },
                    { "VersionRange", "[1.0.0]" },
                    { "TargetFrameworks", "netstandard1.0;net45" },
                    { "CrossTargeting", "true" },
                });

                // Framework assembly
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "FrameworkAssembly" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "System.IO" },
                    { "TargetFrameworks", "net46" },
                    { "CrossTargeting", "true" },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single(e => e.Name == "a");
                var project2Spec = dgSpec.Projects.Single(e => e.Name == "b");

                var msbuildDependency = project1Spec.RestoreMetadata.TargetFrameworks
                    .Single(e => e.FrameworkName.Equals(NuGetFramework.Parse("netstandard1.6")))
                    .ProjectReferences
                    .Single();

                // Assert
                // Verify p2p reference
                Assert.Equal("AA2C20DE-DFF9-4BD0-B90A-BD3201AA351A", msbuildDependency.ProjectUniqueName);
                Assert.Equal(project2Path, msbuildDependency.ProjectPath);
                Assert.Equal(LibraryIncludeFlags.All, msbuildDependency.IncludeAssets);
                Assert.Equal(LibraryIncludeFlags.None, msbuildDependency.ExcludeAssets);
                Assert.Equal(LibraryIncludeFlagUtils.DefaultSuppressParent, msbuildDependency.PrivateAssets);
                Assert.Equal("netstandard1.6", string.Join("|", project1Spec.RestoreMetadata.TargetFrameworks
                    .Where(e => e.ProjectReferences.Count > 0)
                    .Select(e => e.FrameworkName.GetShortFolderName())
                    .OrderBy(s => s, StringComparer.Ordinal)));

                // Dependency counts
                Assert.Equal(0, project1Spec.Dependencies.Count);
                Assert.Equal(0, project2Spec.Dependencies.Count);

                Assert.Equal(2, project1Spec.GetTargetFramework(NuGetFramework.Parse("net46")).Dependencies.Count);
                Assert.Equal(1, project1Spec.GetTargetFramework(NuGetFramework.Parse("netstandard1.6")).Dependencies.Count);

                Assert.Equal(1, project2Spec.GetTargetFramework(NuGetFramework.Parse("net45")).Dependencies.Count);
                Assert.Equal(1, project2Spec.GetTargetFramework(NuGetFramework.Parse("netstandard1.0")).Dependencies.Count);

                // Verify dependencies
                var xDep = project1Spec.GetTargetFramework(NuGetFramework.Parse("net46")).Dependencies.Single(e => e.Name == "x");
                var zDep = project1Spec.GetTargetFramework(NuGetFramework.Parse("netstandard1.6")).Dependencies.Single(e => e.Name == "z");

                var yDep1 = project2Spec.GetTargetFramework(NuGetFramework.Parse("netstandard1.0")).Dependencies.Single();
                var yDep2 = project2Spec.GetTargetFramework(NuGetFramework.Parse("net45")).Dependencies.Single();

                var systemIO = project1Spec.GetTargetFramework(NuGetFramework.Parse("net46")).Dependencies.Single(e => e.Name == "System.IO");

                Assert.Equal("x", xDep.Name);
                Assert.Equal(VersionRange.Parse("1.0.0-beta.*"), xDep.LibraryRange.VersionRange);
                Assert.Equal(LibraryDependencyTarget.Package, xDep.LibraryRange.TypeConstraint);
                Assert.Equal(LibraryIncludeFlags.All, xDep.IncludeType);
                Assert.Equal(LibraryIncludeFlagUtils.DefaultSuppressParent, xDep.SuppressParent);

                Assert.Equal("z", zDep.Name);
                Assert.Equal(VersionRange.Parse("2.0.0"), zDep.LibraryRange.VersionRange);
                Assert.Equal(LibraryDependencyTarget.Package, zDep.LibraryRange.TypeConstraint);
                Assert.Equal(LibraryIncludeFlags.All, zDep.IncludeType);
                Assert.Equal(LibraryIncludeFlagUtils.DefaultSuppressParent, zDep.SuppressParent);

                Assert.Equal("y", yDep1.Name);
                Assert.Equal(VersionRange.Parse("[1.0.0]"), yDep1.LibraryRange.VersionRange);
                Assert.Equal(LibraryDependencyTarget.Package, yDep1.LibraryRange.TypeConstraint);
                Assert.Equal(LibraryIncludeFlags.All, yDep1.IncludeType);
                Assert.Equal(LibraryIncludeFlagUtils.DefaultSuppressParent, yDep1.SuppressParent);

                Assert.Equal(yDep1, yDep2);

                Assert.Equal("System.IO", systemIO.Name);
                Assert.Equal(VersionRange.All, systemIO.LibraryRange.VersionRange);
                Assert.Equal(LibraryDependencyTarget.Reference, systemIO.LibraryRange.TypeConstraint);
                Assert.Equal(LibraryIncludeFlags.All, systemIO.IncludeType);
                Assert.Equal(LibraryIncludeFlagUtils.DefaultSuppressParent, systemIO.SuppressParent);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCore_VerifyDuplicateItemsAreIgnored()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var projectRoot = Path.Combine(workingDir, "a");
                var projectPath = Path.Combine(projectRoot, "a.csproj");
                var outputPath = Path.Combine(projectRoot, "obj");

                var items = new List<IDictionary<string, string>>();

                var specItem = new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", projectPath },
                    { "TargetFrameworks", "net46;netstandard1.6" },
                    { "CrossTargeting", "true" },
                };

                // Add each item twice
                items.Add(specItem);
                items.Add(specItem);

                // A -> B
                var projectRef = new Dictionary<string, string>()
                {
                    { "Type", "ProjectReference" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectReferenceUniqueName", "AA2C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", "otherProjectPath.csproj" },
                    { "TargetFrameworks", "netstandard1.6" },
                    { "CrossTargeting", "true" },
                };

                items.Add(projectRef);
                items.Add(projectRef);

                // Package references
                // A netstandard1.6 -> Z
                var packageRef1 = new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "z" },
                    { "VersionRange", "2.0.0" },
                    { "TargetFrameworks", "netstandard1.6" },
                    { "CrossTargeting", "true" },
                };

                items.Add(packageRef1);
                items.Add(packageRef1);

                // B ALL -> Y
                var packageRef2 = new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "y" },
                    { "VersionRange", "[1.0.0]" },
                    { "TargetFrameworks", "netstandard1.6;net46" },
                    { "CrossTargeting", "true" },
                };

                items.Add(packageRef2);
                items.Add(packageRef2);

                // Framework assembly
                var frameworkAssembly = new Dictionary<string, string>()
                {
                    { "Type", "FrameworkAssembly" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "Id", "System.IO" },
                    { "TargetFrameworks", "net46" },
                    { "CrossTargeting", "true" },
                };

                items.Add(frameworkAssembly);
                items.Add(frameworkAssembly);

                // TFM info
                var tfmInfo = new Dictionary<string, string>()
                {
                    { "Type", "TargetFrameworkInformation" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "PackageTargetFallback", "portable-net45+win8;dnxcore50;;" },
                    { "TargetFramework", "netstandard16" }
                };

                items.Add(tfmInfo);
                items.Add(tfmInfo);

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var projectSpec = dgSpec.Projects.Single(e => e.Name == "a");

                // Assert
                Assert.Equal(0, projectSpec.Dependencies.Count);
                Assert.Equal(1, dgSpec.Projects.Count);
                Assert.Equal("System.IO|y", string.Join("|", projectSpec.GetTargetFramework(NuGetFramework.Parse("net46")).Dependencies.Select(e => e.Name)));
                Assert.Equal("z|y", string.Join("|", projectSpec.GetTargetFramework(NuGetFramework.Parse("netstandard1.6")).Dependencies.Select(e => e.Name)));
                Assert.Equal(2, projectSpec.RestoreMetadata.TargetFrameworks.Count);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCore_IgnoreBadItemWithMismatchedIds()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var projectRoot = Path.Combine(workingDir, "a");
                var projectPath = Path.Combine(projectRoot, "a.csproj");
                var outputPath = Path.Combine(projectRoot, "obj");

                var items = new List<IDictionary<string, string>>();

                var specItem = new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath },
                    { "ProjectUniqueName", "AA2C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", projectPath },
                    { "TargetFrameworks", "net46;netstandard1.6" },
                    { "CrossTargeting", "true" },
                };

                items.Add(specItem);

                // A -> B
                var projectRef = new Dictionary<string, string>()
                {
                    { "Type", "ProjectReference" },

                    // This ID does not match the project!
                    { "ProjectUniqueName", "BB2C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectReferenceUniqueName", "CC2C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", "otherProjectPath.csproj" },
                    { "TargetFrameworks", "netstandard1.6" },
                    { "CrossTargeting", "true" },
                };

                items.Add(projectRef);

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var projectSpec = dgSpec.Projects.Single(e => e.Name == "a");

                // Assert
                Assert.NotNull(projectSpec);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_UAP_P2P()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project2Root = Path.Combine(workingDir, "b");

                var project1JsonPath = Path.Combine(project1Root, "project.json");
                var project2JsonPath = Path.Combine(project2Root, "project.json");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var project2Path = Path.Combine(project2Root, "b.csproj");

                var items = new List<IDictionary<string, string>>();

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectJsonPath", project1JsonPath },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "ProjectJson" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                });

                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectJsonPath", project2JsonPath },
                    { "ProjectName", "b" },
                    { "ProjectStyle", "ProjectJson" },
                    { "ProjectUniqueName", "AA2C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project2Path },
                });

                // A -> B
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectReference" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectReferenceUniqueName", "AA2C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project2Path },
                });

                var project1Json = @"
                {
                    ""version"": ""1.0.0"",
                    ""description"": """",
                    ""authors"": [ ""author"" ],
                    ""tags"": [ """" ],
                    ""projectUrl"": """",
                    ""licenseUrl"": """",
                    ""frameworks"": {
                        ""net45"": {
                        }
                    }
                }";

                var project2Json = @"
                {
                    ""version"": ""1.0.0"",
                    ""description"": """",
                    ""authors"": [ ""author"" ],
                    ""tags"": [ """" ],
                    ""projectUrl"": """",
                    ""licenseUrl"": """",
                    ""frameworks"": {
                        ""net45"": {
                        }
                    }
                }";

                Directory.CreateDirectory(project1Root);
                Directory.CreateDirectory(project2Root);

                File.WriteAllText(project1JsonPath, project1Json);
                File.WriteAllText(project2JsonPath, project2Json);

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single(e => e.Name == "a");
                var project2Spec = dgSpec.Projects.Single(e => e.Name == "b");

                var allDependencies1 = project1Spec.Dependencies.Concat(project1Spec.TargetFrameworks.Single().Dependencies).ToList();
                var allDependencies2 = project2Spec.Dependencies.Concat(project2Spec.TargetFrameworks.Single().Dependencies).ToList();
                var msbuildDependency = project1Spec.RestoreMetadata.TargetFrameworks.Single().ProjectReferences.Single();

                // Assert
                Assert.Equal("AA2C20DE-DFF9-4BD0-B90A-BD3201AA351A", msbuildDependency.ProjectUniqueName);
                Assert.Equal(project2Path, msbuildDependency.ProjectPath);
                Assert.Equal(LibraryIncludeFlags.All, msbuildDependency.IncludeAssets);
                Assert.Equal(LibraryIncludeFlags.None, msbuildDependency.ExcludeAssets);
                Assert.Equal(LibraryIncludeFlagUtils.DefaultSuppressParent, msbuildDependency.PrivateAssets);
                Assert.Equal("net45", string.Join("|", project1Spec.RestoreMetadata.TargetFrameworks
                    .Select(e => e.FrameworkName.GetShortFolderName())
                    .OrderBy(s => s, StringComparer.Ordinal)));

                Assert.Equal(0, allDependencies2.Count);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_UAP_VerifyMetadata()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var projectJsonPath = Path.Combine(workingDir, "project.json");
                var projectPath = Path.Combine(workingDir, "a.csproj");

                var items = new List<IDictionary<string, string>>();
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectJsonPath", projectJsonPath },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "ProjectJson" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", projectPath },
                });

                var projectJson = @"
                {
                    ""version"": ""1.0.0"",
                    ""description"": """",
                    ""authors"": [ ""author"" ],
                    ""tags"": [ """" ],
                    ""projectUrl"": """",
                    ""licenseUrl"": """",
                    ""frameworks"": {
                        ""net45"": {
                        }
                    }
                }";

                File.WriteAllText(projectJsonPath, projectJson);

                // Act
                var spec = MSBuildRestoreUtility.GetPackageSpec(items.Select(CreateItems));

                // Assert
                Assert.Equal(projectJsonPath, spec.FilePath);
                Assert.Equal("a", spec.Name);
                Assert.Equal(ProjectStyle.ProjectJson, spec.RestoreMetadata.ProjectStyle);
                Assert.Equal("482C20DE-DFF9-4BD0-B90A-BD3201AA351A", spec.RestoreMetadata.ProjectUniqueName);
                Assert.Equal(projectPath, spec.RestoreMetadata.ProjectPath);
                Assert.Equal(0, spec.RestoreMetadata.TargetFrameworks.SelectMany(e => e.ProjectReferences).Count());
                Assert.Equal(projectJsonPath, spec.RestoreMetadata.ProjectJsonPath);
                Assert.Equal(NuGetFramework.Parse("net45"), spec.TargetFrameworks.Single().FrameworkName);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_UAP_IgnoresUnexpectedProperties()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var projectJsonPath = Path.Combine(workingDir, "project.json");
                var projectPath = Path.Combine(workingDir, "a.csproj");

                var items = new List<IDictionary<string, string>>();
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectJsonPath", projectJsonPath },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "ProjectJson" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", projectPath },
                    { "CrossTargeting", "true" },
                    { "RestoreLegacyPackagesDirectory", "true" },
                });

                var projectJson = @"
                {
                    ""version"": ""1.0.0"",
                    ""description"": """",
                    ""authors"": [ ""author"" ],
                    ""tags"": [ """" ],
                    ""projectUrl"": """",
                    ""licenseUrl"": """",
                    ""frameworks"": {
                        ""net45"": {
                        }
                    }
                }";

                File.WriteAllText(projectJsonPath, projectJson);

                // Act
                var spec = MSBuildRestoreUtility.GetPackageSpec(items.Select(CreateItems));

                // Assert
                Assert.False(spec.RestoreMetadata.CrossTargeting);
                Assert.False(spec.RestoreMetadata.LegacyPackagesDirectory);
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NonNuGetProject()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var projectPath = Path.Combine(workingDir, "a.csproj");

                var items = new List<IDictionary<string, string>>();
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", projectPath },
                    { "TargetFrameworks", "net462" },
                    { "ProjectName", "a" },
                    { "CrossTargeting", "true" },
                });

                // Act
                var spec = MSBuildRestoreUtility.GetPackageSpec(items.Select(CreateItems));

                // Assert
                Assert.Equal(projectPath, spec.FilePath);
                Assert.Equal("a", spec.Name);
                Assert.Equal(ProjectStyle.Unknown, spec.RestoreMetadata.ProjectStyle);
                Assert.Equal("482C20DE-DFF9-4BD0-B90A-BD3201AA351A", spec.RestoreMetadata.ProjectUniqueName);
                Assert.Equal(projectPath, spec.RestoreMetadata.ProjectPath);
                Assert.Equal(NuGetFramework.Parse("net462"), spec.TargetFrameworks.Single().FrameworkName);
                Assert.Equal(0, spec.RestoreMetadata.TargetFrameworks.SelectMany(e => e.ProjectReferences).Count());
                Assert.Null(spec.RestoreMetadata.ProjectJsonPath);
            }
        }

        [Theory]
        [InlineData("a", "a")]
        [InlineData("", "")]
        [InlineData(" ", "")]
        [InlineData(null, "")]
        [InlineData(";;;;;;", "")]
        [InlineData("\n", "")]
        [InlineData(" ;\n;\t;;  \n ", "")]
        [InlineData("a;b;c", "a|b|c")]
        [InlineData(" a ; b ; c ", "a|b|c")]
        [InlineData("a;c \n ", "a|c")]
        public void MSBuildRestoreUtility_Split(string input, string expected)
        {
            // Arrange && Act
            var parts = MSBuildStringUtility.Split(input);
            var output = string.Join("|", parts);

            // Assert
            Assert.Equal(expected, output);
        }

        [Theory]
        [InlineData("a", "a")]
        [InlineData(" ", null)]
        [InlineData(null, null)]
        [InlineData("\n", null)]
        [InlineData(" a ; b ; c ", "a ; b ; c")]
        [InlineData(" a;c\n ", "a;c")]
        public void MSBuildRestoreUtility_GetProperty_Trim(string input, string expected)
        {
            // Arrange
            var item = new MSBuildItem("a", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "key", input }
            });

            // Act
            var trimmed = item.GetProperty("key");
            var raw = item.GetProperty("key", trim: false);

            // Assert
            Assert.Equal(expected, trimmed);

            // Verify the value was not changed when it was stored
            Assert.Equal(input, raw);
        }

        [Theory]
        [InlineData("a", false)]
        [InlineData("", false)]
        [InlineData("cLear", false)]
        [InlineData("cLear;clear", false)]
        [InlineData("cLear;a", true)]
        [InlineData("a;CLEAR", true)]
        [InlineData("a;CLEAR;CLEAR", true)]
        public void MSBuildRestoreUtility_HasInvalidClear(string input, bool expected)
        {
            Assert.Equal(expected, MSBuildRestoreUtility.HasInvalidClear(MSBuildStringUtility.Split(input)));
        }

        [Theory]
        [InlineData("a", false)]
        [InlineData("", false)]
        [InlineData("c;lear", false)]
        [InlineData("a;b", false)]
        [InlineData("cLear", true)]
        [InlineData("cLear;clear", true)]
        [InlineData("cLear;a", true)]
        [InlineData("a;CLEAR", true)]
        [InlineData("a;CLEAR;CLEAR", true)]
        public void MSBuildRestoreUtility_ContainsClearKeyword(string input, bool expected)
        {
            Assert.Equal(expected, MSBuildRestoreUtility.ContainsClearKeyword(MSBuildStringUtility.Split(input)));
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyProjectWideWarningProperties()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");
                var configFilePath = Path.Combine(project1Root, "nuget.config");

                var items = new List<IDictionary<string, string>>();

                
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "Version", "2.0.0-rc.2+a.b.c" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                    { "ConfigFilePaths", configFilePath },
                    { "TreatWarningsAsErrors", "true" },
                    { "WarningsAsErrors", "NU1001;NU1002" },
                    { "NoWarn", "NU1100;NU1101" },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();
                var props = project1Spec.RestoreMetadata.ProjectWideWarningProperties;

                // Assert
                props.AllWarningsAsErrors.Should().BeTrue();
                props.NoWarn.ShouldBeEquivalentTo(new[] { NuGetLogCode.NU1100, NuGetLogCode.NU1101 });
                props.WarningsAsErrors.ShouldBeEquivalentTo(new[] { NuGetLogCode.NU1001, NuGetLogCode.NU1002 });
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyEmptyProjectWideWarningProperties()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var project1Path = Path.Combine(project1Root, "a.csproj");
                var outputPath1 = Path.Combine(project1Root, "obj");
                var fallbackFolder = Path.Combine(project1Root, "fallback");
                var packagesFolder = Path.Combine(project1Root, "packages");
                var configFilePath = Path.Combine(project1Root, "nuget.config");

                var items = new List<IDictionary<string, string>>();

                
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "Version", "2.0.0-rc.2+a.b.c" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", "482C20DE-DFF9-4BD0-B90A-BD3201AA351A" },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                    { "ConfigFilePaths", configFilePath },
                    { "TreatWarningsAsErrors", "" },
                    { "WarningsAsErrors", "" },
                    { "NoWarn", "" },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();
                var props = project1Spec.RestoreMetadata.ProjectWideWarningProperties;

                // Assert
                props.AllWarningsAsErrors.Should().BeFalse();
                props.NoWarn.Should().BeEmpty();
                props.WarningsAsErrors.Should().BeEmpty();
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyPackageWarningProperties()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var uniqueName = "482C20DE-DFF9-4BD0-B90A-BD3201AA351A";

                var items = new List<IDictionary<string, string>>();

                items.Add(CreateProject(project1Root, uniqueName));

                // Package references
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", uniqueName },
                    { "Id", "x" },
                    { "VersionRange", "1.0.0" },
                    { "TargetFrameworks", "net46" },
                    { "CrossTargeting", "true" },
                    { "NoWarn", "NU1001;NU1002" },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();
                var packageDependency = project1Spec.TargetFrameworks[0].Dependencies[0];

                // Assert
                packageDependency.NoWarn.ShouldBeEquivalentTo(new[] { NuGetLogCode.NU1001, NuGetLogCode.NU1002 });
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyEmptyPackageWarningProperties()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var uniqueName = "482C20DE-DFF9-4BD0-B90A-BD3201AA351A";

                var items = new List<IDictionary<string, string>>();

                items.Add(CreateProject(project1Root, uniqueName));

                // Package references
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", uniqueName },
                    { "Id", "x" },
                    { "VersionRange", "1.0.0" },
                    { "TargetFrameworks", "net46" },
                    { "CrossTargeting", "true" },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();
                var packageDependency = project1Spec.TargetFrameworks[0].Dependencies[0];

                // Assert
                packageDependency.NoWarn.Should().BeEmpty();
            }
        }

        [Fact]
        public void MSBuildRestoreUtility_GetPackageSpec_NetCoreVerifyAutoReferencedTrue()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                var project1Root = Path.Combine(workingDir, "a");
                var uniqueName = "482C20DE-DFF9-4BD0-B90A-BD3201AA351A";

                var items = new List<IDictionary<string, string>>();

                items.Add(CreateProject(project1Root, uniqueName));

                // Package references
                items.Add(new Dictionary<string, string>()
                {
                    { "Type", "Dependency" },
                    { "ProjectUniqueName", uniqueName },
                    { "Id", "x" },
                    { "VersionRange", "1.0.0" },
                    { "TargetFrameworks", "net46" },
                    { "IsImplicitlyDefined", "true" },
                });

                var wrappedItems = items.Select(CreateItems).ToList();

                // Act
                var dgSpec = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);
                var project1Spec = dgSpec.Projects.Single();
                var packageDependency = project1Spec.TargetFrameworks[0].Dependencies[0];

                // Assert
                packageDependency.AutoReferenced.Should().BeTrue();
            }
        }

        [Theory]
        [InlineData("NU1607")]
        [InlineData(";NU1607")]
        [InlineData(";NU1607;")]
        [InlineData("nu1607")]
        [InlineData("$(AnotherProperty);NU1607;")]
        [InlineData("$(AnotherProperty);nu1607;")]
        [InlineData("NU1607;1607")]
        [InlineData("NU1607;random;values;are;here")]
        [InlineData("NU1607;CSC1607")]
        [InlineData("NU1607;MSB1607")]
        [InlineData("NU1607;1607;600")]
        [InlineData("NU1607;1607;0;-1;abc123")]
        [InlineData(",NU1607")]
        [InlineData(",NU1607,")]
        [InlineData("nu1607")]
        [InlineData("$(AnotherProperty),NU1607,")]
        [InlineData("$(AnotherProperty),nu1607,")]
        [InlineData("NU1607,1607")]
        [InlineData("NU1607,random,values,are,here")]
        [InlineData("NU1607,CSC1607")]
        [InlineData("NU1607,MSB1607")]
        [InlineData("NU1607,1607,600")]
        [InlineData("NU1607,1607,0,-1,abc123")]
        [InlineData(", NU1607   ,")]
        [InlineData(" NU1607   ")]
        [InlineData("$(AnotherProperty), NU1607   ,")]
        [InlineData("$(AnotherProperty), NU1607   ,")]
        [InlineData(" NU1607   ,1607")]
        [InlineData(" NU1607   ,random,values,are,here")]
        [InlineData(" NU1607   ,CSC1607")]
        [InlineData(" NU1607   ,MSB1607")]
        [InlineData(" NU1607   ,1607,600")]
        [InlineData(" NU1607   ,1607,0,-1,abc123")]
        [InlineData("; NU1607   ;")]
        [InlineData(" NU1607   ")]
        [InlineData("$(AnotherProperty); NU1607   ;")]
        [InlineData("$(AnotherProperty); NU1607   ;")]
        [InlineData(" NU1607   ;1607")]
        [InlineData(" NU1607   ;random;values;are;here")]
        [InlineData(" NU1607   ;CSC1607")]
        [InlineData(" NU1607   ;MSB1607")]
        [InlineData(" NU1607   ;1607;600")]
        [InlineData(" NU1607   ;1607;0;-1;abc123")]
        [InlineData(" NU1607   ,1607;0;-1,abc123")]
        [InlineData(" NU1607  \t ;1607;0;-1;abc123")]
        [InlineData(" NU1607  \t\r\n ,\t1607;0;-1,abc123")]
        public void MSBuildRestoreUtility_GetNuGetLogCodes_ParsesPropertyWithOneCode(string property)
        {
            // Arrange && Act
            var codes = MSBuildRestoreUtility.GetNuGetLogCodes(property);

            // Assert
            codes.Should().NotBeNull();
            codes.ShouldBeEquivalentTo(new[] { NuGetLogCode.NU1607 });
        }

        [Theory]
        [InlineData("NU1607;NU1701")]
        [InlineData(";NU1607;NU1701")]
        [InlineData(";NU1607;;NU1701")]
        [InlineData("nu1607;nu1701")]
        [InlineData(";NU1701;$(AnotherProperty);NU1607;")]
        [InlineData("$(AnotherProperty);NU1701;nu1607;")]
        [InlineData("NU1607;1607;NU1701")]
        [InlineData("NU1607;random;values;are;here;NU1701")]
        [InlineData("NU1607;CSC1607;NU1701")]
        [InlineData("NU1607;MSB1607;NU1701")]
        [InlineData("NU1607;1607;600;NU1701")]
        [InlineData("NU1607;1607;0;-1;abc123;NU1701")]
        [InlineData("NU1607,NU1701")]
        [InlineData(",NU1607,NU1701")]
        [InlineData(",NU1607,,NU1701")]
        [InlineData("nu1607,nu1701")]
        [InlineData(",NU1701,$(AnotherProperty),NU1607,")]
        [InlineData("$(AnotherProperty),NU1701,nu1607,")]
        [InlineData("NU1607,1607,NU1701")]
        [InlineData("NU1607,random,values,are,here,NU1701")]
        [InlineData("NU1607,CSC1607,NU1701")]
        [InlineData("NU1607,MSB1607,NU1701")]
        [InlineData("NU1607,1607,600,NU1701")]
        [InlineData("NU1607,1607,0,-1,abc123,NU1701")]
        [InlineData("         NU1607     	,NU1701")]
        [InlineData(",         NU1607     	,NU1701")]
        [InlineData(",         NU1607     	,,NU1701")]
        [InlineData("         NU1607     	,nu1701")]
        [InlineData(",NU1701,$(AnotherProperty),         NU1607     	,")]
        [InlineData("$(AnotherProperty),NU1701,         NU1607     	,")]
        [InlineData("         NU1607     	,1607,NU1701")]
        [InlineData("         NU1607   \t  	,random,values,are,here,NU1701")]
        [InlineData("         NU1607     	,CSC1607,NU1701")]
        [InlineData("         NU1607     	,MSB1607,NU1701")]
        [InlineData("         NU1607    \t 	,1607,600,NU1701")]
        [InlineData("         NU1607    \t 	,1607,0,-1,abc123,NU1701")]
        [InlineData("         NU1607    \n\t 	,1607,0,-1,abc123,NU1701")]
        [InlineData("         NU1607    \n\t\r 	,1607,0,-1,abc123,NU1701")]
        [InlineData("         NU1607    \n\t\r 	,1607,0,-1;abc123,NU1701")]
        public void MSBuildRestoreUtility_GetNuGetLogCodes_ParsesPropertyWithMultipleCodes(string property)
        {
            // Arrange && Act
            var codes = MSBuildRestoreUtility.GetNuGetLogCodes(property);

            // Assert
            codes.Should().NotBeNull();
            codes.ShouldBeEquivalentTo(new[] { NuGetLogCode.NU1607, NuGetLogCode.NU1701 });
        }

        [Theory]
        [InlineData("NU9999")]
        [InlineData("NU 1607")]
        [InlineData("NU1 607")]
        [InlineData("NU1607a")]
        [InlineData("1607")]
        [InlineData("random;values;are;here")]
        [InlineData("CSC1607")]
        [InlineData("MSB1607")]
        [InlineData("1607;600")]
        [InlineData("1607;0;-1;abc123")]
        [InlineData("$(NoWarn);0;-1;abc123")]
        [InlineData("")]
        public void MSBuildRestoreUtility_GetNuGetLogCodes_DoesNotParseInvalidCodes(string property)
        {
            // Arrange && Act
            var codes = MSBuildRestoreUtility.GetNuGetLogCodes(property);

            // Assert
            codes.Should().NotBeNull();
            codes.Should().BeEmpty();
        }

        [Theory]
        [InlineData("/tmp/test/", "/tmp/test/")]
        [InlineData("/tmp/test", "/tmp/test")]
        [InlineData("tmp/test", "tmp/test")]
        [InlineData("tmp", "tmp")]
        [InlineData("http:", "http:")]
        [InlineData("https:", "https:")]
        [InlineData("file:", "file:")]
        [InlineData("http:/", "http:/")]
        [InlineData("https:/", "https:/")]
        [InlineData("file:/", "file:/")]
        [InlineData("http://", "http://")]
        [InlineData("https://", "https://")]
        [InlineData("file://", "file://")]
        [InlineData("http://a", "http://a")]
        [InlineData("https://a", "https://a")]
        [InlineData("http:/a", "http://a")]
        [InlineData("https:/a", "https://a")]
        [InlineData("HTtP:/a", "HTtP://a")]
        [InlineData("HTTPs:/a", "HTTPs://a")]
        [InlineData("http:///", "http:///")]
        [InlineData("https:///", "https:///")]
        [InlineData("file:///", "file:///")]
        [InlineData("HTTPS:/api.NUGET.org/v3/index.json", "HTTPS://api.NUGET.org/v3/index.json")]
        [InlineData(@"C:\source\", @"C:\source\")]
        [InlineData(@"\\share\", @"\\share\")]
        public void MSBuildRestoreUtility_FixSourcePath(string input, string expected)
        {
            MSBuildRestoreUtility.FixSourcePath(input).Should().Be(expected);
        }

        [PlatformFact(Platform.Windows)]
        public void MSBuildRestoreUtility_FixSourcePath_VerifyDoubleSlashWindows()
        {
            var input = "file:/C:\tmp";

            MSBuildRestoreUtility.FixSourcePath(input).Should().Be("file://C:\tmp");
        }

        [PlatformFact(Platform.Linux, Platform.Darwin)]
        public void MSBuildRestoreUtility_FixSourcePath_VerifyTripleSlashOnNonWindows()
        {
            var input = "file:/tmp/test/";

            MSBuildRestoreUtility.FixSourcePath(input).Should().Be("file:///tmp/test/");
        }

        private static IDictionary<string, string> CreateProject(string root, string uniqueName)
        {
            var project1Path = Path.Combine(root, "a.csproj");
            var outputPath1 = Path.Combine(root, "obj");
            var fallbackFolder = Path.Combine(root, "fallback");
            var packagesFolder = Path.Combine(root, "packages");
            var configFilePath = Path.Combine(root, "nuget.config");

            return new Dictionary<string, string>()
                {
                    { "Type", "ProjectSpec" },
                    { "Version", "2.0.0-rc.2+a.b.c" },
                    { "ProjectName", "a" },
                    { "ProjectStyle", "PackageReference" },
                    { "OutputPath", outputPath1 },
                    { "ProjectUniqueName", uniqueName },
                    { "ProjectPath", project1Path },
                    { "TargetFrameworks", "net46" },
                    { "Sources", "https://nuget.org/a/index.json;https://nuget.org/b/index.json" },
                    { "FallbackFolders", fallbackFolder },
                    { "PackagesPath", packagesFolder },
                    { "ConfigFilePaths", configFilePath },
                };
        }

        private IMSBuildItem CreateItems(IDictionary<string, string> properties)
        {
            return new MSBuildItem(Guid.NewGuid().ToString(), properties);
        }
    }
}