namespace San14.Data;

/// <summary>Persistent logical Sky2 bindings, owned by the scene that creates
/// and retires its device programs. A zero package reference skips assignment.</summary>
public sealed class Sky2TextureBindings : IDisposable
{
    private readonly object gate = new();
    private readonly LoadedSkySamplingResource?[] textures = new LoadedSkySamplingResource?[3];
    private long revision;
    private bool retired;

    public void ApplyPackage(SkySeasonPackageSampling sampling,
        Func<SkySamplingReference, LoadedSkySamplingResource> resolve)
    {
        RequireActive();
        var assignments = Resolve(sampling, resolve);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(retired, this);
            bool changed = false;
            for (int index = 0; index < textures.Length; index++)
                if (assignments[index] is { } resource && !SameTexture(textures[index], resource))
                    changed = true;
            if (!changed) return;
            long nextRevision = checked(revision + 1);
            for (int index = 0; index < textures.Length; index++)
                if (assignments[index] is { } resource) textures[index] = resource;
            revision = nextRevision;
        }
    }

    /// <summary>Prepare an independent binding owner for replacement device
    /// resources. The installed owner stays current until its caller commits
    /// the replacement and retires it. The caller can request a separate lifetime
    /// when another physical Sky input changes. Otherwise unchanged returns null.</summary>
    public Sky2TextureBindings? PrepareReplacement(SkySeasonPackageSampling sampling,
        Func<SkySamplingReference, LoadedSkySamplingResource> resolve, bool forceReplacement = false)
    {
        LoadedSkySamplingResource?[] retained;
        long precedingRevision;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(retired, this);
            retained = textures.ToArray();
            precedingRevision = revision;
        }
        var assignments = Resolve(sampling, resolve);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(retired, this);
            if (revision != precedingRevision)
                throw new InvalidOperationException("Sky2 bindings changed while replacement resources were resolving.");
            bool changed = forceReplacement;
            for (int index = 0; index < retained.Length; index++)
                if (assignments[index] is { } resource && !SameTexture(retained[index], resource))
                {
                    retained[index] = resource;
                    changed = true;
                }
            if (!changed) return null;
            var replacement = new Sky2TextureBindings { revision = checked(revision + 1) };
            retained.CopyTo(replacement.textures, 0);
            return replacement;
        }
    }

    /// <summary>An existing program keeps its imported descriptors. Changed
    /// bindings require replacement of that scene's actual device resources.</summary>
    public void RequireCompatiblePackage(SkySeasonPackageSampling sampling,
        Func<SkySamplingReference, LoadedSkySamplingResource> resolve)
    {
        RequireActive();
        var assignments = Resolve(sampling, resolve);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(retired, this);
            for (int index = 0; index < textures.Length; index++)
                if (assignments[index] is { } resource && !SameTexture(textures[index], resource))
                    throw new NotSupportedException("Changed Sky2 bindings require scene resource replacement.");
        }
    }

    private void RequireActive()
    {
        lock (gate) ObjectDisposedException.ThrowIf(retired, this);
    }

    public Sky2TextureBindingSnapshot Capture()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(retired, this);
            return new(this, revision, textures[0], textures[1], textures[2]);
        }
    }

    internal void RequireCurrent(long capturedRevision)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(retired, this);
            if (capturedRevision != revision)
                throw new InvalidOperationException("Sky2 bindings changed after this program or frame was prepared.");
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            retired = true;
            Array.Clear(textures);
        }
    }

    private static LoadedSkySamplingResource?[] Resolve(SkySeasonPackageSampling sampling,
        Func<SkySamplingReference, LoadedSkySamplingResource> resolve)
    {
        ArgumentNullException.ThrowIfNull(sampling);
        ArgumentNullException.ThrowIfNull(resolve);
        return [Read(sampling.Earth), Read(sampling.Moon), Read(sampling.MilkyWay)];

        LoadedSkySamplingResource? Read(SkySamplingReference reference)
        {
            if (reference.IsSourceReferenceZero) return null;
            var resource = resolve(reference);
            if (resource is null || resource.Role != reference.Role ||
                resource.CameraReference.SourceObjectId != reference.SourceObjectId)
                throw new InvalidDataException("Resolved Sky2 texture does not match its declared role and object key.");
            return resource;
        }
    }

    private static bool SameTexture(LoadedSkySamplingResource? left, LoadedSkySamplingResource right)
        => left is not null && left.Texture.Identity == right.Texture.Identity &&
            left.Texture.SamplerDescriptorSha256 == right.Texture.SamplerDescriptorSha256;
}

/// <summary>A program and its frames borrow the same binding generation.
/// Presence reflects installed resources, independently of shader coefficients.</summary>
public sealed class Sky2TextureBindingSnapshot
{
    public Sky2TextureBindings Owner { get; }
    public long Revision { get; }
    public SkySceneTexturePresence Presence { get; }
    private readonly LoadedSkySamplingResource? earth, moon, milky;

    internal Sky2TextureBindingSnapshot(Sky2TextureBindings owner, long revision,
        LoadedSkySamplingResource? earth, LoadedSkySamplingResource? moon, LoadedSkySamplingResource? milky)
    {
        Owner = owner; Revision = revision;
        this.earth = earth; this.moon = moon; this.milky = milky;
        Presence = new(earth is not null, moon is not null, milky is not null);
    }

    public void RequireCurrent() => Owner.RequireCurrent(Revision);

    public LoadedSkySamplingResource? GetTexture(SkySamplingRole role) => role switch
    {
        SkySamplingRole.Earth => earth,
        SkySamplingRole.Moon => moon,
        SkySamplingRole.MilkyWay => milky,
        _ => throw new ArgumentOutOfRangeException(nameof(role), "Star textures have a separate owner.")
    };

    public Sky2NullTextureBinding RequireNull(SkySamplingRole role)
    {
        RequireCurrent();
        if (GetTexture(role) is not null)
            throw new InvalidOperationException("A bound Sky2 texture cannot use a null-sample shader specialization.");
        return new(this, role);
    }
}

/// <summary>A null slot captured from an actual binding owner, rather than
/// inferred from a resource declaration or caller-selected lifetime scope.</summary>
public sealed class Sky2NullTextureBinding
{
    public Sky2TextureBindingSnapshot Snapshot { get; }
    public SkySamplingRole Role { get; }
    public uint PixelRegister => SkySamplingResources.PixelRegister(Role);
    internal Sky2NullTextureBinding(Sky2TextureBindingSnapshot snapshot, SkySamplingRole role)
        => (Snapshot, Role) = (snapshot, role);
    public void Validate() => Snapshot.RequireCurrent();
}
