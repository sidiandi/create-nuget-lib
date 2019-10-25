using System;
using System.Threading.Tasks;
using System.ComponentModel;
using Amg.GetOpt;
using Amg.Build;
using Amg.FileSystem;
using System.Collections.Generic;
using System.Reflection;
using Amg.Extensions;
using System.Linq;

namespace gt
{
    public class Program
    {
        private static readonly Serilog.ILogger Logger = Serilog.Log.Logger.ForContext(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);
	
	    static int Main(string[] args) => Runner.Run(args);

        void Fail(string reason)
        {
            throw new InvalidOperationException(reason);
        }

        [Once]
        public virtual string SolutionDir { get; set; }

        [Once]
        public virtual string Name { get; set; }

        [Once] protected virtual string classlibDir => SolutionDir.Combine(Name).EnsureDirectoryExists();
        [Once] protected virtual string unitTestDir => SolutionDir.Combine(testAssemblyName).EnsureDirectoryExists();

        [Once] protected virtual string testAssemblyName => Name + ".Tests";
        [Once] protected virtual string Namespace => Name;
        [Once] protected virtual string TestNamespace => Name + ".Tests";

        [Once] protected virtual string classlibCsproj => classlibDir.Combine(classlibDir.FileName() + ".csproj");
        [Once] protected virtual string unitTestCsproj => unitTestDir.Combine(unitTestDir.FileName() + ".csproj");

        [Once] protected virtual string slnFile => SolutionDir.Combine(Name + ".sln");


        [Once, Description("Create new nuget library")]
	    public virtual async Task<IEnumerable<string>> New(string name)
	    {
            Name = name;
            SolutionDir = System.Environment.CurrentDirectory.Combine(name);

            if (SolutionDir.Exists())
            {
                Fail($"{SolutionDir} already exists");
            }

            SolutionDir.EnsureDirectoryExists();

            var dotnet = Tools.Default.WithFileName("dotnet.exe")
                .WithWorkingDirectory(SolutionDir);

            var git = Tools.Default.WithFileName("git.exe")
                .WithWorkingDirectory(SolutionDir);

            await Task.WhenAll(
                dotnet.Run("new", "sln", "--name", name),
                dotnet.Run("new", "classlib", "--name", name, "--output", classlibDir),
                dotnet.Run("new", "nunit", "--name", name + ".Tests", "--output", unitTestDir),
                git.Run("init"));

            // delete auto-generated class1 file
            classlibDir.Combine("Class1.cs").EnsureFileNotExists();

            // delete auto-generated unit test source file
            unitTestDir.Combine("UnitTest1.cs").EnsureFileNotExists();

            await dotnet.Run("sln", slnFile, "add", classlibCsproj);
            await dotnet.Run("sln", slnFile, "add", unitTestCsproj);

            await DefaultClass();
            await CsprojGitignore(classlibDir);
            await CsprojGitignore(unitTestDir);
            await Gitversion();
            await InternalsVisibleToTest();
            await MitLicense();
            await Readme();
            await CreateUnitTestFiles();

            await dotnet.Run("pack", slnFile);
            var testResult = await dotnet.DoNotCheckExitCode().Run("test", slnFile);
            if (testResult.ExitCode != 1)
            {
                throw new InvalidOperationException("unexpected test result");
            }

            await git.Run("add", ".");
            await git.Run("commit", "-a", "-m", "autogenerated nuget library skeleton");

            return SolutionDir.Glob("**/*").EnumerateFiles();
        }

        [Once, Description("Create a x.Tests.cs file for every library source file x.cs")]
        public virtual async Task<IEnumerable<string>> CreateUnitTestFiles()
        {
            FindSolution();
            await Task.CompletedTask;
            var csFiles = classlibDir.Glob("**/*.cs");
            return csFiles.Select(_ => ProvideUnitTestFile(_).Result).ToList();
        }

        [Once]
        protected virtual async Task<string> ProvideUnitTestFile(string csFile)
        {
            var solutionDirRelativeCsFile = csFile.RelativeTo(SolutionDir);
            var p = solutionDirRelativeCsFile.SplitDirectories();
            p[0] = p[0] + ".Tests";
            var fn = p[p.Length - 1];
            p[p.Length - 1] = fn.FileNameWithoutExtension() + ".Tests" + fn.Extension();
            var unitTestFile = SolutionDir.Combine(p);
            if (!unitTestFile.IsFile())
            {
                var testClassName = csFile.FileNameWithoutExtension();
                var testNamespace = Namespace;
                await unitTestFile.WriteAllTextAsync(
$@"using NUnit.Framework;

namespace {testNamespace}
{{
    /// <summary>
    /// Tests for {solutionDirRelativeCsFile}
    /// </summary>
    [TestFixture]
    public class {testClassName}
    {{
        [Test]
        public void Test()
        {{
            Assert.Fail();
		}}
    }}
}}
");
            }
            return unitTestFile;
        }

        [Once]
        protected virtual void FindSolution()
        {
        }

        [Once]
        protected virtual async Task<string> CsprojGitignore(string csprojDir)
        {
            return await csprojDir.Combine(".gitignore")
                .WriteAllTextAsync($@"/obj
/bin
/.vs
");
        }

        [Once]
        protected virtual async Task<string> Gitversion()
        {
            return await SolutionDir.Combine("GitVersion.yml")
                .WriteAllTextAsync($@"mode: ContinuousDeployment");
        }

        [Once]
        protected virtual async Task<string> DefaultClass()
        {
            var defaultClassName = Identifier.CsharpClassName(Name);
            return await classlibDir.Combine(defaultClassName + ".cs")
                .WriteAllTextAsync($@"

namespace {Namespace}
{{
    /// <summary>
    /// {defaultClassName}
    /// </summary>
    public class {defaultClassName}
    {{
    }}
}}
");
        }

        [Once]
        protected virtual async Task<string> InternalsVisibleToTest()
        {
            var csFile = classlibDir.Combine("InternalsVisibleToTest.cs");
            return await csFile
                .WriteAllTextAsync(
$@"[assembly: System.Runtime.CompilerServices.InternalsVisibleTo({testAssemblyName.Quote()})]"
                );
        }

        [Once]
        protected virtual async Task<string> MitLicense()
        {
            return await SolutionDir.Combine("LICENSE")
                .WriteAllTextAsync(
$@"The MIT License (MIT)

Copyright (c) Andreas M. Grimme and Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the ""Software""), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE."
                );
        }

        [Once]
        protected virtual async Task<string> Readme()
        {
            return await SolutionDir.Combine("Readme.md")
                .WriteAllTextAsync(
$@"# {Name}

{Name} library.

Usage:
```
dotnet add package {Name}
```
"
                );
        }
    }
}
