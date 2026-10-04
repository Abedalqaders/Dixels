using Volo.Abp.BlobStoring;

namespace Dixels.Users;

/// <summary>Where profile pictures are kept (ABP blob storing): one per user, named by their id.</summary>
[BlobContainerName("profile-pictures")]
public class ProfilePictureContainer
{
}
