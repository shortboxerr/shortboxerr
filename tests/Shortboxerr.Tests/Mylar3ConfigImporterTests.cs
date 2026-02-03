using Microsoft.EntityFrameworkCore;
using Shortboxerr.Core.Ddl;
using Shortboxerr.Infrastructure.Ddl;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Tests;

public class Mylar3ConfigImporterTests : IDisposable
{
    private readonly ShortboxerrDbContext _context;
    private readonly Mylar3ConfigImporter _importer;

    public Mylar3ConfigImporterTests()
    {
        var options = new DbContextOptionsBuilder<ShortboxerrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ShortboxerrDbContext(options);
        _importer = new Mylar3ConfigImporter(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ParseConfig_WithEmptyContent_ReturnsSuccessWithNoProviders()
    {
        var result = _importer.ParseConfig("");

        Assert.True(result.Success);
        Assert.Empty(result.DdlProviders);
    }

    [Fact]
    public void ParseConfig_WithGeneralSection_ExtractsGeneralSettings()
    {
        var config = """
            [General]
            comic_location = /data/comics
            auto_grab = true
            preferred_format = cbz
            staging_folder = /data/staging
            """;

        var result = _importer.ParseConfig(config);

        Assert.True(result.Success);
        Assert.NotNull(result.GeneralSettings);
        Assert.Equal("/data/comics", result.GeneralSettings.ComicLocation);
        Assert.True(result.GeneralSettings.AutoGrab);
        Assert.Equal("cbz", result.GeneralSettings.PreferredFormat);
        Assert.Equal("/data/staging", result.GeneralSettings.StagingFolder);
    }

    [Fact]
    public void ParseConfig_WithDdlSection_ExtractsProvider()
    {
        var config = """
            [DDL-1]
            name = My Getty Provider
            site_type = GettyComics
            url = https://gettycomics.example.com
            enabled = true
            rate_limit = 5
            timeout = 45
            max_retries = 5
            """;

        var result = _importer.ParseConfig(config);

        Assert.True(result.Success);
        Assert.Single(result.DdlProviders);
        
        var provider = result.DdlProviders[0];
        Assert.Equal("My Getty Provider", provider.Name);
        Assert.Equal("GettyComics", provider.SiteType);
        Assert.Equal("https://gettycomics.example.com", provider.BaseUrl);
        Assert.True(provider.IsEnabled);
        Assert.NotNull(provider.Settings);
        Assert.Equal(5, provider.Settings.RateLimitPerMinute);
        Assert.Equal(45, provider.Settings.TimeoutSeconds);
        Assert.Equal(5, provider.Settings.MaxRetries);
    }

    [Fact]
    public void ParseConfig_WithMultipleDdlSections_ExtractsAllProviders()
    {
        var config = """
            [DDL-1]
            name = Provider One
            site_type = GettyComics
            enabled = true
            
            [DDL-2]
            name = Provider Two
            site_type = ReadComicOnline
            enabled = false
            
            [GetComics]
            name = GetComics Provider
            enabled = true
            """;

        var result = _importer.ParseConfig(config);

        Assert.True(result.Success);
        Assert.Equal(3, result.DdlProviders.Count);
        Assert.Contains(result.DdlProviders, p => p.Name == "Provider One");
        Assert.Contains(result.DdlProviders, p => p.Name == "Provider Two");
        Assert.Contains(result.DdlProviders, p => p.Name == "GetComics Provider");
    }

    [Fact]
    public void ParseConfig_WithCredentials_ExtractsCredentials()
    {
        var config = """
            [DDL-1]
            name = Authenticated Provider
            site_type = GettyComics
            username = testuser
            password = testpass
            api_key = testapikey
            """;

        var result = _importer.ParseConfig(config);

        Assert.True(result.Success);
        Assert.Single(result.DdlProviders);
        
        var provider = result.DdlProviders[0];
        Assert.Equal("testuser", provider.Username);
        Assert.Equal("testpass", provider.Password);
        Assert.Equal("testapikey", provider.ApiKey);
    }

    [Fact]
    public void ParseConfig_WithUnmappedSettings_TracksUnmapped()
    {
        var config = """
            [DDL-1]
            name = Test Provider
            site_type = GettyComics
            custom_setting = custom_value
            another_unknown = value2
            """;

        var result = _importer.ParseConfig(config);

        Assert.True(result.Success);
        Assert.Contains("DDL-1", result.UnmappedSettings.Keys);
        Assert.Contains("custom_setting", result.UnmappedSettings["DDL-1"]);
        Assert.Contains("another_unknown", result.UnmappedSettings["DDL-1"]);
    }

    [Fact]
    public void ParseConfig_WithUnknownSection_TracksUnmapped()
    {
        var config = """
            [SomeRandomSection]
            setting = value
            """;

        var result = _importer.ParseConfig(config);

        Assert.True(result.Success);
        Assert.Contains("SomeRandomSection", result.UnmappedSections);
    }

    [Fact]
    public void ParseConfig_InfersSiteTypeFromSectionName()
    {
        var config = """
            [GettyComics]
            name = Getty Provider
            enabled = true
            """;

        var result = _importer.ParseConfig(config);

        Assert.True(result.Success);
        Assert.Single(result.DdlProviders);
        Assert.Equal("GettyComics", result.DdlProviders[0].SiteType);
    }

    [Fact]
    public void ParseConfig_InfersSiteTypeFromUrl()
    {
        var config = """
            [DDL-1]
            name = Inferred Provider
            url = https://gettycomics.example.com/page
            enabled = true
            """;

        var result = _importer.ParseConfig(config);

        Assert.True(result.Success);
        Assert.Single(result.DdlProviders);
        Assert.Equal("GettyComics", result.DdlProviders[0].SiteType);
    }

    [Fact]
    public void ParseConfig_WithComments_IgnoresComments()
    {
        var config = """
            # This is a comment
            ; This is also a comment
            [DDL-1]
            name = Test Provider
            # inline comment above
            site_type = GettyComics
            """;

        var result = _importer.ParseConfig(config);

        Assert.True(result.Success);
        Assert.Single(result.DdlProviders);
    }

    [Fact]
    public void ParseConfig_WithDifferentBooleanFormats_ParsesCorrectly()
    {
        var config = """
            [DDL-1]
            name = Provider One
            site_type = GettyComics
            enabled = true
            
            [DDL-2]
            name = Provider Two
            site_type = GettyComics
            enabled = 1
            
            [DDL-3]
            name = Provider Three
            site_type = GettyComics
            enabled = yes
            
            [DDL-4]
            name = Provider Four
            site_type = GettyComics
            enabled = false
            """;

        var result = _importer.ParseConfig(config);

        Assert.True(result.Success);
        Assert.Equal(4, result.DdlProviders.Count);
        
        Assert.True(result.DdlProviders.First(p => p.Name == "Provider One").IsEnabled);
        Assert.True(result.DdlProviders.First(p => p.Name == "Provider Two").IsEnabled);
        Assert.True(result.DdlProviders.First(p => p.Name == "Provider Three").IsEnabled);
        Assert.False(result.DdlProviders.First(p => p.Name == "Provider Four").IsEnabled);
    }

    [Fact]
    public async Task ValidateImport_WithNoExistingProviders_ReturnsValid()
    {
        var config = """
            [DDL-1]
            name = New Provider
            site_type = GettyComics
            """;

        var parseResult = _importer.ParseConfig(config);
        var validation = await _importer.ValidateImportAsync(parseResult);

        Assert.True(validation.IsValid);
        Assert.Empty(validation.Errors);
        Assert.Contains("New Provider", validation.ProvidersToCreate);
    }

    [Fact]
    public async Task ExecuteImport_CreatesProviders()
    {
        var config = """
            [DDL-1]
            name = Created Provider
            site_type = GettyComics
            url = https://example.com
            enabled = true
            rate_limit = 15
            """;

        var parseResult = _importer.ParseConfig(config);
        var options = new Mylar3ImportOptions();
        var result = await _importer.ExecuteImportAsync(parseResult, options);

        Assert.True(result.Success);
        Assert.Equal(1, result.ProvidersCreated);
        Assert.Single(result.CreatedProviderIds);
        
        var provider = await _context.Providers.FirstOrDefaultAsync();
        Assert.NotNull(provider);
        Assert.Equal("Created Provider", provider.Name);
        Assert.Equal("https://example.com", provider.BaseUrl);
    }

    [Fact]
    public async Task ExecuteImport_WithNamePrefix_AddsPrefixToNames()
    {
        var config = """
            [DDL-1]
            name = Provider
            site_type = GettyComics
            """;

        var parseResult = _importer.ParseConfig(config);
        var options = new Mylar3ImportOptions { NamePrefix = "[Mylar3] " };
        var result = await _importer.ExecuteImportAsync(parseResult, options);

        Assert.True(result.Success);
        
        var provider = await _context.Providers.FirstOrDefaultAsync();
        Assert.NotNull(provider);
        Assert.Equal("[Mylar3] Provider", provider.Name);
    }

    [Fact]
    public async Task ExecuteImport_WithoutImportCredentials_OmitsCredentials()
    {
        var config = """
            [DDL-1]
            name = Secure Provider
            site_type = GettyComics
            username = testuser
            password = secretpass
            api_key = secretkey
            """;

        var parseResult = _importer.ParseConfig(config);
        var options = new Mylar3ImportOptions { ImportCredentials = false };
        var result = await _importer.ExecuteImportAsync(parseResult, options);

        Assert.True(result.Success);
        
        var provider = await _context.Providers.FirstOrDefaultAsync();
        Assert.NotNull(provider);
        Assert.Null(provider.Username);
        Assert.Null(provider.Password);
        Assert.Null(provider.ApiKey);
    }

    [Fact]
    public async Task ExecuteImport_WithExistingProvider_SkipsByDefault()
    {
        // Create existing provider
        _context.Providers.Add(new Core.Entities.ProviderDefinition
        {
            Name = "Existing Provider",
            Implementation = "DdlProvider",
            Category = Core.Entities.ProviderCategory.Indexer,
            Type = Core.Providers.ProviderType.Ddl
        });
        await _context.SaveChangesAsync();

        var config = """
            [DDL-1]
            name = Existing Provider
            site_type = GettyComics
            """;

        var parseResult = _importer.ParseConfig(config);
        var options = new Mylar3ImportOptions { OverwriteExisting = false };
        var result = await _importer.ExecuteImportAsync(parseResult, options);

        Assert.True(result.Success);
        Assert.Equal(0, result.ProvidersCreated);
        Assert.Equal(1, result.ProvidersSkipped);
    }

    [Fact]
    public async Task ExecuteImport_WithOverwriteExisting_UpdatesProvider()
    {
        // Create existing provider
        _context.Providers.Add(new Core.Entities.ProviderDefinition
        {
            Name = "Existing Provider",
            Implementation = "DdlProvider",
            Category = Core.Entities.ProviderCategory.Indexer,
            Type = Core.Providers.ProviderType.Ddl,
            BaseUrl = "https://old.example.com"
        });
        await _context.SaveChangesAsync();

        var config = """
            [DDL-1]
            name = Existing Provider
            site_type = GettyComics
            url = https://new.example.com
            """;

        var parseResult = _importer.ParseConfig(config);
        var options = new Mylar3ImportOptions { OverwriteExisting = true };
        var result = await _importer.ExecuteImportAsync(parseResult, options);

        Assert.True(result.Success);
        Assert.Equal(0, result.ProvidersCreated);
        Assert.Equal(1, result.ProvidersUpdated);
        
        var provider = await _context.Providers.FirstOrDefaultAsync();
        Assert.NotNull(provider);
        Assert.Equal("https://new.example.com", provider.BaseUrl);
    }

    [Fact]
    public void DdlProviderSettings_CreateMylar3Default_ReturnsCorrectDefaults()
    {
        var gettySettings = DdlProviderSettings.CreateMylar3Default("GettyComics");
        var rcoSettings = DdlProviderSettings.CreateMylar3Default("ReadComicOnline");

        Assert.Equal("GettyComics", gettySettings.SiteType);
        Assert.Equal(10, gettySettings.RateLimitPerMinute);
        Assert.Equal(30, gettySettings.TimeoutSeconds);
        Assert.Equal(3, gettySettings.MaxRetries);
        
        Assert.Equal("ReadComicOnline", rcoSettings.SiteType);
        Assert.Equal(5, rcoSettings.RateLimitPerMinute); // More restrictive
        Assert.Equal(45, rcoSettings.TimeoutSeconds);
    }

    [Fact]
    public void DdlProviderSettings_ToJsonAndFromJson_RoundTrips()
    {
        var settings = new DdlProviderSettings
        {
            SiteType = "TestSite",
            RateLimitPerMinute = 20,
            TimeoutSeconds = 60,
            MaxRetries = 5,
            UserAgent = "Custom/1.0",
            AutoGrabEnabled = false,
            BannedWords = new List<string> { "test", "banned" }
        };

        var json = settings.ToJson();
        var restored = DdlProviderSettings.FromJson(json);

        Assert.NotNull(restored);
        Assert.Equal("TestSite", restored.SiteType);
        Assert.Equal(20, restored.RateLimitPerMinute);
        Assert.Equal(60, restored.TimeoutSeconds);
        Assert.Equal(5, restored.MaxRetries);
        Assert.Equal("Custom/1.0", restored.UserAgent);
        Assert.False(restored.AutoGrabEnabled);
        Assert.Equal(2, restored.BannedWords.Count);
    }
}



