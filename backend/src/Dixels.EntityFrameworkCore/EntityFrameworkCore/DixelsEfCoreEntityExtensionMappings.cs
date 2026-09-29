using System;
using Dixels.Users;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.ObjectExtending;
using Volo.Abp.Threading;

namespace Dixels.EntityFrameworkCore;

public static class DixelsEfCoreEntityExtensionMappings
{
    private static readonly OneTimeRunner OneTimeRunner = new OneTimeRunner();

    public static void Configure()
    {
        DixelsGlobalFeatureConfigurator.Configure();
        DixelsModuleExtensionConfigurator.Configure();

        OneTimeRunner.Run(() =>
        {
            /* USE THIS CLASS ONLY TO CONFIGURE EF CORE RELATED MAPPING.
             * The properties themselves are defined in DixelsModuleExtensionConfigurator
             * (Domain.Shared). See:
             * https://docs.abp.io/en/abp/latest/Customizing-Application-Modules-Extending-Entities
             */

            // A real column rather than a key in the ExtraProperties JSON: the Users list
            // filters by building in SQL, and the index keeps that cheap.
            ObjectExtensionManager.Instance
                .MapEfCoreProperty<IdentityUser, Guid?>(
                    DixelsUserConsts.BuildingIdPropertyName,
                    (entityBuilder, _) =>
                    {
                        entityBuilder.HasIndex(DixelsUserConsts.BuildingIdPropertyName);
                    }
                );
        });
    }
}
