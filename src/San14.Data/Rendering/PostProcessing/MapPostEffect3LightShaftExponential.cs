using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Owned scalar exponential for the finite Gaussian exponent range
/// binary32 [-112.5,0]. Reduction and polynomial operations retain separate
/// binary64 rounding. AVX, overflow and underflow handlers are separate paths.</summary>
public sealed class MapPostEffect3LightShaftExponential
{
    private const double NearZeroLimit=1.4901161193847656e-08d;
    private const double StepsPerNaturalLog=92.33248261689366d;
    private const double ReductionLeading=-0.010830424696223417d;
    private const double ReductionResidual=-2.5728046223276688e-14d;
    private const double SixthCoefficient=1d/720,FifthCoefficient=1d/120,FourthCoefficient=1d/24;
    private const double ThirdCoefficient=1d/6,HalfCoefficient=.5d;
    private MapPostEffect3LightShaftExponential(string locale)
    {
        Locale=locale;
        SourceEvidence=RenderRuleIdentity.For("post-effect3-light-shaft-exponential",locale)+
            ";binary32-Gaussian/SSE-order/binary64-reduction-and-polynomial";
    }
    public string Locale{get;}
    public string SourceEvidence{get;}
    public static MapPostEffect3LightShaftExponential LoadSseSource(GameResourceRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if(locale is not ("SC" or "TC"))throw new ArgumentOutOfRangeException(nameof(locale));
        return new(locale);
    }
    public float EvaluateSse(float binary32Exponent)
    {
        if(!float.IsFinite(binary32Exponent)||binary32Exponent>0||binary32Exponent< -112.5f)
            throw new NotSupportedException("LightShaft exponential is outside the closed finite Gaussian SSE domain.");
        double x=binary32Exponent;
        if(Math.Abs(x)<=NearZeroLimit)return (float)A(x,1);
        int n=(int)M(x,StepsPerNaturalLog); // Truncate reduction, not round-to-nearest.
        double a=M(n,ReductionLeading),b=M(n,ReductionResidual);
        double r=A(b,A(x,a)),r2=M(r,r),r4=M(r2,r2);
        double p=M(SixthCoefficient,r);p=A(p,FifthCoefficient);p=M(p,r);p=A(p,FourthCoefficient);p=M(p,r4);
        double q=M(ThirdCoefficient,r);q=A(q,HalfCoefficient);q=M(q,r2);q=A(q,r);q=A(q,p);
        var anchor=Anchors[n&63];q=M(q,anchor.Scale);q=A(q,anchor.Residual);q=A(q,anchor.Leading);
        ulong bits=unchecked(BitConverter.DoubleToUInt64Bits(q)+((ulong)(long)(n>>6)<<52));
        return (float)BitConverter.UInt64BitsToDouble(bits);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static double A(double a,double b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double M(double a,double b)=>a*b;

    private readonly record struct ExponentialAnchor(double Scale,double Residual,double Leading);
    // 2^(index/64) anchors with a leading/residual split for the rounded kernel.
    private static readonly ExponentialAnchor[] Anchors=
    [
        new(1.0d,0.0d,1.0d),
        new(1.0108892860517005d,5.389303957134853e-08d,1.0108892321586609d),
        new(1.0218971486541166d,1.1489047098156355e-08d,1.0218971371650696d),
        new(1.0330248790212284d,3.151375161585948e-08d,1.0330248475074768d),
        new(1.0442737824274138d,4.8334701437978214e-08d,1.0442737340927124d),
        new(1.0556451783605572d,1.0532950957636466e-08d,1.0556451678276062d),
        new(1.0671404006768237d,2.6712513184139607e-10d,1.0671404004096985d),
        new(1.0787607977571199d,5.461594547733443e-08d,1.0787607431411743d),
        new(1.0905077326652577d,4.6527104583035135e-08d,1.090507686138153d),
        new(1.102382583307841d,4.260502111933766e-08d,1.1023825407028198d),
        new(1.1143867425958924d,5.249243366386938e-09d,1.1143867373466492d),
        new(1.1265216186082418d,3.12364157279198e-08d,1.1265215873718262d),
        new(1.1387886347566916d,5.3862221438860076e-08d,1.1387885808944702d),
        new(1.1511892299529827d,2.1922282022224007e-08d,1.1511892080307007d),
        new(1.1637248587775775d,1.9090230101704197e-08d,1.1637248396873474d),
        new(1.1763969916502812d,2.5669751491128395e-08d,1.1763969659805298d),
        new(1.189207115002721d,3.79763538792175e-08d,1.1892070770263672d),
        new(1.202156731452703d,8.907079362799521e-09d,1.2021567225456238d),
        new(1.215247359980469d,2.6930694708194645e-08d,1.2152473330497742d),
        new(1.22848053610687d,1.8242642710772132e-08d,1.2284805178642273d),
        new(1.241857812073484d,4.4968381509531176e-08d,1.2418577671051025d),
        new(1.255380757024691d,7.32223747629814e-09d,1.2553807497024536d),
        new(1.2690509571917332d,1.419333320210669e-09d,1.2690509557724d),
        new(1.2828700160787783d,2.1438427938929792e-08d,1.2828699946403503d),
        new(1.2968395546510096d,1.9414651023355627e-08d,1.2968395352363586d),
        new(1.3109612115247644d,2.4638930601688614e-08d,1.3109611868858337d),
        new(1.3252366431597413d,2.4640911948926412e-08d,1.3252366185188293d),
        new(1.339667524053303d,2.4987903835438155e-08d,1.3396674990653992d),
        new(1.3542555469368927d,4.948129580446989e-08d,1.354255497455597d),
        new(1.3690024229745905d,2.1145824742788976e-08d,1.3690024018287659d),
        new(1.383909881963832d,8.488722380757845e-10d,1.3839098811149597d),
        new(1.3989796725383112d,8.689434187084528e-09d,1.398979663848877d),
        new(1.4142135623730951d,2.4203234208957936e-08d,1.4142135381698608d),
        new(1.42961333839197d,4.617471537256319e-08d,1.4296132922172546d),
        new(1.4451808069770467d,3.324200023331825e-08d,1.4451807737350464d),
        new(1.460917794180647d,2.3318067642948176e-08d,1.4609177708625793d),
        new(1.4768261459394993d,1.4595657758652532e-08d,1.4768261313438416d),
        new(1.4929077282912648d,2.5368443804278766e-08d,1.492907702922821d),
        new(1.5091644275934228d,3.464527210500039e-08d,1.5091643929481506d),
        new(1.5255981507445384d,4.197607517208563e-08d,1.5255981087684631d),
        new(1.5422108254079407d,8.07090469079979e-09d,1.5422108173370361d),
        new(1.559004400237837d,3.38399793010181e-08d,1.5590043663978577d),
        new(1.5759808451078865d,2.9943916134083948e-09d,1.5759808421134949d),
        new(1.593142151342267d,5.911433110692162e-08d,1.5931420922279358d),
        new(1.6104903319492543d,9.83621719880452e-09d,1.610490322113037d),
        new(1.6280274218573478d,4.234456090161384e-08d,1.6280273795127869d),
        new(1.645755478153965d,8.35492309647188e-09d,1.6457554697990417d),
        new(1.6636765803267364d,2.040165708934321e-08d,1.6636765599250793d),
        new(1.681792830507429d,3.484931751379662e-08d,1.6817927956581116d),
        new(1.7001063537185235d,3.095317312770448e-08d,1.7001063227653503d),
        new(1.718619298122478d,1.110847034726997e-08d,1.7186192870140076d),
        new(1.7373338352737062d,1.102411082978577e-09d,1.7373338341712952d),
        new(1.7562521603732995d,5.0368874434284035e-08d,1.756252110004425d),
        new(1.7753764925265212d,5.34319777955193e-08d,1.7753764390945435d),
        new(1.7947090750031072d,4.8189600106349574e-08d,1.794709026813507d),
        new(1.8142521755003989d,3.736258137343733e-08d,1.8142521381378174d),
        new(1.8340080864093424d,4.8365366633408956e-08d,1.8340080380439758d),
        new(1.8539791250833855d,1.4365612130892453e-08d,1.8539791107177734d),
        new(1.8741676341103d,1.2974588231408124e-08d,1.8741676211357117d),
        new(1.8945759815869656d,2.810338409837147e-08d,1.8945759534835815d),
        new(1.9152065613971474d,9.845328446216361e-09d,1.9152065515518188d),
        new(1.9360617934922943d,5.3570724626379305e-08d,1.9360617399215698d),
        new(1.9571441241754002d,4.2582840454565194e-08d,1.9571440815925598d),
        new(1.978456026387951d,6.0327263588832495e-09d,1.9784560203552246d),
    ];
}
