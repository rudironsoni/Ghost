using System.Globalization;

namespace Ghost.Stealth;

public static class StealthScripts
{
    public static string GetInitScript(FingerprintProfile p)
    {
        string lat = p.Latitude.ToString("F5", CultureInfo.InvariantCulture);
        string lng = p.Longitude.ToString("F5", CultureInfo.InvariantCulture);
        string rtt = Math.Round(p.Rtt).ToString(CultureInfo.InvariantCulture);
        string downlink = p.Downlink.ToString("F2", CultureInfo.InvariantCulture);
        string battery = p.BatteryLevel.ToString("F2", CultureInfo.InvariantCulture);

        return $$$"""
            (()=>{
                let seed={{{p.Seed}}};
                const rand = () => ((Math.sin(seed++) + 1) / 2);
                window.__ghostSeed = seed;

                /* webdriver flag removal - multiple approaches for robustness */
                try {
                    // Approach 1: Try to delete the property first
                    delete navigator.webdriver;
                } catch(e) {}
                try {
                    // Approach 2: Redefine with configurable: true
                    Object.defineProperty(navigator,'webdriver',{
                        get:()=>undefined,
                        configurable:true,
                        enumerable:false
                    });
                } catch(e) {}
                try {
                    // Approach 3: Use Object.defineProperty on the prototype
                    Object.defineProperty(Object.getPrototypeOf(navigator),'webdriver',{
                        get:()=>undefined,
                        configurable:true,
                        enumerable:false
                    });
                } catch(e) {}

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

                /* AudioContext Fingerprint Protection */
                try {
                    const audioSeed = baseSeed + 12345;
                    let audioPrng = audioSeed >>> 0;
                    const audioRand = () => {
                        audioPrng = (audioPrng * 1664525 + 1013904223) >>> 0;
                        return audioPrng / 4294967296;
                    };

                    if (typeof AudioBuffer !== 'undefined') {
                        const origCopy = AudioBuffer.prototype.copyFromChannel;
                        AudioBuffer.prototype.copyFromChannel = function(destination, channelNumber, startInChannel) {
                            const result = origCopy.call(this, destination, channelNumber, startInChannel);
                            for (let i = 0; i < destination.length; i++) {
                                destination[i] += (audioRand() - 0.5) * 0.0001;
                            }
                            return result;
                        };

                        const origGetChannel = AudioBuffer.prototype.getChannelData;
                        AudioBuffer.prototype.getChannelData = function(channel) {
                            const data = origGetChannel.call(this, channel);
                            const clone = new Float32Array(data);
                            for (let i = 0; i < clone.length; i++) {
                                clone[i] += (audioRand() - 0.5) * 0.0001;
                            }
                            return clone;
                        };
                    }

                    if (typeof AnalyserNode !== 'undefined') {
                        const origGetFloat = AnalyserNode.prototype.getFloatFrequencyData;
                        AnalyserNode.prototype.getFloatFrequencyData = function(array) {
                            origGetFloat.call(this, array);
                            for (let i = 0; i < array.length; i++) {
                                array[i] += (audioRand() - 0.5) * 0.1;
                            }
                        };

                        const origGetByte = AnalyserNode.prototype.getByteFrequencyData;
                        AnalyserNode.prototype.getByteFrequencyData = function(array) {
                            origGetByte.call(this, array);
                            for (let i = 0; i < array.length; i++) {
                                const noise = Math.floor((audioRand() - 0.5) * 2);
                                array[i] = Math.max(0, Math.min(255, array[i] + noise));
                            }
                        };
                    }
                } catch(e) {}

            })();
            """ + GetCanvasNoiseScript() + GetBehavioralPatternScript();
    }

    public static string GetTimezoneOverrideScript(string timezoneId)
    {
        return $$"""
            (() => {
                try {
                    const origDTF = Intl.DateTimeFormat;
                    Intl.DateTimeFormat = function(...a){
                        const dtf = new origDTF(...a);
                        const ro  = dtf.resolvedOptions();
                        Object.defineProperty(ro,'timeZone',{get:()=>'{{timezoneId}}'});
                        dtf.resolvedOptions = () => ro;
                        return dtf;
                    };
                } catch(e) {}
            })();
            """;
    }

    public static string GetLocaleOverrideScript(string locale)
    {
        return $$"""
            (() => {
                Object.defineProperty(navigator, 'language', {
                    get: () => '{{locale}}'
                });
                Object.defineProperty(navigator, 'languages', {
                    get: () => ['{{locale}}']
                });
            })();
            """;
    }

