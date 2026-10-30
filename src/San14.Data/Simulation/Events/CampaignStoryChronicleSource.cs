using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

/// <summary>Source-time classification for ordinary historical events. It
/// reads only the reached current owners and emits no world mutation or RNG.
/// Event identities and message kinds are the finite original rule data;
/// no executable, instruction table or research cache is read at runtime.</summary>
internal static partial class CampaignStoryChronicleSource
{
    internal static bool IsDeferred(int eventId) => eventId is
        49 or 52 or 54 or 55 or 58 or 318 or 329 or 330 or 332 or 515 or 519;

    internal static ImmutableArray<CampaignChronicleCommand> Prepare(GameState state,
        SourceWorldContext current, int eventId, int campaignDay, CampaignStoryChronicleInputs inputs)
    {
        var read = new Current(state, current, inputs);
        switch (eventId)
        {
            case 1:
            {
                if (!(read.PlayerForceOfPerson(952))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(952);
                return ImmutableArray.Create(Command(1001, false, 38, hex0));
            }
            case 2:
            {
                int hex0 = read.PersonHomeHex(736);
                return ImmutableArray.Create(Command(1002, true, 39, hex0));
            }
            case 3:
            {
                int hex0 = read.PersonHomeHex(521);
                return ImmutableArray.Create(Command(1003, true, 40, hex0));
            }
            case 4:
            {
                int hex0 = read.CityHex(20);
                return ImmutableArray.Create(Command(1004, true, 0, hex0));
            }
            case 5:
            {
                return ImmutableArray.Create(Command(1005, true, 0));
            }
            case 6:
            {
                int hex0 = read.PersonHomeHex(736);
                return ImmutableArray.Create(Command(1006, true, 41, hex0));
            }
            case 7:
            {
                int hex0 = read.PersonHomeHex(987);
                return ImmutableArray.Create(Command(1007, true, 42, hex0));
            }
            case 8:
            {
                int hex0 = read.PersonHomeHex(987);
                return ImmutableArray.Create(Command(1008, true, 43, hex0));
            }
            case 13:
            {
                if (!(read.PlayerRulerIs(521) || read.PlayerRulerIs(724))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(9);
                return ImmutableArray.Create(Command(1013, true, 44, hex0));
            }
            case 14:
            {
                if (!(read.PlayerRulerIs(952))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(952);
                return ImmutableArray.Create(Command(1014, false, 45, hex0));
            }
            case 15:
            {
                if (!(read.PlayerRulerIs(952))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(952);
                return ImmutableArray.Create(Command(1015, true, 46, hex0));
            }
            case 17:
            {
                if (!(read.PlayerRulerIs(557))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(557);
                return ImmutableArray.Create(Command(1017, true, 47, hex0));
            }
            case 20:
            {
                int hex0 = read.CityHex(16);
                return ImmutableArray.Create(Command(1020, true, 0, hex0));
            }
            case 21:
            {
                int hex0 = read.PersonHomeHex(32);
                return ImmutableArray.Create(Command(1021, true, 48, hex0));
            }
            case 22:
            {
                if (!(read.PlayerForceOfPerson(557))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(557);
                return ImmutableArray.Create(Command(1022, true, 49, hex0));
            }
            case 24:
            {
                int hex0 = read.PersonHomeHex(553);
                return ImmutableArray.Create(Command(1024, true, 50, hex0));
            }
            case 27:
            {
                if (!(read.PlayerRulerIs(952))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(952);
                return ImmutableArray.Create(Command(1027, true, 51, hex0));
            }
            case 31:
            {
                int hex0 = read.PersonHomeHex(34);
                return ImmutableArray.Create(Command(1031, true, 52, hex0));
            }
            case 32:
            {
                if (!(read.PlayerRelatedToPerson(526))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(526);
                return ImmutableArray.Create(Command(1032, true, 53, hex0));
            }
            case 33:
            {
                if (!(read.PlayerForceOfPerson(440))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(440);
                return ImmutableArray.Create(Command(1033, true, 54, hex0));
            }
            case 37:
            {
                if (!(read.PlayerRulerIs(952))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(33);
                return ImmutableArray.Create(Command(1037, false, 55, hex0));
            }
            case 38:
            {
                if (!(read.PlayerRulerIs(952))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(33);
                return ImmutableArray.Create(Command(1038, false, 56, hex0));
            }
            case 39:
            {
                if (!(read.PlayerRulerIs(952))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(33);
                return ImmutableArray.Create(Command(1039, false, 57, hex0));
            }
            case 41:
            {
                int hex0 = read.PersonHomeHex(946);
                return ImmutableArray.Create(Command(1041, true, 58, hex0));
            }
            case 44:
            {
                if (!(read.PlayerRulerIs(952) || read.PlayerForceOfPerson(565))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(952);
                return ImmutableArray.Create(Command(1044, true, 59, hex0));
            }
            case 47:
            {
                if (!(read.PlayerRulerIs(521))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(521);
                return ImmutableArray.Create(Command(1047, true, 60, hex0));
            }
            case 49:
            {
                int hex0 = read.SelectedForceRulerHomeHex();
                return ImmutableArray.Create(Command(1049, true, 61, hex0));
            }
            case 50:
            {
                int hex0 = read.PersonHomeHex(952);
                return ImmutableArray.Create(Command(1050, true, 62, hex0));
            }
            case 52:
            {
                if (!(read.SelectedForceIsPlayer())) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.SelectedForceRulerHomeHex();
                return ImmutableArray.Create(Command(1052, false, 63, hex0));
            }
            case 54:
            {
                if (!(read.SelectedForceIsPlayer())) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.SelectedForceRulerHomeHex();
                return ImmutableArray.Create(Command(1054, false, 64, hex0));
            }
            case 55:
            {
                if (!(read.SelectedForceIsPlayer())) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.SelectedForceRulerHomeHex();
                return ImmutableArray.Create(Command(1055, false, 65, hex0));
            }
            case 56:
            {
                if (!(read.PlayerRulerIs(553))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(553);
                return ImmutableArray.Create(Command(1056, true, 66, hex0));
            }
            case 57:
            {
                if (!(read.PlayerForceOfPerson(440))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(441);
                return ImmutableArray.Create(Command(1057, true, 67, hex0));
            }
            case 58:
            {
                if (!(read.SelectedForceIsPlayer())) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.SelectedForceRulerHomeHex();
                return ImmutableArray.Create(Command(1058, false, 68, hex0));
            }
            case 218:
            {
                return ImmutableArray.Create(Command(1218, true, 0));
            }
            case 219:
            {
                return ImmutableArray.Create(Command(1219, true, 0));
            }
            case 220:
            {
                return ImmutableArray.Create(Command(1220, true, 0));
            }
            case 221:
            {
                return ImmutableArray.Create(Command(1221, true, 0));
            }
            case 222:
            {
                return ImmutableArray.Create(Command(1222, true, 0));
            }
            case 223:
            {
                return ImmutableArray.Create(Command(1223, true, 0));
            }
            case 224:
            {
                return ImmutableArray.Create(Command(1224, true, 0));
            }
            case 225:
            {
                return ImmutableArray.Create(Command(1225, true, 0));
            }
            case 226:
            {
                return ImmutableArray.Create(Command(1226, true, 0));
            }
            case 227:
            {
                return ImmutableArray.Create(Command(1227, true, 0));
            }
            case 228:
            {
                return ImmutableArray.Create(Command(1228, true, 0));
            }
            case 229:
            {
                return ImmutableArray.Create(Command(1229, true, 0));
            }
            case 230:
            {
                return ImmutableArray.Create(Command(1230, true, 0));
            }
            case 231:
            {
                return ImmutableArray.Create(Command(1231, true, 0));
            }
            case 232:
            {
                return ImmutableArray.Create(Command(1232, true, 0));
            }
            case 233:
            {
                return ImmutableArray.Create(Command(1233, true, 0));
            }
            case 234:
            {
                return ImmutableArray.Create(Command(1234, true, 0));
            }
            case 235:
            {
                return ImmutableArray.Create(Command(1235, true, 0));
            }
            case 236:
            {
                return ImmutableArray.Create(Command(1236, true, 0));
            }
            case 237:
            {
                return ImmutableArray.Create(Command(1237, true, 0));
            }
            case 238:
            {
                return ImmutableArray.Create(Command(1238, true, 0));
            }
            case 239:
            {
                return ImmutableArray.Create(Command(1239, true, 0));
            }
            case 240:
            {
                return ImmutableArray.Create(Command(1240, true, 0));
            }
            case 241:
            {
                return ImmutableArray.Create(Command(1241, false, 0));
            }
            case 242:
            {
                return ImmutableArray.Create(Command(1242, false, 0));
            }
            case 243:
            {
                return ImmutableArray.Create(Command(1243, false, 0));
            }
            case 244:
            {
                return ImmutableArray.Create(Command(1244, false, 0));
            }
            case 245:
            {
                return ImmutableArray.Create(Command(1245, false, 0));
            }
            case 246:
            {
                return ImmutableArray.Create(Command(1246, false, 0));
            }
            case 247:
            {
                if (!(read.PlayerRulerIs(771))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(20);
                return ImmutableArray.Create(Command(1247, false, 69, hex0));
            }
            case 248:
            {
                if (!(read.PlayerRulerIs(769) || read.PersonExists(521))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(20);
                return ImmutableArray.Create(Command(1248, true, 70, hex0));
            }
            case 249:
            {
                if (!(read.PlayerRulerIs(952) || read.PersonExists(938) || read.PersonExists(666))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(42);
                return ImmutableArray.Create(Command(1249, true, 71, hex0));
            }
            case 252:
            {
                if (!(read.PlayerRulerIs(952) || read.PersonExists(521) || read.PersonExists(987))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(14);
                return ImmutableArray.Create(Command(1252, true, 72, hex0));
            }
            case 253:
            {
                if (!(read.PlayerRulerIs(521) || read.PersonExists(987))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(10);
                return ImmutableArray.Create(Command(1253, true, 73, hex0));
            }
            case 254:
            {
                if (!(read.PlayerRulerIs(521) || read.PersonExists(987))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(9);
                return ImmutableArray.Create(Command(1254, true, 74, hex0));
            }
            case 255:
            {
                if (!(read.PlayerRulerIs(266) || read.PersonExists(35) || read.PersonExists(952))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.AreaCityHex(12);
                return ImmutableArray.Create(Command(1255, true, 75, hex0));
            }
            case 256:
            {
                int hex0 = read.PersonHomeHex(32);
                return ImmutableArray.Create(Command(1256, true, 76, hex0));
            }
            case 257:
            {
                if (!(read.PlayerRulerIs(521) || read.PersonExists(35))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(35);
                return ImmutableArray.Create(Command(1257, true, 77, hex0));
            }
            case 259:
            {
                if (!(read.PlayerRulerIs(521) || read.PersonExists(37) || read.PersonExists(34))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(6);
                return ImmutableArray.Create(Command(1259, true, 78, hex0));
            }
            case 260:
            {
                if (!(read.PlayerRulerIs(521) || read.PersonExists(34))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(2);
                return ImmutableArray.Create(Command(1260, true, 79, hex0));
            }
            case 262:
            {
                if (!(read.PlayerRulerIs(553))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(553);
                return ImmutableArray.Create(Command(1262, false, 0, hex0));
            }
            case 267:
            {
                if (!(read.PlayerRulerIs(35))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(6);
                return ImmutableArray.Create(Command(1267, true, 84, hex0));
            }
            case 268:
            {
                if (!(read.PlayerRulerIs(551))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(557);
                return ImmutableArray.Create(Command(1268, false, 83, hex0));
            }
            case 269:
            {
                if (!(read.PlayerRulerIs(266))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(3);
                return ImmutableArray.Create(Command(1269, true, 85, hex0));
            }
            case 270:
            {
                if (!(read.PlayerRulerIs(521))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(14);
                return ImmutableArray.Create(Command(1270, true, 86, hex0));
            }
            case 271:
            {
                if (!(read.PlayerRulerIs(32))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(19);
                return ImmutableArray.Create(Command(1271, true, 87, hex0));
            }
            case 272:
            {
                if (!(read.PlayerRulerIs(952) || read.PlayerRulerIs(724))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(10);
                return ImmutableArray.Create(Command(1272, true, 88, hex0));
            }
            case 275:
            {
                if (!(read.PlayerRulerIs(553) || read.PlayerRulerIs(521))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(32);
                int hex1 = read.CityHex(34);
                int hex2 = read.CityHex(35);
                return ImmutableArray.Create(Command(1275, true, 89, hex2, hex1, hex0));
            }
            case 309:
            {
                int hex0 = read.PersonHomeHex(132);
                return ImmutableArray.Create(Command(1309, true, 90, hex0));
            }
            case 311:
            {
                if (!(read.PlayerRulerIs(736))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(18);
                return ImmutableArray.Create(Command(1311, true, 91, hex0));
            }
            case 313:
            {
                if (!(read.PlayerRulerIs(736))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(521);
                return ImmutableArray.Create(Command(1313, true, 92, hex0));
            }
            case 314:
            {
                int hex0 = read.CityHex(18);
                return ImmutableArray.Create(Command(1314, false, 0, hex0));
            }
            case 318:
            {
                if (!(read.PlayerRulerIs(987) || read.PlayerRulerIs(521))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.SelectedCityHex();
                return ImmutableArray.Create(Command(1318, true, 93, hex0));
            }
            case 320:
            {
                if (!(read.PlayerRulerIs(557))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(963);
                return ImmutableArray.Create(Command(1320, true, 94, hex0));
            }
            case 322:
            {
                if (!(read.PlayerRulerIs(557))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(239);
                return ImmutableArray.Create(Command(1322, true, 95, hex0));
            }
            case 323:
            {
                if (!(read.PlayerRulerIs(557))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(80);
                return ImmutableArray.Create(Command(1323, true, 96, hex0));
            }
            case 325:
            {
                if (!(read.PlayerRulerIs(945) || read.PlayerRulerIs(845))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(45);
                return ImmutableArray.Create(Command(1325, true, 101, hex0));
            }
            case 327:
            {
                if (!(read.PlayerRulerIs(945))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(46);
                return ImmutableArray.Create(Command(1327, true, 102, hex0));
            }
            case 329:
            {
                if (!(read.PlayerRulerIs(945) || read.PlayerIsSelectedForce())) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(22);
                return ImmutableArray.Create(Command(1329, true, 103, hex0));
            }
            case 330:
            {
                if (!(read.PlayerRulerIs(945) || read.PlayerIsSelectedForce())) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(23);
                return ImmutableArray.Create(Command(1330, true, 104, hex0));
            }
            case 332:
            {
                if (!(read.PlayerRulerIs(945) || read.PlayerIsSelectedForce())) return ImmutableArray<CampaignChronicleCommand>.Empty;
                return ImmutableArray.Create(Command(1332, true, 105, read.AreaHex(174)));
            }
            case 335:
            {
                if (!(read.PlayerRulerIs(952) || read.PlayerRulerIs(521))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(35);
                return ImmutableArray.Create(Command(1335, true, 106, hex0));
            }
            case 336:
            {
                if (!(read.PlayerRulerIs(952))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(39);
                return ImmutableArray.Create(Command(1336, true, 107, hex0));
            }
            case 337:
            {
                if (!(read.PlayerRulerIs(952))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(37);
                int hex1 = read.CityHex(38);
                return ImmutableArray.Create(Command(1337, true, 108, hex1, hex0));
            }
            case 338:
            {
                if (!(read.PlayerRulerIs(952))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(36);
                return ImmutableArray.Create(Command(1338, true, 109, hex0));
            }
            case 439:
            {
                if (!(read.PlayerRulerIs(521))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(521);
                return ImmutableArray.Create(Command(1439, true, 112, hex0));
            }
            case 440:
            {
                if (!(read.PlayerRulerIs(521))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(521);
                return ImmutableArray.Create(Command(1440, true, 113, hex0));
            }
            case 441:
            {
                if (!(read.PlayerRulerIs(521))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(521);
                return ImmutableArray.Create(Command(1441, true, 114, hex0));
            }
            case 442:
            {
                if (!(read.PlayerForceOfPerson(516))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(516);
                return ImmutableArray.Create(Command(1442, true, 115, hex0));
            }
            case 443:
            {
                if (!(read.PlayerForceOfPerson(516))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(516);
                return ImmutableArray.Create(Command(1443, true, 116, hex0));
            }
            case 444:
            {
                if (!(read.PlayerForceOfPerson(516))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(516);
                return ImmutableArray.Create(Command(1444, true, 117, hex0));
            }
            case 445:
            {
                if (!(read.PlayerForceOfPerson(516))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(516);
                return ImmutableArray.Create(Command(1445, true, 118, hex0));
            }
            case 447:
            {
                if (!(read.PlayerForceOfPerson(440))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(440);
                return ImmutableArray.Create(Command(1447, true, 119, hex0));
            }
            case 448:
            {
                return ImmutableArray.Create(Command(1448, true, 120));
            }
            case 460:
            {
                if (!(read.PlayerRulerIs(132) || read.PlayerRulerIs(607))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(16);
                return ImmutableArray.Create(Command(1460, true, 121, hex0));
            }
            case 463:
            {
                int hex0 = read.CityHex(7);
                return ImmutableArray.Create(Command(1463, false, 122, hex0));
            }
            case 464:
            {
                int hex0 = read.CityHex(19);
                return ImmutableArray.Create(Command(1464, true, 123, hex0));
            }
            case 466:
            {
                return ImmutableArray.Create(Command(1466, true, 0));
            }
            case 467:
            {
                if (!(read.PlayerRulerIs(551))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(36);
                return ImmutableArray.Create(Command(1467, true, 124, hex0));
            }
            case 468:
            {
                return ImmutableArray.Create(Command(1468, true, 0));
            }
            case 469:
            {
                if (!(read.PlayerRulerIs(932))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(7);
                return ImmutableArray.Create(Command(1469, true, 125, hex0));
            }
            case 470:
            {
                if (!(read.PlayerRulerIs(736))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(987);
                return ImmutableArray.Create(Command(1470, true, 126, hex0));
            }
            case 471:
            {
                int hex0 = read.GateHex(2);
                return ImmutableArray.Create(Command(1471, true, 127, hex0));
            }
            case 473:
            {
                int hex0 = read.GateHex(2);
                return ImmutableArray.Create(Command(1473, true, 128, hex0));
            }
            case 478:
            {
                var first = Command(1478, true, 0);
                if (!(read.PlayerRulerIs(557) || read.PlayerRulerIs(32)))
                    return ImmutableArray.Create(first);
                int hex = read.PersonHomeHex(557);
                return ImmutableArray.Create(first, Command(1479, true, 129, hex));
            }
            case 479:
            {
                if (!(read.PlayerRulerIs(557) || read.PlayerRulerIs(32))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(557);
                return ImmutableArray.Create(Command(1479, true, 129, hex0));
            }
            case 481:
            {
                if (!(read.PlayerRulerIs(521) || read.PlayerRulerIs(987))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(987);
                return ImmutableArray.Create(Command(1481, true, 130, hex0));
            }
            case 485:
            {
                int hex0 = read.PersonHomeHex(880);
                return ImmutableArray.Create(Command(1485, true, 131, hex0));
            }
            case 486:
            {
                if (!(read.PlayerRulerIs(893))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(625);
                return ImmutableArray.Create(Command(1486, true, 0, hex0));
            }
            case 487:
            {
                if (!(read.PlayerRulerIs(521) || read.PlayerRulerIs(625))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(19);
                return ImmutableArray.Create(Command(1487, true, 132, hex0));
            }
            case 488:
            {
                if (!(read.PlayerRulerIs(521) || read.PlayerRulerIs(952))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(9);
                return ImmutableArray.Create(Command(1488, true, 133, hex0));
            }
            case 491:
            {
                if (!(read.PlayerRulerIs(521))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(625);
                return ImmutableArray.Create(Command(1491, true, 134, hex0));
            }
            case 496:
            {
                if (!(read.PlayerRulerIs(521))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(33);
                int hex1 = read.CityHex(17);
                return ImmutableArray.Create(Command(1496, true, 0, hex1, hex0));
            }
            case 497:
            {
                if (!(read.PlayerRulerIs(521) || read.PlayerRulerIs(35))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(35);
                return ImmutableArray.Create(Command(1497, true, 135, hex0));
            }
            case 498:
            {
                if (!(read.PlayerRulerIs(521))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(5);
                return ImmutableArray.Create(Command(1498, true, 136, hex0));
            }
            case 499:
            {
                if (!(read.PlayerRulerIs(521))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(4);
                return ImmutableArray.Create(Command(1499, true, 137, hex0));
            }
            case 500:
            {
                if (!(read.PlayerRulerIs(521))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(2);
                return ImmutableArray.Create(Command(1500, true, 138, hex0));
            }
            case 501:
            {
                if (!(read.PlayerRulerIs(553))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(32);
                return ImmutableArray.Create(Command(1501, true, 139, hex0));
            }
            case 503:
            {
                if (!(read.PlayerRulerIs(952))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(44);
                return ImmutableArray.Create(Command(1503, true, 0, hex0));
            }
            case 505:
            {
                if (!(read.PlayerRulerIs(952))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(44);
                return ImmutableArray.Create(Command(1505, true, 140, hex0));
            }
            case 508:
            {
                if (!(read.PlayerRulerIs(521) || read.PlayerRulerIs(952))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(41);
                return ImmutableArray.Create(Command(1508, true, 141, hex0));
            }
            case 510:
            {
                if (!(read.PlayerRulerIs(521) || read.PlayerRulerIs(952))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(41);
                return ImmutableArray.Create(Command(1510, true, 142, hex0));
            }
            case 511:
            {
                if (!(read.PlayerRulerIs(952) || read.PlayerRulerIs(553))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(35);
                return ImmutableArray.Create(Command(1511, false, 143, hex0));
            }
            case 512:
            {
                if (!(read.PlayerRulerIs(526))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(526);
                return ImmutableArray.Create(Command(1512, false, 144, hex0));
            }
            case 515:
            {
                if (!(read.PlayerRulerIs(952))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.SelectedCityHex();
                return ImmutableArray.Create(Command(1515, false, 0, hex0));
            }
            case 517:
            {
                if (!(read.PlayerRulerIs(952) || read.PlayerRulerIs(553))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(35);
                return ImmutableArray.Create(Command(1517, true, 145, hex0));
            }
            case 519:
            {
                if (!(read.PlayerForceOfPerson(440))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.SelectedCityHex();
                return ImmutableArray.Create(Command(1519, false, 146, hex0));
            }
            case 521:
            {
                if (!(read.PlayerKingdomNameIs(3))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(45);
                int hex1 = read.CityHex(46);
                return ImmutableArray.Create(Command(1521, true, 0, hex1, hex0));
            }
            case 522:
            {
                if (!(read.PlayerRulerIs(553))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(31);
                int hex1 = read.CityHex(30);
                return ImmutableArray.Create(Command(1522, true, 147, hex1, hex0));
            }
            case 523:
            {
                if (!(read.PlayerRulerIs(553) || read.PlayerForceOfPerson(505))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(13);
                return ImmutableArray.Create(Command(1523, true, 0, hex0, 48400));
            }
            case 524:
            {
                if (!(read.PlayerForceOfPerson(440))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.PersonHomeHex(440);
                return ImmutableArray.Create(Command(1524, true, 0, hex0));
            }
            case 525:
            {
                if (!(read.PlayerRulerIs(553))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(12);
                return ImmutableArray.Create(Command(1525, true, 148, hex0));
            }
            case 527:
            {
                if (!(read.PlayerKingdomNameIs(3) || read.PlayerForceOfPerson(344))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(20);
                return ImmutableArray.Create(Command(1527, false, 149, hex0));
            }
            case 528:
            {
                if (!(read.PlayerRulerIs(521))) return ImmutableArray<CampaignChronicleCommand>.Empty;
                int hex0 = read.CityHex(41);
                return ImmutableArray.Create(Command(1528, true, 150, hex0));
            }
            default:
                return ImmutableArray<CampaignChronicleCommand>.Empty;
        }

        CampaignChronicleCommand Command(int code, bool primary, uint kind, params int[] hexes)
        {
            if (hexes.Length > 4) throw new InvalidDataException("campaign-story-chronicle.hex_count");
            return new(code, 2, campaignDay, string.Empty, 0, kind,
                ImmutableArray.Create(string.Empty, string.Empty, string.Empty),
                ImmutableArray.Create(hexes.Length > 0 ? hexes[0] : 48400, hexes.Length > 1 ? hexes[1] : 48400,
                    hexes.Length > 2 ? hexes[2] : 48400, hexes.Length > 3 ? hexes[3] : 48400),
                primary, AdditionalEvent: eventId);
        }
    }
}
