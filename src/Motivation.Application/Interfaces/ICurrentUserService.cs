namespace Motivation.Application.Interfaces
{
    public interface ICurrentUserService
    {
        Guid? GetUserId();
    }
}
