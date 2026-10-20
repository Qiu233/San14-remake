using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Only the reached3452 body operands. No draft, report ring or UI state is created.</summary>
internal sealed class CityDomesticAssociationReportSource(GameState state, ArmyOperationContext definitions,
    LoadedScenario scenario, string language, GameResourceRepository repository)
{
    internal CityDomesticAssociationNames Read(SourceWorldContext current, AssociationRemovalReport report)
    {
        //Register the actual source arguments before reading the resource or any name.
        ushort person = current.ReadPersonIdentity(report.PersonPhysicalSlot) ?? throw Missing("person_argument");
        int force = current.ReadForcePhysicalSlot(report.TargetForce) ?? throw Missing("force_argument");
        if (language is not ("TC" or "SC")) throw Missing("locale");
        try
        {
            var context = new Names(state, definitions, scenario, language, repository, current, person, force);
            _ = new NarrationResourceMessages(repository.LoadMessages(language, MessageKind.Game).Messages)
                .ResolveAssociationRemoval(context);
            return context.Result();
        }
        catch (IOException error)
        { throw new InvalidDataException("city-domestic.association.resource", error); }
    }

    private sealed class Names(GameState state, ArmyOperationContext definitions, LoadedScenario scenario,
        string language, GameResourceRepository repository, SourceWorldContext current, ushort person, int force)
        : INarrationCurrentContext
    {
        private string? officerText, forceText;
        private LoadedForceNameDefinitions? custom;
        private MessageDocument? data;
        internal CityDomesticAssociationNames Result() => new(officerText ?? throw Missing("person_field"),
            forceText ?? throw Missing("force_field"));

        public string ForceText(int argument, int field)
        {
            if (argument != 0 || field != '4') throw Missing("force_field");
            if (force is < 1 or > 51) return forceText = string.Empty;
            //The native field re-looks-up its registered virtual60 argument.
            var operand = TrapCounterReportForceNameSource.Capture(state, current, (byte)force);
            string body = operand.Form switch
            {
                TrapCounterForceNameForm.Empty => string.Empty,
                TrapCounterForceNameForm.CustomKingdom => Custom(operand.CustomKingdomIndex!.Value),
                TrapCounterForceNameForm.PersonWithArmySuffix => Name(operand.PersonNameOperandAtSource, "force_name") + Suffix(),
                TrapCounterForceNameForm.PersonWithoutArmySuffix => Name(operand.PersonNameOperandAtSource, "force_name"),
                _ => throw Missing("force_name_form")
            };
            return forceText = CityDomesticMessageFormatting.FormatCityNameField(body);
        }

        public string PersonText(int argument, int field)
        {
            if (argument != 0 || field != '3') throw Missing("person_field");
            if (person is < 1 or > 6000) return officerText = string.Empty;
            int physical = current.ReadPersonLookupReference(person)?.PhysicalSlot ?? throw Missing("person_pointer");
            bool valid = current.ReadPersonNativeValidity(physical) ?? throw Missing("person_validity");
            if (!valid && person is not (>= 2001 and <= 2088)) return officerText = string.Empty;
            ushort identity = current.ReadPersonIdentity(physical) ?? throw Missing("person_identity");
            if (definitions.ResolveOfficerSource(identity) != physical || current.ResolveOfficerSource(identity) != physical ||
                !state.Officers.TryGetValue(identity, out var owner)) throw Missing("person_owner");
            return officerText = CityDomesticMessageFormatting.FormatPersonNameField(Name(owner.Name, "person_name"));
        }

        private string Custom(byte index)
        {
            custom ??= repository.LoadForceNameDefinitions(scenario.FixedDataSource.SourcePath);
            return Head(custom.Catalog.Definitions[index].ShortName);
        }
        private string Suffix()
        {
            data ??= repository.LoadMessages(language, MessageKind.Data).Messages;
            if (data.Sections[1].Records.Count <= 0x34F || data.Sections[1].Records[0x34F].Text is not string value)
                throw Missing("suffix");
            string text = Head(value);
            //The paired ordinary resource is one UTF16 unit. Longer edited
            //printf inputs have no proved intermediate-format domain here.
            if (text.Length > 1) throw Missing("suffix_format_domain");
            return text;
        }
        private string Name(string? value, string field)
        {
            if (value is null) throw Missing(field);
            string text = Head(value);
            //Current CE names occupy two source7-unit(TC)/9-unit(SC) fields.
            //No invented clipping or unproved long native printf is performed.
            if (text.Length > (language == "SC" ? 18 : 14)) throw Missing(field + "_format_domain");
            return text;
        }
        private static string Head(string value)
        { int nul = value.IndexOf('\0'); return nul < 0 ? value : value[..nul]; }
        public int PersonValue(int argument, int field) => throw Missing("numeric_operand");
        public int Relationship(int field, int firstPersonId, int secondPersonId) => throw Missing("relationship");
        public string AreaText(int argument, int field) => throw Missing("area_operand");
    }

    private static InvalidDataException Missing(string field) => new("city-domestic.association." + field);
}
