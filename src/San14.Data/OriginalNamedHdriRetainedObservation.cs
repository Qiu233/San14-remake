using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace San14.Data;

/// <summary>The twelve current retained extents of one observed persistent
/// Game+F0 CPU allocation. These are observed contents, not initial stack
/// values, native defaults, or a selected Main/post-modifier package.</summary>
public sealed class OriginalNamedHdriRetainedObservation
{
    public const string ReadPlanRevision = "20261003-retained210-no-q-v3";
    public const string ReadPlanPath = "docs/.local/ce-named-hdri-retained-v3-20261003/READ_PLAN_V3.json";
    public const string ReadPlanSha256 = "AEBB71014429F1B792FC62C1AA26AEC54C52C2AC7EFBF71AD44A4CB5FF83644A";
    public const string ReaderSha256 = "79D9F953FBB4B8A9048F2A2121C0F1EF3B1ED992A2D63C1A3EB638B866310F67";
    private const long ProcessCreation = 134352672663205194;
    private const string ObservedStatus = "Game-default-exact-named-camera-HDRI-base-source-observed";
    private static readonly (int Offset, int Length)[] Layout =
    [
        (0x164,76),(0x2BC,4),(0xAD5,3),(0xB6F,1),(0xB95,3),(0xDC9,3),
        (0xDD5,3),(0xDE1,3),(0xE01,3),(0xF21,71),(0x1090,32),(0x10B8,8)
    ];
    private readonly byte[][] bytes;
    public string EvidenceUri { get; }
    public string EvidenceSha256 { get; }
    public string Locale { get; }
    public string ExecutableSha256 { get; }
    public string ObservedPersistentAllocationIdentity { get; }
    public string RetainedSha256 { get; }
    public int ObservedTotalReadBytes { get; }
    public int RetainedByteCount => 210;

    private OriginalNamedHdriRetainedObservation(string uri, string sha, string locale,
        string executable, string observedOwner, byte[][] current, int readBytes)
    {
        EvidenceUri = uri; EvidenceSha256 = sha; Locale = locale;
        ExecutableSha256 = executable; ObservedPersistentAllocationIdentity = observedOwner;
        bytes = current; ObservedTotalReadBytes = readBytes;
        RetainedSha256 = Hash(current.SelectMany(value => value).ToArray());
    }

