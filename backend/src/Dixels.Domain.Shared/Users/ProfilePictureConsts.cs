namespace Dixels.Users;

public static class ProfilePictureConsts
{
    /// <summary>The largest picture the server keeps. The web app shrinks a picture to a small
    /// square before sending it (well under this), so only a picture sent some other way meets it.</summary>
    public const int MaxBytes = 1024 * 1024;
}
