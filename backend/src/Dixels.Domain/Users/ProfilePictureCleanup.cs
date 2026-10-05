using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.EventBus;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace Dixels.Users;

/// <summary>
/// Removes a person's profile picture when their account is deleted, wherever it's deleted
/// from (the Users page, ABP's own account screens, anything added later). It listens for
/// ABP's entity event instead of being called by the delete code, so user management doesn't
/// need to know pictures exist. The file goes once the delete is saved: a delete that fails
/// keeps the picture.
/// </summary>
public class ProfilePictureCleanup : ILocalEventHandler<EntityDeletedEventData<IdentityUser>>, ITransientDependency
{
    private readonly ProfilePictureManager _pictures;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public ProfilePictureCleanup(ProfilePictureManager pictures, IUnitOfWorkManager unitOfWorkManager)
    {
        _pictures = pictures;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public Task HandleEventAsync(EntityDeletedEventData<IdentityUser> eventData)
    {
        var userId = eventData.Entity.Id;
        var unitOfWork = _unitOfWorkManager.Current;
        if (unitOfWork is null)
        {
            return _pictures.DeleteAsync(userId);
        }

        // Resolved again when the save completes: this handler's own scope is disposed by then.
        unitOfWork.OnCompleted(() => unitOfWork.ServiceProvider.GetRequiredService<ProfilePictureManager>().DeleteAsync(userId));
        return Task.CompletedTask;
    }
}
