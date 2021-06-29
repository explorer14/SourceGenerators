var target = Argument("target", "Test");
var configuration = Argument("configuration", "Release");

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
    DotNetCoreBuild("./<your-solution-name>.sln", new DotNetCoreBuildSettings
    {
        Configuration = configuration,
    });
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    DotNetCoreTest("./your-solution-name.sln", new DotNetCoreTestSettings
    {
        Configuration = configuration,
        NoBuild = true,
    });
});

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);