using System.Globalization;

namespace Ghost.Stealth;

public static class StealthScripts
{
    public static string GetInitScript(FingerprintProfile p)
    {
        var lat = p.Latitude.ToString("F5", CultureInfo.InvariantCulture);
        var lng = p.Longitude.ToString("F5", CultureInfo.InvariantCulture);
        var rtt = Math.Round(p.Rtt).ToString(CultureInfo.InvariantCulture);
        var downlink = p.Downlink.ToString("F2", CultureInfo.InvariantCulture);
        var battery = p.BatteryLevel.ToString("F2", CultureInfo.InvariantCulture);

        return $$$"""
            (()=>{
                let seed={{{p.Seed}}};
                const rand = () => ((Math.sin(seed++) + 1) / 2);

                /* webdriver flag removal */
                Object.defineProperty(navigator,'webdriver',{get:()=>undefined});

                /* hardware concurrency */
                Object.defineProperty(navigator,'hardwareConcurrency',{get:()=>{{{p.Cores}}}});
                Object.defineProperty(navigator,'deviceMemory',{get:()=>{{{p.MemoryGb}}}});
                Object.defineProperty(navigator,'platform',{get:()=>'{{{p.Platform}}}'});
                Object.defineProperty(navigator,'vendor',{get:()=>'Google Inc.'});

                /* screen properties */
                Object.defineProperty(window,'outerWidth',{get:()=>{{{p.ViewportWidth}}}});
                Object.defineProperty(window,'outerHeight',{get:()=>{
                    const chromeBar = Math.min(Math.max(70, 85*window.devicePixelRatio), 115);
                    return {{{p.ViewportHeight}}} + chromeBar;
                }});

                /* UA-CH Client Hints */
                const brands=[
                    {brand:'Chromium',version:'120'},
                    {brand:'Google Chrome',version:'120'},
                    {brand:'Not;A=Brand',version:'99'}
                ];
                const uaData={
                    brands,
                    mobile:false,
                    platform:'Windows',
                    getHighEntropyValues:async h=>Object.fromEntries(
                        h.map(x=>[x,{
                            architecture:'x86',
                            model:'',
                            bitness:'64',
                            platformVersion:'15.0.0',
                            uaFullVersion:'{{{p.ChromeVersion}}}',
                            fullVersionList:brands
                        }[x]]))
                };
                Object.defineProperty(navigator,'userAgentData',{get:()=>uaData});

                /* window.chrome stub */
                if(!window.chrome) {
                    window.chrome={
                        runtime:{},
                        webstore:{
                            onInstallStageChanged:{addListener:()=>{}},
                            onDownloadProgress:{addListener:()=>{}}
                        }
                    };
                }

                /* permissions latency shim */
                const realQuery = navigator.permissions.query.bind(navigator.permissions);
                navigator.permissions.query = d => new Promise(res=>{
                    setTimeout(async ()=>{
                        const r = await realQuery(d);
                        res({state:r.state,onchange:null});
                    },20+rand()*30);
                });

                /* navigator.connection */
                if(!navigator.connection) {
                    Object.defineProperty(navigator,'connection',{ get: () => ({
                        downlink    : {{{downlink}}},
                        downlinkMax : 100,
                        effectiveType:'4g',
                        rtt         : {{{rtt}}},
                        saveData    : false
                    })});
                }

                /* Battery API */
                if(!navigator.getBattery) {
                    navigator.getBattery = () => Promise.resolve({
                        charging:true,
                        chargingTime:0,
                        dischargingTime:Infinity,
                        level:{{{battery}}},
                        onchargingchange:null,
                        onlevelchange:null,
                        onchargingtimechange:null,
                        ondischargingtimechange:null
                    });
                }

                /* Intl timezone */
                try {
                    const origDTF = Intl.DateTimeFormat;
                    Intl.DateTimeFormat = function(...a){
                        const dtf = new origDTF(...a);
                        const ro  = dtf.resolvedOptions();
                        Object.defineProperty(ro,'timeZone',{get:()=>'{{{p.TimeZone}}}'});
                        dtf.resolvedOptions = () => ro;
                        return dtf;
                    };
                } catch(e) {}

                /* WebGL Vendor Spoofing */
                try {
                    const getParam = WebGLRenderingContext.prototype.getParameter;
                    WebGLRenderingContext.prototype.getParameter = function(p){
                        // UNMASKED_VENDOR_WEBGL = 37445
                        if(p===37445) return '{{{p.VideoCardVendor}}}';
                        // UNMASKED_RENDERER_WEBGL = 37446
                        if(p===37446) return '{{{p.VideoCardRenderer}}}';
                        return getParam.call(this,p);
                    };
                } catch(e) {}

                /* WebRTC Leak Block (strip ICE candidates) */
                try {
                    const pc = RTCPeerConnection.prototype;
                    ['createOffer','createAnswer'].forEach(fn=>{
                        const o = pc[fn];
                        pc[fn] = function(...a){
                            return o.apply(this,a).then(d=>{
                                if(d && d.sdp) d.sdp = d.sdp.replace(/a=candidate:.+\r?\n/g,'');
                                return d;
                            });
                        };
                    });
                } catch(e) {}

                /* Geolocation */
                const position = {
                    coords:{latitude:{{{lat}}},longitude:{{{lng}}},accuracy:25},
                    timestamp:Date.now()
                };
                if(!navigator.geolocation) navigator.geolocation={};
                navigator.geolocation.getCurrentPosition = cb => setTimeout(()=>cb(position),200+rand()*300);
                navigator.geolocation.watchPosition      = cb => (cb(position),1);

            })();
            """;
    }
}
