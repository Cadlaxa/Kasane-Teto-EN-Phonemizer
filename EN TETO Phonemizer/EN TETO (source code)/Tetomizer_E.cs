using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using Classic;
using OpenUtau.Api;
using OpenUtau.Classic;
using OpenUtau.Core.G2p;
using OpenUtau.Core.Ustx;
using Serilog;
using WanaKanaNet;
using YamlDotNet.Core.Tokens;

namespace OpenUtau.Plugin.Builtin {
    [Phonemizer("TETO EN+JP Phonemizer", "EN TETO v100", "Cadlaxa", language: "EN")]
    // Custom Phonemizer for Teto to handle Japanese and English phonemes
    public class Tetomizer : SyllableBasedPhonemizer {
        private readonly string[] vowels = {
        "aa", "ax", "ae", "ah", "ao", "aw", "ay", "eh", "er", "ey", "ih", "iy", "ow", "oy", "uh", "uw", "a", "e", "i", "o", "u", "ai", "ei", "oi", "au", "ou", "ix", "ux",
        "aar", "ar", "axr", "aer", "ahr", "aor", "or", "awr", "aur", "ayr", "air", "ehr", "eyr", "eir", "ihr", "iyr", "ir", "owr", "our", "oyr", "oir", "uhr", "uwr", "ur",
        "aal", "al", "axl", "ael", "ahl", "aol", "ol", "awl", "aul", "ayl", "ail", "ehl", "el", "eyl", "eil", "ihl", "iyl", "il", "owl", "oul", "oyl", "oil", "uhl", "uwl", "ul",
        "aan", "an", "axn", "aen", "ahn", "aon", "on", "awn", "aun", "ayn", "ain", "ehn", "en", "eyn", "ein", "ihn", "iyn", "in", "own", "oun", "oyn", "oin", "uhn", "uwn", "un",
        "aang", "ang", "axng", "aeng", "ahng", "aong", "ong", "awng", "aung", "ayng", "aing", "ehng", "eng", "eyng", "eing", "ihng", "iyng", "ing", "owng", "oung", "oyng", "oing", "uhng", "uwng", "ung",
        "aam", "am", "axm", "aem", "ahm", "aom", "om", "awm", "aum", "aym", "aim", "ehm", "em", "eym", "eim", "ihm", "iym", "im", "owm", "oum", "oym", "oim", "uhm", "uwm", "um", "oh",
        "eu", "oe", "yw", "yx", "wx", "ox", "ex", "ea", "ia", "oa", "ua", "ean", "eam", "eang", "nn", "mm", "ll"
        };
        private readonly string[] consonants = "b,ch,d,dh,dr,dx,f,g,hh,jh,k,l,m,n,nx,ng,p,q,r,s,sh,t,th,tr,v,w,y,z,zh,N".Split(',');
        private readonly string[] affricates = "ch,jh,j".Split(',');
        private readonly string[] tapConsonant = "dx,nx,lx".Split(",");
        private readonly string[] semilongConsonants = "ng,n,m,v,z,q,hh,N,ん".Split(",");
        private readonly string[] semiVowels = "y,w".Split(",");
        private readonly string[] connectingGlides = "l,r,ll".Split(",");
        private readonly string[] longConsonants = "f,s,sh,th,zh,dr,tr,ts,c,vf".Split(",");
        private readonly string[] normalConsonants = "b,d,dh,g,k,p,t,l,r".Split(',');
        private readonly string[] connectingNormCons = "b,d,g,k,p,t".Split(',');
        protected override string[] GetVowels() => vowels;
        protected override string[] GetConsonants() => consonants;
        protected override string GetDictionaryName() => "cmudict-0_7b.txt";
        protected override Dictionary<string, string> GetDictionaryPhonemesReplacement() => dictionaryPhonemesReplacement;

        // For banks with missing vowels
        private readonly Dictionary<string, string> missingVphonemes = "aa=ah,ae=ah,iy=ih,uh=uw,ix=ih,ux=uh,oh=ao,eu=uh,oe=ax,uy=uw,yw=uw,yx=iy,wx=uw,ea=eh,ia=iy,oa=ao,ua=uw,R=-,N=n,mm=m,ll=l".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isMissingVPhonemes = false;

        // For banks with missing custom consonants
        private readonly Dictionary<string, string> missingCphonemes = "nx=n,tx=t,dx=d,ty=t,ky=k,ry=r,ly=l,ng=n,cl=q,vf=q,dd=d,lx=l,ts=t,th=s,v=f,j=jh,dh=d".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isMissingCPhonemes = false;

        // TIMIT symbols
        private readonly Dictionary<string, string> timitphonemes = "axh=ax,bcl=b,dcl=d,eng=ng,gcl=g,hv=hh,kcl=k,pcl=p,tcl=t".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isTimitPhonemes = false;
        private bool cPV_FallBack = false;

        private static readonly Dictionary<string, string> dictionaryPhonemesReplacement = new Dictionary<string, string> {
            { "aa", "a" },
            { "ae", "e" },
            { "ah", "a" },
            { "ao", "o" },
            { "ax", "a" },
            { "eh", "e" },
            { "er", "er" },
            { "ih", "i" },
            { "iy", "i" },
            { "jh", "j" },
            { "uh", "o" },
            { "uw", "u" },
            { "N", "n" },
            { "dx", "r" },
            { "zh", "sh" },
        };
        private Dictionary<string, string> AltCv => altCv;
        private static readonly Dictionary<string, string> altCv = new Dictionary<string, string> {
            {"dxa", "ra" },
            {"dxax", "ra" },
            {"dxi", "ri" },
            {"dxu", "ru" },
            {"dxe", "re"},
            {"dxo", "ro"},
            {"ta", "tsa" },
            {"tax", "tsa" },
            {"ti", "tsi" },
            {"tu", "tsu" },
            {"te", "tse"},
            {"to", "tso"},
            {"hha", "ha" },
            {"hhi", "hi" },
            {"hhu", "fu" },
            {"hhe", "he"},
            {"hho", "ho"},
            {"tha", "tsa" },
            {"thi", "suli" },
            {"thu", "su" },
            {"the", "tse"},
            {"tho", "so"},
            {"dha", "za" },
            {"dhi", "zuli" },
            {"dhu", "du" },
            {"dhe", "ze"},
            {"dho", "zo"},
            {"si", "suli" },
            {"zi", "zuli" },
            {"di", "deli" },
            {"du", "dolu" },
            {"yi", "i" },
            {"wu", "u" },
            {"wo", "ulo" },
            {"rra", "wa" },
            {"rri", "wi" },
            {"rru", "ru" },
            {"rre", "we" },
            {"rro", "ulo" },
        };

        private readonly Dictionary<string, string> en_jpVV =
        new Dictionary<string, string>() {
            { "aa", "a" },
            { "ae", "e" },
            { "ah", "a" },
            { "ao", "o" },
            { "ax", "a" },
            { "eh", "e" },
            { "er", "a" },
            { "ey", "i" },
            { "ih", "i" },
            { "iy", "i" },
            { "uh", "o" },
            { "uw", "u" },
        };

        private readonly Dictionary<string, string> vcFallBacks =
        new Dictionary<string, string>() {
            {"aw","u"},
            {"ow","u"},
            {"uh","u"},
            {"ay","i"},
            {"ey","i"},
            {"oy","i"},
            {"aa","a"},
            {"ae","h"},
            {"ao","a"},
            {"i","i"},
            {"u","u"},
            {"a","aa"},
            {"e","eh"},
            {"o","ao"},
            //{"eh","ah"},
            //{"er","ah"},
        };

        private readonly Dictionary<string, string> vvExceptions =
        new Dictionary<string, string>() {
            {"aw","w"},
            {"ow","w"},
            {"uw","w"},
            {"uh","w"},
            {"ay","y"},
            {"ey","y"},
            {"iy","y"},
            {"oy","y"},
            {"ih","y"},
            {"er","r"},
            {"aar","r"},
            {"aen","n"},
            {"aeng","ng"},
            {"aor","r"},
            {"ehr","r"},
            {"ihng","ng"},
            {"ihr","r"},
            {"uwr","r"},
            {"awn","n"},
            {"awng","ng"},
            {"ean","n"},
            {"eam","m"},
            {"eang","ng"},
            // r-colored vowel and l
            {"ar","r"},
            {"or","r"},
            {"air","r"},
            {"ir","r"},
            {"ur","r"},
            {"al","l"},
            {"ol","l"},
            {"il","l"},
            {"el","l"},
            {"ul","l"},
        };

        private Dictionary<string, string> VCVException => vcvException;
        private static readonly Dictionary<string, string> vcvException = new Dictionary<string, string> {
            {"w","u"},
            {"y","i"},
            {"r","u"},
            {"l","u"},
            {"m","n"},
            {"n","n"},
            {"ng","n"},
        };

        private readonly string[] ccvException = { "ch", "dh", "dx", "fh", "gh", "hh", "jh", "kh", "ph", "ng", "sh", "th", "vh", "wh", "zh" };
        private readonly string[] RomajiException = { "a", "e", "i", "o", "u" };
        private string[] tails = "-,R".Split(',');

