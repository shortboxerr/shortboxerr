using Shortboxerr.Core.Import;
using Shortboxerr.Infrastructure.Import;
using Xunit;

namespace Shortboxerr.Tests;

public class Mylar3ConfigImporterTests
{
    private readonly Mylar3ConfigImporter _importer = new();

    #region ParseConfigContentAsync Tests

    [Fact]
    public async Task ParseConfigContentAsync_EmptyContent_ReturnsFailed()
    {
        var result = await _importer.ParseConfigContentAsync("");

        Assert.False(result.Success);
        Assert.Contains("empty", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParseConfigContentAsync_WhitespaceContent_ReturnsFailed()
    {
        var result = await _importer.ParseConfigContentAsync("   \n\t  ");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ParseConfigContentAsync_ValidIni_ReturnsSuccess()
    {
        var content = @"
[General]
comic_location = /comics
enable_nzb = true

[SABnzbd]
sab_host = localhost
sab_port = 8080
sab_apikey = abc123
use_sabnzbd = true
";

        var result = await _importer.ParseConfigContentAsync(content);

        Assert.True(result.Success);
        Assert.Contains("General", result.SectionsFound);
        Assert.Contains("SABnzbd", result.SectionsFound);
    }

    [Fact]
    public async Task ParseConfigContentAsync_ParsesComments()
    {
        var content = @"
# This is a comment
[General]
; This is also a comment
comic_location = /comics
";

        var result = await _importer.ParseConfigContentAsync(content);

        Assert.True(result.Success);
        Assert.NotNull(result.General);
        Assert.Equal("/comics", result.General.ComicLocation);
    }

    #endregion

    #region Indexer Parsing Tests

    [Fact]
    public async Task ParseConfigContentAsync_ParsesSingleNewznab()
    {
        var content = @"
[Newznab]
newznab_name = NZBgeek
newznab_host = https://api.nzbgeek.info
newznab_api = myapikey123
newznab_enabled = true
newznab_categories = 7030,7040
";

        var result = await _importer.ParseConfigContentAsync(content);

        Assert.True(result.Success);
        Assert.Single(result.Indexers);

        var indexer = result.Indexers[0];
        Assert.Equal("NZBgeek", indexer.Name);
        Assert.Equal("https://api.nzbgeek.info", indexer.Host);
        Assert.Equal("myapikey123", indexer.ApiKey);
        Assert.True(indexer.Enabled);
        Assert.Contains("7030", indexer.Categories);
        Assert.Contains("7040", indexer.Categories);
    }

    [Fact]
    public async Task ParseConfigContentAsync_ParsesNumberedNewznab()
    {
        var content = @"
[Newznab1]
name = Indexer1
host = https://indexer1.com
apikey = key1
enabled = true

[Newznab2]
name = Indexer2
host = https://indexer2.com
apikey = key2
enabled = false
";

        var result = await _importer.ParseConfigContentAsync(content);

        Assert.True(result.Success);
        Assert.Equal(2, result.Indexers.Count);

        Assert.Equal("Indexer1", result.Indexers[0].Name);
        Assert.Equal("https://indexer1.com", result.Indexers[0].Host);
        Assert.True(result.Indexers[0].Enabled);

        Assert.Equal("Indexer2", result.Indexers[1].Name);
        Assert.Equal("https://indexer2.com", result.Indexers[1].Host);
        Assert.False(result.Indexers[1].Enabled);
    }

    [Fact]
    public async Task ParseConfigContentAsync_ParsesExtraNewznabs()
    {
        var content = @"
[General]
extra_newznabs = [('NZBgeek', 'https://api.nzbgeek.info', True, 'apikey1', '', True, '7030')]
";

        var result = await _importer.ParseConfigContentAsync(content);

        Assert.True(result.Success);
        Assert.Single(result.Indexers);
        Assert.Equal("NZBgeek", result.Indexers[0].Name);
        Assert.Equal("https://api.nzbgeek.info", result.Indexers[0].Host);
    }

    #endregion

    #region SABnzbd Parsing Tests

    [Fact]
    public async Task ParseConfigContentAsync_ParsesSabnzbd()
    {
        var content = @"
[SABnzbd]
sab_host = 192.168.1.100
sab_port = 8080
sab_apikey = sabapikey123
sab_category = comics
sab_ssl = true
use_sabnzbd = true
sab_priority = high
";

        var result = await _importer.ParseConfigContentAsync(content);

        Assert.True(result.Success);
        Assert.NotNull(result.Sabnzbd);
        Assert.Equal("192.168.1.100", result.Sabnzbd.Host);
        Assert.Equal(8080, result.Sabnzbd.Port);
        Assert.Equal("sabapikey123", result.Sabnzbd.ApiKey);
        Assert.Equal("comics", result.Sabnzbd.Category);
        Assert.True(result.Sabnzbd.UseSsl);
        Assert.True(result.Sabnzbd.Enabled);
        Assert.Equal("high", result.Sabnzbd.Priority);
    }

    [Fact]
    public async Task ParseConfigContentAsync_ParsesSabnzbdFromGeneral()
    {
        var content = @"
[General]
sab_host = myserver
sab_port = 9090
sab_apikey = generalkey
use_sabnzbd = true
";

        var result = await _importer.ParseConfigContentAsync(content);

        Assert.True(result.Success);
        Assert.NotNull(result.Sabnzbd);
        Assert.Equal("myserver", result.Sabnzbd.Host);
        Assert.Equal(9090, result.Sabnzbd.Port);
    }

    [Fact]
    public async Task ParseConfigContentAsync_SabnzbdDefaultPort()
    {
        var content = @"
[SABnzbd]
sab_host = localhost
sab_apikey = key
use_sabnzbd = true
";

        var result = await _importer.ParseConfigContentAsync(content);

        Assert.NotNull(result.Sabnzbd);
        Assert.Equal(8080, result.Sabnzbd.Port); // Default port
    }

    #endregion

    #region NZBGet Parsing Tests

    [Fact]
    public async Task ParseConfigContentAsync_ParsesNzbget()
    {
        var content = @"
[NZBGet]
nzbget_host = 192.168.1.101
nzbget_port = 6789
nzbget_username = nzbget
nzbget_password = tegbzn6789
nzbget_category = comics
nzbget_ssl = false
use_nzbget = true
";

        var result = await _importer.ParseConfigContentAsync(content);

        Assert.True(result.Success);
        Assert.NotNull(result.Nzbget);
        Assert.Equal("192.168.1.101", result.Nzbget.Host);
        Assert.Equal(6789, result.Nzbget.Port);
        Assert.Equal("nzbget", result.Nzbget.Username);
        Assert.Equal("tegbzn6789", result.Nzbget.Password);
        Assert.Equal("comics", result.Nzbget.Category);
        Assert.False(result.Nzbget.UseSsl);
        Assert.True(result.Nzbget.Enabled);
    }

    [Fact]
    public async Task ParseConfigContentAsync_NzbgetDefaultPort()
    {
        var content = @"
[NZBGet]
nzbget_host = localhost
use_nzbget = true
";

        var result = await _importer.ParseConfigContentAsync(content);

        Assert.NotNull(result.Nzbget);
        Assert.Equal(6789, result.Nzbget.Port); // Default port
    }

    #endregion

    #region General Config Tests

    [Fact]
    public async Task ParseConfigContentAsync_ParsesGeneral()
    {
        var content = @"
[General]
comic_location = /media/comics
download_dir = /downloads
nzb_startup_search = true
enable_torrents = false
nzb_downloader = sabnzbd
";

        var result = await _importer.ParseConfigContentAsync(content);

        Assert.True(result.Success);
        Assert.NotNull(result.General);
        Assert.Equal("/media/comics", result.General.ComicLocation);
        Assert.Equal("/downloads", result.General.DownloadDirectory);
        Assert.True(result.General.NzbEnabled);
        Assert.False(result.General.TorrentEnabled);
        Assert.Equal("sabnzbd", result.General.PreferredNzbClient);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task ValidateAsync_ValidConfig_ReturnsValid()
    {
        var parseResult = Mylar3ConfigParseResult.Parsed(
            new List<Mylar3NewznabConfig>
            {
                new()
                {
                    Name = "Test",
                    Host = "https://test.com",
                    ApiKey = "key123",
                    Enabled = true
                }
            },
            new Mylar3SabnzbdConfig
            {
                Host = "localhost",
                ApiKey = "sabkey",
                Enabled = true
            },
            null,
            null,
            new List<string>(),
            new List<string> { "Newznab", "SABnzbd" });

        var report = await _importer.ValidateAsync(parseResult);

        Assert.True(report.IsValid);
        Assert.Empty(report.Errors);
    }

    [Fact]
    public async Task ValidateAsync_MissingApiKey_ReturnsError()
    {
        var parseResult = Mylar3ConfigParseResult.Parsed(
            new List<Mylar3NewznabConfig>
            {
                new()
                {
                    Name = "Test",
                    Host = "https://test.com",
                    ApiKey = "", // Missing
                    Enabled = true
                }
            },
            null,
            null,
            null,
            new List<string>(),
            new List<string>());

        var report = await _importer.ValidateAsync(parseResult);

        Assert.False(report.IsValid);
        Assert.Contains(report.Errors, e => e.Field == "apikey");
    }

    [Fact]
    public async Task ValidateAsync_MissingHost_ReturnsError()
    {
        var parseResult = Mylar3ConfigParseResult.Parsed(
            new List<Mylar3NewznabConfig>
            {
                new()
                {
                    Name = "Test",
                    Host = "", // Missing
                    ApiKey = "key",
                    Enabled = true
                }
            },
            null,
            null,
            null,
            new List<string>(),
            new List<string>());

        var report = await _importer.ValidateAsync(parseResult);

        Assert.False(report.IsValid);
        Assert.Contains(report.Errors, e => e.Field == "host");
    }

    [Fact]
    public async Task ValidateAsync_DisabledIndexer_ReturnsInfo()
    {
        var parseResult = Mylar3ConfigParseResult.Parsed(
            new List<Mylar3NewznabConfig>
            {
                new()
                {
                    Name = "Test",
                    Host = "https://test.com",
                    ApiKey = "key",
                    Enabled = false // Disabled
                }
            },
            null,
            null,
            null,
            new List<string>(),
            new List<string>());

        var report = await _importer.ValidateAsync(parseResult);

        Assert.True(report.IsValid);
        Assert.Contains(report.Info, i => i.Message.Contains("disabled"));
    }

    [Fact]
    public async Task ValidateAsync_Summary_IsCorrect()
    {
        var parseResult = Mylar3ConfigParseResult.Parsed(
            new List<Mylar3NewznabConfig>
            {
                new() { Name = "A", Host = "h", ApiKey = "k", Enabled = true },
                new() { Name = "B", Host = "h", ApiKey = "k", Enabled = true },
                new() { Name = "C", Host = "h", ApiKey = "k", Enabled = false }
            },
            new Mylar3SabnzbdConfig { Host = "h", ApiKey = "k", Enabled = true },
            new Mylar3NzbgetConfig { Host = "h", Enabled = false },
            null,
            new List<string>(),
            new List<string>());

        var report = await _importer.ValidateAsync(parseResult);

        Assert.Equal(3, report.Summary.TotalIndexers);
        Assert.Equal(2, report.Summary.EnabledIndexers);
        Assert.True(report.Summary.HasSabnzbd);
        Assert.True(report.Summary.SabnzbdEnabled);
        Assert.True(report.Summary.HasNzbget);
        Assert.False(report.Summary.NzbgetEnabled);
    }

    #endregion

    #region Import Tests

    [Fact]
    public async Task ImportAsync_FailedParse_ReturnsError()
    {
        var parseResult = Mylar3ConfigParseResult.Failed("Parse error");

        var result = await _importer.ImportAsync(parseResult, new Mylar3ImportOptions());

        Assert.False(result.Success);
        Assert.Contains("parsing failed", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportAsync_ImportsEnabledIndexers()
    {
        var parseResult = Mylar3ConfigParseResult.Parsed(
            new List<Mylar3NewznabConfig>
            {
                new() { Name = "A", Host = "https://a.com", ApiKey = "k1", Enabled = true },
                new() { Name = "B", Host = "https://b.com", ApiKey = "k2", Enabled = false }
            },
            null,
            null,
            null,
            new List<string>(),
            new List<string>());

        var result = await _importer.ImportAsync(parseResult, new Mylar3ImportOptions
        {
            ImportIndexers = true,
            ImportDisabled = false
        });

        Assert.True(result.Success);
        Assert.Equal(1, result.IndexersImported);
        Assert.Equal(1, result.IndexersSkipped);
    }

    [Fact]
    public async Task ImportAsync_ImportsAllIndexersWhenImportDisabled()
    {
        var parseResult = Mylar3ConfigParseResult.Parsed(
            new List<Mylar3NewznabConfig>
            {
                new() { Name = "A", Host = "https://a.com", ApiKey = "k1", Enabled = true },
                new() { Name = "B", Host = "https://b.com", ApiKey = "k2", Enabled = false }
            },
            null,
            null,
            null,
            new List<string>(),
            new List<string>());

        var result = await _importer.ImportAsync(parseResult, new Mylar3ImportOptions
        {
            ImportIndexers = true,
            ImportDisabled = true
        });

        Assert.True(result.Success);
        Assert.Equal(2, result.IndexersImported);
        Assert.Equal(0, result.IndexersSkipped);
    }

    [Fact]
    public async Task ImportAsync_ImportsSabnzbd()
    {
        var parseResult = Mylar3ConfigParseResult.Parsed(
            new List<Mylar3NewznabConfig>(),
            new Mylar3SabnzbdConfig
            {
                Host = "localhost",
                Port = 8080,
                ApiKey = "sabkey",
                Enabled = true
            },
            null,
            null,
            new List<string>(),
            new List<string>());

        var result = await _importer.ImportAsync(parseResult, new Mylar3ImportOptions
        {
            ImportSabnzbd = true
        });

        Assert.True(result.Success);
        Assert.True(result.SabnzbdImported);
    }

    [Fact]
    public async Task ImportAsync_ImportsNzbget()
    {
        var parseResult = Mylar3ConfigParseResult.Parsed(
            new List<Mylar3NewznabConfig>(),
            null,
            new Mylar3NzbgetConfig
            {
                Host = "localhost",
                Port = 6789,
                Enabled = true
            },
            null,
            new List<string>(),
            new List<string>());

        var result = await _importer.ImportAsync(parseResult, new Mylar3ImportOptions
        {
            ImportNzbget = true
        });

        Assert.True(result.Success);
        Assert.True(result.NzbgetImported);
    }

    [Fact]
    public async Task ImportAsync_SkipsDisabledClients()
    {
        var parseResult = Mylar3ConfigParseResult.Parsed(
            new List<Mylar3NewznabConfig>(),
            new Mylar3SabnzbdConfig
            {
                Host = "localhost",
                ApiKey = "key",
                Enabled = false
            },
            null,
            null,
            new List<string>(),
            new List<string>());

        var result = await _importer.ImportAsync(parseResult, new Mylar3ImportOptions
        {
            ImportSabnzbd = true,
            ImportDisabled = false
        });

        Assert.True(result.Success);
        Assert.False(result.SabnzbdImported);
    }

    [Fact]
    public async Task ImportAsync_ItemResults_ArePopulated()
    {
        var parseResult = Mylar3ConfigParseResult.Parsed(
            new List<Mylar3NewznabConfig>
            {
                new() { Name = "Test", Host = "https://test.com", ApiKey = "key", Enabled = true }
            },
            new Mylar3SabnzbdConfig { Host = "localhost", ApiKey = "key", Enabled = true },
            null,
            null,
            new List<string>(),
            new List<string>());

        var result = await _importer.ImportAsync(parseResult, new Mylar3ImportOptions());

        Assert.Equal(2, result.ItemResults.Count);
        Assert.Contains(result.ItemResults, r => r.ItemType == "Indexer");
        Assert.Contains(result.ItemResults, r => r.ItemType == "SABnzbd");
    }

    #endregion

    #region INI Parsing Edge Cases

    [Fact]
    public async Task ParseConfigContentAsync_HandlesQuotedValues()
    {
        var content = @"
[General]
comic_location = ""/path/with spaces/comics""
download_dir = '/another/path'
";

        var result = await _importer.ParseConfigContentAsync(content);

        Assert.True(result.Success);
        Assert.NotNull(result.General);
        Assert.Equal("/path/with spaces/comics", result.General.ComicLocation);
        Assert.Equal("/another/path", result.General.DownloadDirectory);
    }

    [Fact]
    public async Task ParseConfigContentAsync_HandlesEmptyValues()
    {
        var content = @"
[General]
comic_location = 
download_dir =
";

        var result = await _importer.ParseConfigContentAsync(content);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ParseConfigContentAsync_CaseInsensitiveKeys()
    {
        var content = @"
[SABnzbd]
SAB_HOST = localhost
sab_PORT = 9090
Sab_ApiKey = key123
";

        var result = await _importer.ParseConfigContentAsync(content);

        Assert.NotNull(result.Sabnzbd);
        Assert.Equal("localhost", result.Sabnzbd.Host);
        Assert.Equal(9090, result.Sabnzbd.Port);
        Assert.Equal("key123", result.Sabnzbd.ApiKey);
    }

    [Fact]
    public async Task ParseConfigContentAsync_CaseInsensitiveSections()
    {
        var content = @"
[general]
comic_location = /comics

[SABNZBD]
sab_host = localhost
";

        var result = await _importer.ParseConfigContentAsync(content);

        Assert.True(result.Success);
        Assert.NotNull(result.General);
        Assert.NotNull(result.Sabnzbd);
    }

    [Fact]
    public async Task ParseConfigContentAsync_ParsesBooleanValues()
    {
        var content = @"
[Test1]
enabled = true

[Test2]
enabled = True

[Test3]
enabled = 1

[Test4]
enabled = yes

[Test5]
enabled = on

[Test6]
enabled = false
";

        var result = await _importer.ParseConfigContentAsync(content);
        Assert.True(result.Success);
    }

    #endregion

    #region Mylar3ImportOptions Tests

    [Fact]
    public void Mylar3ImportOptions_DefaultValues()
    {
        var options = new Mylar3ImportOptions();

        Assert.True(options.ImportIndexers);
        Assert.True(options.ImportSabnzbd);
        Assert.True(options.ImportNzbget);
        Assert.False(options.OverwriteExisting);
        Assert.False(options.ImportDisabled);
        Assert.True(options.TestConnections);
    }

    #endregion

    #region ImportAction Enum Tests

    [Fact]
    public void ImportAction_Values()
    {
        Assert.Equal(0, (int)ImportAction.Imported);
        Assert.Equal(1, (int)ImportAction.Updated);
        Assert.Equal(2, (int)ImportAction.Skipped);
        Assert.Equal(3, (int)ImportAction.Failed);
    }

    #endregion

    #region Factory Method Tests

    [Fact]
    public void Mylar3ConfigParseResult_Failed_CreatesFailedResult()
    {
        var result = Mylar3ConfigParseResult.Failed("Test error");

        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal("Test error", result.Errors[0]);
    }

    [Fact]
    public void Mylar3ImportResult_Failed_CreatesFailedResult()
    {
        var result = Mylar3ImportResult.Failed("Import error");

        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal("Import error", result.Errors[0]);
    }

    #endregion
}
