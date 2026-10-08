using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalSkyPatternProjectionMatrixSource LoadSkyPatternProjectionMatrixSource(string directory, string locale)
    {
        var profile=Profile(locale);
        byte[] exe=File.ReadAllBytes(Path.Combine(directory,$"SAN14CE_{locale}.exe"));
        VerifyHash(exe,profile.ExeHash,"Sky pattern projection executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int textRva);
        VerifyHash(text,profile.TextHash,"Sky pattern projection decoded text");
        bool tc=locale=="TC";
        void Check(int tcRva,int scRva,int length,string tcHash,string scHash)
            =>VerifyHash(text.AsSpan((tc?tcRva:scRva)-textRva,length),tc?tcHash:scHash,"Sky pattern numerical source");
        Check(0xD49EE0,0xD4AC10,2037,
            "3CE053B5771B2EB0F50C49F03E6F295F7643EEAA394459FBB8CACA3A37741582","1BA01889ECE6D37057C4B763EF04E46C2BAE514C7A7D833E4EB72997A6159FA1");
        Check(0xD01F30,0xD02C60,220,
            "F826723826AC78D0CEED1E93540DBB0186FBBE81DCE499F96584442213952495","46B7B2DA222BADB6A5EC8B009C36DD85B5B862BEDAD558EEAE30D2FCD84D5FA9");
        Check(0xAB6510,0xAB75A0,37,
            "BC580E2903E8B45F0F2AC538024A0484D9061A9AAAD2F7812528B150256F8EB9","BC580E2903E8B45F0F2AC538024A0484D9061A9AAAD2F7812528B150256F8EB9");
        Check(0xAB5BD0,0xAB6C60,159,
            "C317077D2982D01ECE8AB91B43A0513A776D8E112C5D4C44B0E68E7A8A21EDC6","61175236ACE3C603011B2DD46495AD21F6F40B5F1359C76C854C595B383DDD5A");
        Check(0x98B630,0x98C770,406,
            "2F11CAA151D29DA8E0F041643236E79B80163CD330F5249DB10F008749934218","2F11CAA151D29DA8E0F041643236E79B80163CD330F5249DB10F008749934218");
        Check(0x11510,0x11510,752,
            "0A67E03F4002FBA6E4632BAC4847B494B4E314F7236EA8A5DE9A10F4305E3417","3B21E406417BF8FA52330FEBF8341E3B913AF2A06644FD63E8E4D92F4D0145A4");
        Check(0x17B070,0x17ADD0,331,
            "E12AFDE597794034EA4B0D8AFA3EC24CD35616EC3272C41A5AB159875C3D52BB","E12AFDE597794034EA4B0D8AFA3EC24CD35616EC3272C41A5AB159875C3D52BB");
        Check(0x101FAD8,0x10209D8,171,
            "4CC01774980B2166907C32C1DD29E62D15DFAE1EDB24A1AFE7CE3700EE566727","4EA85EA754BFCF0998798C13B82AA04E299927FA9AA3587A2B54D4669BD1D134");
        ReadOnlySpan<byte> Literal(int tcRva,int scRva,int size) => image.Raw(tc?tcRva:scRva,size);
        float Scalar(int tcRva,int scRva) => BinaryPrimitives.ReadSingleLittleEndian(Literal(tcRva,scRva,4));
        Vector4 Vector(int tcRva,int scRva)
        {var b=Literal(tcRva,scRva,16);return new(BinaryPrimitives.ReadSingleLittleEndian(b),
            BinaryPrimitives.ReadSingleLittleEndian(b[4..]),BinaryPrimitives.ReadSingleLittleEndian(b[8..]),BinaryPrimitives.ReadSingleLittleEndian(b[12..]));}
        if (!Literal(0x1343B80,0x1344B80,4).SequenceEqual(Convert.FromHexString("0000003F")))
            throw new InvalidDataException("Original Sky pattern scalar literal changed.");
        if (!Literal(0x13948C8,0x13958C8,4).SequenceEqual(Convert.FromHexString("0050C347")))
            throw new InvalidDataException("Original Sky pattern scalar literal changed.");
        if (!Literal(0x1343B54,0x1344B54,4).SequenceEqual(Convert.FromHexString("00004837")))
            throw new InvalidDataException("Original Sky pattern scalar literal changed.");
        if (!Literal(0x1343BE0,0x1344BE0,4).SequenceEqual(Convert.FromHexString("00000040")))
            throw new InvalidDataException("Original Sky pattern scalar literal changed.");
        if (!Literal(0x13A194C,0x13A294C,4).SequenceEqual(Convert.FromHexString("000000C0")))
            throw new InvalidDataException("Original Sky pattern scalar literal changed.");
        if (!Literal(0x1343CDC,0x1344CDC,4).SequenceEqual(Convert.FromHexString("000080BF")))
            throw new InvalidDataException("Original Sky pattern scalar literal changed.");
        if (!Literal(0x14D1A20,0x14D2770,16).SequenceEqual(Convert.FromHexString("00000000000080BF0000000000000000")))
            throw new InvalidDataException("Original Sky pattern vector literal changed.");
        if (!Literal(0x14C4D00,0x14C59E0,16).SequenceEqual(Convert.FromHexString("000000000000803F0000000000000000")))
            throw new InvalidDataException("Original Sky pattern vector literal changed.");
        if (!Literal(0x14D1850,0x14D2580,16).SequenceEqual(Convert.FromHexString("0000803F000000000000000000000000")))
            throw new InvalidDataException("Original Sky pattern vector literal changed.");
        if (!Literal(0x14CA1C0,0x14CAEC0,16).SequenceEqual(Convert.FromHexString("0000000000000000000080BF00000000")))
            throw new InvalidDataException("Original Sky pattern vector literal changed.");
        if (!Literal(0x14BB860,0x14BC740,16).SequenceEqual(Convert.FromHexString("0000803F000000000000000000000000")))
            throw new InvalidDataException("Original Sky pattern vector literal changed.");
        if (!Literal(0x14BB870,0x14BC750,16).SequenceEqual(Convert.FromHexString("000000000000803F0000000000000000")))
            throw new InvalidDataException("Original Sky pattern vector literal changed.");
        if (!Literal(0x14BB880,0x14BC760,16).SequenceEqual(Convert.FromHexString("00000000000000000000803F00000000")))
            throw new InvalidDataException("Original Sky pattern vector literal changed.");
        if (!Literal(0x14BB890,0x14BC770,16).SequenceEqual(Convert.FromHexString("0000000000000000000000000000803F")))
            throw new InvalidDataException("Original Sky pattern vector literal changed.");
        if (!Literal(0x16C9190,0x16C9E30,16).SequenceEqual(Convert.FromHexString("0000003F0000003F0000000000000000")))
            throw new InvalidDataException("Original Sky pattern vector literal changed.");
        Vector4 r0=Vector(0x14BB860,0x14BC740),r1=Vector(0x14BB870,0x14BC750),r2=Vector(0x14BB880,0x14BC760),r3=Vector(0x14BB890,0x14BC770);
        var affine=new Matrix4x4(r0.X,r0.Y,r0.Z,r0.W,r1.X,r1.Y,r1.Z,r1.W,r2.X,r2.Y,r2.Z,r2.W,r3.X,r3.Y,r3.Z,r3.W);
        var constants=new OriginalSkyPatternProjectionConstants(
            Scalar(0x1343B80,0x1344B80),Scalar(0x13948C8,0x13958C8),Scalar(0x1343B54,0x1344B54),
            Scalar(0x1343BE0,0x1344BE0),Scalar(0x13A194C,0x13A294C),Scalar(0x1343CDC,0x1344CDC),
            Vector(0x14D1A20,0x14D2770),Vector(0x14C4D00,0x14C59E0),Vector(0x14D1850,0x14D2580),Vector(0x14CA1C0,0x14CAEC0),affine,Vector(0x16C9190,0x16C9E30));
        return new(locale,$"SkyPattern180/{locale}@{profile.ExeHash};TC-D49EE0/SC-D4AC10-full2037;"+
            "actualCamera10/18/144/148/14C/210;selectedView1B20-CPU8-direction;98B630/11510/17B070;source-literals-once",constants);
    }
}