        protected override string[] GetSymbols(Note note) {
            string[] original = base.GetSymbols(note);
            if (tails.Contains(note.lyric)) {
                return new string[] { note.lyric };
            }
            if (note.lyric.ToUpper() == "R") {
                return new string[] { "-" };
            }
            if (original == null) {
                return null;
            }
            List<string> modified = new List<string>();

            // SPLITS UP DR AND TR
            string[] tr = new[] { "tr" };
            string[] dr = new[] { "dr" };
            string[] ie = new[] { "ie" };
            string[] wh = new[] { "wh" };
            var plo = new List<string> { "bb", "dd", "ff", "gg", "jj", "kk", "ll", "mm", "nn", "pp", "rr", "ss", "tt", "vv", "ww", "yy", "zz" };
            var plo1 = new List<string> { "cch", "ddh", "hhh", "jjh", "ssh", "tth", "zzh" };
            string[] av_c = new[] { "al", "am", "an", "ang", "ar" };
            string[] ev_c = new[] { "el", "em", "en", "eng", "err" };
            string[] iv_c = new[] { "il", "im", "in", "ing", "ir" };
            string[] ov_c = new[] { "ol", "om", "on", "ong", "or" };
            string[] uv_c = new[] { "ul", "um", "un", "ung", "ur" };
            string[] diphthongs1 = new[] { "ay", "ey", "oy" };
            string[] diphthongs2 = new[] { "ow", "aw" };
            var consonatsV1 = new List<string> { "l", "m", "n", "r" };
            var consonatsV2 = new List<string> { "mm", "nn", "ng" };
            // SPLITS UP 2 SYMBOL VOWELS AND 1 SYMBOL CONSONANT
            List<string> vowel3S = new List<string>();
            foreach (string V1 in vowels) {
                foreach (string C1 in consonatsV1) {
                    vowel3S.Add($"{V1}{C1}");
                }
            }
            // SPLITS UP 2 SYMBOL VOWELS AND 2 SYMBOL CONSONANT
            List<string> vowel4S = new List<string>();
            foreach (string V1 in vowels) {
                foreach (string C1 in consonatsV2) {
                    vowel3S.Add($"{V1}{C1}");
                }
            }
            foreach (string s in original) {
                switch (s) {
                    case var str when dr.Contains(str):
                        modified.AddRange(new string[] { "jh", s[1].ToString() });
                        break;
                    case var str when tr.Contains(str):
                        modified.AddRange(new string[] { "ch", s[1].ToString() });
                        break;
                    case var str when ie.Contains(str):
                        modified.AddRange(new string[] { "i", s[1].ToString() });
                        break;
                    case var str when plo.Contains(str):
                        modified.AddRange(new string[] { "_" + s[0].ToString(), s[1].ToString() });
                        break;
                    case var str when plo1.Contains(str):
                        modified.AddRange(new string[] { "_" + s.Substring(1, 2), s.Substring(1, 2) });
                        break;
                    case var str when wh.Contains(str) && !HasOto($"{str} {vowels}", note.tone) && !HasOto($"ay {str}", note.tone):
                        modified.AddRange(new string[] { "hh", s[1].ToString() });
                        break;
                    case var str when av_c.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        modified.AddRange(new string[] { "a", s[1].ToString() });
                        break;
                    case var str when ev_c.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        modified.AddRange(new string[] { "e", s[1].ToString() });
                        break;
                    case var str when iv_c.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        modified.AddRange(new string[] { "y", s[1].ToString() });
                        break;
                    case var str when ov_c.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        modified.AddRange(new string[] { "o", s[1].ToString() });
                        break;
                    case var str when uv_c.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        modified.AddRange(new string[] { "u", s[1].ToString() });
                        break;
                    case var str when vowel3S.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        modified.AddRange(new string[] { s.Substring(0, 2), s[2].ToString() });
                        break;
                    case var str when vowel4S.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        modified.AddRange(new string[] { s.Substring(0, 2), s.Substring(2, 2) });
                        break;
                    case var str when diphthongs1.Contains(str):
                        modified.AddRange(new string[] { s[0].ToString(), s[1].ToString(), });
                        break;
                    case var str when diphthongs2.Contains(str):
                        modified.AddRange(new string[] { s[0].ToString(), s[1].ToString(), });
                        break;

                    default:
                        modified.Add(s);
                        break;
                }
            }
            return modified.ToArray();
        }