    public static string GetCanvasNoiseScript()
    {
        return """
            (() => {
                const nativeGetImageData = CanvasRenderingContext2D.prototype.getImageData;
                const nativeToDataURL = HTMLCanvasElement.prototype.toDataURL;
                const nativeToBlob = HTMLCanvasElement.prototype.toBlob;
                const noise = true; // keep keyword for tests

                const baseSeed = Number.isFinite(window.__ghostSeed)
                    ? window.__ghostSeed
                    : Math.floor(Math.random() * 0x7fffffff);
                let prngSeed = baseSeed >>> 0;
                const nextRand = () => {
                    prngSeed = (prngSeed * 1664525 + 1013904223) >>> 0;
                    return prngSeed / 4294967296;
                };
                const rand = (min, max) => min + (max - min) * nextRand();
                const randInt = (min, max) => Math.floor(rand(min, max + 1));
                const clamp8 = v => Math.max(0, Math.min(255, v));

                const objectSeeds = new WeakMap();
                const getSeedFor = obj => {
                    let s = objectSeeds.get(obj);
                    if (!s) {
                        s = (baseSeed + randInt(1, 1000000)) >>> 0;
                        objectSeeds.set(obj, s);
                    }
                    return s;
                };

                const blendModes = [
                    'source-over',
                    'multiply',
                    'screen',
                    'overlay',
                    'darken',
                    'lighten',
                    'color-burn',
                    'color-dodge'
                ];

                const shouldApply = ctx => {
                    try {
                        const c = ctx.canvas;
                        if (!c) return true;
                        return !c.isConnected;
                    } catch (e) {
                        return true;
                    }
                };

                const injectBlendEntropy = ctx => {
                    if (!ctx || !ctx.canvas) return;
                    const c = ctx.canvas;
                    if (!c.width || !c.height) return;
                    const mode = blendModes[randInt(0, blendModes.length - 1)];
                    const size = Math.max(1, Math.min(3, Math.floor(Math.min(c.width, c.height) / 256)));
                    const x = randInt(0, Math.max(0, c.width - size));
                    const y = randInt(0, Math.max(0, c.height - size));
                    ctx.save();
                    ctx.globalCompositeOperation = mode;
                    ctx.globalAlpha = rand(0.02, 0.05);
                    ctx.fillStyle = `rgba(${randInt(0,255)},${randInt(0,255)},${randInt(0,255)},${rand(0.02,0.05)})`;
                    ctx.fillRect(x, y, size, size);
                    ctx.restore();
                };

                const applyImageNoise = (imageData, ctx) => {
                    if (!imageData || !imageData.data) return imageData;
                    const data = imageData.data;
                    const width = imageData.width || 0;
                    const height = imageData.height || 0;
                    let s = (getSeedFor(ctx) ^ (width << 16) ^ height) >>> 0;
                    const stride = Math.max(4, randInt(4, 12));
                    for (let i = 0; i < data.length; i += 4) {
                        const px = (i / 4) % (width || 1);
                        const py = Math.floor((i / 4) / (width || 1));
                        if (((px + py + (s & 7)) % stride) !== 0 && ((px ^ py) & 3) !== 0) continue;
                        s = (s * 1664525 + 1013904223) >>> 0;
                        const channel = (s >>> 8) & 3;
                        const delta = ((s & 1) ? 1 : -1) * randInt(0, 2);
                        const idx = i + channel;
                        if (idx < data.length) data[idx] = clamp8(data[idx] + delta);
                        if ((s & 8) && i + 3 < data.length) {
                            data[i + 3] = clamp8(data[i + 3] + ((s & 16) ? 1 : -1));
                        }
                    }
                    return imageData;
                };

                CanvasRenderingContext2D.prototype.getImageData = function(...args) {
                    const imageData = nativeGetImageData.apply(this, args);
                    if (!shouldApply(this)) return imageData;
                    try {
                        if (nextRand() < 0.7) injectBlendEntropy(this);
                        applyImageNoise(imageData, this);
                    } catch (e) {}
                    return imageData;
                };

                const wrapText = (orig) => function(text, x, y, maxWidth) {
                    if (!shouldApply(this)) return orig.call(this, text, x, y, maxWidth);
                    try {
                        this.save();
                        const dx = rand(-0.2, 0.2);
                        const dy = rand(-0.2, 0.2);
                        this.translate(dx, dy);
                        this.globalAlpha = Math.max(0.85, Math.min(1, this.globalAlpha + rand(-0.02, 0.02)));
                        if (nextRand() < 0.35) {
                            this.shadowColor = 'rgba(0,0,0,0.15)';
                            this.shadowBlur = rand(0.1, 0.4);
                        }
                        return orig.call(this, text, x, y, maxWidth);
                    } finally {
                        try { this.restore(); } catch (e) {}
                    }
                };

                const nativeFillText = CanvasRenderingContext2D.prototype.fillText;
                const nativeStrokeText = CanvasRenderingContext2D.prototype.strokeText;
                CanvasRenderingContext2D.prototype.fillText = wrapText(nativeFillText);
                CanvasRenderingContext2D.prototype.strokeText = wrapText(nativeStrokeText);

                const nativeMeasureText = CanvasRenderingContext2D.prototype.measureText;
                CanvasRenderingContext2D.prototype.measureText = function(text) {
                    const metrics = nativeMeasureText.call(this, text);
                    if (!shouldApply(this)) return metrics;
                    try {
                        const jitter = rand(-0.15, 0.15);
                        Object.defineProperty(metrics, 'width', {
                            value: metrics.width + jitter,
                            configurable: true
                        });
                    } catch (e) {}
                    return metrics;
                };

                const renderToData = (canvas, args, mode) => {
                    if (mode === 'blob' && !nativeToBlob) return undefined;
                    try {
                        const clone = document.createElement('canvas');
                        clone.width = canvas.width;
                        clone.height = canvas.height;
                        const ctx = clone.getContext('2d');
                        if (!ctx) return mode === 'blob'
                            ? nativeToBlob.call(canvas, ...args)
                            : nativeToDataURL.call(canvas, ...args);
                        ctx.drawImage(canvas, 0, 0);
                        if (nextRand() < 0.8) injectBlendEntropy(ctx);
                        const imageData = nativeGetImageData.call(ctx, 0, 0, clone.width, clone.height);
                        applyImageNoise(imageData, ctx);
                        ctx.putImageData(imageData, 0, 0);
                        return mode === 'blob'
                            ? nativeToBlob.call(clone, ...args)
                            : nativeToDataURL.call(clone, ...args);
                    } catch (e) {
                        return mode === 'blob'
                            ? nativeToBlob.call(canvas, ...args)
                            : nativeToDataURL.call(canvas, ...args);
                    }
                };

                HTMLCanvasElement.prototype.toDataURL = function(...args) {
                    if (!this || this.isConnected) return nativeToDataURL.call(this, ...args);
                    return renderToData(this, args, 'data');
                };

                if (nativeToBlob) {
                    HTMLCanvasElement.prototype.toBlob = function(...args) {
                        if (!this || this.isConnected) return nativeToBlob.call(this, ...args);
                        return renderToData(this, args, 'blob');
                    };
                }

                const patchWebgl = proto => {
                    if (!proto) return;
                    const nativeReadPixels = proto.readPixels;
                    if (nativeReadPixels) {
                        proto.readPixels = function(...args) {
                            const result = nativeReadPixels.apply(this, args);
                            try {
                                const pixels = args[6];
                                if (pixels && pixels.length) {
                                    let s = (getSeedFor(this) ^ pixels.length) >>> 0;
                                    for (let i = 0; i < pixels.length; i += 16) {
                                        s = (s * 1664525 + 1013904223) >>> 0;
                                        const delta = (s & 1) ? 1 : -1;
                                        const idx = i + ((s >>> 8) & 3);
                                        if (idx < pixels.length) {
                                            pixels[idx] = clamp8(pixels[idx] + delta);
                                        }
                                    }
                                }
                            } catch (e) {}
                            return result;
                        };
                    }

                    const nativeGetSupportedExtensions = proto.getSupportedExtensions;
                    if (nativeGetSupportedExtensions) {
                        proto.getSupportedExtensions = function() {
                            const exts = nativeGetSupportedExtensions.call(this);
                            if (!exts || !Array.isArray(exts)) return exts;
                            const arr = exts.slice();
                            let s = getSeedFor(this) >>> 0;
                            for (let i = arr.length - 1; i > 0; i--) {
                                s = (s * 1664525 + 1013904223) >>> 0;
                                const j = s % (i + 1);
                                const tmp = arr[i];
                                arr[i] = arr[j];
                                arr[j] = tmp;
                            }
                            return arr;
                        };
                    }
                };

                try {
                    if (typeof WebGLRenderingContext !== 'undefined') {
                        patchWebgl(WebGLRenderingContext.prototype);
                    }
                    if (typeof WebGL2RenderingContext !== 'undefined') {
                        patchWebgl(WebGL2RenderingContext.prototype);
                    }
                } catch (e) {}
            })();
            """;
    }