    /// <summary>Reads a saved V3 report with a caller-pinned whole-file hash.
    /// No original installation/process/paths in the report are accessed.</summary>
    public static OriginalNamedHdriRetainedObservation ReadSavedV3(
        ReadOnlySpan<byte> savedJson, string evidenceUri, string expectedEvidenceSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceUri);
        string evidenceSha = Hash(savedJson);
        Require(IsSha(expectedEvidenceSha256) && evidenceSha.Equals(expectedEvidenceSha256,
            StringComparison.OrdinalIgnoreCase), "Saved retained evidence hash differs.");
        try
        {
            using var document = JsonDocument.Parse(savedJson.ToArray());
            JsonElement root = document.RootElement;
            Require(!root.TryGetProperty("fixtureOnly", out var fixture) || !fixture.GetBoolean(),
                "Synthetic fixture is not original observation evidence.");
            Require(I(root,"schemaVersion") == 3 && S(root,"readPlanRevision") == ReadPlanRevision &&
                S(root,"readPlanPath") == ReadPlanPath && S(root,"scriptSha256") == ReaderSha256,
                "Saved evidence requires the separately frozen V3 reader/plan.");
            Require(B(root,"captureAccepted") && B(root,"stableAcrossTwoReads") &&
                I(root,"completedSampleCount") == 2 && S(root,"status") == ObservedStatus,
                "Two complete accepted stable source samples are required.");
            Require(I(root,"processId") == 45800 && root.GetProperty("confirmedProcessCreationFileTime").GetInt64() == ProcessCreation &&
                I(root,"sampleByteLimit") == 16384 && I(root,"processByteLimit") == 32768 &&
                I(root,"maximumDistinctTreeNodes") == 15 && I(root,"maximumCaptureAttempts") == 1 &&
                I(root,"retries") == 0 && !B(root,"includeOptionalQGate") &&
                I(root,"payloadBytesPerCompleteSample") == 570 && I(root,"additionalPayloadBytesPerCompleteSample") == 134 &&
                I(root,"retainedByteCount") == 210 && I(root,"retainedExtentCount") == 12,
                "Saved source bounds, opt-in scope or process identity changed.");
            CheckClaims(root);
            var locks = root.GetProperty("lockedFilesSha256");
            Require(S(locks,ReadPlanPath) == ReadPlanSha256 &&
                S(locks,"docs/.local/ce-named-hdri-doclock-20261003/REVISED_SCRIPT_V2.py") ==
                    "ED9C331C36F97E8A00DBE1DBCEDA3809989CB18E0F5C399CF789C170EC20E018" &&
                S(locks,"docs/.local/ce-named-hdri-doclock-20261003/READ_PLAN_V2.json") ==
                    "2613DCBF764FBC2BE071BDD02E294582D69941C83A8D8AC8DFDA5DE81056A077",
                "Frozen V2/V3 gate locks changed.");
            foreach (string locale in new[] { "TC", "SC" })
                Require(S(locks,$"docs/.local/ce-main-light-construction-loader-20261002/{locale}-source-null-light-write-mask.bin") ==
                    "1D58B0671BDC0CC22C2CB297868AC30CD0D49116A1C13AA46240390AE9F65843",
                    "Paired constructor retained-mask locks changed.");
            string lang = S(root,"locale");
            var profile = lang switch
            {
                "TC" => (Executable: "0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464", Factory: 23852624UL, Cpu: 23851536UL, Camera: 23806688UL),
                "SC" => (Executable: "CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88", Factory: 23855864UL, Cpu: 23854776UL, Camera: 23809936UL),
                _ => throw new InvalidDataException("Unsupported retained observation locale.")
            };
            Require(S(root,"executableSha256") == profile.Executable, "Observed executable profile differs.");
            JsonElement first = root.GetProperty("firstSample"), second = root.GetProperty("secondSample");
            Require(EqualSample(first,second), "Saved sample identity or current bytes differ.");
            byte[][] one = ReadSample(first,profile), two = ReadSample(second,profile);
            Require(one.Zip(two).All(pair => pair.First.AsSpan().SequenceEqual(pair.Second)), "Retained bytes differ between samples.");
            JsonElement identity = first.GetProperty("processIdentity");
            var checks = root.GetProperty("processIdentityChecks").EnumerateArray().ToArray();
            Require(checks.Length == 4, "Complete before/after identity checks are missing.");
            for (int i=0; i<checks.Length; i++)
            {
                Require(I(checks[i],"sampleOrdinal") == i/2 && S(checks[i],"phase") ==
                    (i%2 == 0 ? "process-identity-before" : "process-identity-after"), "Process identity check order differs.");
                foreach (var field in identity.EnumerateObject())
                    Require(JsonEqual(field.Value,checks[i].GetProperty(field.Name)), "Before/after process identity drift.");
            }
            var metadata = root.GetProperty("sampleMetadata").EnumerateArray().ToArray();
            Require(metadata.Length == 2, "Sample read accounting missing.");
            int total = 0;
            for (int i=0; i<2; i++)
            {
                int traceBytes = (i==0 ? first : second).GetProperty("readTrace").EnumerateArray().Sum(entry => I(entry,"byteLength"));
                Require(B(metadata[i],"completed") && I(metadata[i],"sampleOrdinal") == i &&
                    I(metadata[i],"bytesRead") == traceBytes && traceBytes <= 16384 &&
                    I(metadata[i],"distinctTreeNodes") == I(i==0 ? first : second,"distinctTreeNodes"),
                    "Complete sample read budget/accounting differs.");
                total = checked(total + traceBytes);
            }
            Require(total == I(root,"processBytesRead") && total <= 32768, "Complete pair read accounting differs.");
            string owner = $"observed-persistent-GameF0:pid=45800;creation={ProcessCreation};exe={profile.Executable};Data={S(first,"gameRetainedHdrData")};CPU={S(first,"sourceCpu")};L={S(first,"sourceLightData")};evidence={evidenceSha}";
            return new(evidenceUri,evidenceSha,lang,profile.Executable,owner,one,total);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or FormatException or InvalidOperationException or OverflowException)
        { throw new InvalidDataException("Malformed saved V3 retained observation.",error); }
    }

    /// <summary>Explicitly copies observed contents into a new allocation owned
    /// by this program. The revision names that new owner, not an original
    /// source construction time or original allocation generation.</summary>
    public OriginalMapSourceCreatedLightingRetainedAllocationInputs CopyToOwnedTemplateInputs(
        string newOwnedAllocationIdentity, long newOwnedAllocationRevision)
    {
        Require(!string.IsNullOrWhiteSpace(newOwnedAllocationIdentity) &&
            newOwnedAllocationIdentity != ObservedPersistentAllocationIdentity && newOwnedAllocationRevision > 0,
            "Copied retained template requires a new explicit owner and positive revision.");
        var ranges = Layout.Select((extent,index) => new OriginalMapSourceCreatedLightingRetainedByteSource(
            extent.Offset,bytes[index].ToArray(),
            $"explicit-copy-of-observed-current-persistent-contents:{ObservedPersistentAllocationIdentity};uri={EvidenceUri};extent={extent.Offset:X4}/{extent.Length};sha256={Hash(bytes[index])};original-initial-stack-values-unproven;cross-restart-defaults-unproven;intervening-writers-unproven;selected-Main-package-unproven")).ToArray();
        return new(newOwnedAllocationIdentity,newOwnedAllocationRevision,Array.AsReadOnly(ranges));
    }

    public OriginalMapSourceCreatedLightingRetainedAllocation CaptureOwnedTemplate(
        LoadedOriginalMainLightBindingSource originalSource, string newOwnedAllocationIdentity,
        long newOwnedAllocationRevision, uint contextFlags3E4) =>
        OriginalMapSourceCreatedLightingRetainedAllocation.CaptureGameDefault(originalSource,
            CopyToOwnedTemplateInputs(newOwnedAllocationIdentity,newOwnedAllocationRevision),contextFlags3E4);

    private static byte[][] ReadSample(JsonElement sample,
        (string Executable, ulong Factory, ulong Cpu, ulong Camera) profile)
    {
        CheckClaims(sample);
        Require(S(sample,"status") == ObservedStatus && B(sample,"sourcePrefixRead") &&
            B(sample,"sourceGameOwnerObserved") && B(sample,"exactNamedViewSourceCameraPairsVerified") &&
            B(sample,"typedSourceIdentityVerified") && S(sample,"sourceClassId") == "66ECB240" &&
            I(sample,"sourceCpuAllocatedBytesProven") == 0x2230 &&
            S(sample,"sourceResourceUriFromStaticRetentionPath") == "san14://rdb/KIDSSystemResource/43AE6F41" &&
            S(sample,"sourceObjectSelectorFromStaticRetentionPath") == "9E08F950" && !B(sample,"sourceKeyRuntimeRead") &&
            !sample.TryGetProperty("optionalNode166ViewBaseMetadata",out _), "Saved typed allocation identity differs.");
        JsonElement identity = sample.GetProperty("processIdentity");
        Require(I(identity,"processId") == 45800 && identity.GetProperty("processCreationFileTime").GetInt64() == ProcessCreation &&
            I(identity,"activeExitCode") == 259 && S(identity,"executableSha256") == profile.Executable,
            "Saved process creation/image identity differs.");
        ulong module = Address(S(identity,"moduleBase")), cpu = Address(S(sample,"sourceCpu")),
            light = Address(S(sample,"sourceLightData")), data = Address(S(sample,"gameRetainedHdrData")),
            factory = Address(S(sample,"sourceFactory"));
        Require(light == cpu+0xA0 && Address(S(sample,"sourceFactoryVtable")) == module+profile.Factory &&
            Address(S(sample,"sourceCpuVtable")) == module+profile.Cpu, "Source CPU/type/range bounds differ.");
        var traces = sample.GetProperty("readTrace").EnumerateArray().ToArray();
        var reads = traces.GroupBy(entry => (Address(S(entry,"address")),I(entry,"byteLength")))
            .ToDictionary(group => group.Key,group => group.ToArray());
        foreach (var (key,pair) in reads)
        {
            Require(pair.Length == 2 && S(pair[0],"phase") == "typed-source-read" &&
                S(pair[1],"phase") == "final-identity-check" && S(pair[0],"label") == S(pair[1],"label"),
                "Every read needs its one exact full identity reread.");
            byte[] raw = Raw(pair[0],key.Item2);
            Require(raw.AsSpan().SequenceEqual(Raw(pair[1],key.Item2)), "Read/reread bytes drifted.");
            Require(key.Item2 > 0 && key.Item1+(ulong)key.Item2 <= 0x800000000000UL, "Invalid bounded trace range.");
        }
        Require(reads.Keys.Count(key => key.Item2 == 448) == I(sample,"distinctTreeNodes") &&
            I(sample,"distinctTreeNodes") is >= 1 and <= 15, "Source tree count exceeds V3 bound.");
        byte[] Trace(ulong address, int count)
        {
            Require(reads.TryGetValue((address,count),out var pair), $"Exact source read missing at {address:X}/{count}.");
            return Raw(pair![0],count);
        }
        ulong Q(ulong address) => BinaryPrimitives.ReadUInt64LittleEndian(Trace(address,8));
        Require(Q(Address(S(sample,"gameOwner"))+0xF0) == data && Q(factory) == module+profile.Factory &&
            Q(cpu) == module+profile.Cpu, "Source owner/type trace differs.");
        byte[] factoryCpu = Trace(data+0x18,16);
        Require(BinaryPrimitives.ReadUInt64LittleEndian(factoryCpu) == factory &&
            BinaryPrimitives.ReadUInt64LittleEndian(factoryCpu.AsSpan(8)) == cpu, "Observed Data factory/CPU pair differs.");
        Require(S(sample.GetProperty("namedView"),"key") == "E1B6A687", "Named View differs.");
        var cameras = sample.GetProperty("setterTargetCameras").EnumerateArray().ToArray();
        Require(cameras.Length == 2, "Exact Camera3/4 identities missing.");
        for (int i=0; i<2; i++)
        {
            int slot=i+3; string key=i==0 ? "1F3FB203" : "937EEA62";
            var named=sample.GetProperty($"namedCamera{slot}"); var camera=cameras[i];
            ulong cameraData=Address(S(camera,"data")), cameraCpu=Address(S(camera,"cpu"));
            Require(I(camera,"slot") == slot && S(camera,"namedKey") == key && S(named,"key") == key &&
                Address(S(named,"data")) == cameraData && Address(S(sample,$"sourceCamera{slot}Data")) == cameraData &&
                Q(Address(S(sample,"viewCpu"))+(i==0 ? 0x48UL : 0x58UL)) == cameraData &&
                Q(cameraData+0x20) == cameraCpu && Q(cameraCpu) == module+profile.Camera,
                "Exact named View/Camera CPU identity differs.");
            ulong retained=Q(cameraCpu+0x2CD8);
            Require(S(camera,"retainedHdrData") == $"0x{retained:X}" &&
                (retained == data ? B(camera,"equalsGameDefaultData") && B(camera,"retainedOwnerObserved") :
                    retained == 0 && !B(camera,"retainedOwnerObserved")), "Camera retained owner differs.");
        }
        var prefix = sample.GetProperty("sourceBaseLightData"); byte[] rawPrefix=Raw(prefix,0x1B0);
        Require(rawPrefix.AsSpan().SequenceEqual(Trace(light,0x1B0)) &&
            Convert.FromHexString(S(prefix,"modeTailRawHex")).AsSpan().SequenceEqual(Trace(light+0x10B0,4)),
            "Fresh base prefix/mode trace differs.");
        var retained210=sample.GetProperty("sourceRetained210");
        Require(I(retained210,"byteLength") == 210 && I(retained210,"extentCount") == 12 &&
            I(retained210,"additionalPayloadBytes") == 134 && Address(S(retained210,"allocationCpu")) == cpu &&
            Address(S(retained210,"lightData")) == light &&
            S(retained210,"contentsScope") == "observed-persistent-GameF0-current-retained-bytes" &&
            !B(retained210,"universalDefaultClaim") && !B(retained210,"currentMainFinalPackageClaim"),
            "Retained allocation scope/identity differs.");
        var extents=retained210.GetProperty("extents").EnumerateArray().ToArray();
        Require(extents.Length == 12, "Complete retained extent set required.");
        byte[][] result=new byte[12][];
        for (int i=0; i<12; i++)
        {
            var extent=Layout[i]; var value=extents[i]; result[i]=Raw(value,extent.Length);
            Require(I(value,"offset") == extent.Offset && Address(S(value,"address")) == light+(ulong)extent.Offset &&
                S(value,"origin") == (i==0 ? "fresh-prefix-first1B0B" : "exact-additional-retained-extent") &&
                result[i].AsSpan().SequenceEqual(i==0 ? rawPrefix.AsSpan(extent.Offset,extent.Length) : Trace(light+(ulong)extent.Offset,extent.Length)),
                "Retained extent bounds/current readset differ.");
        }
        Require(S(retained210,"packedSha256") == Hash(result.SelectMany(value => value).ToArray()), "Retained210 packed hash differs.");
        return result;
    }

    private static void CheckClaims(JsonElement item)
    {
        foreach (string name in new[] { "processWrites","originalFunctionsCalled","processSuspended","injectedCode",
            "atomicFrameSnapshot","activeMainMapRoleProven","activeGpuFrameProven","currentGroup8PublisherProven",
            "finalPostModifierLightDataObserved","completeLightDataRecordRead","preparedTableRead","ringFallbackUsed",
            "runtimeSourceObjectKeyObserved" }) Require(!B(item,name), "Saved evidence exceeds persistent source scope: "+name);
    }
    private static bool EqualSample(JsonElement a, JsonElement b) =>
        a.EnumerateObject().Where(field => field.Name != "readTrace").All(field =>
            b.TryGetProperty(field.Name,out var value) && JsonEqual(field.Value,value)) &&
        a.EnumerateObject().Count() == b.EnumerateObject().Count();
    private static bool JsonEqual(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind) return false;
        if (a.ValueKind == JsonValueKind.Object)
            return a.EnumerateObject().Count() == b.EnumerateObject().Count() &&
                a.EnumerateObject().All(field => b.TryGetProperty(field.Name,out var value) && JsonEqual(field.Value,value));
        if (a.ValueKind == JsonValueKind.Array)
            return a.GetArrayLength() == b.GetArrayLength() && a.EnumerateArray().Zip(b.EnumerateArray()).All(pair => JsonEqual(pair.First,pair.Second));
        return a.GetRawText() == b.GetRawText();
    }
    private static byte[] Raw(JsonElement item, int length)
    {
        byte[] raw=Convert.FromHexString(S(item,"rawHex"));
        Require(I(item,"byteLength") == length && raw.Length == length && S(item,"sha256") == Hash(raw), "Raw bytes/extent hash differs.");
        return raw;
    }
    private static ulong Address(string text)
    {
        Require(text.StartsWith("0x",StringComparison.Ordinal) && ulong.TryParse(text.AsSpan(2),NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,out _), "Malformed observed address.");
        ulong value=ulong.Parse(text.AsSpan(2),NumberStyles.HexNumber,CultureInfo.InvariantCulture);
        Require(value is >= 0x10000 and < 0x800000000000, "Noncanonical/null observed address.");
        return value;
    }
    private static string S(JsonElement item,string name) => item.GetProperty(name).GetString() ?? throw new InvalidDataException("Null source identity.");
    private static int I(JsonElement item,string name) => item.GetProperty(name).GetInt32();
    private static bool B(JsonElement item,string name) => item.GetProperty(name).GetBoolean();
    private static string Hash(ReadOnlySpan<byte> raw) => Convert.ToHexString(SHA256.HashData(raw));
    private static bool IsSha(string text) => text is { Length: 64 } && text.All(Uri.IsHexDigit);
    private static void Require(bool condition,string message)
    { if (!condition) throw new InvalidDataException(message); }
}
