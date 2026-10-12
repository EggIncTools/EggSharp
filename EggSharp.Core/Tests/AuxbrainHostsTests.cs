namespace EggSharp.Core.Tests;

public sealed class AuxbrainHostsTests {
    [Fact]
    public void Live_ResolvesToWwwAuxbrain() {
        Assert.Equal(new Uri("https://www.auxbrain.com/ei/bot_first_contact"), AuxbrainHosts.Live.Resolve("/ei/bot_first_contact"));
    }

    [Fact]
    public void PathOrigin_WinsByLongestPrefix() {
        var hosts = new AuxbrainHosts("https://www.auxbrain.com", new Dictionary<string, string> {
            ["ei_ctx/"] = "https://ctx-dot-auxbrainhome.appspot.com",
            ["ei_ctx/get_season_infos_v2"] = "http://localhost:5080"
        });
        Assert.Equal(new Uri("https://ctx-dot-auxbrainhome.appspot.com/ei_ctx/get_contracts_info"), hosts.Resolve("ei_ctx/get_contracts_info"));
        Assert.Equal(new Uri("http://localhost:5080/ei_ctx/get_season_infos_v2"), hosts.Resolve("ei_ctx/get_season_infos_v2"));
        Assert.Equal(new Uri("https://www.auxbrain.com/ei/get_periodicals"), hosts.Resolve("ei/get_periodicals"));
    }

    [Fact]
    public void RelativeProxyPrefix_StaysRelative() {
        var hosts = new AuxbrainHosts("/egg-api");
        var uri = hosts.Resolve("ei_afx/complete_mission");
        Assert.False(uri.IsAbsoluteUri);
        Assert.Equal("/egg-api/ei_afx/complete_mission", uri.OriginalString);
    }
}
