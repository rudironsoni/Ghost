namespace Ghost.Platform.Google.Jobs;

/// <summary>
/// Strategy for attempting job search methods.
/// </summary>
public enum JobSearchStrategy
{
    /// <summary>
    /// Try HTTP API first, fall back to browser if no results.
    /// </summary>
    HttpFirst,

    /// <summary>
    /// Try browser first, fall back to HTTP API if browser fails.
    /// </summary>
    BrowserFirst,

    /// <summary>
    /// Only use HTTP API, never attempt browser.
    /// </summary>
    HttpOnly,

    /// <summary>
    /// Only use browser, never attempt HTTP API.
    /// </summary>
    BrowserOnly
}

public sealed class GoogleJobsOptions
{
    public bool Enabled { get; set; } = true;
    public string Country { get; set; } = "US";
    public int MinDelayMs { get; set; } = 200;
    public int MaxDelayMs { get; set; } = 800;

    /// <summary>
    /// Strategy for attempting job search methods.
    /// Default is BrowserFirst for better reliability.
    /// </summary>
    public JobSearchStrategy Strategy { get; set; } = JobSearchStrategy.BrowserFirst;

    /// <summary>
    /// [OBSOLETE] Use Strategy property instead.
    /// This property is maintained for backward compatibility.
    /// </summary>
    [Obsolete("Use Strategy property instead. This property will be removed in a future version.")]
    public bool UseBrowserFallback
    {
        get => Strategy != JobSearchStrategy.HttpOnly;
        set
        {
            if (value && Strategy == JobSearchStrategy.HttpOnly)
            {
                Strategy = JobSearchStrategy.HttpFirst;
            }
            else if (!value)
            {
                Strategy = JobSearchStrategy.HttpOnly;
            }
        }
    }
    
    /// <summary>
    /// Async bootstrap string for Google Jobs pagination calls.
    /// This is a long basejs/xjs bootstrap string that Google expects.
    /// Default value is from JobSpy implementation.
    /// </summary>
    public string AsyncBootstrapString { get; set; } = "_basejs:/xjs/_/js/k=xjs.s.en_US.JwveA-JiKmg.2018.O/am=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIAAAAAAAAACAAAoICAAAAAAAKMAfAAAAIAQAAAAAAAAAAAAACCAAAEJDAAACAAAAAGABAIAAARBAAABAAAAAgAgQAABAASKAfv8JAAABAAAAAAwAQAQACQAAAAAAcAEAQABoCAAAABAAAIABAACAAAAEAAAAFAAAAAAAAAAAAAAAAAAAAAAAAACAQADoBwAAAAAAAAAAAAAQBAAAAATQAAoACOAHAAAAAAAAAQAAAIIAAAA_ZAACAAAAAAAAcB8APB4wHFJ4AAAAAAAAAAAAAAAACECCYA5If0EACAAAAAAAAAAAAAAAAAAAUgRNXG4AMAE/dg=0/br=1/rs=ACT90oGxMeaFMCopIHq5tuQM-6_3M_VMjQ,_basecss:/xjs/_/ss/k=xjs.s.IwsGu62EDtU.L.B1.O/am=QOoQIAQAAAQAREADEBAAAAAAAAAAAAAAAAAAAAAgAQAAIAAAgAQAAAIAIAIAoEwCAADIC8AfsgEAawwAPkAAjgoAGAAAAAAAAEADAAAAAAIgAECHAAAAAAAAAAABAQAggAARQAAAQCEAAAAAIAAAABgAAAAAIAQIACCAAfB-AAFIQABoCEA_CgEAAIABAACEgHAEwwAEFQAM4CgAAAAAAAAAAAAACABCAAAAQEAAABAgAMCPAAA4AoE2BAEAggSAAIoAQAAAAAgAAAAACCAQAAAxEwA_ZAACAAAAAAAAAAkAAAAAAAAgAAAAAAAAAAAAAAAAAAAAAAAAQAEAAAAAAAAAAAAAAAAAAAAAQA/br=1/rs=ACT90oGZc36t3uUQkj0srnIvvbHjO2hgyg,_basecomb:/xjs/_/js/k=xjs.s.en_US.JwveA-JiKmg.2018.O/ck=xjs.s.IwsGu62EDtU.L.B1.O/am=QOoQIAQAAAQAREADEBAAAAAAAAAAAAAAAAAAAAAgAQAAIAAAgAQAAAKAIAoIqEwCAADIK8AfsgEAawwAPkAAjgoAGAAACCAAAEJDAAACAAIgAGCHAIAAARBAAABBAQAggAgRQABAQSOAfv8JIAABABgAAAwAYAQICSCAAfB-cAFIQABoCEA_ChEAAIABAACEgHAEwwAEFQAM4CgAAAAAAAAAAAAACABCAACAQEDoBxAgAMCPAAA4AoE2BAEAggTQAIoASOAHAAgAAAAACSAQAIIxEwA_ZAACAAAAAAAAcB8APB4wHFJ4AAAAAAAAAAAAAAAACECCYA5If0EACAAAAAAAAAAAAAAAAAAAUgRNXG4AMAE/d=1/ed=1/dg=0/br=1/ujg=1/rs=ACT90oFNLTjPzD_OAqhhtXwe2pg1T3WpBg,_fmt:prog,_id:fc_5FwaZ86OKsfdwN4P4La3yA4_2";
}