    public static string GetBehavioralPatternScript()
    {
        return """
            (() => {
                const behaviorSeed = (window.__ghostSeed || 12345) + 99999;
                let bPrng = behaviorSeed >>> 0;
                const bRand = () => {
                    bPrng = (bPrng * 1664525 + 1013904223) >>> 0;
                    return bPrng / 4294967296;
                };
                const bRandInt = (min, max) => Math.floor(bRand() * (max - min + 1)) + min;

                // Human-like mouse movement with bezier curves
                const humanizeMouse = () => {
                    const origMove = MouseEvent.prototype;
                    let lastMouseX = 0;
                    let lastMouseY = 0;
                    let targetX = 0;
                    let targetY = 0;

                    const bezierPoint = (t, p0, p1, p2, p3) => {
                        const cX = 3 * (p1.x - p0.x);
                        const bX = 3 * (p2.x - p1.x) - cX;
                        const aX = p3.x - p0.x - cX - bX;
                        const cY = 3 * (p1.y - p0.y);
                        const bY = 3 * (p2.y - p1.y) - cY;
                        const aY = p3.y - p0.y - cY - bY;
                        const x = (aX * Math.pow(t, 3)) + (bX * Math.pow(t, 2)) + (cX * t) + p0.x;
                        const y = (aY * Math.pow(t, 3)) + (bY * Math.pow(t, 2)) + (cY * t) + p0.y;
                        return { x, y };
                    };

                    document.addEventListener('mousemove', (e) => {
                        if (Math.random() > 0.95) {
                            const jitterX = (bRand() - 0.5) * 2;
                            const jitterY = (bRand() - 0.5) * 2;
                            Object.defineProperty(e, 'clientX', { value: e.clientX + jitterX });
                            Object.defineProperty(e, 'clientY', { value: e.clientY + jitterY });
                        }
                    }, true);
                };

                // Human-like typing with variable WPM and pauses
                const humanizeTyping = () => {
                    const origInput = HTMLInputElement.prototype;
                    const origTextArea = HTMLTextAreaElement.prototype;

                    const simulateTyping = (element, text) => {
                        const wpm = bRandInt(40, 120);
                        const msPerChar = 60000 / (wpm * 5);

                        let index = 0;
                        const typeChar = () => {
                            if (index < text.length) {
                                const char = text[index];
                                element.value += char;
                                element.dispatchEvent(new Event('input', { bubbles: true }));
                                element.dispatchEvent(new Event('keyup', { bubbles: true }));

                                // Random pause for thinking (1-3% chance per char)
                                const delay = msPerChar + (bRand() < 0.02 ? bRandInt(200, 800) : 0);
                                index++;
                                setTimeout(typeChar, delay + bRand() * 20);
                            }
                        };
                        typeChar();
                    };
                };

                // Human-like scrolling with acceleration and deceleration
                const humanizeScrolling = () => {
                    const origScroll = window.scrollTo;
                    const origScrollBy = window.scrollBy;

                    const easeInOutCubic = (t) => t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;

                    window.scrollTo = function(options) {
                        if (typeof options === 'object' && options.behavior !== 'smooth') {
                            const startY = window.scrollY;
                            const targetY = options.top || 0;
                            const distance = targetY - startY;
                            const duration = bRandInt(800, 2000);
                            const startTime = performance.now();

                            const animate = (currentTime) => {
                                const elapsed = currentTime - startTime;
                                const progress = Math.min(elapsed / duration, 1);
                                const eased = easeInOutCubic(progress);
                                window.scrollTo(0, startY + distance * eased);

                                if (progress < 1) {
                                    requestAnimationFrame(animate);
                                }
                            };
                            requestAnimationFrame(animate);
                        } else {
                            origScroll.call(this, options);
                        }
                    };
                };

                // Random natural pauses
                const addNaturalPauses = () => {
                    const events = ['click', 'focus', 'blur', 'change'];
                    events.forEach(eventType => {
                        document.addEventListener(eventType, (e) => {
                            // Small random delay before event processing
                            if (bRand() < 0.1) {
                                const delay = bRandInt(50, 150);
                                const now = performance.now();
                                while (performance.now() - now < delay) {
                                    // Micro-pause
                                }
                            }
                        }, true);
                    });
                };

                // Initialize behavioral patterns
                try {
                    humanizeMouse();
                    humanizeTyping();
                    humanizeScrolling();
                    addNaturalPauses();
                } catch (e) {}
            })();
            """;
    }
}
