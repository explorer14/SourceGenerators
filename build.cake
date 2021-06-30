#addin nuget:?package=Cake.SemVer&version=4.0.0
#addin nuget:?package=semver&version=2.0.4
#addin nuget:?package=Cake.FileHelpers&version=3.3.0

var target = Argument("target", "Build");
var configuration = Argument("configuration", "Release");
var artifactDirPath = "./artifacts/";
var packagePublishDirPath = "./publish/";

Setup(ctx=>
{
	SetUpNuget();
});

void SetUpNuget()
{
	var feed = new
	{
		Name = "SkynetNuget",
	    Source = "https://skynetcode.pkgs.visualstudio.com/_packaging/skynetpackagefeed/nuget/v3/index.json"
	};

	if (!NuGetHasSource(source:feed.Source))
	{
	    var nugetSourceSettings = new NuGetSourcesSettings
                             {
                                 UserName = "skynetcode",
                                 Password = EnvironmentVariable("SYSTEM_ACCESSTOKEN"),
                                 Verbosity = NuGetVerbosity.Detailed
                             };		

		NuGetAddSource(
		    name:feed.Name,
		    source:feed.Source,
		    settings:nugetSourceSettings);
	}	
}

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Build")    
    .Does(() =>
{
    DotNetCoreBuild("./src/DtoGenerators/DtoGenerators.csproj", new DotNetCoreBuildSettings
    {
        Configuration = configuration,
    });
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    // DotNetCoreTest("./SourceGenerators.sln", new DotNetCoreTestSettings
    // {
    //     Configuration = configuration,
    //     NoBuild = true,
    // });
});

Task("PushToNuget")
	.IsDependentOn("Pack")
	.Does(()=>
{
    PushToNugetFeed(
        "https://skynetcode.pkgs.visualstudio.com/_packaging/skynetpackagefeed/nuget/v3/index.json", 
        "gibberish");
});

void PushToNugetFeed(string source, string apiKey)
{
    var files = GetFiles($"{artifactDirPath}DtoGenerators.*.nupkg");

    foreach(var file in files)
    {
        Information(file.FullPath);
        var settings = new DotNetCoreNuGetPushSettings
        {
            Source = source,
            ApiKey = apiKey,
            SkipDuplicate = true
        };

        DotNetCoreNuGetPush(file.FullPath, settings);
    }
}

Task("Pack")
	.IsDependentOn("Test")
	.Does(()=>{
		var settings = new DotNetCorePackSettings
		{
		    Configuration = "Release",
		    OutputDirectory = artifactDirPath,
			NoBuild = true,
			NoRestore = true
		};

		DotNetCorePack("./src/DtoGenerators/DtoGenerators.csproj", 
            settings);
});

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);