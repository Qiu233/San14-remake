namespace San14.Data;

public sealed partial class ArmyOperationContext
{
    private GameResourceRepository? portraitResources;
    internal GameResourceRepository PortraitResources => portraitResources
        ?? throw new InvalidDataException("officer.portrait.resource_owner");
    internal void BindPortraitResources(GameResourceRepository repository) => portraitResources = repository;
}