        protected override IG2p LoadBaseDictionary() {
            var g2ps = new List<IG2p>();
            // LOAD DICTIONARY FROM FOLDER
            string path = Path.Combine(PluginDir, "en_teto.yaml");
            if (!File.Exists(path)) {
                Directory.CreateDirectory(PluginDir);
                File.WriteAllBytes(path, EN_TETO.Data.Resources.en_teto_template);
            }
            // LOAD DICTIONARY FROM SINGER FOLDER
            if (singer != null && singer.Found && singer.Loaded) {
                string file = Path.Combine(singer.Location, "en_teto.yaml");
                if (File.Exists(file)) {
                    try {
                        g2ps.Add(G2pDictionary.NewBuilder().Load(File.ReadAllText(file)).Build());
                    } catch (Exception e) {
                        Log.Error(e, $"Failed to load {file}");
                    }
                }
            }
            g2ps.Add(G2pDictionary.NewBuilder().Load(File.ReadAllText(path)).Build());
            g2ps.Add(new ArpabetPlusG2p());
            return new G2pFallbacks(g2ps.ToArray());
        }
        protected override List<string> ProcessSyllable(Syllable syllable) {
            syllable.prevV = tails.Contains(syllable.prevV) ? "" : syllable.prevV;
            var prevV = syllable.prevV == "" ? "" : $"{syllable.prevV}";
            //string prevV = syllable.prevV;
            string[] cc = syllable.cc;
            string v = syllable.v;
            string vH = WanaKana.ToHiragana(v);
            string basePhoneme;
            var phonemes = new List<string>();
            var lastC = cc.Length - 1;
            var firstC = 0;

            string[] CurrentWordCc = syllable.CurrentWordCc;
            string[] PreviousWordCc = syllable.PreviousWordCc;
            int prevWordConsonantsCount = syllable.prevWordConsonantsCount;

            // Check for missing vowel phonemes
            foreach (var entry in missingVphonemes) {
                if (!HasOto(entry.Key, syllable.tone) && !HasOto(entry.Key, syllable.tone)) {
                    isMissingVPhonemes = true;
                    break;
                }
            }

            // Check for missing consonant phonemes
            foreach (var entry in missingCphonemes) {
                if (!HasOto(entry.Key, syllable.tone) && !HasOto(entry.Value, syllable.tone)) {
                    isMissingCPhonemes = true;
                    break;
                }
            }

            // Check for missing TIMIT phonemes
            foreach (var entry in timitphonemes) {
                if (!HasOto(entry.Key, syllable.tone) && !HasOto(entry.Value, syllable.tone)) {
                    isTimitPhonemes = true;
                    break;
                }
            }

            // STARTING V
            if (syllable.IsStartingV) {
                if (HasOto(AliasFormat(vH, "startingV", syllable.vowelTone, ""), syllable.vowelTone) || HasOto(ValidateAlias(AliasFormat(vH, "startingV", syllable.vowelTone, "")), syllable.vowelTone)) {
                    basePhoneme = AliasFormat(vH, "startingV", syllable.vowelTone, "");
                } else if (HasOto(vH, syllable.vowelTone) || HasOto(ValidateAlias(vH), syllable.vowelTone)) {
                    basePhoneme = AliasFormat(v, "startingV", syllable.vowelTone, "");
                } else {
                    basePhoneme = AliasFormat(v, "startingV", syllable.vowelTone, "");
                }
            }
            // [V V] or [V C][C V]/[V]
            else if (syllable.IsVV) {
                if (!CanMakeAliasExtension(syllable)) {
                    var vvH = ToHiragana(v, syllable.vowelTone);
                    basePhoneme = $"{prevV} {v}";

                    if (!HasOto(basePhoneme, syllable.vowelTone) && !HasOto(ValidateAlias(basePhoneme), syllable.vowelTone) && vvExceptions.ContainsKey(prevV) && prevV != v) {
                        // VV IS NOT PRESENT, CHECKS VVEXCEPTIONS LOGIC
                        //var vc = $"{prevV}{vvExceptions[prevV]}";
                        var vc = AliasFormat($"{vvExceptions[prevV]}", "vcEx", syllable.vowelTone, prevV);
                        phonemes.Add(vc);
                        var cvH = WanaKana.ToHiragana($"{vvExceptions[prevV]}{v}");
                        if (HasOto(cvH, syllable.vowelTone) || HasOto(ValidateAlias(cvH), syllable.vowelTone)) {
                            basePhoneme = cvH;
                        } else if (HasOto(AliasFormat($"{vvExceptions[prevV]} {v}", "dynMid", syllable.vowelTone, ""), syllable.vowelTone)) {
                            basePhoneme = AliasFormat($"{vvExceptions[prevV]} {v}", "dynMid", syllable.vowelTone, "");
                        }
                    } else if (HasOto($"{prevV} {vvH}", syllable.vowelTone) && HasOto(ValidateAlias($"{prevV} {vvH}"), syllable.vowelTone)) {
                        basePhoneme = $"{prevV} {vvH}";
                    } else {
                        if (HasOto($"{prevV} {v}", syllable.vowelTone) || HasOto(ValidateAlias($"{prevV} {v}"), syllable.vowelTone)) {
                            basePhoneme = AliasFormat($"{prevV} {v}", "dynMid_vv", syllable.vowelTone, "");
                            // CV
                        } else if (HasOto(ToHiragana($"{prevV}{v}", syllable.vowelTone), syllable.vowelTone) || HasOto(ValidateAlias(ToHiragana($"{prevV}{v}", syllable.vowelTone)), syllable.vowelTone) && !HasOto($"{prevV} {v}", syllable.vowelTone)) {
                            basePhoneme = ToHiragana($"{prevV}{v}", syllable.vowelTone);
                        } else if (HasOto(vvH, syllable.vowelTone) || HasOto(ValidateAlias(vvH), syllable.vowelTone)) {
                            basePhoneme = $"{prevV} {vvH}";
                        } else if (!(HasOto($"{prevV} {v}", syllable.vowelTone) || HasOto(ValidateAlias($"{prevV} {v}"), syllable.vowelTone))) {
                            basePhoneme = AliasFormat($"{v}", "dynMid", syllable.vowelTone, "");
                        } else {
                            basePhoneme = AliasFormat($"{prevV}", "ending", syllable.vowelTone, "");
                            TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{v}", "cc_end", syllable.vowelTone, ""));
                        }
                    }
                    // EXTEND AS [V V]
                } else if (HasOto($"{prevV} {v}", syllable.vowelTone) && HasOto(ValidateAlias($"{prevV} {v}"), syllable.vowelTone) || missingVphonemes.ContainsKey(prevV)) {
                    basePhoneme = $"{prevV} {v}";
                } else if (HasOto($"{prevV} {ToHiragana(v, syllable.vowelTone)}", syllable.vowelTone) && HasOto(ValidateAlias($"{prevV} {ToHiragana(v, syllable.vowelTone)}"), syllable.vowelTone)) {
                    basePhoneme = $"{prevV} {ToHiragana(v, syllable.vowelTone)}";
                } else if (HasOto($"{v}", syllable.vowelTone) && HasOto(ValidateAlias($"{v}"), syllable.vowelTone) || missingVphonemes.ContainsKey(prevV)) {
                    basePhoneme = v;
                } else {
                    // PREVIOUS ALIAS WILL EXTEND as [V V]
                    basePhoneme = null;
                }

                // [- CV/C V] or [- C][CV/C V]
            } else if (syllable.IsStartingCVWithOneConsonant) {
                var rcv = $"- {cc[0]}{v}";
                var rcv1 = $"- {cc[0]} {v}";
                var crv = $"{cc[0]} {v}";
                var cvH1 = AltCv.ContainsKey($"{cc[0]}{v}") ? AltCv[$"{cc[0]}{v}"] : $"{cc[0]}{v}";
                var rcvH = $"- {ToHiragana(cvH1, syllable.vowelTone)}";
                var cvH = $"{ToHiragana(cvH1, syllable.vowelTone)}";

                if ((HasOto(rcvH, syllable.vowelTone) || HasOto(ValidateAlias(rcvH), syllable.vowelTone))) {
                    basePhoneme = rcvH;
                } else if ((HasOto(rcv, syllable.vowelTone) || HasOto(ValidateAlias(rcv), syllable.vowelTone)) || (HasOto(rcv1, syllable.vowelTone) || HasOto(ValidateAlias(rcv1), syllable.vowelTone))) {
                    basePhoneme = AliasFormat($"{cc[0]} {v}", "dynStart", syllable.vowelTone, "");
                } else if ((HasOto(cvH, syllable.vowelTone) || HasOto(ValidateAlias(cvH), syllable.vowelTone))) {
                    basePhoneme = cvH;
                    TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc_start", syllable.vowelTone, ""));
                } else if ((HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone))) {
                    basePhoneme = AliasFormat($"{cc[0]} {v}", "dynMid", syllable.vowelTone, "");
                    TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc_start", syllable.vowelTone, ""));
                } else {
                    basePhoneme = AliasFormat($"{cc[0]} {v}", "dynMid", syllable.vowelTone, "");
                    TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc_start", syllable.vowelTone, ""));
                }
                // [CCV/CC V] or [C C] + [CV/C V]
            } else if (syllable.IsStartingCVWithMoreThanOneConsonant) {
                // TRY [- CCV]/[- CC V] or [- CC][CCV]/[CC V] or [- C][C C][C V]/[CV]
                var rccv = $"- {string.Join("", cc)} {v}";
                var rccv1 = $"- {string.Join("", cc)}{v}";
                var crv = $"{cc.Last()} {v}";
                var crv1 = $"{cc.Last()}{v}";
                var ccv = $"{string.Join("", cc)} {v}";
                var ccv1 = $"{string.Join("", cc)}{v}";
                var rccvH = $"- {ToHiragana($"{string.Join("", cc)}{v}", syllable.vowelTone)}";
                var ccvH = $"{ToHiragana($"{string.Join("", cc)}{v}", syllable.vowelTone)}";
                var cvH1 = AltCv.ContainsKey($"{cc.Last()}{v}") ? AltCv[$"{cc.Last()}{v}"] : $"{cc.Last()}{v}";
                var cvH = $"{ToHiragana(cvH1, syllable.vowelTone)}";
                var vcvEx = VCVException.ContainsKey($"{cc.Last()}") ? VCVException[$"{cc.Last()}"] : $"{cc.Last()}";
                var ucvH = TryVcv(vcvEx, cvH, syllable.tone);

                /// - CCV
                if (HasOto(rccvH, syllable.vowelTone) || HasOto(ValidateAlias(rccvH), syllable.vowelTone)) {
                    basePhoneme = rccvH;
                    lastC = 0;
                } else if (HasOto(rccv, syllable.vowelTone) || HasOto(ValidateAlias(rccv), syllable.vowelTone) || HasOto(rccv1, syllable.vowelTone) || HasOto(ValidateAlias(rccv1), syllable.vowelTone) && !ccvException.Contains(cc[0])) {
                    basePhoneme = AliasFormat($"{string.Join("", cc)} {v}", "dynStart", syllable.vowelTone, "");
                    lastC = 0;
                } else {
                    /// CCV and CV
                    if (HasOto(ccvH, syllable.vowelTone) || HasOto(ValidateAlias(ccvH), syllable.vowelTone)) {
                        basePhoneme = ccvH;
                        lastC = 0;
                    } else if (HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv), syllable.vowelTone) || HasOto(ccv1, syllable.vowelTone) || HasOto(ValidateAlias(ccv1), syllable.vowelTone) && !ccvException.Contains(cc[0])) {
                        basePhoneme = AliasFormat($"{string.Join("", cc)} {v}", "dynMid", syllable.vowelTone, "");
                        lastC = 0;
                    } else if (HasOto(ucvH, syllable.vowelTone) || HasOto(ValidateAlias(ucvH), syllable.vowelTone)) {
                        basePhoneme = ucvH;
                    } else if (HasOto(cvH, syllable.vowelTone) || HasOto(ValidateAlias(cvH), syllable.vowelTone)) {
                        basePhoneme = cvH;
                    } else if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone) || HasOto(crv1, syllable.vowelTone) || HasOto(ValidateAlias(crv1), syllable.vowelTone)) {
                        basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                    } else {
                        basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                    }
                    // TRY RCC [- CC]
                    for (var i = cc.Length; i > 1; i--) {
                        if (!ccvException.Contains(cc[0])) {
                            if (TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{string.Join("", cc.Take(i))}", "cc_start", syllable.vowelTone, ""))) {
                                firstC = i - 1;
                            }
                        }
                        break;
                    }
                    // [- C]
                    if (phonemes.Count == 0) {
                        //TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc_start", syllable.vowelTone, ""));
                        TryAddPhoneme(phonemes, syllable.tone, $"- {cc[0].Replace("_", "")}", ValidateAlias($"- {cc[0].Replace("_", "")}"));
                    }
                    // try [CC V] or [CCV]
                    for (var i = firstC; i < cc.Length - 1; i++) {
                        /// CCV
                        if (syllable.CurrentWordCc.Length >= 2 && !ccvException.Contains(cc[i])) {
                            if (HasOto(ccvH, syllable.vowelTone) || HasOto(ValidateAlias(ccvH), syllable.vowelTone)) {
                                basePhoneme = ccvH;
                                lastC = i;
                                break;
                            } else if (HasOto(ucvH, syllable.vowelTone) || HasOto(ValidateAlias(ucvH), syllable.vowelTone)) {
                                basePhoneme = ucvH;
                                //lastC = i;
                                //break;
                            } else if (HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv), syllable.vowelTone) || HasOto(ccv1, syllable.vowelTone) || HasOto(ValidateAlias(ccv1), syllable.vowelTone)) {
                                basePhoneme = AliasFormat($"{string.Join("", cc)} {v}", "dynMid", syllable.vowelTone, "");
                                lastC = i;
                                break;
                            }
                            /// C-Last V
                        } else if (syllable.CurrentWordCc.Length == 1) {
                            if (HasOto(ucvH, syllable.vowelTone) || HasOto(ValidateAlias(ucvH), syllable.vowelTone)) {
                                basePhoneme = ucvH;
                            } else if (HasOto(cvH, syllable.vowelTone) || HasOto(ValidateAlias(cvH), syllable.vowelTone)) {
                                basePhoneme = cvH;
                            } else {
                                basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                            }
                        }
                    }
                }
            } else { // VCV
                var crv = $"{cc.Last()} {v}";
                var cvH1 = AltCv.ContainsKey($"{cc.Last()}{v}") ? AltCv[$"{cc.Last()}{v}"] : $"{cc.Last()}{v}";
                var ccvH1 = AltCv.ContainsKey($"{string.Join("", cc)}{v}") ? AltCv[$"{string.Join("", cc)}{v}"] : $"{string.Join("", cc)}{v}";
                var cvH = $"{ToHiragana(cvH1, syllable.vowelTone)}";
                var vcvH = $"{prevV} {ToHiragana(cvH1, syllable.vowelTone)}";
                var vccvH = $"{prevV} {ToHiragana(ccvH1, syllable.vowelTone)}";
                var vcvEnd = $"{prevV}{cc[0]} {v}";
                var vcvEx = VCVException.ContainsKey($"{cc.Last()}") ? VCVException[$"{cc.Last()}"] : $"{cc.Last()}";
                var ucvH = TryVcv(vcvEx, cvH, syllable.tone);

                // Use regular JP VCV if the current word starts with one consonant and the previous word ends with none
                if (syllable.IsVCVWithOneConsonant && (HasOto(vcvH, syllable.vowelTone) || HasOto(ValidateAlias(vcvH), syllable.vowelTone)) && prevWordConsonantsCount == 0 && CurrentWordCc.Length == 1) {
                    basePhoneme = vcvH;
                    // Use end VCV if current word does not start with a consonant but the previous word does end with one
                } else if (syllable.IsVCVWithOneConsonant && prevWordConsonantsCount == 1 && CurrentWordCc.Length == 0 && (HasOto(vcvEnd, syllable.vowelTone) || HasOto(ValidateAlias(vcvEnd), syllable.vowelTone))) {
                    basePhoneme = vcvEnd;
                    // Use regular VCV if end VCV does not exist
                } else if (syllable.IsVCVWithOneConsonant && !HasOto(vcvEnd, syllable.vowelTone) && !HasOto(ValidateAlias(vcvEnd), syllable.vowelTone) && (HasOto(vcvH, syllable.vowelTone) || HasOto(ValidateAlias(vcvH), syllable.vowelTone))) {
                    basePhoneme = vcvH;
                    // VCV with multiple consonants, only for current word onset and null previous word ending
                    // TODO: multi-VCV for words ending with one or more consonants?
                } else if (syllable.IsVCVWithMoreThanOneConsonant && (HasOto(vccvH, syllable.vowelTone) || HasOto(ValidateAlias(vccvH), syllable.vowelTone))) {
                    basePhoneme = vccvH;
                    lastC = 0;
                } else {
                    /// CV
                    if (HasOto(ucvH, syllable.vowelTone) || HasOto(ValidateAlias(ucvH), syllable.vowelTone)) {
                        basePhoneme = ucvH;
                    } else if (HasOto(cvH, syllable.vowelTone) || HasOto(ValidateAlias(cvH), syllable.vowelTone)) {
                        basePhoneme = cvH;
                    } else {
                        basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                    }
                    // try [CC V] or [CCV]
                    for (var i = firstC; i < cc.Length - 1; i++) {
                        var ccv = $"{string.Join("", cc)} {v}";
                        var ccv1 = $"{string.Join("", cc)}{v}";
                        var ccvH = $"{ToHiragana($"{string.Join("", cc)}{v}", syllable.vowelTone)}";
                        /// CCV
                        if (syllable.CurrentWordCc.Length >= 2 && !ccvException.Contains(cc[i])) {
                            if (HasOto(ccvH, syllable.vowelTone) || HasOto(ValidateAlias(ccvH), syllable.vowelTone)) {
                                basePhoneme = ccvH;
                                lastC = i;
                                break;
                            } else if (HasOto(ucvH, syllable.vowelTone) || HasOto(ValidateAlias(ucvH), syllable.vowelTone)) {
                                basePhoneme = ucvH;
                                //lastC = i;
                                //break;
                            } else if (HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv), syllable.vowelTone) || HasOto(ccv1, syllable.vowelTone) || HasOto(ValidateAlias(ccv1), syllable.vowelTone)) {
                                basePhoneme = AliasFormat($"{string.Join("", cc)} {v}", "dynMid", syllable.vowelTone, "");
                                lastC = i;
                                break;
                            }
                            /// C-Last V
                        } else if (syllable.CurrentWordCc.Length == 1) {
                            if (HasOto(ucvH, syllable.vowelTone) || HasOto(ValidateAlias(ucvH), syllable.vowelTone)) {
                                basePhoneme = ucvH;
                            } else if (HasOto(cvH, syllable.vowelTone) || HasOto(ValidateAlias(cvH), syllable.vowelTone)) {
                                basePhoneme = cvH;
                            } else {
                                basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                            }
                        }
                    }
                    // try [V C], [V CC], [VC C], [V -][- C]
                    for (var i = lastC + 1; i >= 0; i--) {
                        var vr = $"{prevV} -";
                        var vr1 = $"{prevV} R";
                        var vc_c = $"{prevV}{string.Join(" ", cc.Take(2))}";
                        var vcc = $"{prevV} {string.Join("", cc.Take(2))}";
                        var vc = $"{prevV} {cc[0]}";
                        var vc_ = $"{prevV} {cc[0].Replace("_", "")}";
                        var vcH = $"{prevV} {ToHiragana($"{cc[0]}", syllable.vowelTone)}";
                        // Boolean Triggers
                        bool CCV = false;
                        if (syllable.CurrentWordCc.Length >= 2 && !ccvException.Contains(cc[0])) {
                            if (HasOto(AliasFormat($"{string.Join("", cc)} {v}", "dynMid", syllable.vowelTone, ""), syllable.vowelTone)) {
                                CCV = true;
                            }
                        }
                        if (i == 0 && (HasOto(vr1, syllable.tone) || HasOto(ValidateAlias(vr1), syllable.tone) || HasOto(vr, syllable.tone) || HasOto(ValidateAlias(vr), syllable.tone))) {
                            // Check if vc_ is found, if not, check vc
                            if (HasOto(vc_, syllable.tone)) {
                                TryAddPhoneme(phonemes, syllable.tone, vc_);
                            } else if (HasOto(vc, syllable.tone)) {
                                TryAddPhoneme(phonemes, syllable.tone, vc);
                            } else {
                                TryAddPhoneme(phonemes, syllable.tone, vr, vr1);
                            }
                            break;
                        } else if ((HasOto(vcc, syllable.tone) || HasOto(ValidateAlias(vcc), syllable.tone)) && CCV && !affricates.Contains(string.Join("", cc.Take(2)))) {
                            phonemes.Add(vcc);
                            firstC = 1;
                            break;
                            /*
                            } else if ((HasOto(vc_c, syllable.tone) || HasOto(ValidateAlias(vc_c), syllable.tone)) && !affricates.Contains(string.Join("", cc.Take(2)))) {
                                phonemes.Add(vc_c);
                                firstC = 1;
                                break;
                            */
                        } else if (cPV_FallBack && (!HasOto(crv, syllable.vowelTone) && !HasOto(ValidateAlias(crv), syllable.vowelTone))) {
                            TryAddPhoneme(phonemes, syllable.tone, vc, ValidateAlias(vc));
                            break;
                        } else if (HasOto(vcH, syllable.tone) || HasOto(ValidateAlias(vcH), syllable.tone)) {
                            phonemes.Add(vcH);
                            break;
                        } else if (HasOto(vc, syllable.tone) || HasOto(ValidateAlias(vc), syllable.tone)) {
                            phonemes.Add(vc);
                            break;
                        } else {
                            continue;
                        }

                    }
                }
            }

            for (var i = firstC; i < lastC; i++) {
                var ccv = $"{string.Join("", cc.Skip(i + 1))} {v}";
                var ccv1 = $"{string.Join("", cc.Skip(i + 1))}{v}";
                var cc1 = $"{string.Join(" ", cc.Skip(i))}";
                var lcv = $"{cc.Last()} {v}";
                var cv = $"{cc.Last()}{v}";
                var cvH1 = AltCv.ContainsKey($"{cc.Last()}{v}") ? AltCv[$"{cc.Last()}{v}"] : $"{cc.Last()}{v}";
                var ccvH1 = AltCv.ContainsKey($"{string.Join("", cc.Skip(i + 1))}{v}") ? AltCv[$"{string.Join("", cc.Skip(i + 1))}{v}"] : $"{string.Join("", cc.Skip(i + 1))}{v}";
                var c_cvH1 = AltCv.ContainsKey($"{string.Join(" ", cc.Skip(i + 1))}{v}") ? AltCv[$"{string.Join(" ", cc.Skip(i + 1))}{v}"] : $"{string.Join(" ", cc.Skip(i + 1))}{v}";
                var ccvH = $"{ToHiragana(ccvH1, syllable.vowelTone)}";
                var c_cvH = $"{ToHiragana(c_cvH1, syllable.vowelTone)}";
                var cvH = $"{ToHiragana(cvH1, syllable.vowelTone)}";
                var vcvEx = VCVException.ContainsKey($"{cc.Last()}") ? VCVException[$"{cc.Last()}"] : $"{cc.Last()}";
                var ucvH = TryVcv(vcvEx, cvH, syllable.tone);

                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1);
                }
                // [C1 C2] Combination
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = $"{cc[i]} {cc[i + 1]}";
                }
                // Validate alias after changes
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1);
                }
                // CC FALLBACKS (Only if all previous attempts fail)
                if (!HasOto(cc1, syllable.tone) &&
                    !HasOto(ValidateAlias(cc1), syllable.tone) &&
                    !HasOto($"{cc[i]} {cc[i + 1]}", syllable.tone)) {

                    // Fallback: [C1_]
                    cc1 = AliasFormat($"{cc[i]}", "cc_teto_end", syllable.vowelTone, "");
                }
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1);
                }
                // CCV
                if (syllable.CurrentWordCc.Length >= 2) {
                    if (HasOto(ccvH, syllable.vowelTone) || HasOto(ValidateAlias(ccvH), syllable.vowelTone)) {
                        basePhoneme = ccvH;
                        lastC = i;
                    } else if (HasOto(c_cvH, syllable.vowelTone) || HasOto(ValidateAlias(c_cvH), syllable.vowelTone)) {
                        basePhoneme = c_cvH;
                        lastC = i;
                    } else if (HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv), syllable.vowelTone) || HasOto(ccv1, syllable.vowelTone) || HasOto(ValidateAlias(ccv1), syllable.vowelTone) && !ccvException.Contains(cc[0])) {
                        basePhoneme = AliasFormat($"{string.Join("", cc.Skip(i + 1))} {v}", "dynMid", syllable.vowelTone, "");
                        lastC = i;
                    } else if (HasOto(ucvH, syllable.vowelTone) || HasOto(ValidateAlias(ucvH), syllable.vowelTone)) {
                        basePhoneme = ucvH;
                        //lastC = i;
                    } else if (HasOto(cv, syllable.vowelTone) || HasOto(ValidateAlias(cv), syllable.vowelTone) || HasOto(lcv, syllable.vowelTone) || HasOto(ValidateAlias(lcv), syllable.vowelTone) && HasOto(cc1, syllable.vowelTone) && !HasOto(ccv, syllable.vowelTone)) {
                        basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                    }
                    // [C1 C2C3]
                    if (HasOto($"{cc[i]} {string.Join("", cc.Skip(i + 1))}", syllable.tone)) {
                        cc1 = $"{cc[i]} {string.Join("", cc.Skip(i + 1))}";
                    }
                    // CV
                } else if (syllable.CurrentWordCc.Length == 1) {
                    if (HasOto(ucvH, syllable.vowelTone) || HasOto(ValidateAlias(ucvH), syllable.vowelTone)) {
                        basePhoneme = ucvH;
                    } else if (HasOto(cvH, syllable.vowelTone) || HasOto(ValidateAlias(cvH), syllable.vowelTone)) {
                        basePhoneme = cvH;
                    } else if (HasOto(AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, ""), syllable.vowelTone) || HasOto(ValidateAlias(AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "")), syllable.vowelTone)) {
                        basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                    }
                    // [C1 C2]
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = $"{cc[i]} {cc[i + 1]}";
                    }
                }

                if (i + 1 < lastC) {
                    var cc2 = $"{cc[i + 1]} {cc[i + 2]}";
                    if (!HasOto(cc2, syllable.tone)) {
                        cc2 = ValidateAlias(cc2);
                    }
                    // [C1 C2] Combination
                    if (!HasOto(cc2, syllable.tone)) {
                        cc2 = $"{cc[i + 1]} {cc[i + 2]}";
                    }
                    // Validate alias after changes
                    if (!HasOto(cc2, syllable.tone)) {
                        cc2 = ValidateAlias(cc2);
                    }
                    // CC FALLBACKS (Only if all previous attempts fail)
                    if (!HasOto(cc2, syllable.tone) &&
                        !HasOto(ValidateAlias(cc2), syllable.tone) &&
                        !HasOto($"{cc[i + 1]} {cc[i + 2]}", syllable.tone)) {

                        // Fallback: [C1_]
                        cc2 = AliasFormat($"{cc[i + 1]}", "cc_teto_end", syllable.vowelTone, "");
                    }
                    if (!HasOto(cc2, syllable.tone)) {
                        cc2 = ValidateAlias(cc2);
                    }
                    // CCV
                    if (syllable.CurrentWordCc.Length >= 2) {
                        if (HasOto(ccvH, syllable.vowelTone) || HasOto(ValidateAlias(ccvH), syllable.vowelTone)) {
                            basePhoneme = ccvH;
                            lastC = i;
                        } else if (HasOto(c_cvH, syllable.vowelTone) || HasOto(ValidateAlias(c_cvH), syllable.vowelTone)) {
                            basePhoneme = c_cvH;
                            lastC = i;
                        } else if (HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv), syllable.vowelTone) || HasOto(ccv1, syllable.vowelTone) || HasOto(ValidateAlias(ccv1), syllable.vowelTone) && !ccvException.Contains(cc[0])) {
                            basePhoneme = AliasFormat($"{string.Join("", cc.Skip(i + 1))} {v}", "dynMid", syllable.vowelTone, "");
                            lastC = i;
                        } else if (HasOto(ucvH, syllable.vowelTone) || HasOto(ValidateAlias(ucvH), syllable.vowelTone)) {
                            basePhoneme = ucvH;
                            //lastC = i;
                        } else if (HasOto(cv, syllable.vowelTone) || HasOto(ValidateAlias(cv), syllable.vowelTone) || HasOto(lcv, syllable.vowelTone) || HasOto(ValidateAlias(lcv), syllable.vowelTone) && HasOto(cc2, syllable.vowelTone) && !HasOto(ccv, syllable.vowelTone)) {
                            basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                        }
                        // [C1 C2C3]
                        if (HasOto($"{cc[i]} {string.Join("", cc.Skip(i + 1))}", syllable.tone)) {
                            cc1 = $"{cc[i]} {string.Join("", cc.Skip(i + 1))}";
                        }
                        // CV
                    } else if (syllable.CurrentWordCc.Length == 1) {
                        if (HasOto(ucvH, syllable.vowelTone) || HasOto(ValidateAlias(ucvH), syllable.vowelTone)) {
                            basePhoneme = ucvH;
                        } else if (HasOto(cvH, syllable.vowelTone) || HasOto(ValidateAlias(cvH), syllable.vowelTone)) {
                            basePhoneme = cvH;
                        } else if (HasOto(AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, ""), syllable.vowelTone) || HasOto(ValidateAlias(AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "")), syllable.vowelTone)) {
                            basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                        }
                        // [C1 C2]
                        if (!HasOto(cc2, syllable.tone)) {
                            cc1 = $"{cc[i]} {cc[i + 1]}";
                        }
                    }

                    if (HasOto(cc1, syllable.tone) && HasOto(cc1, syllable.tone) && !cc1.Contains($"{string.Join("", cc.Skip(i))}")) {
                        // like [V C1] [C1 C2] [C2 C3] [C3 ..]
                        phonemes.Add(cc1);
                    } else if (TryAddPhoneme(phonemes, syllable.tone, cc1)) {
                        // like [V C1] [C1 C2] [C2 ..]
                        if (cc1.Contains($"{string.Join(" ", cc.Skip(i + 1))}")) {
                            i++;
                        }
                    } else {
                        // like [V C1] [C1] [C2 ..]
                        TryAddPhoneme(phonemes, syllable.tone, cc[i], ValidateAlias(cc[i]));
                    }
                } else {
                    TryAddPhoneme(phonemes, syllable.tone, cc1);
                }
            }

            phonemes.Add(basePhoneme);
            return phonemes;
        }

        protected override List<string> ProcessEnding(Ending ending) {
            string prevV = ending.prevV;
            string[] cc = ending.cc;
            string v = ending.prevV;
            var phonemes = new List<string>();
            var lastC = cc.Length - 1;
            var firstC = 0;
            if (tails.Contains(ending.prevV)) {
                return new List<string>();
            }
            if (ending.IsEndingV) {
                var vR = $"{v} -";
                var vR1 = $"{v} R";
                var vR2 = $"{v}-";
                if (HasOto(vR, ending.tone) || HasOto(ValidateAlias(vR), ending.tone) || HasOto(vR1, ending.tone) || HasOto(ValidateAlias(vR1), ending.tone) || HasOto(vR2, ending.tone) || HasOto(ValidateAlias(vR2), ending.tone)) {
                    phonemes.Add(AliasFormat($"{v}", "ending", ending.tone, ""));
                }
            } else if (ending.IsEndingVCWithOneConsonant) {
                var vc = $"{v} {cc[0]}";
                var vcr = $"{v} {cc[0]}-";
                var vcr2 = $"{v}{cc[0]} -";
                if (!RomajiException.Contains(cc[0])) {
                    if (HasOto(vcr, ending.tone) || HasOto(ValidateAlias(vcr), ending.tone) || HasOto(vcr2, ending.tone) || HasOto(ValidateAlias(vcr2), ending.tone)) {
                        phonemes.Add(AliasFormat($"{v} {cc[0]}", "dynEnd", ending.tone, ""));
                    } else {
                        phonemes.Add(vc);
                        if (vc.Contains(cc[0])) {
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "ending", ending.tone, ""), ValidateAlias(AliasFormat($"{cc[0]}", "ending", ending.tone, "")));
                        }
                    }
                }
            } else {
                for (var i = lastC; i >= 0; i--) {
                    var vr = $"{v} -";
                    var vr1 = $"{v} R";
                    var vr2 = $"{v}-";
                    var vcc = $"{v} {string.Join("", cc.Take(2))}-";
                    var vcc2 = $"{v}{string.Join(" ", cc.Take(2))} -";
                    var vcc3 = $"{v}{string.Join(" ", cc.Take(2))}";
                    var vcc4 = $"{v} {string.Join("", cc.Take(2))}";
                    var vc = $"{v} {cc[0]}";
                    if (!RomajiException.Contains(cc[0])) {
                        if (i == 0) {
                            if (HasOto(vr, ending.tone) || HasOto(ValidateAlias(vr), ending.tone) || HasOto(vr2, ending.tone) || HasOto(ValidateAlias(vr2), ending.tone) || HasOto(vr1, ending.tone) || HasOto(ValidateAlias(vr1), ending.tone) && !HasOto(vc, ending.tone)) {
                                //phonemes.Add(AliasFormat($"{v}", "ending", ending.tone, ""));
                            }
                            break;
                        } else if ((HasOto(vcc, ending.tone) || HasOto(ValidateAlias(vcc), ending.tone)) && lastC == 1 && !ccvException.Contains(cc[0])) {
                            phonemes.Add(vcc);
                            firstC = 1;
                            break;
                        } else if ((HasOto(vcc2, ending.tone) || HasOto(ValidateAlias(vcc2), ending.tone)) && lastC == 1 && !ccvException.Contains(cc[0])) {
                            phonemes.Add(vcc2);
                            firstC = 1;
                            break;
                            /*
                            } else if (HasOto(vcc3, ending.tone) || HasOto(ValidateAlias(vcc3), ending.tone) && !ccvException.Contains(cc[0])) {
                                phonemes.Add(vcc3);
                                if (vcc3.EndsWith(cc.Last())) {
                                    if (consonants.Contains(cc.Last())) {
                                        TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc.Last()}", "ending", ending.tone, ""));
                                    }
                                }
                                firstC = 1;
                                break;
                            */
                        } else if (HasOto(vcc4, ending.tone) || HasOto(ValidateAlias(vcc4), ending.tone)) {
                            phonemes.Add(vcc4);
                            if (vcc4.EndsWith(cc.Last())) {
                                if (consonants.Contains(cc.Last())) {
                                    TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc.Last()}", "ending", ending.tone, ""));
                                }
                            }
                            break;
                        } else {
                            phonemes.Add(vc);
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc.Last()}", "ending", ending.tone, ""));
                            break;
                        }
                    }
                }
                for (var i = firstC; i < lastC; i++) {
                    var cc1 = $"{cc[i]} {cc[i + 1]}";
                    if (i < cc.Length - 2) {
                        var cc2 = $"{cc[i + 1]} {cc[i + 2]}";
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        if (!HasOto(cc2, ending.tone)) {
                            cc2 = ValidateAlias(cc2);
                        }
                        if (!HasOto(cc2, ending.tone) && !HasOto($"{cc[i + 1]} {cc[i + 2]}", ending.tone)) {
                            // [C1 -] [- C2]
                            cc2 = AliasFormat($"{cc[i + 2]}", "cc_teto_end", ending.tone, "");
                            //TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i + 1]}", "cc_end", ending.tone, ""), ValidateAlias(AliasFormat($"{cc[i + 1]}", "cc_end", ending.tone, "")));
                        }
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        if (HasOto(cc1, ending.tone) && (HasOto(cc2, ending.tone) || HasOto($"{cc[i + 1]} {cc[i + 2]}-", ending.tone) || HasOto(ValidateAlias($"{cc[i + 1]} {cc[i + 2]}-"), ending.tone))) {
                            // like [C1 C2][C2 ...]
                            phonemes.Add(cc1);
                        } else if ((HasOto(cc[i], ending.tone) || HasOto(ValidateAlias(cc[i]), ending.tone) && (HasOto(cc2, ending.tone) || HasOto($"{cc[i + 1]} {cc[i + 2]}-", ending.tone) || HasOto(ValidateAlias($"{cc[i + 1]} {cc[i + 2]}-"), ending.tone)))) {
                            // like [C1 C2-][C3 ...]
                            phonemes.Add(cc[i]);
                        } else if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i + 1]} {cc[i + 2]}-", ValidateAlias($"{cc[i + 1]} {cc[i + 2]}-"))) {
                            // like [C1 C2-][C3 ...]
                            i++;
                        } else if (TryAddPhoneme(phonemes, ending.tone, cc1, ValidateAlias(cc1))) {
                            i++;
                        } else if (!HasOto(cc1, ending.tone) && !HasOto($"{cc[i]} {cc[i + 1]}", ending.tone)) {
                            // [C1 -] [- C2]
                            TryAddPhoneme(phonemes, ending.tone, $"- {cc[i + 1]}", ValidateAlias($"- {cc[i + 1]}"));
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i + 1]}", "cc_end", ending.tone, ""), ValidateAlias(AliasFormat($"{cc[i + 1]}", "cc_end", ending.tone, "")));
                            i++;
                        } else {
                            // like [C1][C2 ...]
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i]}", "cc_teto_end", ending.tone, ""));
                            //TryAddPhoneme(phonemes, ending.tone, cc[i + 1], ValidateAlias(cc[i + 1]), $"{cc[i + 1]} -", ValidateAlias($"{cc[i + 1]} -"));
                            i++;
                        }
                    } else {
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = $"{cc[i]} {cc[i + 1]}";
                        }
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i]} {cc[i + 1]}-", ValidateAlias($"{cc[i]} {cc[i + 1]}-"))) {
                            // like [C1 C2-]
                            i++;
                        } else if (TryAddPhoneme(phonemes, ending.tone, cc1, ValidateAlias(cc1))) {
                            // like [C1 C2][C2 -]
                            TryAddPhoneme(phonemes, ending.tone, $"{cc[i + 1]} -", ValidateAlias($"{cc[i + 1]} -"), cc[i + 1], ValidateAlias(cc[i + 1]));
                            i++;
                        } else if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i]}{cc[i + 1]}", ValidateAlias($"{cc[i]}{cc[i + 1]}"))) {
                            // like [C1C2][C2 -]
                            TryAddPhoneme(phonemes, ending.tone, $"{cc[i + 1]} -", ValidateAlias($"{cc[i + 1]} -"), cc[i + 1], ValidateAlias(cc[i + 1]));
                            i++;
                        } else {
                            // like [C1][C2 -]
                            TryAddPhoneme(phonemes, ending.tone, cc[i], ValidateAlias(cc[i]), $"{cc[i]} -", ValidateAlias($"{cc[i]} -"));
                            TryAddPhoneme(phonemes, ending.tone, $"{cc[i + 1]} -", ValidateAlias($"{cc[i + 1]} -"), cc[i + 1], ValidateAlias(cc[i + 1]));
                            i++;
                        }
                    }
                }
            }
            return phonemes;
        }
        private string TryVcv(string vowel, string cv, int tone) {
            var vcv = $"{vowel} {cv}";
            return HasOto(vcv, tone) ? vcv : FixCv(cv, tone);
        }
        private string FixCv(string cv, int tone) {
            var alt = $"- {cv}";
            return HasOto(cv, tone) ? cv : HasOto(alt, tone) ? alt : cv;
        }
        private string ToHiragana(string alias, int tone) {
            // Check if the alias or its validated version has an OTO
            if (HasOto(WanaKana.ToHiragana(alias), tone) || HasOto(ValidateAlias(WanaKana.ToHiragana(alias)), tone)) {
                return WanaKana.ToHiragana(alias);
            }

            // Convert the alias to Hiragana
            var hiragana = WanaKana.ToHiragana(alias);

            // Apply specific character replacements
            hiragana = hiragana.Replace("ゔ", "ヴ");
            hiragana = hiragana.Replace("q", "-");

            // Return the modified Hiragana
            return hiragana;
        }

        private string AliasFormat(string alias, string type, int tone, string prevV) {
            var aliasFormats = new Dictionary<string, string[]> {
            // Define alias formats for different types
                { "dynStart", new string[] { "" } },
                { "dynMid", new string[] { "" } },
                { "dynMid_vv", new string[] { "" } },
                { "dynEnd", new string[] { "" } },
                { "startingV", new string[] { "-", "- ", "_", "" } },
                { "vcEx", new string[] { $"{prevV} ", $"{prevV}" } },
                { "vvExtend", new string[] { "", "_", "-", "- " } },
                { "cv", new string[] { "-", "", "- ", "_" } },
                { "cvStart", new string[] { "-", "- ", "_" } },
                { "consEn", new string[] { "_", "- ", "_" } },
                { "ending", new string[] { " R", "-", " -" } },
                { "ending_mix", new string[] { "-", " -", "R", " R", "_", "--" } },
                { "cc", new string[] { "", "-", "- ", "_" } },
                { "cc_start", new string[] { "- ", "-"} },
                { "cc_end", new string[] { " -", "-", "" } },
                { "cc_mix", new string[] { " -", " R", "-", "", "_", "- ", "-" } },
                { "cc1_mix", new string[] { "", " -", "-", " R", "_", "- ", "-" } },
                { "cc_teto", new string[] { "_", ""} },
                { "cc_teto_end", new string[] { "_", ""} }
            };

            // Check if the given type exists in the aliasFormats dictionary
            if (!aliasFormats.ContainsKey(type) && !type.Contains("dynamic")) {
                return alias;
            }

            // Handle dynamic variations when type contains "dynamic"
            if (type.Contains("dynStart")) {
                string consonant = "";
                string vowel = "";
                // If the alias contains a space, split it into consonant and vowel
                if (alias.Contains(" ")) {
                    var parts = alias.Split(' ');
                    consonant = parts[0];
                    vowel = parts[1];
                } else {
                    consonant = alias;
                }

                // Handle the alias with space and without space
                var dynamicVariations = new List<string> {
                    // Variations with space, dash, and underscore
                    $"- {consonant}{vowel}",        // "- CV"
                    $"- {consonant} {vowel}",       // "- C V"
                    $"-{consonant} {vowel}",        // "-C V"
                    $"-{consonant}{vowel}",         // "-CV"
                    $"-{consonant}_{vowel}",        // "-C_V"
                    $"- {consonant}_{vowel}",       // "- C_V"
                };
                // Check each dynamically generated format
                foreach (var variation in dynamicVariations) {
                    if (HasOto(variation, tone) || HasOto(ValidateAlias(variation), tone)) {
                        return variation;
                    }
                }
            }

            if (type.Contains("dynMid")) {
                string consonant = "";
                string vowel = "";
                // If the alias contains a space, split it into consonant and vowel
                if (alias.Contains(" ")) {
                    var parts = alias.Split(' ');
                    consonant = parts[0];
                    vowel = parts[1];
                } else {
                    consonant = alias;
                }
                var dynamicVariations1 = new List<string> {
                    $"{consonant}{vowel}",    // "CV"
                    $"{consonant} {vowel}",    // "C V"
                    $"{consonant}_{vowel}",    // "C_V"
                };
                // Check each dynamically generated format
                foreach (var variation1 in dynamicVariations1) {
                    if (HasOto(variation1, tone) || HasOto(ValidateAlias(variation1), tone)) {
                        return variation1;
                    }
                }
            }

            if (type.Contains("dynMid_vv")) {
                string consonant = "";
                string vowel = "";
                if (alias.Contains(" ")) {
                    var parts = alias.Split(' ');
                    consonant = parts[0];
                    vowel = parts[1];
                } else {
                    consonant = alias;
                }
                var dynamicVariations1 = new List<string> {
                    $"{consonant} {vowel}",    // "C V"
                    $"{consonant}{vowel}",    // "CV"
                    $"{consonant}_{vowel}",    // "C_V"
                };
                // Check each dynamically generated format
                foreach (var variation1 in dynamicVariations1) {
                    if (HasOto(variation1, tone) || HasOto(ValidateAlias(variation1), tone)) {
                        return variation1;
                    }
                }
            }

            if (type.Contains("dynEnd")) {
                string consonant = "";
                string vowel = "";
                // If the alias contains a space, split it into consonant and vowel
                if (alias.Contains(" ")) {
                    var parts = alias.Split(' ');
                    consonant = parts[1];
                    vowel = parts[0];
                } else {
                    consonant = alias;
                }
                var dynamicVariations1 = new List<string> {
                    $"{vowel}{consonant} -",    // "VC -"
                    $"{vowel} {consonant}-",    // "V C-"
                    $"{vowel}{consonant}-",    // "VC-"
                    $"{vowel} {consonant} -",    // "V C -"
                };
                // Check each dynamically generated format
                foreach (var variation1 in dynamicVariations1) {
                    if (HasOto(variation1, tone) || HasOto(ValidateAlias(variation1), tone)) {
                        return variation1;
                    }
                }
            }

            // Get the array of possible alias formats for the specified type if not dynamic
            var formatsToTry = aliasFormats[type];
            int counter = 0;
            foreach (var format in formatsToTry) {
                string aliasFormat;
                if (type.Contains("mix") && counter < 4) {
                    aliasFormat = (counter % 2 == 0) ? $"{alias}{format}" : $"{format}{alias}";
                    counter++;
                } else if (type.Contains("end") && !(type.Contains("dynEnd"))) {
                    aliasFormat = $"{alias}{format}";
                } else {
                    aliasFormat = $"{format}{alias}";
                }
                // Check if the formatted alias exists
                if (HasOto(aliasFormat, tone) || HasOto(ValidateAlias(aliasFormat), tone)) {
                    return aliasFormat;
                }
            }
            return alias;
        }

        protected override string ValidateAlias(string alias) {
            //CV FALLBACKS
            if (alias == "la") {
                return alias.Replace("la", "l aa");
            } else if (alias == "li") {
                return alias.Replace("li", "l iy");
            } else if (alias == "lu") {
                return alias.Replace("lu", "l uw");
            } else if (alias == "le") {
                return alias.Replace("le", "l eh");
            } else if (alias == "lo") {
                return alias.Replace("lo", "l ow");
            } else if (alias == "h er") {
                return alias.Replace("h", "hh");
            } else if (alias == "h u") {
                return alias.Replace("h u", "hh uw");
            } else if (alias == "- h") {
                return alias.Replace("h", "hh");
            } else if (alias == "ch r") {
                return alias.Replace("ch r", "ch er");
            } else if (alias == "j er") {
                return alias.Replace("j", "jh");
            } else if (alias == "jh r") {
                return alias.Replace("jh r", "jh er");
            } else if (alias == "- j") {
                return alias.Replace("j", "jh");
            }

            // VALIDATE ALIAS DEPENDING ON METHOD
            if (isMissingVPhonemes || isMissingCPhonemes || isTimitPhonemes) {
                foreach (var phoneme in missingVphonemes.Concat(missingCphonemes).Concat(timitphonemes)) {
                    alias = alias.Replace(phoneme.Key, phoneme.Value);
                }
            }

            var CVMappings = new Dictionary<string, string[]> {
                { "ao", new[] { "ow" } },
                { "oy", new[] { "ow" } },
                { "aw", new[] { "ah" } },
                { "ay", new[] { "ah" } },
                { "eh", new[] { "ae" } },
                { "ey", new[] { "eh" } },
                { "ow", new[] { "ao" } },
                { "uh", new[] { "uw" } },

            };
            foreach (var kvp in CVMappings) {
                var v1 = kvp.Key;
                var vfallbacks = kvp.Value;
                foreach (var vfallback in vfallbacks) {
                    foreach (var c1 in consonants) {
                        alias = alias.Replace(c1 + " " + v1, c1 + " " + vfallback);
                    }
                }
            }

            //VV (diphthongs) some
            var vvReplacements = new Dictionary<string, List<string>> {
                { "ay ay", new List<string> { "y ah" } },
                { "ey ey", new List<string> { "iy ey" } },
                { "oy oy", new List<string> { "y ow" } },
                { "er er", new List<string> { "er" } },
                { "aw aw", new List<string> { "w ae" } },
                { "ow ow", new List<string> { "w ao" } },
                { "uw uw", new List<string> { "w uw" } }
            };

            // Apply VV replacements
            foreach (var (originalValue, replacementOptions) in vvReplacements) {
                foreach (var replacementOption in replacementOptions) {
                    alias = alias.Replace(originalValue, replacementOption);
                }
            }
            //VC (diphthongs)

            //VC (R specific)
            if (alias == "a r") {
                return alias.Replace("a", "aa");
            }
            if (alias == "e r") {
                return alias.Replace("e", "eh");
            }
            if (alias == "i r") {
                return alias.Replace("i", "iy");
            }
            if (alias == "o r") {
                return alias.Replace("o", "ao");
            }
            if (alias == "u r") {
                return alias.Replace("u", "uh");
            }

            //VC (L specific)
            if (alias == "a l") {
                return alias.Replace("a", "aa");
            }
            if (alias == "e l") {
                return alias.Replace("e", "eh");
            }
            if (alias == "i l") {
                return alias.Replace("i", "iy");
            }
            if (alias == "o l") {
                return alias.Replace("o", "ao");
            }
            if (alias == "u l") {
                return alias.Replace("u", "uw");
            }

            //CV (n specific)
            if (alias == "n a") {
                return alias.Replace("a", $"{ToHiragana("a", 0)}");
            }
            if (alias == "n i") {
                return alias.Replace("i", $"{ToHiragana("i", 0)}");
            }
            if (alias == "n u") {
                return alias.Replace("u", $"{ToHiragana("u", 0)}");
            }
            if (alias == "n e") {
                return alias.Replace("e", $"{ToHiragana("e", 0)}");
            }
            if (alias == "n o") {
                return alias.Replace("o", $"{ToHiragana("o", 0)}");
            }

            //- V
            if (alias == "- a") {
                return alias.Replace("a", $"{ToHiragana("a", 0)}");
            }
            if (alias == "- i") {
                return alias.Replace("i", $"{ToHiragana("i", 0)}");
            }
            if (alias == "- u") {
                return alias.Replace("u", $"{ToHiragana("u", 0)}");
            }
            if (alias == "- e") {
                return alias.Replace("e", $"{ToHiragana("e", 0)}");
            }
            if (alias == "- o") {
                return alias.Replace("o", $"{ToHiragana("o", 0)}");
            }
            if (alias == "q a") {
                return alias.Replace("q a", $"- {ToHiragana("a", 0)}");
            }
            if (alias == "q- i") {
                return alias.Replace("q i", $"- {ToHiragana("i", 0)}");
            }
            if (alias == "q u") {
                return alias.Replace("q u", $"- {ToHiragana("u", 0)}");
            }
            if (alias == "q e") {
                return alias.Replace("q e", $"- {ToHiragana("e", 0)}");
            }
            if (alias == "q o") {
                return alias.Replace("q o", $"- {ToHiragana("o", 0)}");
            }

            //VC (dx specific)
            if (alias == "a dx") {
                return alias.Replace("dx", "r");
            }
            if (alias == "e dx") {
                return alias.Replace("dx", "r");
            }
            if (alias == "i dx") {
                return alias.Replace("dx", "r");
            }
            if (alias == "o dx") {
                return alias.Replace("dx", "r");
            }
            if (alias == "u dx") {
                return alias.Replace("dx", "r");
            }

            bool ccSpecific = true;
            if (ccSpecific) {

                //CC (ng)
                foreach (var c1 in new[] { "ng" }) {
                    foreach (var c2 in consonants) {
                        alias = alias.Replace(c1 + " " + c2, "n" + " " + c2);
                    }
                }
                // CC (r C)
                foreach (var c2 in consonants) {
                    if (!(alias.Contains($"aw {c2}") || alias.Contains($"ew {c2}") || alias.Contains($"ow {c2}") || alias.Contains($"uw {c2}"))) {
                        alias = alias.Replace($"r {c2}", $"er {c2}");
                    }
                }
                // CC (C r)
                foreach (var c2 in consonants) {
                    if (!(alias.Contains($"aw {c2}") || alias.Contains($"ew {c2}") || alias.Contains($"ow {c2}") || alias.Contains($"uw {c2}"))) {
                        alias = alias.Replace($"{c2} r", $"{c2} er");

                    }
                }
                // CC (w C)
                foreach (var c2 in consonants) {
                    if (!(alias.Contains($"aw {c2}") || alias.Contains($"ew {c2}") || alias.Contains($"iw {c2}") || alias.Contains($"ow {c2}") || alias.Contains($"uw {c2}"))) {
                        alias = alias.Replace($"w {c2}", $"u {c2}");
                    }
                }
                // CC (C w)
                foreach (var c2 in consonants) {
                    if (!(alias.Contains($"aw {c2}") || alias.Contains($"ew {c2}") || alias.Contains($"iw {c2}") || alias.Contains($"ow {c2}") || alias.Contains($"uw {c2}"))) {
                        alias = alias.Replace($"{c2} w", $"{c2} uw");
                    }
                }
                if (alias == "w -") {
                    return alias.Replace("w", "uw");
                }

                //CC (y C)
                foreach (var c2 in consonants) {
                    if (!(alias.Contains($"ay {c2}") || alias.Contains($"ey {c2}") || alias.Contains($"iy {c2}") || alias.Contains($"oy {c2}"))) {
                        alias = alias.Replace($"y {c2}", $"i {c2}");
                    }
                }
                //CC (C y)
                foreach (var c2 in consonants) {
                    if (!(alias.Contains($"ay {c2}") || alias.Contains($"ey {c2}") || alias.Contains($"iy {c2}") || alias.Contains($"oy {c2}"))) {
                        alias = alias.Replace($"{c2} y", $"{c2} y");
                    }
                }
                if (alias == "y -") {
                    return alias.Replace("y", "iy");
                }
                //CC (C R)
                foreach (var c2 in consonants) {
                    if (!(alias.Contains($"ay {c2}") || alias.Contains($"ey {c2}") || alias.Contains($"iy {c2}") || alias.Contains($"oy {c2}"))) {
                        alias = alias.Replace($"{c2} R", $"{c2} -");
                    }
                }
                //CC (a -)
                foreach (var c2 in vowels) {
                    alias = alias.Replace($"{c2} -", $"{c2} R");
                }

            }

            //VC's
            foreach (var v1 in vcFallBacks) {
                foreach (var c1 in consonants) {
                    alias = alias.Replace(v1.Key + " " + c1, v1.Value + " " + c1);
                }
            }

            // glottal
            foreach (var v1 in vowels) {
                if (!alias.Contains("cl " + v1) || !alias.Contains("q " + v1)) {
                    alias = alias.Replace("q " + v1, "- " + v1);
                }
            }
            foreach (var c2 in consonants) {
                if (!alias.Contains(c2 + " cl") || !alias.Contains(c2 + " q")) {
                    alias = alias.Replace(c2 + " q", $"{c2} -");
                }
            }
            foreach (var c2 in consonants) {
                if (!alias.Contains("cl " + c2) || !alias.Contains("q " + c2)) {
                    alias = alias.Replace("q " + c2, "- " + c2);
                }
            }

            return base.ValidateAlias(alias);
        }


        protected override double GetTransitionBasicLengthMs(string alias = "") {
            //I wish these were automated instead :')
            double transitionMultiplier = 1.0; // Default multiplier
            bool isEndingConsonant = false;
            bool isEndingVowel = false;
            bool hasCons = false;
            bool haslr = false;
            var excludedVowels = new List<string> { "a", "e", "i", "o", "u" };
            var GlideVCCons = new List<string> { $"{excludedVowels} {connectingGlides}" };
            var NormVCCons = new List<string> { $"{excludedVowels} {connectingNormCons}" };
            var arpabetFirstVDiphthong = new List<string> { "a", "e", "i", "o", "u" };
            var excludedEndings = new List<string> { $"{arpabetFirstVDiphthong}y -", $"{arpabetFirstVDiphthong}w -", $"{arpabetFirstVDiphthong}r -", };

            foreach (var c in longConsonants) {
                if (alias.Contains(c) && !alias.StartsWith(c) && !alias.Contains("ng -")) {
                    return base.GetTransitionBasicLengthMs() * 2.5;
                }
            }

            foreach (var c in normalConsonants) {
                foreach (var v in normalConsonants) {
                    if (alias.Contains(c) && !alias.StartsWith(c) &&
                    !alias.Contains("dx") && !alias.Contains($"{c} -")) {
                        if ("b,d,g,k,p,t".Split(',').Contains(c)) {
                            hasCons = true;
                        } else if ("l,r".Split(',').Contains(c)) {
                            haslr = true;
                        }
                    }
                }
            }

            foreach (var c in connectingNormCons) {
                foreach (var v in vowels.Except(excludedVowels)) {
                    if (alias.Contains(c) && !alias.Contains("- ") && alias.Contains($"{v} {c}")
                       && !alias.Contains("dx")) {
                        return base.GetTransitionBasicLengthMs() * 2.0;
                    }
                }
            }

            foreach (var c in tapConsonant) {
                foreach (var v in vowels) {
                    if (alias.Contains($"{v} {c}") || alias.Contains(c)) {
                        return base.GetTransitionBasicLengthMs() * 0.5;
                    }
                }
            }

            foreach (var c in affricates) {
                if (alias.Contains(c) && !alias.StartsWith(c)) {
                    return base.GetTransitionBasicLengthMs() * 1.5;
                }
            }

            foreach (var c in connectingGlides) {
                foreach (var v in vowels.Except(excludedVowels)) {
                    if (alias.Contains($"{v} {c}") && !alias.Contains($"{c} -") && !alias.Contains($"{v} -")) {
                        return base.GetTransitionBasicLengthMs() * 2.3;
                    }
                }
            }

            foreach (var c in connectingGlides) {
                foreach (var v in vowels.Where(v => excludedVowels.Contains(v))) {
                    if (alias.Contains($"{v} r")) {
                        return base.GetTransitionBasicLengthMs() * 2.0;

                    }
                }
            }

            foreach (var c in semilongConsonants) {
                foreach (var v in semilongConsonants.Except(excludedEndings)) {
                    if (alias.Contains(c) && !alias.StartsWith(c) && !alias.Contains($"{c} -") && !alias.Contains($"- q")) {
                        return base.GetTransitionBasicLengthMs() * 1.5;
                    }
                }
            }

            foreach (var c in semiVowels) {
                foreach (var v in semilongConsonants.Except(excludedEndings)) {
                    if (alias.Contains(c) && !alias.StartsWith(c) && !alias.Contains($"{c} -") && !alias.EndsWith(c)) {
                        return base.GetTransitionBasicLengthMs() * 1.5;
                    }
                }
            }
            foreach (var cc in semiVowels) {
                foreach (var v in vowels) {
                    if (alias.EndsWith(cc) && !alias.Contains($"{v} {cc}") && !alias.Contains($"_{cc}")) {
                        return base.GetTransitionBasicLengthMs() * 0.7;
                    }
                }
            }

            if (hasCons) {
                return base.GetTransitionBasicLengthMs() * 1.3; // Value for 'cons'
            } else if (haslr) {
                return base.GetTransitionBasicLengthMs() * 1.7; // Value for 'cons'
            }

            // Check if the alias ends with a consonant or vowel
            foreach (var c in consonants) {
                if (alias.Contains($"{c} -") || alias.Contains($"{c} R")) {
                    isEndingConsonant = true;
                    break;
                }
            }

            foreach (var v in vowels) {
                if (alias.Contains(v) && alias.Contains($"{v} -") || alias.Contains($"{v} R")) {
                    isEndingVowel = true;
                    break;
                }
            }



            // If the alias ends with a consonant or vowel, return 0.5 ms
            if (isEndingConsonant || isEndingVowel) {
                return base.GetTransitionBasicLengthMs() * 0.5;
            }

            return base.GetTransitionBasicLengthMs() * transitionMultiplier;
        }
    }
}
